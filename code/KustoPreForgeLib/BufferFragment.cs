using Kusto.Cloud.Platform.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoPreForgeLib
{
    internal class BufferFragment : IEnumerable<byte>, IDisposable
    {
        private readonly IImmutableList<ReferenceCounter> _counters;
        private readonly BufferSubset _bufferSubset;

        #region Constructors
        public BufferFragment(ReferenceCounter counter, BufferSubset bufferSubset)
            : this(new[] { counter }, bufferSubset)
        {
        }

        private  BufferFragment(IEnumerable<ReferenceCounter> counters, BufferSubset bufferSubset)
        {
            _counters = counters.ToImmutableArray();
            _counters.ForEach(c => c.Increment());
            _bufferSubset = bufferSubset;
        }
        #endregion

        public static BufferFragment Empty { get; } =
            new BufferFragment(new ReferenceCounter[0], BufferSubset.Empty);

        public int Length => _bufferSubset.Length;

        public bool Any() => Length > 0;

        public Memory<byte> ToMemoryBlock()
        {
            return new Memory<byte>(_bufferSubset.Buffer, _bufferSubset.Offset, Length);
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
                other._counters.ForEach(c => c.Increment());

                return other;
            }
            else if (other.Length == 0)
            {
                _counters.ForEach(c => c.Increment());

                return this;
            }
            else
            {
                if (!object.ReferenceEquals(_bufferSubset.Buffer, other._bufferSubset.Buffer))
                {
                    throw new ArgumentException(nameof(other), "Not related to same buffer");
                }

                var end = _bufferSubset.Offset + Length;
                var otherEnd = other._bufferSubset.Offset + other.Length;

                if (end == other._bufferSubset.Offset)
                {
                    return new BufferFragment(
                        _counters.Concat(other._counters),
                        new BufferSubset(
                            _bufferSubset.Buffer,
                            _bufferSubset.Offset,
                            Length + other.Length));
                }
                else if (otherEnd == _bufferSubset.Offset)
                {
                    return new BufferFragment(
                        _counters.Concat(other._counters),
                        new BufferSubset(
                            _bufferSubset.Buffer,
                            other._bufferSubset.Offset,
                            Length + other.Length));
                }
                else
                {
                    return null;
                }
            }
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
                _counters.ForEach(c => c.Increment());
                
                return this;
            }
            else if (index == 0)
            {
                return Empty;
            }
            else
            {
                return new BufferFragment(
                    _counters,
                    new BufferSubset(_bufferSubset.Buffer, _bufferSubset.Offset, index));
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
                _counters.ForEach(c => c.Increment());

                return this;
            }
            else
            {
                return new BufferFragment(
                    _counters,
                    new BufferSubset(
                        _bufferSubset.Buffer,
                        (_bufferSubset.Offset + index + 1) % _bufferSubset.Buffer.Length,
                        Length - index - 1));
            }
        }
        #endregion

        #region IEnumerable<byte>
        IEnumerator<byte> IEnumerable<byte>.GetEnumerator()
        {
            var end = _bufferSubset.Offset + Length;

            for (int i = _bufferSubset.Offset; i != end; ++i)
            {
                yield return _bufferSubset.Buffer[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<byte>)this).GetEnumerator();
        }
        #endregion

        #region IDisposable
        void IDisposable.Dispose()
        {
            _counters.ForEach(c => c.Decrement());
        }
        #endregion
    }
}