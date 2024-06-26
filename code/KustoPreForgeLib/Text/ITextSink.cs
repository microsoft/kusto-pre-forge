﻿using KustoPreForgeLib.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoPreForgeLib.Text
{
    internal interface ITextSink
    {
        Task ProcessAsync(IWaitingQueue<BufferFragment> fragmentQueue);
    }
}