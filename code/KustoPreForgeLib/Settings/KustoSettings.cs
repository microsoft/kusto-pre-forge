namespace KustoPreForgeLib.Settings
{
    public class KustoSettings
    {
        public KustoSettings(
            Uri? kustoIngestUri,
            string? kustoDb,
            string? kustoTable)
        {
            IngestUri = kustoIngestUri;
            Database = kustoDb;
            Table = kustoTable;
        }

        public Uri? IngestUri { get; }

        public string? Database { get; }

        public string? Table { get; }

        public void WriteOutSettings()
        {
            Console.WriteLine($"KustoIngestUri:  {IngestUri}");
            Console.WriteLine($"KustoDb:  {Database}");
            Console.WriteLine($"KustoTable:  {Table}");
        }
    }
}