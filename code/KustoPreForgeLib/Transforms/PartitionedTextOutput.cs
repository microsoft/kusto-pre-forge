﻿using Kusto.Cloud.Platform.Utils;
using KustoPreForgeLib.Memory;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoPreForgeLib.Transforms
{
    internal record PartitionedTextOutput(
        BufferFragment Content,
        IImmutableList<int> RecordLengths,
        IImmutableList<MemoryInterval> PartitionValues);
}