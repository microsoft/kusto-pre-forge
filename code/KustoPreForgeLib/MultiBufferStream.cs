using KustoPreForgeLib.Memory;
using System.Collections.Immutable;

namespace KustoPreForgeLib
{
    internal class MultiBufferStream : Stream
    {
        private readonly IImmutableList<BufferFragment> _bufferList;
        private int _bufferIndex = 0;
        private int _positionInBuffer = 0;

        public MultiBufferStream(List<BufferFragment> bufferList)
        {
            if (bufferList.Count == 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(bufferList),
                    "Should have at least one element");
            }
            _bufferList = bufferList.ToImmutableArray();
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => _bufferList.Sum(b => (long)b.Length);

        public override long Position
        {
            get => _bufferList.Take(_bufferIndex).Sum(b => (long)b.Length) + _positionInBuffer;
            set
            {
                var remainingSeek = value;

                _bufferIndex = 0;
                _positionInBuffer = 0;
                while (_bufferIndex < _bufferList.Count
                    && remainingSeek > _bufferList[_bufferIndex].Length)
                {
                    remainingSeek -= _bufferList[_bufferIndex].Length;
                    ++_bufferIndex;
                }
                if (_bufferIndex > _bufferList.Count)
                {
                    throw new ArgumentOutOfRangeException();
                }
                _positionInBuffer = (int)remainingSeek;
            }
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_bufferIndex > _bufferList.Count)
            {
                return 0;
            }
            var bytesRead = 0;

            while (count > 0 && _bufferIndex < _bufferList.Count)
            {
                var originSpan = _bufferList[_bufferIndex].ToSpan().Slice(_positionInBuffer);
                var destinationSpan = new Span<byte>(buffer, offset, count);
                var bytesToCopy = Math.Min(originSpan.Length, destinationSpan.Length);

                originSpan.Slice(0, bytesToCopy).CopyTo(destinationSpan);
                bytesRead += bytesToCopy;
                offset += bytesToCopy;
                count -= bytesToCopy;
                _positionInBuffer += bytesToCopy;

                if (_positionInBuffer >= _bufferList[_bufferIndex].Length)
                {
                    ++_bufferIndex;
                    _positionInBuffer = 0;
                }
            }

            return bytesRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    Position = offset;
                    break;

                case SeekOrigin.End:
                    Position = Length - offset;
                    break;

                case SeekOrigin.Current:
                    Position += offset;
                    break;
                default:
                    throw new NotSupportedException(origin.ToString());
            }

            return Position;
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}