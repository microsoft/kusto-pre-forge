using KustoBlobSplitLib.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoBlobSplitLib.LineBased
{
    internal class TextSplitSink : ITextSink
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

        private readonly Func<string, ITextSink> _sinkFactory;

        public TextSplitSink(Func<string, ITextSink> sinkFactory)
        {
            _sinkFactory = sinkFactory;
        }

        async Task ITextSink.ProcessAsync(
            Memory<byte>? header,
            IWaitingQueue<BufferFragment> fragmentQueue,
            IWaitingQueue<BufferFragment> releaseQueue)
        {
            var counter = new ThreadSafeCounter();
            var processingTasks = Enumerable.Range(0, 2 * Environment.ProcessorCount + 1)
                .Select(i => ProcessFragmentsAsync(
                    counter,
                    header,
                    fragmentQueue,
                    releaseQueue))
                .ToImmutableArray();

            await Task.WhenAll(processingTasks);
        }

        private async Task ProcessFragmentsAsync(
            ThreadSafeCounter counter,
            Memory<byte>? header,
            IWaitingQueue<BufferFragment> fragmentQueue,
            IWaitingQueue<BufferFragment> releaseQueue)
        {
            while (!fragmentQueue.HasCompleted)
            {
                var sink = _sinkFactory(counter.GetNextCounter().ToString("00000"));

                await sink.ProcessAsync(header, fragmentQueue, releaseQueue);
            }
        }
    }
}