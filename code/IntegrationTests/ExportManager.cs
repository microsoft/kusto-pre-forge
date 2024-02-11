
using Azure.Core;
using Kusto.Cloud.Platform.Data;
using Kusto.Cloud.Platform.Storage.PersistentStorage;
using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Data.Net.Client;
using System;
using System.Collections.Immutable;

namespace IntegrationTests
{
    internal class ExportManager
    {
        private readonly ICslAdminProvider _kustoProvider;
        private readonly OperationManager _operationManager;
        private readonly Lazy<Task<ResourceManager>> _resourceManagerLazyInit;

        public ExportManager(
            OperationManager operationManager,
            ICslAdminProvider kustoProvider)
        {
            _kustoProvider = kustoProvider;
            _operationManager = operationManager;
            _resourceManagerLazyInit = new Lazy<Task<ResourceManager>>(
                CreateResourceManagerAsync,
                LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public async Task RunExportAsync(string script)
        {
            var resourceManager = await _resourceManagerLazyInit.Value;

            await resourceManager.PostResourceUtilizationAsync(async () =>
            {
                var operationId = await ExecuteAsyncExportAsync(script);

                await _operationManager.AwaitCompletionAsync(operationId);
            });
        }

        private async Task<string> ExecuteAsyncExportAsync(string script)
        {
            using (var reader = await _kustoProvider.ExecuteControlCommandAsync(
                string.Empty,
                script))
            {
                var dataRow = reader.ToDataSet().Tables[0].Rows[0];
                var operationId = (Guid)dataRow[0];

                return operationId.ToString();
            }
        }

        private async Task<ResourceManager> CreateResourceManagerAsync()
        {
            using (var reader = await _kustoProvider.ExecuteControlCommandAsync(
                string.Empty,
                ".show capacity data-export | project Total"))
            {
                var capacity = (long)reader.ToDataSet().Tables[0].Rows[0][0];

                return new((int)capacity);
            }
        }
    }
}