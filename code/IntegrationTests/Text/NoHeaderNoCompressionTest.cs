

namespace IntegrationTests.Text
{
    public class NoHeaderNoCompressionTest : TestBase
    {
        public NoHeaderNoCompressionTest()
            :base("Text/NoHeaderNoCompression")
        {
        }

        [Fact]
        public async Task TestOutput()
        {
            await EnsureTemplateBlobTask();
        }
    }
}