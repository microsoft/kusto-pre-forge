using Kusto.Cloud.Platform.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntegrationTests
{
    internal class ResourceManager
    {
        #region Inner types
        private record QueueItem(
            Func<Task> resourceUtilizationFunc,
            TaskCompletionSource source);

        private record RunningItem(
            Task runningTask,
            TaskCompletionSource source);
        #endregion

        private readonly int _capacity;
        private readonly ConcurrentQueue<QueueItem> _requestQueue = new();
        private readonly object _lock = new object();
        private IImmutableList<RunningItem> _runningTaskItems =
            ImmutableArray<RunningItem>.Empty;

        public ResourceManager(int capacity)
        {
            _capacity = capacity;
        }

        internal Task PostResourceUtilizationAsync(Func<Task> resourceUtilizationFunc)
        {
            var queueItem = new QueueItem(
                resourceUtilizationFunc,
                new TaskCompletionSource());

            _requestQueue.Enqueue(queueItem);
            TryUnqueue();

            return queueItem.source.Task;
        }

        private void TryUnqueue()
        {
            lock (_lock)
            {
                var newRunningTaskItems = CleanRunningTasks();

                while (newRunningTaskItems.Count < _capacity
                    && _requestQueue.TryDequeue(out var request))
                {
                    var runningTask = Task.Run(async () =>
                    {
                        await request.resourceUtilizationFunc();
                        TryUnqueue();
                    });
                    var runningItem = new RunningItem(runningTask, request.source);

                    newRunningTaskItems.Add(runningItem);
                }
                _runningTaskItems = newRunningTaskItems.ToImmutableArray();
            }
        }

        private List<RunningItem> CleanRunningTasks()
        {
            lock (_lock)
            {
                var newRunningTaskItems = new List<RunningItem>(_runningTaskItems.Count);

                foreach (var item in _runningTaskItems)
                {
                    if (item.runningTask.IsCompleted)
                    {   //  Signal completion
                        item.source.SetResult();
                    }
                    else
                    {
                        newRunningTaskItems.Add(item);
                    }
                }

                return newRunningTaskItems;
            }
        }
    }
}