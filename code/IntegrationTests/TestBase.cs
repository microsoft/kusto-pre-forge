using Azure.Core;
using Azure.Identity;
using Azure.Storage.Files.DataLake;
using Azure.Storage.Files.DataLake.Specialized;
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

        private static readonly TokenCredential _credentials;
        private static readonly DataLakeDirectoryClient _templateRoot;
        private static readonly DataLakeDirectoryClient _testRoot;

        private readonly string _testPath;
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
                .GetSubDirectoryClient("template");
            _testRoot = landingTest.GetSubDirectoryClient(Guid.NewGuid().ToString());
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

        protected TestBase(string testPath)
        {
            _testPath = testPath;
            _testTemplate = _templateRoot.GetSubDirectoryClient(_testPath);
        }

        protected async Task<string> GetEmbeddedScriptAsync()
        {
            var assembly = GetType().Assembly;
            var fullName = $"{assembly.GetName().Name}.{_testPath.Replace('/', '.')}.kql";

            using (var stream = assembly.GetManifestResourceStream(fullName))
            {
                if (stream == null)
                {
                    throw new ArgumentException(
                        "No associated stream in assembly",
                        nameof(_testPath));
                }
                using (var reader = new StreamReader(stream))
                {
                    var text = await reader.ReadToEndAsync();
                    var replacedText = text.Replace(
                        "TEMPLATE_PATH",
                        _testTemplate.Uri.ToString());

                    return replacedText;
                }
            }
        }

        protected async Task EnsureTemplateBlobTask(string script)
        {
            if (!await _testTemplate.ExistsAsync())
            {
                await RunExportAsync();
            }
        }

        private Task RunExportAsync()
        {
            throw new NotImplementedException();
        }
    }
}