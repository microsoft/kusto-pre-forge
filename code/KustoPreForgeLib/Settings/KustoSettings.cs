namespace KustoPreForgeLib.Settings
{
    public class KustoSettings
    {
        public KustoSettings(
            Uri kustoIngestUri,
            string kustoDb,
            string kustoTable,
            string? tempDirectory)
        {
            IngestUri = kustoIngestUri;
            Database = kustoDb;
            Table = kustoTable;
            TempDirectory = Path.Combine(tempDirectory ?? Path.GetTempPath(), "kusto-pre-forge");
        }

        public Uri IngestUri { get; }

        public string Database { get; }

        public string Table { get; }

        public string TempDirectory { get; }

        public void WriteOutSettings()
        {
            Console.WriteLine($"KustoIngestUri:  {IngestUri}");
            Console.WriteLine($"KustoDb:  {Database}");
            Console.WriteLine($"KustoTable:  {Table}");
            Console.WriteLine($"TempDirectory:  {TempDirectory}");
        }
    }
}