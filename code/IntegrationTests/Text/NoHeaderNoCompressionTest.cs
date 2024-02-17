

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

            var counts = await QueryOneRowAsync(
                $@"TextNoHeaderNoCompression
| project Csv=parse_csv(Text)
| project Id=tolong(Csv[0]), Timestamp=todatetime(Csv[1]), Level=tostring(Csv[2])
| summarize MaxId=max(Id), RowCount=count()",
                r => new
                {
                    MaxId = (long)r["MaxId"],
                    RowCount = (long)r["RowCount"]
                });

            Assert.NotNull(counts);
            Assert.Equal(counts.MaxId, counts.RowCount);

            var levels = await QueryAsync(
                $@"TextNoHeaderNoCompression
| project Csv=parse_csv(Text)
| project Id=tolong(Csv[0]), Timestamp=todatetime(Csv[1]), Level=tostring(Csv[2])
| summarize by Level",
                r => (string)r["Level"]);

            Assert.NotNull(levels);
            Assert.Equal(3, levels.Count);
        }
    }
}