
using Azure.Core;
using Kusto.Cloud.Platform.Data;
using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Data.Net.Client;

namespace IntegrationTests
{
    internal class ExportManager
    {
        private readonly ICslAdminProvider _kustoProvider;
        private readonly string _database;
        private readonly Lazy<Task<long>> _exportCapacityLazyInit;

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
            var capacity = await _exportCapacityLazyInit.Value;
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