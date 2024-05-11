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

            private readonly WorkQueue _workQueue;
            private readonly PerfCounterJournal _journal;
            private readonly IImmutableList<BlobContainerClient> _stagingContainers;
            private readonly IDictionary<int, PartitionContext> _partitionContextMap =
                new Dictionary<int, PartitionContext>();
            private int _stagingContainerIndex = 0;

            public PartitionsWriter(
                WorkQueue workQueue,
                PerfCounterJournal journal,
                IImmutableList<BlobContainerClient> stagingContainers)
            {
                _workQueue = workQueue;
                _journal = journal;
                _stagingContainers = stagingContainers;
            }

            public void Push(SinglePartitionContent content)
            {
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
                _workQueue.QueueWorkItem(() => WriteBlockAsync(
                    context,
                    content,
                    GetBlockId(blockIndex),
                    writeCompletion));
            }

            public void Flush()
            {
                foreach (var context in _partitionContextMap.Values)
                {
                    //  Avoid capture of variable
                    var partitionContext = context;

                    _workQueue.QueueWorkItem(() => FlushPartitionAsync(partitionContext));
                }
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
            }
        }
        #endregion

        private const int MAX_PARALLEL_WRITES = 16;

        private readonly IDataSource<SinglePartitionContent> _contentSource;
        private readonly IImmutableList<BlobContainerClient> _stagingContainers;
        private readonly TimeSpan _flushInterval;
        private readonly PerfCounterJournal _journal;

        public PartitionedContentSink(
            IDataSource<SinglePartitionContent> contentSource,
            IImmutableList<BlobContainerClient> stagingContainers,
            TimeSpan flushInterval,
            PerfCounterJournal journal)
        {
            _contentSource = contentSource;
            _stagingContainers = stagingContainers;
            _flushInterval = flushInterval;
            _journal = journal;
        }

        async Task ISink.ProcessSourceAsync()
        {
            var workQueue = new WorkQueue(MAX_PARALLEL_WRITES);
            Func<PartitionsWriter> writerFactory = () => new PartitionsWriter(
                workQueue,
                _journal,
                _stagingContainers);
            var writer = writerFactory();
            var intervalStart = DateTime.Now;
            var lastUnitId = Guid.NewGuid();

            await foreach (var data in _contentSource)
            {
                if (data.Data.UnitId != lastUnitId
                    && intervalStart + _flushInterval < DateTime.Now)
                {
                    writer.Flush();
                    writer = writerFactory();
                    intervalStart = DateTime.Now;
                }
                writer.Push(data.Data);
                await workQueue.ObserveCompletedAsync();
                //data.Data.Content.Release();
                //await using (data)
                //{
                //}
            }
            writer.Flush();
            await workQueue.WhenAllAsync();
        }
    }
}