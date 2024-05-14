using Azure.Storage.Sas;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoPreForgeLib.Memory
{
    /// <summary>
    /// Tracker of memory blocks.  Doesn't hold memory buffer itself.
    /// </summary>
    /// <remarks>
    /// Implementation is based on optimistic concurrency.  There are no locks (as we
    /// had deadlock issues before).  Instead an internal state keeps a readonly state
    /// which is managed by transitionning from state to state in an atomic way.
    /// </remarks>
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

        private record PreReservation(
            MemoryInterval interval,
            int? length,
            TaskCompletionSource<MemoryInterval> source);

        private record InternalState(
            ImmutableArray<MemoryInterval> ReservedIntervals,
            ImmutableArray<PreReservation> PreReservations)
        {
            public InternalState? TryReserve(MemoryInterval interval)
            {
                if (interval.Length < 0 || interval.Offset < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(interval));
                }
                if (interval.Length == 0)
                {
                    return this;
                }

                var index = ReservedIntervals.BinarySearch(
                    interval,
                    MemoryBlockComparer.Singleton);

                if (!IsAvailable(interval))
                {
                    return null;
                }
                else if (index >= 0)
                {   //  This should never be triggered
                    throw new InvalidOperationException(
                        "Existing block:  can't be reserved twice");
                }
                else
                {
                    var newIndex = ~index;

                    return AddIntervalAndMerge(newIndex, interval);
                }
            }

            public InternalState? TryReserveWithin(
                MemoryInterval interval,
                int length,
                out MemoryInterval outputSubInterval)
            {
                if (length < 1 || length > interval.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(length));
                }

                var subInterval = TryFindFreeIntervalWithin(interval, length);

                if (subInterval != null)
                {
                    var finalState = TryReserve(subInterval.Value);

                    outputSubInterval = finalState != null
                        ? subInterval.Value
                        : default(MemoryInterval);

                    return finalState;
                }
                else
                {
                    outputSubInterval = default(MemoryInterval);

                    return null;
                }
            }

            public InternalState Release(MemoryInterval interval)
            {
                if (interval.Length < 0 || interval.Offset < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(interval));
                }
                if (interval.Length == 0)
                {
                    return this;
                }

                var index = ReservedIntervals.BinarySearch(
                    interval,
                    MemoryBlockComparer.Singleton);

                if (index >= 0)
                {   //  The offset match a block
                    if (ReservedIntervals[index].Length == interval.Length)
                    {   //  Exact match
                        return new InternalState(
                            ReservedIntervals.RemoveAt(index),
                            PreReservations);
                    }
                    else if (ReservedIntervals[index].Length > interval.Length)
                    {   //  Existing block is bigger than release one
                        var builder = ReservedIntervals.ToBuilder();

                        builder[index] = new MemoryInterval(
                            interval.End,
                            ReservedIntervals[index].Length - interval.Length);

                        return new InternalState(builder.ToImmutable(), PreReservations);
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
                        if (ReservedIntervals[~index - 1].End > interval.Offset
                            && ReservedIntervals[~index - 1].End >= interval.End)
                        {
                            var before = new MemoryInterval(
                                ReservedIntervals[~index - 1].Offset,
                                interval.Offset - ReservedIntervals[~index - 1].Offset);
                            var after = new MemoryInterval(
                                interval.End,
                                ReservedIntervals[~index - 1].End - interval.End);
                            var validBlocks = new[] { before, after }
                            .Where(b => b.Length > 0);
                            var builder = ReservedIntervals.ToBuilder();

                            builder.RemoveAt(~index - 1);
                            builder.InsertRange(~index - 1, validBlocks);

                            return new InternalState(
                                builder.ToImmutable(),
                                PreReservations);
                        }
                        else
                        {
                            throw new InvalidDataException("Outside reserved blocks");
                        }
                    }
                }
            }

            private bool IsAvailable(MemoryInterval interval)
            {
                foreach (var block in ReservedIntervals)
                {
                    if (block.HasOverlap(interval))
                    {
                        return false;
                    }
                }

                return true;
            }

            private InternalState AddIntervalAndMerge(int newIndex, MemoryInterval newInterval)
            {
                var builder = ReservedIntervals.ToBuilder();

                if (ReservedIntervals.Length > newIndex
                    && ReservedIntervals[newIndex].Offset == newInterval.End)
                {   //  New interval's end is an old interval's beginning, let's merge
                    newInterval = new MemoryInterval(
                        newInterval.Offset,
                        newInterval.Length + ReservedIntervals[newIndex].Length);
                    builder.RemoveAt(newIndex);
                }
                if (newIndex > 0
                    && ReservedIntervals[newIndex - 1].End == newInterval.Offset)
                {   //  New interval's beginning is an old interval's end, let's merge
                    newInterval = new MemoryInterval(
                        ReservedIntervals[newIndex - 1].Offset,
                        newInterval.Length + ReservedIntervals[newIndex - 1].Length);
                    builder.RemoveAt(newIndex - 1);
                    --newIndex;
                }
                builder.Insert(newIndex, newInterval);

                return new InternalState(builder.ToImmutableArray(), PreReservations);
            }

            private MemoryInterval? TryFindFreeIntervalWithin(MemoryInterval interval, int length)
            {
                foreach (var availableInterval in EnumerateAvailableIntervals())
                {
                    var clippedAvailableInterval = availableInterval.Intersect(interval);

                    if (clippedAvailableInterval.Length >= length)
                    {
                        return new MemoryInterval(clippedAvailableInterval.Offset, length);
                    }
                }

                return null;
            }

            /// <summary>This gives the inverse of reservations into the available intervals.</summary>
            /// <returns></returns>
            private IEnumerable<MemoryInterval> EnumerateAvailableIntervals()
            {
                var start = 0;

                foreach (var reservation in ReservedIntervals)
                {
                    if (reservation.Offset != start)
                    {
                        yield return new MemoryInterval(start, reservation.Offset - start);
                    }
                    start = reservation.End;
                }

                yield return new MemoryInterval(start, int.MaxValue - start);
            }
        }
        #endregion

        private volatile InternalState _internalState = new InternalState(
            ImmutableArray<MemoryInterval>.Empty,
            ImmutableArray<PreReservation>.Empty);

        #region Reservation
        public void Reserve(MemoryInterval interval)
        {
            if (!TryReplaceState(snapshot => snapshot.TryReserve(interval)))
            {
                throw new InvalidOperationException("Interval isn't available to reserve");
            }
        }

        public Task ReserveAsync(MemoryInterval interval)
        {
            if (TryReplaceState(snapshot => snapshot.TryReserve(interval)))
            {
                return Task.CompletedTask;
            }
            else
            {
                var source = new TaskCompletionSource<MemoryInterval>();
                var preReservation = new PreReservation(interval, null, source);

                ReplaceState(snapshot => new InternalState(
                    snapshot.ReservedIntervals,
                    snapshot.PreReservations.Add(preReservation)));

                return source.Task;
            }
        }

        /// <summary>Reserves a length of memory within an interval.</summary>>
        /// <param name="interval">Interval to look within.</param>
        /// <param name="length">Length of memory to reserve.</param>
        /// <returns></returns>
        public Task<MemoryInterval> ReserveWithinAsync(
            MemoryInterval interval,
            int length,
            CancellationToken ct = default(CancellationToken))
        {
            var snapshot = _internalState;
            var newState = snapshot.TryReserveWithin(
                interval,
                length,
                out var outputSubInterval);

            if (newState != null)
            {
                if (TryReplaceState(snapshot, newState))
                {
                    return Task.FromResult(outputSubInterval);
                }
                else
                {   //  Retry
                    return ReserveWithinAsync(interval, length, ct);
                }
            }
            else
            {
                var source = new TaskCompletionSource<MemoryInterval>();
                var task = AwaitTaskWithCancellationAsync(source.Task, ct);
                var preReservation = new PreReservation(interval, length, source);

                ReplaceState(snapshot => new InternalState(
                    snapshot.ReservedIntervals,
                    snapshot.PreReservations.Add(preReservation)));

                return task;
            }
        }
        #endregion

        public void Release(MemoryInterval interval)
        {
            ReplaceState(snapshot => snapshot.Release(interval));
            RaiseTracking();
        }

        private void RaiseTracking()
        {
            var snapshot = _internalState;

            for (int i = 0; i != snapshot.PreReservations.Length; ++i)
            {
                var preReservation = snapshot.PreReservations[i];

                if (preReservation.length == null)
                {   //  Reservation of a specific interval
                    var newState = snapshot.TryReserve(preReservation.interval);

                    if (newState != null)
                    {
                        var newPreReservations = snapshot.PreReservations.RemoveAt(i);

                        newState =
                            new InternalState(newState.ReservedIntervals, newPreReservations);
                        if (TryReplaceState(snapshot, newState))
                        {
                            preReservation.source.SetResult(preReservation.interval);
                        }
                        //  Recursive call ; either to retry or to continue
                        RaiseTracking();
                    }
                }
                else
                {   //  Reservation within an interval
                    var newState = snapshot.TryReserveWithin(
                        preReservation.interval,
                        preReservation.length.Value,
                        out var outputSubInterval);

                    if (newState != null)
                    {
                        var newPreReservations = snapshot.PreReservations.RemoveAt(i);

                        newState =
                            new InternalState(newState.ReservedIntervals, newPreReservations);
                        if (TryReplaceState(snapshot, newState))
                        {
                            preReservation.source.SetResult(outputSubInterval);
                        }
                        //  Recursive call ; either to retry or to continue
                        RaiseTracking();
                    }
                }
            }
        }

        private static async Task<MemoryInterval> AwaitTaskWithCancellationAsync(
            Task<MemoryInterval> task,
            CancellationToken ct)
        {
            await Task.WhenAny(task, Task.Delay(-1, ct));

            ct.ThrowIfCancellationRequested();

            return await task;
        }

        #region State management
        private bool ReplaceState(Func<InternalState, InternalState> stateTransition)
        {
            var snapshot = _internalState;
            var newState = stateTransition(snapshot);

            if (TryReplaceState(snapshot, newState))
            {
                return true;
            }
            else
            {   //  Retry
                return TryReplaceState(stateTransition);
            }
        }

        private bool TryReplaceState(Func<InternalState, InternalState?> stateTransition)
        {
            var snapshot = _internalState;
            var newState = stateTransition(snapshot);

            if (newState == null)
            {
                return false;
            }
            else
            {
                if (TryReplaceState(snapshot, newState))
                {
                    return true;
                }
                else
                {   //  Retry
                    return TryReplaceState(stateTransition);
                }
            }
        }

        private bool TryReplaceState(InternalState snapshot, InternalState newState)
        {
            var oldState =
                Interlocked.CompareExchange(ref _internalState, newState, snapshot);

            return object.ReferenceEquals(oldState, snapshot);
        }
        #endregion
    }
}