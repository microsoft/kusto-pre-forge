using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoPreForgeLib
{
    public class SourceData<T> : IAsyncDisposable
    {
        public SourceData(
            T data,
            Action? onDisposeAction,
            Func<Task>? onDisposeAsyncAction,
            params IAsyncDisposable[] dependantDisposables)
        {
            Data = data;
        }

        public T Data { get; }

        ValueTask IAsyncDisposable.DisposeAsync()
        {
            throw new NotImplementedException();
        }
    }
}