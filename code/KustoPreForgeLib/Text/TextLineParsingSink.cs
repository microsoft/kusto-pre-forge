using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoPreForgeLib.Text
{
    /// <summary>Take arbitrary blocks and repackage them into blocks cutting at new lines.</summary>
    internal class TextLineParsingSink : ITextSink
    {
        private const int MIN_SINK_BUFFER_SIZE = 1024 * 1024;

        private readonly Func<Memory<byte>?, ITextSink> _nextSinkFactory;
        private readonly bool _propagateHeader;

        public TextLineParsingSink(
            Func<Memory<byte>?, ITextSink> nextSinkFactory,
            bool propagateHeader)
        {
            _nextSinkFactory = nextSinkFactory;
            _propagateHeader = propagateHeader;
        }

        async Task ITextSink.ProcessAsync(IWaitingQueue<BufferFragment> inputFragmentQueue)
        {
            var outputFragmentQueue =
                new WaitingQueue<BufferFragment>() as IWaitingQueue<BufferFragment>;
            var remainingFragment = BufferFragment.Empty;
            ITextSink? nextSink = null;

            while (true)
            {
                var inputResult = await inputFragmentQueue.DequeueAsync();

                if (inputResult.IsCompleted)
                {
                    //  Push what is left (in case no \n at the end of line)
                    PushFragment(outputFragmentQueue, remainingFragment);
                    outputFragmentQueue.Complete();

                    return;
                }
                else
                {
                    var i = 0;
                    var lastNewLineIndex = 0;
                    var currentFragment = inputResult.Item!;
                    var croppedFragment = currentFragment;

                    foreach (var b in currentFragment)
                    {
                        if (b == '\n')
                        {
                            lastNewLineIndex = i;
                            if (nextSink == null)
                            {   //  First line ever
                                if (_propagateHeader)
                                {
                                    var headerFragment = currentFragment.Splice(i).Left;

                                    nextSink = _nextSinkFactory(headerFragment.ToArray());
                                    headerFragment.Release();
                                    //  Remove the header from the fragment, not to write it twice
                                    croppedFragment = currentFragment.Splice(i).Right;
                                }
                                else
                                {
                                    nextSink = _nextSinkFactory(null);
                                }
                            }
                        }
                        ++i;
                    }
                    var outputFragment = remainingFragment.TryMerge(croppedFragment);

                    if (outputFragment == null)
                    {
                        throw new InvalidOperationException("Can't merge fragments");
                    }
                    if (lastNewLineIndex == 0)
                    {
                        throw new InvalidDataException("No new line in a block");
                    }
                    outputFragmentQueue.Enqueue(outputFragment);
                    remainingFragment = croppedFragment.Splice(lastNewLineIndex).Right;
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