namespace KustoPreForgeLib.Memory
{
    internal struct BufferSubset
    {
        public BufferSubset()
            : this(Empty.Buffer, new MemoryInterval(0, 0))
        {
        }

        public BufferSubset(byte[] buffer, MemoryInterval interval)
        {
            Buffer = buffer;
            Interval = interval;
        }

        public static BufferSubset Empty { get; } =
            new BufferSubset(new byte[0], new MemoryInterval(0, 0));

        public byte[] Buffer { get; }

        public MemoryInterval Interval { get; }

        public override string ToString()
        {
            return $"({Interval.Offset}, {(Interval.Offset + Interval.Length)}):"
                + $"  Length = {Interval.Length}";
        }
    }
}