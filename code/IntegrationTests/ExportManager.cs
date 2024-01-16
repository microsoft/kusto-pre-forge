
using Azure.Core;
using Kusto.Cloud.Platform.Data;
using Kusto.Cloud.Platform.Storage.PersistentStorage;
using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Data.Net.Client;
using System.Collections.Immutable;

namespace IntegrationTests
{
    internal class ExportManager
    {
        #region Inner types
        private record ExportItem(
            string operationId,
            TaskCompletionSource source);
        #endregion

        private readonly ICslAdminProvider _kustoProvider;
        private readonly string _database;
        private readonly Lazy<Task<long>> _exportCapacityLazyInit;
        private volatile IImmutableList<ExportItem> _exportItems =
            ImmutableArray<ExportItem>.Empty;
        private volatile Task _registerTask = Task.CompletedTask;

        public ExportManager(
            Uri kustoIngestUri,
            string database,
            TokenCredential credentials)
        {
            var uriBuilder = new UriBuilder(kustoIngestUri);

            uriBuilder.Host = uriBuilder.Host.Replace("ingest-", string.Empty);

            var kustoBuilder = new KustoConnectionStringBuilder(uriBuilder.ToString())
                .WithAadAzureTokenCredentialsAuthentication(credentials);
            var kustoProvider = KustoClientFactory.CreateCslAdminProvider(kustoBuilder);

            _kustoProvider = kustoProvider;
            _database = database;
            _exportCapacityLazyInit = new Lazy<Task<long>>(
                () => FetchCapacityAsync(),
                LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public async Task RunExportAsync(string script)
        {
            var item = await PostExportAsync(script);

            await item.source.Task;
        }

        private async Task<ExportItem> PostExportAsync(string script)
        {
            var capacity = await _exportCapacityLazyInit.Value;
            var source = await ChallengeRegisterAsync();

            while (_exportItems.Count >= capacity)
            {
                await Task.WhenAny(_exportItems.Select(i => i.source.Task));
                CleanItems();
            }
            try
            {
                using (var reader = await _kustoProvider.ExecuteControlCommandAsync(
                    _database,
                    script))
                {
                    var operationId = (string)reader.ToDataSet().Tables[0].Rows[0][0];
                    var taskSource = new TaskCompletionSource();
                    var item = new ExportItem(operationId, taskSource);

                    _exportItems = _exportItems.Add(item);

                    return item;
                }
            }
            finally
            {
                source.SetResult();
            }
        }

        private void CleanItems()
        {
            _exportItems = _exportItems
                                .Where(i => !i.source.Task.IsCompleted)
                                .ToImmutableArray();
        }

        private async Task<TaskCompletionSource> ChallengeRegisterAsync()
        {   //  Capture current register
            var oldTask = _registerTask;
            var newTaskSource = new TaskCompletionSource();

            await oldTask;
            if (object.ReferenceEquals(
                oldTask,
                Interlocked.CompareExchange(ref _registerTask, newTaskSource.Task, oldTask)))
            {
                return newTaskSource;
            }
            else
            {
                return await ChallengeRegisterAsync();
            }
        }

        private async Task<long> FetchCapacityAsync()
        {
            using (var reader = await _kustoProvider.ExecuteControlCommandAsync(
                _database,
                ".show capacity data-export | project Total"))
            {
                var capacity = (long)reader.ToDataSet().Tables[0].Rows[0][0];

                return capacity;
            }
        }
    }
}