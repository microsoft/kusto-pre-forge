using KustoPreForgeLib.Memory;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoPreForgeLib.Transforms
{
    internal record SinglePartitionContent(
        BufferFragment Content,
        Guid UnitId,
        int PartitionId,
        string PartitionValueSample);
}