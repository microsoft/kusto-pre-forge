using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Files.DataLake;
using Azure.Storage.Files.DataLake.Specialized;
using Kusto.Cloud.Platform.Data;
using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Data.Net.Client;
using KustoPreForgeLib;
using System.Collections.Immutable;
using System.Reflection;
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

        private static readonly IImmutableDictionary<string, TestCaseConfiguration> _configuration =
            TestCaseConfiguration.LoadConfigurations();
        private static readonly string _testId = Guid.NewGuid().ToString();
        private static readonly TokenCredential _credentials;
        private static readonly DataLakeDirectoryClient _templateRoot;
        private static readonly DataLakeDirectoryClient _testsRoot;
        private static readonly ICslAdminProvider _kustoProvider;
        private static readonly ExportManager _exportManager;
        private static readonly Task _setupTask;

        private readonly string _tableName;
        private readonly DataLakeDirectoryClient _testTemplate;

        #region Static Construction
        static TestBase()
        {
            FileToEnvironmentVariables();

            _credentials = GetCredentials();

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
            var kustoDb = GetEnvironmentVariable("KustoDb");

            _kustoProvider = CreateKustoProvider(kustoIngestUri, kustoDb, _credentials);
            _exportManager = new ExportManager(
                new OperationManager(_kustoProvider),
                _kustoProvider);
            _setupTask = SetupAsync();
        }

        private static async Task SetupAsync()
        {
            await EnsureTemplatesAsync();
            await CleanTablesAsync();
            await CopyTemplatesAsync();
        }

        private static ICslAdminProvider CreateKustoProvider(
            string kustoIngestUri,
            string kustoDb,
            TokenCredential credentials)
        {
            var uriBuilder = new UriBuilder(kustoIngestUri);

            uriBuilder.Host = uriBuilder.Host.Replace("ingest-", string.Empty);
            uriBuilder.Path = kustoDb;

            var kustoBuilder = new KustoConnectionStringBuilder(uriBuilder.ToString())
                .WithAadAzureTokenCredentialsAuthentication(credentials);
            var kustoProvider = KustoClientFactory.CreateCslAdminProvider(kustoBuilder);

            return kustoProvider;
        }

        private static string GetEnvironmentVariable(string name)
        {
            var blobLandingFolder = Environment.GetEnvironmentVariable(name);

            if (string.IsNullOrWhiteSpace(blobLandingFolder))
            {
                throw new ArgumentNullException(name);
            }

            return blobLandingFolder;
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
            _tableName = tableName;
            _testTemplate = _templateRoot.GetSubDirectoryClient(_tableName);
        }

        protected async Task EnsureTableLoadAsync()
        {
            await _setupTask;
        }

        #region Export
        private static async Task EnsureTemplatesAsync()
        {
            var configurations = await GetConfigToExportAsync();

            if (configurations.Any())
            {
                var connectionString = _templateRoot.GetParentFileSystemClient().Uri.ToString();
                var exportTasks = configurations
                    .Select(config => ExportDataAsync(connectionString, config));

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
                var remainingConfig = _configuration;

                foreach (var subPath in subPaths)
                {
                    foreach (var config in _configuration.Values)
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
                return _configuration.Values.ToImmutableArray();
            }
        }

        private static async Task ExportDataAsync(
            string connectionString,
            TestCaseConfiguration config)
        {
            var exportFormat = config.Format == "txt"
                ? "csv"
                : config.Format;
            var prefix = $"{_templateRoot.Path}/{config.BlobFolder}/";
            var script = $@"
.export async compressed to {exportFormat} (
    @""{connectionString};impersonate""
  )
  with (
    sizeLimit=1000000000,
    namePrefix=""{prefix}"",
    distribution=""single"",
    includeHeaders=""all""
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
                _configuration.Values
                .Select(c => c.Table));
            var setTables = string.Join(
                Environment.NewLine + Environment.NewLine,
                _configuration.Values
                .Select(c => $".set {c.Table} <| {c.Function}() | take 0"));
            var script = @$"
.execute database script with (ThrowOnErrors=true) <|
    .drop tables ({tableTextList}) ifexists

    {setTables}
";

            await _kustoProvider.ExecuteControlCommandAsync(
                string.Empty,
                script);
        }
        #endregion

        #region Template Copy
        private static async Task CopyTemplatesAsync()
        {
            var copyTasks = new List<Task>(_configuration.Count);

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