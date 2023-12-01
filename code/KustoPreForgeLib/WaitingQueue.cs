using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoPreForgeLib
{
    internal partial class WaitingQueue<T> : IWaitingQueue<T>
    {
        private readonly ConcurrentQueue<T> _queue = new();
        private readonly TaskCompletionSource _isCompletedSource = new();
        private volatile TaskCompletionSource _newItemSource = new();

        bool IWaitingQueue<T>.HasData => _queue.Any();

        bool IWaitingQueue<T>.HasCompleted => _isCompletedSource.Task.IsCompleted
            && !_queue.Any();

        void IWaitingQueue<T>.Enqueue(T item)
        {
            if (_isCompletedSource.Task.IsCompleted)
            {
                throw new InvalidDataException("Queue is already marked as completed");
            }

            var oldSource = Interlocked.Exchange(
                ref _newItemSource,
                new());

            _queue.Enqueue(item);
            oldSource.SetResult();
        }

        async ValueTask<QueueResult<T>> IWaitingQueue<T>.DequeueAsync()
        {
            var newItemTask = _newItemSource.Task;

            if (_queue.TryDequeue(out var result))
            {
                return new QueueResult<T>(false, result);
            }
            else if (_isCompletedSource.Task.IsCompleted && !_queue.Any())
            {
                return new QueueResult<T>(true, default(T));
            }
            else
            {
                await Task.WhenAny(newItemTask, _isCompletedSource.Task);

                //  Recurse to actually dequeue the value
                return await ((IWaitingQueue<T>)this).DequeueAsync();
            }
        }

        void IWaitingQueue<T>.Complete()
        {
            _isCompletedSource.SetResult();
        }
    }
}