using System.Reflection;

namespace IntegrationTests
{
    public abstract class TestBase
    {
        private readonly string _testPath;

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