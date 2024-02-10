using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Storage.Files.DataLake;
using Azure.Storage.Files.DataLake.Specialized;
using Kusto.Cloud.Platform.Utils;
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
        private static readonly TokenCredential _credentials;
        private static readonly DataLakeDirectoryClient _templateRoot;
        private static readonly DataLakeDirectoryClient _testRoot;
        private static readonly ExportManager _exportManager;
        private static readonly Task _exportTask;

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
            _testRoot = landingTest
                .GetSubDirectoryClient("tests")
                .GetSubDirectoryClient(Guid.NewGuid().ToString());

            var kustoIngestUri = GetEnvironmentVariable("KustoIngestUri");
            var kustoDb = GetEnvironmentVariable("KustoDb");
            var kustoProvider = CreateKustoProvider(kustoIngestUri, kustoDb, _credentials);

            _exportManager = new ExportManager(
                new OperationManager(kustoProvider),
                kustoProvider);
            _exportTask = EnsureTemplatesAsync();
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
            await _exportTask;
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
            var exportFormat = config.Format == "text"
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
    }
}