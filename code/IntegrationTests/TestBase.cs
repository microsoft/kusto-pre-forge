using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Files.DataLake;
using Azure.Storage.Files.DataLake.Specialized;
using Kusto.Cloud.Platform.Data;
using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Data.Net.Client;
using Kusto.Ingest;
using KustoPreForgeLib;
using Microsoft.VisualStudio.TestPlatform.Common;
using System.Collections.Immutable;
using System.Data;
using System.Text.Json;

namespace IntegrationTests
{
    public abstract class TestBase
    {
        #region Inner Types
        private class MainSettings
        {
            public IDictionary<string, ProjectSetting>? Profiles { get; set; }

            public IDictionary<string, string> GetEnvironmentVariables()
            {
                if (Profiles == null)
                {
                    throw new InvalidOperationException("'profiles' element isn't present in 'launchSettings.json'");
                }
                if (Profiles.Count == 0)
                {
                    throw new InvalidOperationException(
                        "No profile is configured within 'profiles' element isn't present "
                        + "in 'launchSettings.json'");
                }
                var profile = Profiles.First().Value;

                if (profile.EnvironmentVariables == null)
                {
                    throw new InvalidOperationException("'environmentVariables' element isn't present in 'launchSettings.json'");
                }

                return profile.EnvironmentVariables;
            }
        }

        private class ProjectSetting
        {
            public IDictionary<string, string>? EnvironmentVariables { get; set; }
        }
        #endregion

        private const string TEMPLATE_FOLDER = "template";
        private static readonly TimeSpan LOAD_TRACK_PERIOD = TimeSpan.FromSeconds(5);

        private static readonly bool _isInProc;
        private static readonly IImmutableDictionary<string, TestCaseConfiguration> _configurationMap =
            TestCaseConfiguration.LoadConfigurations();
        private static readonly string _testId = Guid.NewGuid().ToString();
        private static readonly TokenCredential _credentials;
        private static readonly DataLakeDirectoryClient _templateRoot;
        private static readonly DataLakeDirectoryClient _testsRoot;
        private static readonly string _database;
        private static readonly ICslAdminProvider _kustoCommandProvider;
        private static readonly ICslQueryProvider _kustoQueryProvider;
        private static readonly IKustoQueuedIngestClient _kustoIngestProvider;
        private static readonly ICslAdminProvider _kustoIngestCommandProvider;
        private static readonly ExportManager _exportManager;
        private static readonly Task _setupTask;

        private readonly TestCaseConfiguration _configuration;
        private readonly DataLakeDirectoryClient _testTemplate;

        protected string TableName { get; }

        #region Static Construction
        static TestBase()
        {
            FileToEnvironmentVariables();

            _credentials = GetCredentials();

            _isInProc = GetBooleanVariable("IsInProc");
            var blobLandingFolder = GetEnvironmentVariable("BlobLandingFolder");

            var landingTest = new DataLakeDirectoryClient(
                new Uri(blobLandingFolder),
                _credentials);

            _templateRoot = landingTest
                .GetParentDirectoryClient()
                .GetSubDirectoryClient(TEMPLATE_FOLDER);
            _testsRoot = landingTest
                .GetSubDirectoryClient("tests");

            var kustoIngestUri = GetEnvironmentVariable("KustoIngestUri");

            _database = GetEnvironmentVariable("KustoDb");
            (_kustoCommandProvider, _kustoQueryProvider, _kustoIngestProvider, _kustoIngestCommandProvider) =
                CreateKustoProviders(kustoIngestUri, _database, _credentials);
            _exportManager = new ExportManager(
                new OperationManager(_kustoCommandProvider),
                _kustoCommandProvider);
            _setupTask = SetupAsync();
        }

        private static async Task SetupAsync()
        {
            await EnsureTemplatesAsync();
            await CleanTablesAsync();
            if (!_isInProc)
            {
                await CopyTemplatesAsync();
            }
        }

        private static (ICslAdminProvider, ICslQueryProvider, IKustoQueuedIngestClient, ICslAdminProvider) CreateKustoProviders(
            string kustoIngestUri,
            string kustoDb,
            TokenCredential credentials)
        {
            var uriBuilder = new UriBuilder(kustoIngestUri);

            uriBuilder.Host = uriBuilder.Host.Replace("ingest-", string.Empty);
            uriBuilder.Path = kustoDb;

            var kustoIngestBuilder = new KustoConnectionStringBuilder(kustoIngestUri)
                .WithAadAzureTokenCredentialsAuthentication(credentials);
            var kustoEngineBuilder = new KustoConnectionStringBuilder(uriBuilder.ToString())
                .WithAadAzureTokenCredentialsAuthentication(credentials);
            var kustoCommandProvider =
                KustoClientFactory.CreateCslAdminProvider(kustoEngineBuilder);
            var kustoQueryProvider =
                KustoClientFactory.CreateCslQueryProvider(kustoEngineBuilder);
            var kustoIngestProvider =
                KustoIngestFactory.CreateQueuedIngestClient(kustoIngestBuilder);
            var kustoIngestCommandProvider =
                KustoClientFactory.CreateCslAdminProvider(kustoIngestBuilder);

            return (kustoCommandProvider, kustoQueryProvider, kustoIngestProvider, kustoIngestCommandProvider);
        }

