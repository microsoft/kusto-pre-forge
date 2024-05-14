using Azure.Storage.Blobs.Models;
using KustoPreForgeLib.BlobSources;
using KustoPreForgeLib.Memory;
using System.Collections.Concurrent;

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
            var dataQueue = new ConcurrentQueue<SourceData<BufferFragment>>();
            var workQueue = new WorkQueue(MAX_READ_CONCURRENCY);

            await foreach (var blobData in _blobSource)
            {   //  Avoid context capture
                var data = blobData;
                var blobBufferTask = _buffer.ReserveSubBufferAsync(
                    (int)blobData.Data.BlobSize,
                    TransformHelper.CreateCancellationToken());

                //  We try to queue as much work as possible
                //  When that is not possible we unqueue data as much as possible
                while (!workQueue.HasCapacity)
                {
                    if (dataQueue.TryDequeue(out var contentData))
                    {
                        yield return contentData;
                    }
                    else
                    {
                        await workQueue.WhenAnyAsync();
                    }
                }
                while (!blobBufferTask.IsCompleted)
                {
                    if (dataQueue.TryDequeue(out var contentData))
                    {
                        yield return contentData;
                    }
                    else
                    {   //  Every second, we'll check if we should return some data
                        await Task.WhenAny(
                            Task.Delay(TimeSpan.FromSeconds(1)),
                            blobBufferTask);
                    }
                }

                var blobBuffer = await blobBufferTask;

                await workQueue.ObserveCompletedAsync();
                workQueue.QueueWorkItem(() => LoadBlobAsync(blobData, blobBuffer, dataQueue));
            }
            await workQueue.WhenAllAsync();
            while (dataQueue.TryDequeue(out var contentData))
            {
                yield return contentData;
            }
        }

        private async Task LoadBlobAsync(
            SourceData<BlobData> blobData,
            BufferFragment blobBuffer,
            ConcurrentQueue<SourceData<BufferFragment>> dataQueue)
        {
            var readOptions = new BlobOpenReadOptions(false)
            {
                BufferSize = blobBuffer.Length
            };

            using (var readStream = await blobData.Data.BlobClient.OpenReadAsync(readOptions))
            {
                var size = await readStream.ReadAsync(blobBuffer.ToMemory());

                if (size != blobBuffer.Length)
                {
                    throw new InvalidDataException("Mismatch in read size");
                }
            }
            _journal.AddReading("DownloadBlob.BlobRead", 1);
            _journal.AddReading("DownloadBlob.Size", blobData.Data.BlobSize);

            dataQueue.Enqueue(new SourceData<BufferFragment>(
                blobBuffer,
                () => _journal.AddReading("DownloadBlob.BlobCommited", 1),
                null,
                blobData));
        }
    }
}