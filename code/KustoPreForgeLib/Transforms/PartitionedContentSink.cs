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
            private struct PartitionContext
            {
                public string LocalPath;
                public Stream WriteFileStream;
                public BlobClient Blob;
                public string PartitionValueSample;
                public long CummulatedSize;
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
            }

            public long MaxCummulatedSize { get; private set; }

            public async Task PushAsync(SourceData<SinglePartitionContent> data)
            {
                var content = data.Data;
                var localPath =
                    Path.Combine(_tempDirectoryPath, $"{_batchId}-{content.PartitionId}.txt");

                _sourceDataList.Add(data);
                if (!_partitionContextMap.ContainsKey(content.PartitionId))
                {
                    var newContext = new PartitionContext
                    {
                        LocalPath = localPath,
                        WriteFileStream = new FileStream(localPath, FileMode.CreateNew),
                        Blob = _stagingContainers[_containerIndex].GetBlobClient($"{_batchId}-{content.PartitionId}"),
                        PartitionValueSample = content.PartitionValueSample,
                        CummulatedSize = 0
                    };

                    _containerIndex = (_containerIndex + 1) % _stagingContainers.Count;
                    _partitionContextMap[content.PartitionId] = newContext;
                }

                var context = _partitionContextMap[content.PartitionId];

                await context.WriteFileStream.WriteAsync(content.Content.ToMemory());
                context.CummulatedSize += content.Content.Length;
                MaxCummulatedSize = Math.Max(MaxCummulatedSize, context.CummulatedSize);
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

            private async Task FlushPartitionAsync(PartitionContext partitionContext)
            {
                partitionContext.WriteFileStream.Close();
                await partitionContext.Blob.UploadAsync(partitionContext.LocalPath);

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
            var diskWorkQueue = new WorkQueue(MAX_DISK_PARALLEL_WRITES);
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
                    writer = writerFactory();
                    intervalStart = DateTime.Now;
                }
                await writer.PushAsync(data);
                await diskWorkQueue.ObserveCompletedAsync();
                await blobWorkQueue.ObserveCompletedAsync();
            }
            await lastWriterTask;
            await writer.FlushAsync();
            await blobWorkQueue.WhenAllAsync();
            await diskWorkQueue.WhenAllAsync();
        }
    }
}