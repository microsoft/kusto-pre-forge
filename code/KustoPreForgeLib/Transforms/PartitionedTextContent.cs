using KustoPreForgeLib.Memory;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoPreForgeLib.Transforms
{
    internal record PartitionedTextContent(
        BufferFragment Content,
        IImmutableList<int> RecordLengths,
        IImmutableList<int> PartitionIds,
        IImmutableDictionary<int, string> PartitionValueSamples);
}