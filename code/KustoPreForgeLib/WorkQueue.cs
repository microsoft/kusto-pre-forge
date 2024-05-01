using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoPreForgeLib
{
    /// <summary>
    /// Component keeping track of multiple asynchronous tasks.
    /// This isn't multithreaded and is assumed to be used one method call at the time.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class WorkQueue<T>
    {
        private readonly List<Task<T>> _queue = new();

        public int Count => _queue.Count;

        public void AddWorkItem(Task<T> task)
        {
            _queue.Add(task);
        }

        public async Task<T> WhenAnyAsync()
        {
            await Task.WhenAny(_queue);

            for (int i = 0; i != _queue.Count; i++)
            {
                if (_queue[i].IsCompleted)
                {
                    var value = await _queue[i];

                    _queue.RemoveAt(i);

                    return value;
                }
            }

            throw new InvalidOperationException("Can't find completed task");
        }
    }
}