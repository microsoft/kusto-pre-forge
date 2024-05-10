using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoPreForgeLib.Transforms
{
    internal class PartitioningHelper
    {
        public static Func<ReadOnlyMemory<byte>, int> GetPartitionStringFunction(
            int maxPartitionCount,
            int seed)
        {
            return buffer =>
            {
                var hash = seed;

                foreach (var b in buffer.Span)
                {
                    hash ^= b;
                }

                return hash % maxPartitionCount;
            };
        }
    }
}