using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoPreForgeLib
{
    internal class ReferenceCounterDisposable : IAsyncDisposable
    {
        #region Inner Types
        private class CentralDependency : IAsyncDisposable
        {
            private readonly IImmutableList<IAsyncDisposable> _dependantDisposables;
            private volatile int _counter;

            public CentralDependency(
                int counter,
                params IAsyncDisposable[] dependantDisposables)
            {
                _counter = counter;
                _dependantDisposables = dependantDisposables.ToImmutableArray();
            }

            public bool DecrementCount()
            {
                var newDisposeCount = Interlocked.Decrement(ref _counter);

                if (newDisposeCount < 0)
                {
                    throw new InvalidOperationException("Disposed too many times");
                }

                return newDisposeCount == 0;
            }

            async ValueTask IAsyncDisposable.DisposeAsync()
            {
                foreach (var subDisposable in _dependantDisposables)
                {
                    await subDisposable.DisposeAsync();
                }
            }
        }
        #endregion

        private readonly CentralDependency _dependency;
        private volatile int _disposedCount = 0;

        #region Constructors
        public static IImmutableList<IAsyncDisposable> Create(
            int counter,
            params IAsyncDisposable[] dependantDisposables)
        {
            var dependency = new CentralDependency(counter, dependantDisposables);
            var disposables = Enumerable.Range(0, counter)
                .Select(i => (IAsyncDisposable)new ReferenceCounterDisposable(dependency))
                .ToImmutableArray();

            return disposables;
        }

        private ReferenceCounterDisposable(CentralDependency dependency)
        {
            _dependency = dependency;
        }
        #endregion

        async ValueTask IAsyncDisposable.DisposeAsync()
        {
            if (Interlocked.CompareExchange(ref _disposedCount, 1, 0) != 0)
            {
                throw new InvalidOperationException("Already disposed");
            }

            if (_dependency.DecrementCount())
            {
                await using (_dependency)
                {
                }
            }
        }
    }
}