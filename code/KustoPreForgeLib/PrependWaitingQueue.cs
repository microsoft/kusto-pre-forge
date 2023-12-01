using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoPreForgeLib
{
    internal class PrependWaitingQueue<T> : IWaitingQueue<T>
    {
        private readonly IWaitingQueue<T> _subQueue;
        private readonly ConcurrentStack<T> _frontItems;

        public PrependWaitingQueue(IWaitingQueue<T> subQueue, IEnumerable<T> frontItems)
        {
            _subQueue = subQueue;
            _frontItems = new ConcurrentStack<T>(frontItems.Reverse());
        }

        bool IWaitingQueue<T>.HasData => _frontItems.Any() || _subQueue.HasData;

        bool IWaitingQueue<T>.HasCompleted => _subQueue.HasCompleted;

        void IWaitingQueue<T>.Enqueue(T item)
        {
            _subQueue.Enqueue(item);
        }

        async ValueTask<QueueResult<T>> IWaitingQueue<T>.DequeueAsync()
        {
            if(_frontItems.TryPop(out var result))
            {
                return new QueueResult<T>(false, result);
            }
            else
            {
                return await _subQueue.DequeueAsync();
            }    
        }

        void IWaitingQueue<T>.Complete()
        {
            _subQueue.Complete();
        }
    }
}