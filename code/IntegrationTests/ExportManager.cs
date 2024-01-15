
using Azure.Core;
using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Data.Net.Client;

namespace IntegrationTests
{
    internal class ExportManager
    {
        private readonly ICslAdminProvider _kustoProvider;
        private readonly string _database;

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
        }

        public async Task RunExportAsync(string script)
        {
            await Task.CompletedTask;
        }
    }
}