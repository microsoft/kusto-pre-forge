using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntegrationTests
{
    internal class OperationManager
    {
        #region Inner types
        private record OperationItem(string operationId, TaskCompletionSource source);
        #endregion

        private readonly object _lock = new object();
        private IImmutableList<OperationItem> _operationItems =
            ImmutableArray<OperationItem>.Empty;

        public async Task AwaitCompletionAsync(string operationId)
        {
            var item = new OperationItem(operationId, new TaskCompletionSource());

            lock (_lock)
            {
                _operationItems = _operationItems.Add(item);
            }

            await item.source.Task;
        }
    }
}