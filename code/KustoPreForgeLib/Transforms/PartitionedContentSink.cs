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
                public BlockBlobClient Blob;

                public string PartitionValueSample;

                public int BlockCount;

                public List<Task> BlockWriteTasks;
            }
            #endregion

            private readonly WorkQueue _diskWorkQueue;
            private readonly WorkQueue _blobWorkQueue;
            private readonly PerfCounterJournal _journal;
            private readonly IImmutableList<BlobContainerClient> _stagingContainers;
            private readonly string _tempDirectoryPath;
            private readonly IDictionary<int, PartitionContext> _partitionContextMap =
                new Dictionary<int, PartitionContext>();
            private readonly List<IAsyncDisposable> _sourceDataList = new();
            private readonly TaskCompletionSource _flushCompleted = new();
            private int _stagingContainerIndex = 0;
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
                _tempDirectoryPath = tempDirectoryPath;
            }

            public void Push(SourceData<SinglePartitionContent> data)
            {
                var content = data.Data;

                _sourceDataList.Add(data);
                if (!_partitionContextMap.ContainsKey(content.PartitionId))
                {
                    var container = _stagingContainers[_stagingContainerIndex];
                    var blob = container.GetBlockBlobClient(Guid.NewGuid().ToString());
                    var newContext = new PartitionContext
                    {
                        Blob = blob,
                        PartitionValueSample = content.PartitionValueSample,
                        BlockCount = 0,
                        BlockWriteTasks = new List<Task>()
                    };

                    _stagingContainerIndex = (_stagingContainerIndex + 1)
                        % _stagingContainers.Count;
                    _partitionContextMap[content.PartitionId] = newContext;
                }

                var context = _partitionContextMap[content.PartitionId];
                var blockIndex = context.BlockCount++;
                var writeCompletion = new TaskCompletionSource();

                context.BlockWriteTasks.Add(writeCompletion.Task);
                _diskWorkQueue.QueueWorkItem(() => WriteBlockAsync(
                    context,
                    content,
                    GetBlockId(blockIndex),
                    writeCompletion));
            }

            public Task FlushAsync()
            {
                _disposeCountDown = _partitionContextMap.Count;
                foreach (var context in _partitionContextMap.Values)
                {
                    //  Avoid capture of variable
                    var partitionContext = context;

                    _diskWorkQueue.QueueWorkItem(() => FlushPartitionAsync(partitionContext));
                }

                return _flushCompleted.Task;
            }

            private static string GetBlockId(int blockIndex)
            {
                var blockId = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(blockIndex.ToString()));

                return blockId;
            }

            private async Task WriteBlockAsync(
                PartitionContext context,
                SinglePartitionContent content,
                string blockId,
                TaskCompletionSource writeCompletion)
            {
                using (var stream = content.Content.ToMemoryStream())
                {
                    await context.Blob.StageBlockAsync(blockId, stream);
                }
                content.Content.Release();
                writeCompletion.SetResult();
                _journal.AddReading(
                    "PartitionedContentSink.Write.Size",
                    content.Content.Length);
            }

            private async Task FlushPartitionAsync(PartitionContext partitionContext)
            {
                await Task.WhenAll(partitionContext.BlockWriteTasks);

                var blockIds = Enumerable.Range(0, partitionContext.BlockCount)
                    .Select(index => GetBlockId(index));

                await partitionContext.Blob.CommitBlockListAsync(blockIds);

                if (Interlocked.Decrement(ref _disposeCountDown) == 0)
                {   //  Last one turn the switches off
                    //  Commit all sources
                    await Task.WhenAll(_sourceDataList.Select(d => d.DisposeAsync().AsTask()));
                    _flushCompleted.SetResult();
                }
            }
        }
        #endregion

        private const int MAX_DISK_PARALLEL_WRITES = 16;
        private const int MAX_BLOB_PARALLEL_WRITES = 16;

        private readonly IDataSource<SinglePartitionContent> _contentSource;
        private readonly IImmutableList<BlobContainerClient> _stagingContainers;
        private readonly TimeSpan _flushInterval;
        private readonly string _tempDirectoryPath;
        private readonly PerfCounterJournal _journal;

        public PartitionedContentSink(
            IDataSource<SinglePartitionContent> contentSource,
            IImmutableList<BlobContainerClient> stagingContainers,
            TimeSpan flushInterval,
            string tempDirectoryPath,
            PerfCounterJournal journal)
        {
            _contentSource = contentSource;
            _stagingContainers = stagingContainers;
            _flushInterval = flushInterval;
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
                    && intervalStart + _flushInterval < DateTime.Now)
                {   //  First wait for the previous flush to complete
                    await lastWriterTask;
                    lastWriterTask = writer.FlushAsync();
                    writer = writerFactory();
                    intervalStart = DateTime.Now;
                }
                writer.Push(data);
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