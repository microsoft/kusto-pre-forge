using KustoPreForgeLib.BlobSources;

namespace KustoPreForgeLib.Transforms
{
    internal class DownloadBlobTransform : IDataSource<object>
    {
        private readonly IDataSource<BlobData> _blobSource;
        private readonly PerfCounterJournal _journal;

        public DownloadBlobTransform(
            IDataSource<BlobData> blobSource,
            PerfCounterJournal journal)
        {
            _blobSource = blobSource;
            _journal = journal;
        }

        async IAsyncEnumerator<SourceData<object>>
            IAsyncEnumerable<SourceData<object>>
            .GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            await foreach(var blobData in _blobSource)
            {
                //blobData.Data.BlobClient
                yield return new SourceData<object>(0, null, null);
            }
        }
    }
}