

using System.Diagnostics.Metrics;

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

            var result = await QueryOneRowAsync(
                $@"
let Data = TextNoHeaderNoCompression
    | project Csv=parse_csv(Text)
    | project Id=tolong(Csv[0]), Timestamp=todatetime(Csv[1]), Level=tostring(Csv[2]);
let IdCardinality = toscalar(Data
    | summarize by Id
    | count);
let TimestampCardinality = toscalar(Data
    | summarize by Timestamp
    | count);
let LevelCardinality = toscalar(Data
    | summarize by Level
    | count);
let RowCount = toscalar(Data
    | count);
print IdCardinality=IdCardinality,
    TimestampCardinality=TimestampCardinality,
    LevelCardinality=LevelCardinality,
    RowCount=RowCount",
                r => new
                {
                    IdCardinality = (long)r["IdCardinality"],
                    TimestampCardinality = (long)r["TimestampCardinality"],
                    LevelCardinality = (long)r["LevelCardinality"],
                    RowCount = (long)r["RowCount"]
                });

            Assert.NotNull(result);
            Assert.Equal(result.RowCount, result.IdCardinality);
            Assert.Equal(result.RowCount, result.TimestampCardinality);
            Assert.Equal(3, result.LevelCardinality);
        }
    }
}