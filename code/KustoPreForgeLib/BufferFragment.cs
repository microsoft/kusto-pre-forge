using KustoPreForgeLib.LineBased;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoPreForgeLib
{
    internal class BufferFragment : IEnumerable<byte>
    {
        private readonly byte[] _buffer;
        private readonly int _offset;

        #region Constructors
        private BufferFragment(
            byte[] buffer,
            int offset,
            int length)
        {
            _buffer = buffer;
            _offset = offset;
            Length = length;
        }

        public static BufferFragment Create(int length)
        {
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length), length.ToString());
            }
            else if (length == 0)
            {
                return Empty;
            }
            else
            {
                return new BufferFragment(new byte[length], 0, length);
            }
        }
        #endregion

        public static BufferFragment Empty { get; } = new BufferFragment(new byte[0], 0, 0);

        public int Length { get; }

        public bool Any() => Length > 0;

        public bool IsContiguouslyBefore(BufferFragment other)
        {
            CheckIfSameBuffer(other);

            if (_buffer.Length != 0)
            {
                var end = (_offset + Length) % _buffer.Length;

                return end == other._offset;
            }
            else
            {
                return true;
            }
        }

        public IEnumerable<Memory<byte>> GetMemoryBlocks()
        {
            if (_offset + Length <= _buffer.Length)
            {
                yield return new Memory<byte>(_buffer, _offset, Length);
            }
            else
            {
                yield return new Memory<byte>(_buffer, _offset, _buffer.Length - _offset);
                yield return new Memory<byte>(_buffer, 0, Length - (_buffer.Length - _offset));
            }
        }

        public override string ToString()
        {
            return $"({_offset}, {(_offset + Length) % _buffer.Length}):  Length = {Length}";
        }

        #region Merge
        public BufferFragment? TryMerge(BufferFragment other)
        {
            if (Length == 0)
            {
                return other;
            }
            else if (other.Length == 0)
            {
                return this;
            }
            else
            {
                CheckIfSameBuffer(other);

                var end = (_offset + Length) % _buffer.Length;
                var otherEnd = (other._offset + other.Length) % _buffer.Length;

                if (end == other._offset)
                {
                    return new BufferFragment(_buffer, _offset, Length + other.Length);
                }
                else if (otherEnd == _offset)
                {
                    return new BufferFragment(_buffer, other._offset, Length + other.Length);
                }
                else
                {
                    return null;
                }
            }
        }

        public BufferFragment Merge(BufferFragment other)
        {
            var mergedFragment = TryMerge(other);

            if (mergedFragment != null)
            {
                return mergedFragment;
            }
            else
            {
                throw new ArgumentException(nameof(other), "Not contiguous");
            }
        }

        public (BufferFragment Fragment, IImmutableList<BufferFragment> List) TryMerge(
            IEnumerable<BufferFragment> others)
        {
            var list = new List<BufferFragment>(others);
            var indexToRemove = new List<int>(list.Count);
            var mergedFragment = this;

            do
            {
                indexToRemove.Clear();

                for (int i = 0; i != list.Count; ++i)
                {
                    var other = list[i];
                    var tryMergedFragment = mergedFragment.TryMerge(other);

                    if (tryMergedFragment != null)
                    {
                        mergedFragment = mergedFragment.Merge(other);
                        indexToRemove.Add(i);
                    }
                }
                foreach (var i in indexToRemove.Reverse<int>())
                {
                    list.RemoveAt(i);
                }
            }
            while (indexToRemove.Any() && list.Any());

            return (mergedFragment, list.ToImmutableArray());
        }
        #endregion

        #region Splice
        /// <summary>This includes the specified index and everything before.</summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public BufferFragment SpliceBefore(int index)
        {
            if (index < 0 || index > Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            if (index == Length)
            {
                return this;
            }
            else if (index == 0)
            {
                return Empty;
            }
            else
            {
                return new BufferFragment(_buffer, _offset, index);
            }
        }

        /// <summary>This excludes the specified index and includes everything after.</summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public BufferFragment SpliceAfter(int index)
        {
            if (index < -1 || index >= Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            if (index == Length - 1)
            {
                return Empty;
            }
            else if (index == -1)
            {
                return this;
            }
            else
            {
                return new BufferFragment(
                    _buffer,
                    (_offset + index + 1) % _buffer.Length,
                    Length - index - 1);
            }
        }
        #endregion

        #region IEnumerable<byte>
        IEnumerator<byte> IEnumerable<byte>.GetEnumerator()
        {
            var end = _offset + Length;

            for (int i = _offset; i != end; ++i)
            {
                yield return _buffer[i % _buffer.Length];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<byte>)this).GetEnumerator();
        }
        #endregion

        private void CheckIfSameBuffer(BufferFragment other)
        {
            if (!object.ReferenceEquals(_buffer, other._buffer))
            {
                throw new ArgumentException(nameof(other), "Not related to same buffer");
            }
        }
    }
}