namespace KustoPreForgeLib.Memory
{
    internal struct BufferSubset
    {
        public BufferSubset()
            : this(Empty.Buffer, 0, 0)
        {
        }

        public BufferSubset(byte[] buffer, int offset, int length)
        {
            Buffer = buffer;
            Offset = offset;
            Length = length;
        }

        public static BufferSubset Empty { get; } = new BufferSubset(new byte[0], 0, 0);

        public byte[] Buffer { get; }

        public int Offset { get; }

        public int Length { get; }

        public override string ToString()
        {
            return $"({Offset}, {(Offset + Length)}):  Length = {Buffer.Length}";
        }
    }
}