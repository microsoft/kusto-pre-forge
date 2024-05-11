﻿using Kusto.Cloud.Platform.Utils;
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
            private readonly WorkQueue _workQueue;

            public PartitionsWriter(WorkQueue workQueue)
            {
                _workQueue = workQueue;
                throw new NotImplementedException();
            }

            public void Push(SinglePartitionContent content)
            {
                throw new NotImplementedException();
            }

            public void Flush()
            {
                throw new NotImplementedException();
            }
        }
        #endregion

        private const int MAX_PARALLEL_WRITES = 16;

        private readonly IDataSource<SinglePartitionContent> _contentSource;
        private readonly TimeSpan _flushInterval;
        private readonly PerfCounterJournal _journal;

        public PartitionedContentSink(
            IDataSource<SinglePartitionContent> contentSource,
            TimeSpan flushInterval,
            PerfCounterJournal journal)
        {
            _contentSource = contentSource;
            _flushInterval = flushInterval;
            _journal = journal;
        }

        async Task ISink.ProcessSourceAsync()
        {
            var workQueue = new WorkQueue(MAX_PARALLEL_WRITES);
            Func<PartitionsWriter> writerFactory = () => new PartitionsWriter(workQueue);
            var writer = writerFactory();
            var intervalStart = DateTime.Now;
            var lastUnitId = Guid.NewGuid();

            await foreach (var data in _contentSource)
            {
                if (data.Data.UnitId != lastUnitId
                    && intervalStart + _flushInterval > DateTime.Now)
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
            await workQueue.WhenAllAsync();
        }
    }
}