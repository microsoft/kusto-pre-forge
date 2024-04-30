using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoPreForgeLib
{
    /// <summary>
    /// Tracker of memory blocks.  Keep no memory block itself.
    /// </summary>
    internal class MemoryTracker
    {
        #region Inner Types
        private record MemoryBlock(int offset, int length)
        {
            public int End => offset + length;

            public bool HasOverlap(MemoryBlock other)
            {
                return (other.offset >= offset && other.offset < End)
                    || (other.End >= offset && other.End < End)
                    || (other.offset <= offset && other.End >= End);
            }
        }

        private class MemoryBlockComparer : IComparer<MemoryBlock>
        {
            public static IComparer<MemoryBlock> Singleton { get; } = new MemoryBlockComparer();

            int IComparer<MemoryBlock>.Compare(MemoryBlock? x, MemoryBlock? y)
            {
                if (x == null)
                {
                    throw new ArgumentNullException(nameof(x));
                }
                if (y == null)
                {
                    throw new ArgumentNullException(nameof(y));
                }

                return x.offset.CompareTo(y.offset);
            }
        }

        private record PreReservation(MemoryBlock block, TaskCompletionSource source);
        #endregion

        private readonly object _lock = new();
        private readonly List<MemoryBlock> _reservedBlocks = new();
        private readonly List<PreReservation> _preReservations = new();

        public bool IsAvailable(int offset, int length)
        {
            var testBlock = new MemoryBlock(offset, length);

            return IsAvailable(testBlock);
        }

        #region Reservation
        public void Reserve(int offset, int length)
        {
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }
            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }
            if (length == 0)
            {
                return;
            }

            lock (_lock)
            {
                var newBlock = new MemoryBlock(offset, length);
                var index = _reservedBlocks.BinarySearch(newBlock, MemoryBlockComparer.Singleton);

                if (!IsAvailable(offset, length))
                {
                    throw new InvalidOperationException("Block isn't available to reserve");
                }
                if (index >= 0)
                {   //  This should never be triggered
                    throw new InvalidOperationException("Existing block:  can't be reserved twice");
                }
                else
                {
                    var newIndex = ~index;

                    _reservedBlocks.Insert(newIndex, newBlock);
                    DoMerge(newIndex);
                }
            }
        }

        public Task ReserveAsync(int offset, int length)
        {
            lock (_lock)
            {
                if (IsAvailable(offset, length))
                {
                    return Task.CompletedTask;
                }
                else
                {
                    var source = new TaskCompletionSource();

                    _preReservations.Add(new PreReservation(
                        new MemoryBlock(offset, length),
                        source));

                    return source.Task;
                }
            }
        }

        public void Release(int offset, int length)
        {
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }
            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }
            if (length == 0)
            {
                return;
            }
            lock (_lock)
            {
                var releaseBlock = new MemoryBlock(offset, length);
                var index = _reservedBlocks.BinarySearch(
                    releaseBlock,
                    MemoryBlockComparer.Singleton);

                if (index >= 0)
                {   //  The offset match a block
                    if (_reservedBlocks[index].length == length)
                    {   //  Exact match
                        _reservedBlocks.RemoveAt(index);
                    }
                    else if (_reservedBlocks[index].length > length)
                    {   //  Existing block is bigger than release one
                        _reservedBlocks[index] = new MemoryBlock(
                            releaseBlock.End,
                            _reservedBlocks[index].length - length);
                    }
                    else
                    {   //  Existing block is smaller than release one:  doesn't work
                        throw new InvalidDataException("Release more than existing block");
                    }
                }
                else
                {
                    if (~index == 0)
                    {
                        throw new InvalidDataException("No reserved block");
                    }
                    else
                    {
                        if (_reservedBlocks[~index - 1].End > offset
                            && _reservedBlocks[~index - 1].End >= releaseBlock.End)
                        {
                            var before = new MemoryBlock(
                                _reservedBlocks[~index - 1].offset,
                                offset - _reservedBlocks[~index - 1].offset);
                            var after = new MemoryBlock(
                                releaseBlock.End,
                                _reservedBlocks[~index - 1].End - releaseBlock.End);
                            var validBlocks = new[] { before, after }
                            .Where(b => b.length > 0);

                            _reservedBlocks.RemoveAt(~index - 1);
                            _reservedBlocks.InsertRange(~index - 1, validBlocks);
                        }
                        else
                        {
                            throw new InvalidDataException("Outside reserved blocks");
                        }
                    }
                }
                RaiseTracking();
            }
        }
        #endregion

        private bool IsAvailable(MemoryBlock testBlock)
        {
            foreach (var block in _reservedBlocks)
            {
                if (block.HasOverlap(testBlock))
                {
                    return false;
                }
            }

            return true;
        }

        private void RaiseTracking()
        {
            lock (_lock)
            {
                var trackersCopy = _preReservations.ToImmutableArray();

                _preReservations.Clear();
                foreach (var tracker in trackersCopy)
                {
                    if (IsAvailable(tracker.block))
                    {
                        tracker.source.SetResult();
                    }
                    else
                    {
                        _preReservations.Add(tracker);
                    }
                }
            }
        }

        private void DoMerge(int newIndex)
        {
            var newBlock = _reservedBlocks[newIndex];

            if (_reservedBlocks.Count > newIndex + 1
                && _reservedBlocks[newIndex + 1].offset == newBlock.End)
            {   //  There is a block right after, let's merge
                newBlock = new MemoryBlock(
                    newBlock.offset,
                    newBlock.length + _reservedBlocks[newIndex + 1].length);
                _reservedBlocks.RemoveRange(newIndex, 2);
                _reservedBlocks.Insert(newIndex, newBlock);
            }
            if (newIndex > 0
                && _reservedBlocks[newIndex - 1].End == newBlock.offset)
            {   //  There is a block right before, let's merge
                newBlock = new MemoryBlock(
                    _reservedBlocks[newIndex - 1].offset,
                    newBlock.length + _reservedBlocks[newIndex - 1].length);
                _reservedBlocks.RemoveRange(newIndex - 1, 2);
                _reservedBlocks.Insert(newIndex - 1, newBlock);
            }
        }
    }
}