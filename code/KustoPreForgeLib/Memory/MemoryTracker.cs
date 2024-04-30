using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoPreForgeLib.Memory
{
    /// <summary>
    /// Tracker of memory blocks.  Keep no memory block itself.
    /// </summary>
    internal class MemoryTracker
    {
        #region Inner Types
        private class MemoryBlockComparer : IComparer<MemoryInterval>
        {
            public static IComparer<MemoryInterval> Singleton { get; } = new MemoryBlockComparer();

            int IComparer<MemoryInterval>.Compare(MemoryInterval x, MemoryInterval y)
            {
                return x.Offset.CompareTo(y.Offset);
            }
        }

        private record PreReservation(MemoryInterval block, TaskCompletionSource source);
        #endregion

        private readonly object _lock = new();
        private readonly List<MemoryInterval> _reservedIntervals = new();
        private readonly List<PreReservation> _preReservations = new();

        public bool IsAvailable(MemoryInterval interval)
        {
            foreach (var block in _reservedIntervals)
            {
                if (block.HasOverlap(interval))
                {
                    return false;
                }
            }

            return true;
        }

        #region Reservation
        public void Reserve(MemoryInterval interval)
        {
            if (interval.Length < 0 || interval.Offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(interval));
            }
            if (interval.Length == 0)
            {
                return;
            }

            lock (_lock)
            {
                var index =
                    _reservedIntervals.BinarySearch(interval, MemoryBlockComparer.Singleton);

                if (!IsAvailable(interval))
                {
                    throw new InvalidOperationException("Interval isn't available to reserve");
                }
                if (index >= 0)
                {   //  This should never be triggered
                    throw new InvalidOperationException("Existing block:  can't be reserved twice");
                }
                else
                {
                    var newIndex = ~index;

                    _reservedIntervals.Insert(newIndex, interval);
                    DoMerge(newIndex);
                }
            }
        }

        public Task ReserveAsync(MemoryInterval interval)
        {
            lock (_lock)
            {
                if (IsAvailable(interval))
                {
                    return Task.CompletedTask;
                }
                else
                {
                    var source = new TaskCompletionSource();

                    _preReservations.Add(new PreReservation(interval, source));

                    return source.Task;
                }
            }
        }
        #endregion

        public void Release(MemoryInterval interval)
        {
            if (interval.Length < 0 || interval.Offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(interval));
            }
            if (interval.Length == 0)
            {
                return;
            }
            lock (_lock)
            {
                var index =
                    _reservedIntervals.BinarySearch(interval, MemoryBlockComparer.Singleton);

                if (index >= 0)
                {   //  The offset match a block
                    if (_reservedIntervals[index].Length == interval.Length)
                    {   //  Exact match
                        _reservedIntervals.RemoveAt(index);
                    }
                    else if (_reservedIntervals[index].Length > interval.Length)
                    {   //  Existing block is bigger than release one
                        _reservedIntervals[index] = new MemoryInterval(
                            interval.End,
                            _reservedIntervals[index].Length - interval.Length);
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
                        if (_reservedIntervals[~index - 1].End > interval.Offset
                            && _reservedIntervals[~index - 1].End >= interval.End)
                        {
                            var before = new MemoryInterval(
                                _reservedIntervals[~index - 1].Offset,
                                interval.Offset - _reservedIntervals[~index - 1].Offset);
                            var after = new MemoryInterval(
                                interval.End,
                                _reservedIntervals[~index - 1].End - interval.End);
                            var validBlocks = new[] { before, after }
                            .Where(b => b.Length > 0);

                            _reservedIntervals.RemoveAt(~index - 1);
                            _reservedIntervals.InsertRange(~index - 1, validBlocks);
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
            var newBlock = _reservedIntervals[newIndex];

            if (_reservedIntervals.Count > newIndex + 1
                && _reservedIntervals[newIndex + 1].Offset == newBlock.End)
            {   //  There is a block right after, let's merge
                newBlock = new MemoryInterval(
                    newBlock.Offset,
                    newBlock.Length + _reservedIntervals[newIndex + 1].Length);
                _reservedIntervals.RemoveRange(newIndex, 2);
                _reservedIntervals.Insert(newIndex, newBlock);
            }
            if (newIndex > 0
                && _reservedIntervals[newIndex - 1].End == newBlock.Offset)
            {   //  There is a block right before, let's merge
                newBlock = new MemoryInterval(
                    _reservedIntervals[newIndex - 1].Offset,
                    newBlock.Length + _reservedIntervals[newIndex - 1].Length);
                _reservedIntervals.RemoveRange(newIndex - 1, 2);
                _reservedIntervals.Insert(newIndex - 1, newBlock);
            }
        }
    }
}