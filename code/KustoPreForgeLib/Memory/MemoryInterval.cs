using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

namespace KustoPreForgeLib.Memory
{
    internal record struct MemoryInterval(int Offset, int Length)
    {
        public int End => Offset + Length;

        public bool HasOverlap(MemoryInterval other)
        {
            return Intersect(other).Length > 0;
        }

        public MemoryInterval Intersect(MemoryInterval interval)
        {
            var start = Math.Max(Offset, interval.Offset);
            var end = Math.Min(End, interval.End);

            return new MemoryInterval(start, Math.Max(0, end - start));
        }

        public override string ToString()
        {
            return $"Offset:  {Offset}, Length:  {Length}";
        }
    }
}