using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoPreForgeLib
{
    /// <summary>
    /// Component keeping track of multiple asynchronous tasks and able to queue more than
    /// its capacity.
    /// </summary>
    internal class WorkQueue
    {
        private readonly ConcurrentQueue<Func<Task>> _asyncFunctionQueue = new();
        private readonly ConcurrentQueue<Task> _completedTasks = new();
        private readonly ConcurrentQueue<int> _availableSlots = new();
        private readonly List<Task> _workingTasks;
        private readonly object _scheduleLock = new object();

        public WorkQueue(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }
            Capacity = capacity;
            _workingTasks = new List<Task>(capacity);
            //  Pre-populate
            for (int i = 0; i < Capacity; i++)
            {
                _availableSlots.Enqueue(i);
                _workingTasks.Add(Task.CompletedTask);
            }
        }

        public int Capacity { get; }

        public bool HasCapacity => _availableSlots.Any();

        public int UsedCapacity => _workingTasks.Count - _availableSlots.Count;

        public bool HasResults => _completedTasks.Any();

        /// <summary>Queue a work item.</summary>
        /// <param name="asyncFunction"></param>
        /// <exception cref="NotImplementedException"></exception>
        public void QueueWorkItem(Func<Task> asyncFunction)
        {
            _asyncFunctionQueue.Enqueue(asyncFunction);
            TryScheduleFunction();
        }

        /// <summary>Observe all completed tasks.</summary>>
        /// <returns></returns>
        public async Task ObserveCompletedAsync()
        {
            while (_completedTasks.TryDequeue(out var task))
            {
                await task;
            }
        }

        /// <summary>Awaits one task to be completed.</summary>>
        /// <returns>False iif no more work was enqueued when called.</returns>
        public async Task<bool> WhenAnyAsync()
        {
            var isCapacityUsed = _availableSlots.Count < _workingTasks.Count;

            if (_completedTasks.TryDequeue(out var task))
            {
                await task;

                return true;
            }
            else if (!isCapacityUsed)
            {
                return false;
            }
            else
            {
                await Task.Delay(TimeSpan.FromSeconds(0.1));

                return await WhenAnyAsync();
            }
        }

        public async Task WhenAllAsync()
        {
            await ObserveCompletedAsync();
            while (await WhenAnyAsync())
            {
            }
        }

        private void TryScheduleFunction()
        {
            lock (_scheduleLock)
            {
                if (_availableSlots.TryDequeue(out var availableSlot))
                {
                    if (_asyncFunctionQueue.TryDequeue(out var asyncFunction))
                    {
                        _workingTasks[availableSlot] =
                            ExecuteTaskAsync(availableSlot, asyncFunction);
                    }
                    else
                    {
                        _availableSlots.Enqueue(availableSlot);
                    }
                }
            }
        }

        private async Task ExecuteTaskAsync(int slot, Func<Task> asyncFunction)
        {
            await asyncFunction();
            _completedTasks.Enqueue(_workingTasks[slot]);
            _availableSlots.Enqueue(slot);
            TryScheduleFunction();
        }
    }
}