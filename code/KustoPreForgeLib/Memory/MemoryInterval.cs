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
            return (other.Offset >= Offset && other.Offset < End)
                || (other.End >= Offset && other.End < End)
                || (other.Offset <= Offset && other.End >= End);
        }

        public override string ToString()
        {
            return $"Offset:  {Offset}, Length:  {Length}";
        }
    }
}