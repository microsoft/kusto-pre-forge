

namespace IntegrationTests.Text
{
    public class NoHeaderNoCompressionTest : TestBase
    {
        [Fact]
        public async Task TestOutput()
        {
            var script = await GetEmbeddedContentAsync("Text.NoHeaderNoCompression.kql");

            await EnsureTemplateBlobTask(script);
        }
    }
}