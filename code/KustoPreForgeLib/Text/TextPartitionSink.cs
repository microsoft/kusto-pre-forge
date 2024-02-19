using KustoPreForgeLib.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoPreForgeLib.Text
{
    /// <summary>Distributes the data among many partitions.</summary>
    internal class TextPartitionSink : ITextSink
    {
        #region Inner types
        private class ThreadSafeCounter
        {
            private volatile int _counter = 0;

            public int GetNextCounter()
            {
                return Interlocked.Increment(ref _counter);
            }
        }
        #endregion

        private readonly Memory<byte>? _header;
        private readonly Func<Memory<byte>?, string, ITextSink> _sinkFactory;

        public TextPartitionSink(
            Memory<byte>? header, Func<Memory<byte>?, string, ITextSink> sinkFactory)
        {
            _header = header;
            _sinkFactory = sinkFactory;
        }

        async Task ITextSink.ProcessAsync(IWaitingQueue<BufferFragment> fragmentQueue)
        {
            var counter = new ThreadSafeCounter();
            var processingTasks = Enumerable.Range(0, 2 * Environment.ProcessorCount + 1)
                .Select(i => ProcessFragmentsAsync(counter, fragmentQueue))
                .ToImmutableArray();

            await Task.WhenAll(processingTasks);
        }

        private async Task ProcessFragmentsAsync(
            ThreadSafeCounter counter,
            IWaitingQueue<BufferFragment> fragmentQueue)
        {
            while (!fragmentQueue.HasCompleted)
            {
                var sink = _sinkFactory(_header, counter.GetNextCounter().ToString("00000"));

                await sink.ProcessAsync(fragmentQueue);
            }
        }
    }
}