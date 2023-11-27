using KustoBlobSplitLib.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoBlobSplitLib.LineBased
{
    internal interface ITextSink
    {
        Task ProcessAsync(
            Memory<byte>? header,
            IWaitingQueue<BufferFragment> fragmentQueue,
            IWaitingQueue<BufferFragment> releaseQueue);
    }
}