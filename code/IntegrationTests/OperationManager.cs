using Kusto.Cloud.Platform.Data;
using Kusto.Data.Common;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntegrationTests
{
    internal class OperationManager
    {
        #region Inner types
        private record OperationItem(string operationId, TaskCompletionSource source);

        private record OperationTerminated(
            string operationId,
            bool isSuccess,
            string status);
        #endregion

        private static readonly TimeSpan OPERATION_TRACK_PERIOD = TimeSpan.FromSeconds(5);

        private readonly ICslAdminProvider _kustoProvider;
        private readonly object _lock = new object();
        private IImmutableList<OperationItem> _operationItems =
            ImmutableArray<OperationItem>.Empty;
        private Task _trackOperationTask = Task.CompletedTask;

        public OperationManager(ICslAdminProvider kustoProvider)
        {
            _kustoProvider = kustoProvider;
        }

        public async Task AwaitCompletionAsync(string operationId)
        {
            var item = new OperationItem(operationId, new TaskCompletionSource());
            var doNeedNewTrackOperation = false;

            lock (_lock)
            {
                doNeedNewTrackOperation = !_operationItems.Any();
                _operationItems = _operationItems.Add(item);
            }

            if (doNeedNewTrackOperation)
            {
                await _trackOperationTask;
                _trackOperationTask = TrackOperationAsync();
            }
            await item.source.Task;
        }

        private async Task TrackOperationAsync()
        {
            do
            {
                await Task.Delay(OPERATION_TRACK_PERIOD);
            }
            while (await UpdateOperationStatusAsync());
        }

        private async Task<bool> UpdateOperationStatusAsync()
        {
            var terminated = await FetchOperationStatusAsync();

            if (terminated.Any())
            {
                var indexedTerminated = terminated
                    .ToImmutableDictionary(t => t.operationId);

                lock (_lock)
                {
                    var results = _operationItems
                        .Where(i => indexedTerminated.ContainsKey(i.operationId))
                        .Select(i => new
                        {
                            Item = i,
                            Terminated = indexedTerminated[i.operationId]
                        });
                    var remainingItems = _operationItems
                        .Where(i => !indexedTerminated.ContainsKey(i.operationId))
                        .ToImmutableArray();

                    foreach (var r in results)
                    {
                        if (r.Terminated.isSuccess)
                        {
                            r.Item.source.SetResult();
                        }
                        else
                        {
                            r.Item.source.SetException(new InvalidOperationException(
                                $"Operation failed with '{r.Terminated.status}'"));
                        }
                    }

                    _operationItems = remainingItems;

                    return remainingItems.Any();
                }
            }

            return true;
        }

        private async Task<IImmutableList<OperationTerminated>> FetchOperationStatusAsync()
        {
            var operationIdList = string.Join(',', GetOperationIdSnapshot());
            var command = $".show operations ({operationIdList})";
            using (var reader = await _kustoProvider.ExecuteControlCommandAsync(
                string.Empty,
                command))
            {
                var results = reader.ToDataSet().Tables[0].Rows
                    .Cast<DataRow>()
                    .Select(r => new
                    {
                        OperationId = ((Guid)r["OperationId"]).ToString(),
                        State = (string)r["State"],
                        Status = (string)r["Status"]
                    });
                var terminated = results
                    .Where(r => r.State != "InProgress")
                    .Select(r => r.State == "Completed"
                    ? new OperationTerminated(
                        r.OperationId,
                        true,
                        string.Empty)
                    : new OperationTerminated(
                        r.OperationId,
                        false,
                        r.Status))
                    .ToImmutableArray();

                return terminated;
            }
        }

        private IImmutableList<string> GetOperationIdSnapshot()
        {
            lock (_lock)
            {
                return _operationItems
                    .Select(i => i.operationId)
                    .ToImmutableArray();
            }
        }
    }
}