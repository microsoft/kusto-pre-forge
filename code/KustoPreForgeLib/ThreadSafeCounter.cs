using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoPreForgeLib
{
    internal class ThreadSafeCounter
    {
        private readonly TaskCompletionSource _taskSource = new();
        private volatile int _counter = 0;

        public Task BackToZeroTask => _taskSource.Task;

        public void Increment()
        {
            Interlocked.Increment(ref _counter);
        }

        public void Decrement()
        {
            if (Interlocked.Decrement(ref _counter) == 0)
            {
                _taskSource.SetResult();
            }
        }
    }
}