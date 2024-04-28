using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;

namespace KustoPreForgeLib.BlobEnumerables
{
    internal class ListBlobEnumerable : IBlobEnumerable
    {
        private readonly BlobContainerClient _blobContainerClient;
        private readonly string _prefix;
        private readonly string? _suffix;

        public ListBlobEnumerable(
            Uri sourceBlobsPrefix,
            string? sourceBlobsSuffix,
            TokenCredential credentials)
        {
            _blobContainerClient = new BlobContainerClient(sourceBlobsPrefix, credentials);
            _prefix = "hi";
            _suffix = sourceBlobsSuffix;
        }

        #region IBlobEnumerable
        async IAsyncEnumerable<BlobClient> IBlobEnumerable.EnumerateBlobs()
        {
            await foreach (var item in _blobContainerClient.GetBlobsAsync(prefix: _prefix))
            {
                if (_suffix == null || item.Name.EndsWith(_suffix))
                {
                    yield return _blobContainerClient.GetBlobClient(item.Name);
                }
            }
        }
        #endregion
    }
}