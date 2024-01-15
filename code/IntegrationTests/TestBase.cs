using System.Reflection;

namespace IntegrationTests
{
    public abstract class TestBase
    {
        protected async Task<string> GetEmbeddedContentAsync(string name)
        {
            var assembly = GetType().Assembly;
            var fullName= $"{assembly.GetName().Name}.{name}";

            using (var stream = assembly.GetManifestResourceStream(fullName))
            {
                if (stream == null)
                {
                    throw new ArgumentException("No associated stream in assembly", nameof(name));
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