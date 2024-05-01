using Azure.Storage.Blobs.Models;
using KustoPreForgeLib.BlobSources;
using KustoPreForgeLib.Memory;

namespace KustoPreForgeLib.Transforms
{
    internal class DownloadBlobTransform : IDataSource<BufferFragment>
    {
        private const int MAX_READ_CONCURRENCY = 16;

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
            var workQueue = new WorkQueue<SourceData<BufferFragment>>();

            await foreach (var blobData in _blobSource)
            {
                if (workQueue.Count == MAX_READ_CONCURRENCY)
                {
                    yield return await PumpContentOutAsync(workQueue);
                }
                workQueue.AddWorkItem(LoadBlobAsync(blobData));
            }
            while (workQueue.Count > 0)
            {
                yield return await PumpContentOutAsync(workQueue);
            }
        }

        private static async Task<SourceData<BufferFragment>> PumpContentOutAsync(
            WorkQueue<SourceData<BufferFragment>> workQueue)
        {
            var data = await workQueue.WhenAnyAsync();

            //  To remove
            data.Data.Release();
            
            return data;
        }

        private async Task<SourceData<BufferFragment>> LoadBlobAsync(
            SourceData<BlobData> blobData)
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
            _journal.AddReading("DownloadBlob.BlobRead", 1);

            return new SourceData<BufferFragment>(
                blobBuffer,
                null,
                null,
                blobData);
        }
    }
}