        private static string GetEnvironmentVariable(string name)
        {
            var variable = Environment.GetEnvironmentVariable(name);

            if (string.IsNullOrWhiteSpace(variable))
            {
                throw new ArgumentNullException(name);
            }

            return variable;
        }

        private static bool GetBooleanVariable(string name, bool defaultValue = false)
        {
            var variable = Environment.GetEnvironmentVariable(name);

            if (string.IsNullOrWhiteSpace(variable))
            {
                return defaultValue;
            }
            else
            {
                return bool.Parse(variable);
            }
        }

        private static TokenCredential GetCredentials()
        {
            var kustoTenantId = GetEnvironmentVariable("KustoTenantId");
            var kustoSpId = GetEnvironmentVariable("KustoSpId");
            var kustoSpSecret = GetEnvironmentVariable("KustoSpSecret");

            return new ClientSecretCredential(
                kustoTenantId,
                kustoSpId,
                kustoSpSecret);
        }

        private static void FileToEnvironmentVariables()
        {
            const string PATH = "Properties\\launchSettings.json";

            if (File.Exists(PATH))
            {
                var settingContent = File.ReadAllText(PATH);
                var mainSetting = JsonSerializer.Deserialize<MainSettings>(
                    settingContent,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    })
                    ?? throw new InvalidOperationException("Can't read 'launchSettings.json'");
                var variables = mainSetting.GetEnvironmentVariables();

                foreach (var variable in variables)
                {
                    Environment.SetEnvironmentVariable(variable.Key, variable.Value);
                }
            }
        }
        #endregion

        protected TestBase(string tableName)
        {
            TableName = tableName;
            _configuration = _configurationMap[tableName];
            _testTemplate = _templateRoot.GetSubDirectoryClient(TableName);
        }

        #region Kusto Helpers
        protected async Task<T?> QueryOneRowAsync<T>(
            string query,
            Func<DataRow, T> projection)
        {
            var results = await QueryAsync(query, projection);

            return results.FirstOrDefault();
        }

        protected async Task<IImmutableList<T>> QueryAsync<T>(
            string query,
            Func<DataRow, T> projection)
        {
            using (var reader = await _kustoQueryProvider.ExecuteQueryAsync(
                string.Empty,
                query,
                new ClientRequestProperties()))
            {
                var results = reader.ToDataSet().Tables[0].Rows
                    .Cast<DataRow>()
                    .Select(r => projection(r))
                    .ToImmutableArray();

                return results;
            }
        }
        #endregion

        #region Table Load
        protected async Task EnsureTableLoadAsync()
        {
            if (_isInProc)
            {
                await LoadTableInProcAsync();
            }
            else
            {
                await EnsureTableLoadedOutOfProcAsync();
            }
        }

        private async Task LoadTableInProcAsync()
        {
            async Task<BlockBlobClient> GetTemplateBlobAsync()
            {
                await foreach (var item in _testTemplate.GetPathsAsync())
                {
                    //_testTemplate.Uri
                    //item.Name;
                    throw new NotImplementedException();
                }

                throw new InvalidDataException("No blob found");
            }

            var sourceBlob = await GetTemplateBlobAsync();
            var ingestionStagingContainers =
                await RunningContext.GetIngestionStagingContainersAsync(_kustoIngestCommandProvider);
            var context = new RunningContext(
                new BlobSettings(
                    _configuration.Format,
                    _configuration.InputCompression,
                    _configuration.OutputCompression,
                    _configuration.HasHeaders,
                    null),
                _credentials,
                sourceBlob,
                null,
                _kustoIngestProvider,
                () => new KustoQueuedIngestionProperties(_database, TableName)
                {
                    Format = _configuration.Format
                },
                ingestionStagingContainers);

            await EtlRun.RunEtlAsync(context);
        }

        private async Task EnsureTableLoadedOutOfProcAsync()
        {
            int? totalShardCount = null;

            await _setupTask;
            while ((totalShardCount = await TrackTotalShardCountLoadedAsync()) == null)
            {
                await Task.Delay(LOAD_TRACK_PERIOD);
            }
            while (await TrackShardCountLoadedAsync() != totalShardCount)
            {
                await Task.Delay(LOAD_TRACK_PERIOD);
            }
        }

