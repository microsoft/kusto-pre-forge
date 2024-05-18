using Azure.Storage.Blobs;
using Kusto.Cloud.Platform.Utils;
using KustoPreForgeLib.Memory;
using System.Collections.Concurrent;
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
                public PartitionContext(BlobClient blob, string partitionValueSample)
                {
                    Blob = blob;
                    PartitionValueSample = partitionValueSample;
                }

                public BlobClient Blob;

                public Stream? BlobStream = null;

                public string PartitionValueSample;

                public volatile Task StreamingTask = Task.CompletedTask;

                public List<Task> WritingTasks = new();

                public ConcurrentQueue<(BufferFragment, TaskCompletionSource)> BufferQueue =
                    new();
            }
            #endregion

            private readonly WorkQueue _workQueue;
            private readonly PerfCounterJournal _journal;
            private readonly IImmutableList<BlobContainerClient> _stagingContainers;
            private readonly IDictionary<int, PartitionContext> _partitionContextMap =
                new Dictionary<int, PartitionContext>();
            private readonly List<IAsyncDisposable> _sourceDataList = new();
            private int _stagingContainerIndex = 0;
            private volatile int _disposeCountDown;

            public PartitionsWriter(
                WorkQueue workQueue,
                PerfCounterJournal journal,
                IImmutableList<BlobContainerClient> stagingContainers)
            {
                _workQueue = workQueue;
                _journal = journal;
                _stagingContainers = stagingContainers;
            }

            public void Push(SourceData<SinglePartitionContent> data)
            {
                var content = data.Data;

                _sourceDataList.Add(data);
                if (!_partitionContextMap.ContainsKey(content.PartitionId))
                {
                    var container = _stagingContainers[_stagingContainerIndex];
                    var blob = container.GetBlobClient(Guid.NewGuid().ToString());
                    var newContext = new PartitionContext(blob, content.PartitionValueSample);

                    _stagingContainerIndex = (_stagingContainerIndex + 1)
                        % _stagingContainers.Count;
                    _partitionContextMap[content.PartitionId] = newContext;
                }

                var context = _partitionContextMap[content.PartitionId];
                var writeCompletion = new TaskCompletionSource();

                context.WritingTasks.Add(writeCompletion.Task);
                context.BufferQueue.Enqueue((content.Content, writeCompletion));
                _workQueue.QueueWorkItem(() => WriteBlockAsync(context));
            }

            public void Flush()
            {
                _disposeCountDown = _partitionContextMap.Count;
                foreach (var context in _partitionContextMap.Values)
                {
                    //  Avoid capture of variable
                    var partitionContext = context;

                    _workQueue.QueueWorkItem(() => FlushPartitionAsync(partitionContext));
                }
            }

            private async Task WriteBlockAsync(PartitionContext context)
            {
                var oldStreamingTask = context.StreamingTask;

                if (oldStreamingTask.IsCompleted)
                {
                    var newStreamingSource = new TaskCompletionSource();
                    var confirmedOldStreamingTask = Interlocked.CompareExchange(
                        ref context.StreamingTask,
                        newStreamingSource.Task,
                        oldStreamingTask);

                    if (object.ReferenceEquals(confirmedOldStreamingTask, oldStreamingTask))
                    {
                        if (context.BlobStream == null)
                        {
                            context.BlobStream = await context.Blob.OpenWriteAsync(true);
                        }
                        while (context.BufferQueue.TryDequeue(out var pair))
                        {
                            var buffer = pair.Item1;
                            var bufferSource = pair.Item2;

                            await context.BlobStream.WriteAsync(buffer.ToMemory());
                            bufferSource.SetResult();
                            _journal.AddReading("PartitionedContentSink.Write.Size", buffer.Length);
                            buffer.Release();
                        }
                        //  Release the "lock" on writing
                        newStreamingSource.SetResult();
                        if (context.BufferQueue.Any())
                        {   //  Make sure we didn't hit a racing condition and left items in queue
                            await WriteBlockAsync(context);
                        }
                    }
                }
            }

            private async Task FlushPartitionAsync(PartitionContext partitionContext)
            {
                await Task.WhenAll(partitionContext.WritingTasks);

                await partitionContext.BlobStream!.DisposeAsync();

                if (Interlocked.Decrement(ref _disposeCountDown) == 0)
                {   //  Last one turn the switches off
                    //  Commit all sources
                    await Task.WhenAll(_sourceDataList.Select(d => d.DisposeAsync().AsTask()));
                }
            }
        }
        #endregion

        private const int MAX_PARALLEL_WRITES = 1;

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
                writer.Push(data);
                if (!workQueue.HasCapacity)
                {
                }
                await workQueue.ObserveCompletedAsync();
            }
            writer.Flush();
            await workQueue.WhenAllAsync();
        }
    }
}