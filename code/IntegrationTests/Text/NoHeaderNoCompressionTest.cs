

namespace IntegrationTests.Text
{
    public class NoHeaderNoCompressionTest : TestBase
    {
        public NoHeaderNoCompressionTest()
            : base("TextNoHeaderNoCompression")
        {
        }

        [Fact]
        public async Task TestOutput()
        {
            await EnsureTableLoadAsync();
        }
    }
}