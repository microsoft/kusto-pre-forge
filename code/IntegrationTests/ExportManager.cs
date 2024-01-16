
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
        #region Inner types
        private record ExportItem(
            string operationId,
            TaskCompletionSource source);
        #endregion

        private readonly ICslAdminProvider _kustoProvider;
        private readonly string _database;
        private readonly OperationManager _operationManager;
        private readonly Lazy<Task<ResourceManager>> _resourceManagerLazyInit;

        public ExportManager(
            OperationManager operationManager,
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
                var operationId = await PostExportAsync(script);

                await _operationManager.AwaitCompletionAsync(operationId);
            });
        }

        private async Task<string> PostExportAsync(string script)
        {
            using (var reader = await _kustoProvider.ExecuteControlCommandAsync(
                _database,
                script))
            {
                var operationId = (string)reader.ToDataSet().Tables[0].Rows[0][0];

                return operationId;
            }
        }

        private async Task<ResourceManager> CreateResourceManagerAsync()
        {
            using (var reader = await _kustoProvider.ExecuteControlCommandAsync(
                _database,
                ".show capacity data-export | project Total"))
            {
                var capacity = (long)reader.ToDataSet().Tables[0].Rows[0][0];

                return new((int)capacity);
            }
        }
    }
}