namespace KustoPreForgeLib
{
    public class SourceSettings
    {
        public SourceSettings(
            string? serviceBusQueueUrl,
            Uri? sourceBlob,
            Uri? sourceBlobsPrefix,
            string? sourceBlobsSuffix)
        {
            var count = (serviceBusQueueUrl == null ? 0 : 1)
                + (sourceBlob == null ? 0 : 1)
                + (sourceBlobsPrefix == null ? 0 : 1);

            if (count != 1)
            {
                throw new ArgumentNullException("One-and-only-one source must be specified");
            }

            ServiceBusQueueUrl = serviceBusQueueUrl;
            SourceBlob = sourceBlob;
            SourceBlobsPrefix = sourceBlobsPrefix;
            SourceBlobsSuffix = sourceBlobsSuffix;
        }

        public string? ServiceBusQueueUrl { get; }

        public Uri? SourceBlob { get; }

        public Uri? SourceBlobsPrefix { get; }

        public string? SourceBlobsSuffix { get; }

        public void WriteOutSettings()
        {
            Console.WriteLine($"ServiceBusQueueUrl:  {ServiceBusQueueUrl}");
            Console.WriteLine($"SourceBlob:  {SourceBlob}");
            Console.WriteLine($"SourceBlobsPrefix:  {SourceBlobsPrefix}");
            Console.WriteLine($"SourceBlobsSuffix:  {SourceBlobsSuffix}");
        }
    }
}