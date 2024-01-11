using KustoPreForgeLib.LineBased;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Kusto.Cloud.Platform.Utils.CachedBufferEncoder;

namespace KustoPreForgeLib.Text
{
    internal class TextLineParsingSink : ITextSink
    {
        private const int MIN_SINK_BUFFER_SIZE = 1024 * 1024;

        private readonly ITextSink _nextSink;
        private readonly bool _propagateHeader;

        public TextLineParsingSink(ITextSink nextSink, bool propagateHeader)
        {
            _nextSink = nextSink;
            _propagateHeader = propagateHeader;
        }

        async Task ITextSink.ProcessAsync(
            Memory<byte>? header,
            IWaitingQueue<BufferFragment> inputFragmentQueue,
            IWaitingQueue<BufferFragment> releaseQueue)
        {
            if (header != null)
            {
                throw new ArgumentOutOfRangeException(nameof(header));
            }

            var outputFragmentQueue =
                new WaitingQueue<BufferFragment>() as IWaitingQueue<BufferFragment>;
            var toPushFragment = BufferFragment.Empty;
            var sinkTask = _propagateHeader
                ? null
                : _nextSink.ProcessAsync(null, outputFragmentQueue, releaseQueue);

            while (true)
            {
                var inputResult = await TaskHelper.AwaitAsync(
                    inputFragmentQueue.DequeueAsync(),
                    sinkTask);

                if (inputResult.IsCompleted)
                {
                    //  Push what is left (in case no \n at the end of line)
                    PushFragment(outputFragmentQueue, toPushFragment);
                    outputFragmentQueue.Complete();
                    if (sinkTask != null)
                    {
                        await sinkTask;
                    }

                    return;
                }
                else
                {
                    var i = 0;
                    var workingFragment = inputResult.Item!;
                    var remainingFragment = workingFragment;

                    foreach (var b in workingFragment)
                    {
                        if (b == '\n')
                        {   //  sinkTask==null => has header
                            if (toPushFragment.Length + i >= MIN_SINK_BUFFER_SIZE || sinkTask == null)
                            {
                                toPushFragment = toPushFragment.Merge(remainingFragment.SpliceBefore(i + 1));
                                //  Remove it from remaining Fragment
                                remainingFragment = remainingFragment.SpliceAfter(i);
                                i = 0;
                                if (sinkTask == null)
                                {
                                    //  Init sink task with header
                                    sinkTask = _nextSink.ProcessAsync(
                                        header,
                                        outputFragmentQueue,
                                        releaseQueue);
                                    //  We release the bytes immediately as they are kept in memory
                                    releaseQueue.Enqueue(toPushFragment);
                                }
                                else
                                {
                                    PushFragment(outputFragmentQueue, toPushFragment);
                                }
                                toPushFragment = BufferFragment.Empty;
                            }
                            else
                            {
                                ++i;
                            }
                        }
                        else
                        {
                            ++i;
                        }
                    }
                    toPushFragment = toPushFragment.Merge(remainingFragment);
                }
            }
        }

        private void PushFragment(
            IWaitingQueue<BufferFragment> outputFragmentQueue,
            BufferFragment fragment)
        {
            if (fragment.Any())
            {
                outputFragmentQueue.Enqueue(fragment);
            }
        }
    }
}