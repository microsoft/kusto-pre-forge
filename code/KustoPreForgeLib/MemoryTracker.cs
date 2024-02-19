using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoPreForgeLib
{
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

        private record Tracker(MemoryBlock block, TaskCompletionSource source);

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
        #endregion

        private readonly object _lock = new();
        private readonly List<MemoryBlock> _reservedBlocks = new();
        private readonly List<Tracker> _trackers = new();

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

                if (index >= 0)
                {
                    throw new InvalidDataException("Existing block:  can't be reserved twice");
                }
                else
                {
                    _reservedBlocks.Insert(index, newBlock);
                    ValidateReservedBlocks();
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

                if (~index == 0)
                {
                    throw new InvalidDataException("No reserved block");
                }

                var existingBlock = _reservedBlocks[index < 0 ? ~index : index];

                _reservedBlocks.RemoveAt(~index);
                //  Bit before the release
                Reserve(existingBlock.offset, Math.Max(0, offset - existingBlock.offset));
                //  Bit after the release
                Reserve(offset + length, Math.Max(0, existingBlock.offset + existingBlock.length - (offset + length)));
                //  Bit after the existing block
                Release(
                    existingBlock.offset + existingBlock.length,
                    Math.Max(0, length - existingBlock.length));
                ValidateReservedBlocks();
                RaiseTracking();
            }
        }

        public Task TrackAsync(int offset, int length)
        {
            lock (_lock)
            {
                var source = new TaskCompletionSource();

                _trackers.Add(new Tracker(
                    new MemoryBlock(offset, length),
                    source));

                return source.Task;
            }
        }

        private void ValidateReservedBlocks()
        {
            lock (_lock)
            {
                MemoryBlock? previousBlock = null;

                foreach (var block in _reservedBlocks)
                {
                    if (previousBlock == null)
                    {
                        previousBlock = block;
                    }
                    else
                    {
                        if (block.offset <= previousBlock.offset)
                        {
                            throw new InvalidDataException("Two blocks in wrong order");
                        }
                        if (block.offset < previousBlock.End)
                        {
                            throw new InvalidDataException("Embedded blocks");
                        }
                    }
                }
            }
        }

        private void RaiseTracking()
        {
            lock (_lock)
            {
                var trackersCopy = _trackers.ToImmutableArray();

                _trackers.Clear();
                foreach (var tracker in trackersCopy)
                {
                    var hasOverlap = false;

                    foreach (var block in _reservedBlocks)
                    {
                        if (block.HasOverlap(tracker.block))
                        {
                            hasOverlap = true;
                            break;
                        }
                    }
                    if (hasOverlap)
                    {
                        _trackers.Add(tracker);
                    }
                    else
                    {
                        tracker.source.SetResult();
                    }
                }
            }
        }
    }
}