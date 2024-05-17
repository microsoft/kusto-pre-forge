using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Kusto.Cloud.Platform.Utils;
using KustoPreForgeLib.Memory;
using System.Collections.Immutable;
using System.Text;

namespace KustoPreForgeLib.Transforms
{
    internal class PartitionedContentSink : ISink
    {
        #region Inner Types
        private class PartitionsWriter
        {
            #region Inner Types
            private class PartitionContext
            {
                public PartitionContext(
                    string localPath,
                    Stream writeFileStream,
                    BlobClient blob,
                    string partitionValueSample)
                {
                    LocalPath = localPath;
                    WriteFileStream = writeFileStream;
                    Blob = blob;
                    PartitionValueSample = partitionValueSample;
                }

                public string LocalPath;
                public Stream WriteFileStream;
                public BlobClient Blob;
                public string PartitionValueSample;
                public long CummulatedSize;
                public Task LastWriteOperation = Task.CompletedTask;
            }
            #endregion

            private readonly string _batchId = Guid.NewGuid().ToString();
            private readonly WorkQueue _diskWorkQueue;
            private readonly WorkQueue _blobWorkQueue;
            private readonly PerfCounterJournal _journal;
            private readonly IImmutableList<BlobContainerClient> _stagingContainers;
            private readonly string _tempDirectoryPath;
            private readonly IDictionary<int, PartitionContext> _partitionContextMap =
                new Dictionary<int, PartitionContext>();
            private readonly List<IAsyncDisposable> _sourceDataList = new();
            private readonly TaskCompletionSource _flushCompleted = new();
            private int _containerIndex = 0;
            private volatile int _disposeCountDown;

            public PartitionsWriter(
                WorkQueue diskWorkQueue,
                WorkQueue blobWorkQueue,
                PerfCounterJournal journal,
                IImmutableList<BlobContainerClient> stagingContainers,
                string tempDirectoryPath)
            {
                _diskWorkQueue = diskWorkQueue;
                _blobWorkQueue = blobWorkQueue;
                _journal = journal;
                _stagingContainers = stagingContainers;
                _tempDirectoryPath = Path.Combine(tempDirectoryPath, _batchId);
                Directory.CreateDirectory(_tempDirectoryPath);
            }

            public long MaxCummulatedSize { get; private set; }

            public void Push(SourceData<SinglePartitionContent> data)
            {
                var content = data.Data;
                var localPath =
                    Path.Combine(_tempDirectoryPath, $"{_batchId}-{content.PartitionId}.txt");

                _sourceDataList.Add(data);
                if (!_partitionContextMap.ContainsKey(content.PartitionId))
                {
                    var blob = _stagingContainers[_containerIndex]
                        .GetBlobClient($"{_batchId}-{content.PartitionId}");
                    var newContext = new PartitionContext(
                        localPath,
                        new FileStream(localPath, new FileStreamOptions
                        {   //  We do not want extra buffering since we manage buffer already
                            BufferSize = 0,
                            Access = FileAccess.Write,
                            Mode = FileMode.CreateNew
                        }),
                        blob,
                        content.PartitionValueSample);

                    _containerIndex = (_containerIndex + 1) % _stagingContainers.Count;
                    _partitionContextMap[content.PartitionId] = newContext;
                }

                var context = _partitionContextMap[content.PartitionId];
                var source = new TaskCompletionSource();
                var lastWriteOperation = context.LastWriteOperation;

                context.CummulatedSize += content.Content.Length;
                MaxCummulatedSize = Math.Max(MaxCummulatedSize, context.CummulatedSize);
                _diskWorkQueue.QueueWorkItem(() => PushToDiskAsync(
                    context,
                    lastWriteOperation,
                    content.Content));
            }

            public Task FlushAsync()
            {
                if (_partitionContextMap.Any())
                {
                    _disposeCountDown = _partitionContextMap.Count;
                    foreach (var context in _partitionContextMap.Values)
                    {
                        //  Avoid capture of variable
                        var partitionContext = context;

                        _blobWorkQueue.QueueWorkItem(() => FlushPartitionAsync(partitionContext));
                    }

                    return _flushCompleted.Task;
                }
                else
                {
                    return Task.CompletedTask;
                }
            }

            private static async Task PushToDiskAsync(
                PartitionContext context,
                Task lastWriteOperation,
                BufferFragment content)
            {
                await lastWriteOperation;
                await context.WriteFileStream.WriteAsync(content.ToMemory());
                content.Release();
            }

            private async Task FlushPartitionAsync(PartitionContext partitionContext)
            {
                partitionContext.WriteFileStream.Close();
                //await partitionContext.Blob.UploadAsync(partitionContext.LocalPath);
                _journal.AddReading("Blob.Size", partitionContext.CummulatedSize);

                if (Interlocked.Decrement(ref _disposeCountDown) == 0)
                {   //  Last one turn the switches off
                    await CommitAllSourcesAsync();
                }
            }

            private async Task CommitAllSourcesAsync()
            {
                await Task.WhenAll(_sourceDataList.Select(d => d.DisposeAsync().AsTask()));
                Directory.Delete(_tempDirectoryPath, true);
                _flushCompleted.SetResult();
            }
        }
        #endregion

        private const int MAX_DISK_PARALLEL_WRITES = 16;
        private const int MAX_BLOB_PARALLEL_WRITES = 16;

        private readonly IDataSource<SinglePartitionContent> _contentSource;
        private readonly IImmutableList<BlobContainerClient> _stagingContainers;
        private readonly TimeSpan _flushInterval;
        private readonly long _maxBlobSize;
        private readonly string _tempDirectoryPath;
        private readonly PerfCounterJournal _journal;

        public PartitionedContentSink(
            IDataSource<SinglePartitionContent> contentSource,
            IImmutableList<BlobContainerClient> stagingContainers,
            TimeSpan flushInterval,
            long maxBlobSize,
            string tempDirectoryPath,
            PerfCounterJournal journal)
        {
            _contentSource = contentSource;
            _stagingContainers = stagingContainers;
            _flushInterval = flushInterval;
            _maxBlobSize = maxBlobSize;
            _tempDirectoryPath = tempDirectoryPath;
            _journal = journal;
        }

        async Task ISink.ProcessSourceAsync()
        {
            var diskWorkQueue = new WorkQueue(MAX_BLOB_PARALLEL_WRITES);
            var blobWorkQueue = new WorkQueue(MAX_BLOB_PARALLEL_WRITES);
            Func<PartitionsWriter> writerFactory = () => new PartitionsWriter(
                diskWorkQueue,
                blobWorkQueue,
                _journal,
                _stagingContainers,
                _tempDirectoryPath);
            var writer = writerFactory();
            var intervalStart = DateTime.Now;
            var lastUnitId = Guid.NewGuid();
            var lastWriterTask = Task.CompletedTask;

            await foreach (var data in _contentSource)
            {
                if (data.Data.UnitId != lastUnitId
                    && (writer.MaxCummulatedSize >= _maxBlobSize
                    || intervalStart + _flushInterval < DateTime.Now))
                {   //  First wait for the previous flush to complete
                    await lastWriterTask;
                    lastWriterTask = writer.FlushAsync();
                    await diskWorkQueue.ObserveCompletedAsync();
                    await blobWorkQueue.ObserveCompletedAsync();
                    writer = writerFactory();
                    intervalStart = DateTime.Now;
                }
                writer.Push(data);
            }
            await lastWriterTask;
            await writer.FlushAsync();
            await diskWorkQueue.WhenAllAsync();
            await blobWorkQueue.WhenAllAsync();
        }
    }
}