        private async Task<int?> TrackTotalShardCountLoadedAsync()
        {
            var query = @$"
{TableName}
| summarize Tags=take_any(extent_tags()) by ExtentId=extent_id()
| where Tags has ""kpf-last-shard""
| mv-expand Tags
| where Tags has ""kpf-shard-id""
| project ShardCount=toint(split(Tags,':')[1])";
            var result = await QueryOneRowAsync(query, r => (int)r["ShardCount"]);

            return result == 0
                ? null
                : result;
        }

        private async Task<int> TrackShardCountLoadedAsync()
        {
            var query = @$"
{TableName}
| summarize Tags=take_any(extent_tags()) by ExtentId=extent_id()
| mv-expand Tags
| where Tags has ""kpf-shard-id""
| project ShardId=split(Tags, "":"")[1]
| summarize Cardinality=toint(count())";
            var cardinality = await QueryOneRowAsync(query, r => (int)r["Cardinality"]);

            return cardinality;
        }
        #endregion

        #region Export
        private static async Task EnsureTemplatesAsync()
        {
            var configurations = await GetConfigToExportAsync();

            if (configurations.Any())
            {
                var connectionString = _templateRoot.GetParentFileSystemClient().Uri.ToString();
                var exportTasks = configurations
                    .Select(config => ExportDataAsync(connectionString, config))
                    .ToImmutableArray();

                await Task.WhenAll(exportTasks);
            }
        }

        private static async Task<IImmutableList<TestCaseConfiguration>> GetConfigToExportAsync()
        {
            if (await _templateRoot.ExistsAsync())
            {
                var items = await _templateRoot.GetPathsAsync(true).ToListAsync();
                var subPaths = items
                    .Where(i => i.IsDirectory != true)
                    .Select(i => i.Name)
                    .Select(n => n.Substring(_templateRoot.Path.Length));
                var remainingConfig = _configurationMap;

                foreach (var subPath in subPaths)
                {
                    foreach (var config in _configurationMap.Values)
                    {
                        if (subPath.TrimStart('/').StartsWith(config.BlobFolder))
                        {
                            remainingConfig = remainingConfig.Remove(config.Table);
                        }
                    }
                }

                return remainingConfig.Values.ToImmutableArray();
            }
            else
            {
                return _configurationMap.Values.ToImmutableArray();
            }
        }

        private static async Task ExportDataAsync(
            string connectionString,
            TestCaseConfiguration config)
        {
            var exportFormat = config.Format == DataSourceFormat.txt
                ? DataSourceFormat.csv
                : config.Format;
            var prefix = $"{_templateRoot.Path}/{config.BlobFolder}/";
            var compressed = config.InputCompression != DataSourceCompressionType.None
                ? "compressed"
                : string.Empty;
            var headers = config.HasHeaders ? "all" : "none";
            var script = $@"
.export async {compressed} to {exportFormat} (
    @""{connectionString};impersonate""
  )
  with (
    sizeLimit=1000000000,
    namePrefix=""{prefix}"",
    distribution=""single"",
    includeHeaders=""{headers}""
  )
  <| 
  {config.Function}";

            await _exportManager.RunExportAsync(script);
        }
        #endregion

        #region Kusto Tables
        private static async Task CleanTablesAsync()
        {
            var tableTextList = string.Join(
                ", ",
                _configurationMap.Values
                .Select(c => c.Table));
            var setTables = string.Join(
                Environment.NewLine + Environment.NewLine,
                _configurationMap.Values
                .Select(c => c.GetCreateTableScript()));
            var script = @$"
.execute database script with (ThrowOnErrors=true) <|
    .drop tables ({tableTextList}) ifexists

    {setTables}
";

            await _kustoCommandProvider.ExecuteControlCommandAsync(
                string.Empty,
                script);
        }
        #endregion

        #region Template Copy
        private static async Task CopyTemplatesAsync()
        {
            var copyTasks = new List<Task>(_configurationMap.Count);

            await foreach (var sourceItem in _templateRoot.GetPathsAsync(true))
            {
                if (sourceItem.IsDirectory != true)
                {
                    var sourceFileClient = _templateRoot
                        .GetParentFileSystemClient()
                        .GetFileClient(sourceItem.Name);
                    var suffix = sourceItem.Name.Substring(_templateRoot.Name.Length);
                    var parts = suffix.Split('/');
                    var partsWithTestId = parts
                        .SkipLast(1)
                        .Skip(1)
                        .Append(_testId)
                        .Append(parts.Last());
                    var suffixWithTestId = string.Join('/', partsWithTestId);
                    var destinationFileClient = _testsRoot.GetFileClient(suffixWithTestId);
                    var destinationBlobClient =
                        new BlockBlobClient(destinationFileClient.Uri, _credentials);
                    var task = destinationBlobClient.StartCopyFromUriAsync(sourceFileClient.Uri);

                    copyTasks.Add(task);
                }
            }

            await Task.WhenAll(copyTasks);
        }
        #endregion
    }
}