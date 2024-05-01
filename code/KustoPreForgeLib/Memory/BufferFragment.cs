using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoPreForgeLib.Memory
{
    /// <summary>Fragment of a byte buffer.</summary>>
    internal class BufferFragment : IEnumerable<byte>
    {
        private readonly BufferSubset _bufferSubset;
        private readonly MemoryTracker _memoryTracker;

        #region Constructors
        public BufferFragment(int size)
            : this(
                  new BufferSubset(new byte[size], new MemoryInterval(0, size)),
                  new MemoryTracker())
        {
        }

        private BufferFragment(BufferSubset bufferSubset, MemoryTracker memoryTracker)
        {
            _bufferSubset = bufferSubset;
            _memoryTracker = memoryTracker;
        }
        #endregion

        public static BufferFragment Empty { get; } = new BufferFragment(
            BufferSubset.Empty,
            new MemoryTracker());

        public int Length => _bufferSubset.Interval.Length;

        public bool Any() => Length > 0;

        public Memory<byte> ToMemoryBlock()
        {
            return new Memory<byte>(
                _bufferSubset.Buffer,
                _bufferSubset.Interval.Offset,
                Length);
        }

        public override string ToString()
        {
            return _bufferSubset.ToString();
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
                if (!object.ReferenceEquals(_bufferSubset.Buffer, other._bufferSubset.Buffer))
                {
                    throw new ArgumentException(nameof(other), "Not related to same buffer");
                }

                var end = _bufferSubset.Interval.Offset + Length;
                var otherEnd = other._bufferSubset.Interval.Offset + other.Length;

                if (end == other._bufferSubset.Interval.Offset)
                {
                    return new BufferFragment(
                        new BufferSubset(
                            _bufferSubset.Buffer,
                            new MemoryInterval(
                                _bufferSubset.Interval.Offset,
                                Length + other.Length)),
                        _memoryTracker);
                }
                else if (otherEnd == _bufferSubset.Interval.Offset)
                {
                    return new BufferFragment(
                        new BufferSubset(
                            _bufferSubset.Buffer,
                            new MemoryInterval(
                                other._bufferSubset.Interval.Offset,
                                Length + other.Length)),
                        _memoryTracker);
                }
                else
                {
                    return null;
                }
            }
        }
        #endregion

        #region Splice
        /// <summary>
        /// The left part includes the specified index and everything before while the right part
        /// excludes the specified index and includes everything after.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public (BufferFragment Left, BufferFragment Right) Splice(int index)
        {
            if (index < 0 || index > Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            if (index == Length)
            {
                return (this, Empty);
            }
            else if (index == 0)
            {
                return (Empty, this);
            }
            else
            {
                var left = new BufferFragment(
                    new BufferSubset(
                        _bufferSubset.Buffer,
                        new MemoryInterval(_bufferSubset.Interval.Offset, index)),
                    _memoryTracker);
                var right = new BufferFragment(
                    new BufferSubset(
                        _bufferSubset.Buffer,
                        new MemoryInterval(
                            _bufferSubset.Interval.Offset + index,
                            Length - index)),
                    _memoryTracker);

                return (left, right);
            }
        }
        #endregion

        #region IEnumerable<byte>
        IEnumerator<byte> IEnumerable<byte>.GetEnumerator()
        {
            var end = _bufferSubset.Interval.End;

            for (int i = _bufferSubset.Interval.Offset; i != end; ++i)
            {
                yield return _bufferSubset.Buffer[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<byte>)this).GetEnumerator();
        }
        #endregion

        #region Memory Tracking
        public void Reserve()
        {
            _memoryTracker.Reserve(_bufferSubset.Interval);
        }

        public void Release()
        {
            _memoryTracker.Release(_bufferSubset.Interval);
        }

        public Task ReserveAsync()
        {
            return _memoryTracker.ReserveAsync(_bufferSubset.Interval);
        }

        public async Task<BufferFragment> ReserveSubBufferAsync(int length)
        {
            var reservedInterval = await _memoryTracker.ReserveWithinAsync(
                _bufferSubset.Interval,
                length);
            var reservedSubset = new BufferSubset(_bufferSubset.Buffer, reservedInterval);

            return new BufferFragment(reservedSubset, _memoryTracker);
        }
        #endregion
    }
}