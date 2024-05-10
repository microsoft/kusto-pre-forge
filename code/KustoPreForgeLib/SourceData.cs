﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoPreForgeLib
{
    public class SourceData<T> : IAsyncDisposable
    {
        private readonly Action? _onDisposeAction;
        private readonly Func<Task>? _onDisposeAsyncAction;
        private readonly IImmutableList<IAsyncDisposable> _dependantDisposables;

        public SourceData(
            T data,
            Action? onDisposeAction,
            Func<Task>? onDisposeAsyncAction,
            params IAsyncDisposable[] dependantDisposables)
        {
            Data = data;
            _onDisposeAction = onDisposeAction;
            _onDisposeAsyncAction = onDisposeAsyncAction;
            _dependantDisposables = dependantDisposables.ToImmutableArray();
        }

        public T Data { get; }

        async ValueTask IAsyncDisposable.DisposeAsync()
        {
            if (_onDisposeAction != null)
            {
                _onDisposeAction();
            }
            if (_onDisposeAsyncAction != null)
            {
                await _onDisposeAsyncAction();
            }
            foreach (var subDisposable in _dependantDisposables)
            {
                await subDisposable.DisposeAsync();
            }
        }
    }
}