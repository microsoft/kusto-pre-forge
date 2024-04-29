using Azure.Core;
using Azure.Storage.Blobs;

namespace KustoPreForgeLib.BlobSources
{
    internal class ListBlobSource : IDataSource<BlobData>
    {
        private readonly BlobContainerClient _blobContainerClient;
        private readonly string _prefix;
        private readonly string? _suffix;

        public ListBlobSource(
            Uri sourceBlobsPrefix,
            string? sourceBlobsSuffix,
            TokenCredential credentials)
        {
            var prefixBuilder = new UriBuilder(sourceBlobsPrefix);

            prefixBuilder.Path = string.Join(string.Empty, sourceBlobsPrefix.Segments.Take(2));
            _blobContainerClient = new BlobContainerClient(prefixBuilder.Uri, credentials);
            _prefix = string.Join(string.Empty, sourceBlobsPrefix.Segments.Skip(2));
            _suffix = sourceBlobsSuffix;
        }

        #region IAsyncEnumerable<SourceData<BlobData>>
        async IAsyncEnumerator<SourceData<BlobData>>
            IAsyncEnumerable<SourceData<BlobData>>.GetAsyncEnumerator(
            CancellationToken cancellationToken)
        {
            await foreach (var item in _blobContainerClient.GetBlobsAsync(prefix: _prefix))
            {
                if (_suffix == null || item.Name.EndsWith(_suffix))
                {
                    yield return new SourceData<BlobData>(
                        new BlobData(
                            _blobContainerClient.GetBlobClient(item.Name),
                            item.Properties.ContentLength ?? 0),
                        null,
                        null);
                }
            }
        }
        #endregion
    }
}