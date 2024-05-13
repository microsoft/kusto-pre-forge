using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace KustoPreForgeLib
{
    /// <summary>
    /// Component keeping track of multiple asynchronous tasks and able to queue more than
    /// its capacity.
    /// </summary>
    internal class WorkQueue
    {
        private readonly ConcurrentQueue<Func<Task>> _asyncFunctionQueue = new();
        private readonly ConcurrentQueue<Task> _runningTasks = new();
        private volatile int _utilization = 0;

        public WorkQueue(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }
            Capacity = capacity;
        }

        public int Capacity { get; }

        public bool HasCapacity => _utilization < Capacity;

        public int Utilization => _utilization;

        public bool HasResults => _runningTasks.Any();

        /// <summary>Queue a work item.</summary>
        /// <param name="asyncFunction"></param>
        /// <exception cref="NotImplementedException"></exception>
        public void QueueWorkItem(Func<Task> asyncFunction)
        {
            _asyncFunctionQueue.Enqueue(asyncFunction);
            TryScheduleFunction();
        }

        #region Task management
        /// <summary>Observe all completed tasks.</summary>
        /// <remarks>
        /// Methods <see cref="ObserveCompletedAsync"/>,
        /// <see cref="WhenAnyAsync"/> and <see cref="WhenAllAsync"/>
        /// should not be called in parallel.
        /// </remarks>
        /// <returns></returns>
        public async Task ObserveCompletedAsync()
        {
            var incompletedTasks = new List<Task>(_runningTasks.Count);

            while (_runningTasks.TryDequeue(out var task))
            {
                if (task.IsCompleted)
                {
                    await task;
                }
                else
                {
                    incompletedTasks.Add(task);
                }
            }
            //  Re-queue incompleted tasks
            foreach (var task in incompletedTasks)
            {
                _runningTasks.Enqueue(task);
            }
        }

        /// <summary>Awaits one task to be completed.</summary>
        /// <returns>False iif no more work was enqueued when called.</returns>
        public async Task WhenAnyAsync()
        {
            var completedTasks = new List<Task>(_runningTasks.Count);
            var incompletedTasks = new List<Task>(_runningTasks.Count);

            while (_runningTasks.TryDequeue(out var task))
            {
                if (task.IsCompleted)
                {
                    completedTasks.Add(task);
                }
                else
                {
                    incompletedTasks.Add(task);
                }
            }
            try
            {
                if (!completedTasks.Any() && !incompletedTasks.Any())
                {
                    if (_utilization > 0)
                    {
                        throw new InvalidOperationException(
                            $"No unobserved tasks while utilization is at {_utilization}");
                    }
                    else
                    {
                        throw new InvalidOperationException("There are no unobserved tasks");
                    }
                }
                if (completedTasks.Any())
                {
                    return;
                }
                else
                {
                    await Task.WhenAny(incompletedTasks);
                }
            }
            finally
            {
                //  Re-queue all tasks
                foreach (var task in completedTasks.Concat(incompletedTasks))
                {
                    _runningTasks.Enqueue(task);
                }
            }
        }

        public async Task WhenAllAsync()
        {
            while (_runningTasks.TryDequeue(out var task))
            {
                await task;
            }
        }
        #endregion

        private void TryScheduleFunction()
        {
            if (Interlocked.Increment(ref _utilization) <= Capacity
                && _asyncFunctionQueue.TryDequeue(out var asyncFunction))
            {
                var task = ExecuteTaskAsync(asyncFunction);

                return;
            }

            if (Interlocked.Decrement(ref _utilization) < Capacity)
            {
                TryScheduleFunction();
            }
        }

        private async Task ExecuteTaskAsync(Func<Task> asyncFunction)
        {
            await asyncFunction();
            Interlocked.Decrement(ref _utilization);
            TryScheduleFunction();
        }
    }
}