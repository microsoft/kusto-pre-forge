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

        private readonly string _testPath;

        #region Static Construction
        static TestBase()
        {
            FileToEnvironmentVariables();
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
        }

        protected async Task<string> GetEmbeddedScriptAsync()
        {
            var assembly = GetType().Assembly;
            string fullName = $"{assembly.GetName().Name}.{_testPath.Replace('/', '.')}.kql";

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

                    return text;
                }
            }
        }

        protected Task EnsureTemplateBlobTask(string script)
        {
            throw new NotImplementedException();
        }
    }
}