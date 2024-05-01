using Azure.Storage.Blobs.Models;
using KustoPreForgeLib.BlobSources;
using KustoPreForgeLib.Memory;

namespace KustoPreForgeLib.Transforms
{
    internal class DownloadBlobTransform : IDataSource<BufferFragment>
    {
        private readonly BufferFragment _buffer;
        private readonly IDataSource<BlobData> _blobSource;
        private readonly PerfCounterJournal _journal;

        public DownloadBlobTransform(
            BufferFragment buffer,
            IDataSource<BlobData> blobSource,
            PerfCounterJournal journal)
        {
            _buffer = buffer;
            _blobSource = blobSource;
            _journal = journal;
        }

        async IAsyncEnumerator<SourceData<BufferFragment>>
            IAsyncEnumerable<SourceData<BufferFragment>>.GetAsyncEnumerator(
            CancellationToken cancellationToken)
        {
            await foreach (var blobData in _blobSource)
            {
                var blobBuffer = await _buffer.ReserveSubBufferAsync((int)blobData.Data.BlobSize);
                var readOptions = new BlobOpenReadOptions(false)
                {
                    BufferSize = blobBuffer.Length
                };

                using (var readStream = await blobData.Data.BlobClient.OpenReadAsync(readOptions))
                {
                    var size = await readStream.ReadAsync(blobBuffer.ToMemoryBlock());

                    if (size != blobBuffer.Length)
                    {
                        throw new InvalidDataException("Mismatch in read size");
                    }
                }
                //  To remove
                blobBuffer.Release();
                yield return new SourceData<BufferFragment>(
                    blobBuffer,
                    null,
                    null,
                    blobData);
            }
        }
    }
}