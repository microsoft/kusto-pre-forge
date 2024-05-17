using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Kusto.Cloud.Platform.Data;
using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Data.Net.Client;
using Kusto.Ingest;
using KustoPreForgeLib.Settings;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoPreForgeLib
{
    public class RunningContext
    {
        private readonly Func<KustoQueuedIngestionProperties>? _ingestionPropertiesFactory;

        #region Constructors
        public static async Task<RunningContext> CreateAsync(RunSettings runSettings)
        {
            var blobSettings = runSettings.BlobSettings;
            var kustoSettings = runSettings.KustoSettings;
            var credentials = runSettings.AuthSettings.GetCredentials();
            var sourceBlobClient = runSettings.SourceSettings.SourceBlob != null
                ? new BlockBlobClient(runSettings.SourceSettings.SourceBlob, credentials)
                : null;
            var destinationBlobClient = runSettings.DestinationBlobPrefix != null
                ? new BlockBlobClient(runSettings.DestinationBlobPrefix, credentials)
                : null;
            var ingestClient = kustoSettings != null
                ? KustoIngestFactory.CreateQueuedIngestClient(
                    new KustoConnectionStringBuilder(
                        kustoSettings.IngestUri.ToString())
                    .WithAadAzureTokenCredentialsAuthentication(credentials))
                : null;
            var ingestionPropertiesFactory = kustoSettings != null
                ? () => new KustoQueuedIngestionProperties(kustoSettings.Database!, kustoSettings.Table!)
                {
                    Format = runSettings.BlobSettings.Format
                }
                : (Func<KustoQueuedIngestionProperties>?)null;
            var kustoAdminIngestClient = kustoSettings != null
                ? KustoClientFactory.CreateCslAdminProvider(
                    new KustoConnectionStringBuilder(
                        kustoSettings.IngestUri.ToString())
                    .WithAadAzureTokenCredentialsAuthentication(credentials))
                : null;
            var stagingContainers = kustoAdminIngestClient == null
                ? (IImmutableList<BlobContainerClient>?)null
                : await FetchIngestionStagingContainersAsync(kustoAdminIngestClient);
            var kustoAdminEngineClient =
                await FetchEngineAdminClientAsync(kustoAdminIngestClient, credentials);

            return new RunningContext(
                blobSettings,
                credentials,
                sourceBlobClient,
                destinationBlobClient,
                ingestClient,
                ingestionPropertiesFactory,
                kustoAdminEngineClient,
                stagingContainers);
        }

        public RunningContext(
            BlobSettings blobSettings,
            TokenCredential credentials,
            BlockBlobClient? sourceBlobClient,
            BlockBlobClient? destinationBlobClient,
            IKustoQueuedIngestClient? ingestClient,
            Func<KustoQueuedIngestionProperties>? ingestionPropertiesFactory,
            ICslAdminProvider? adminEngineClient,
            IImmutableList<BlobContainerClient>? stagingContainers)
        {
            BlobSettings = blobSettings;
            Credentials = credentials;
            SourceBlobClient = sourceBlobClient;
            DestinationBlobClient = destinationBlobClient;
            IngestClient = ingestClient;
            _ingestionPropertiesFactory = ingestionPropertiesFactory;
            AdminEngineClient = adminEngineClient;
            StagingContainers = stagingContainers;
        }

        private static async Task<ICslAdminProvider?> FetchEngineAdminClientAsync(
            ICslAdminProvider? ingestAdminClient, TokenCredential credentials)
        {
            if (ingestAdminClient != null)
            {
                var queryReader = await ingestAdminClient.ExecuteControlCommandAsync(
                    string.Empty,
                    ".show query service uri");
                var queryUri = queryReader.ToDataSet().Tables[0].Rows[0][0].ToString();
                var builder = new KustoConnectionStringBuilder(queryUri)
                    .WithAadAzureTokenCredentialsAuthentication(credentials);
                var engineAdminClient = KustoClientFactory.CreateCslAdminProvider(builder);

                return engineAdminClient;
            }
            else
            {
                return null;
            }
        }
        #endregion

        public BlobSettings BlobSettings { get; }

        public TokenCredential Credentials { get; }

        public BlockBlobClient? SourceBlobClient { get; }

        public BlockBlobClient? DestinationBlobClient { get; }

        public IKustoQueuedIngestClient? IngestClient { get; }

        public ICslAdminProvider? AdminEngineClient { get; }

        public IImmutableList<BlobContainerClient>? StagingContainers { get; }

        public KustoQueuedIngestionProperties CreateIngestionProperties()
        {
            if (_ingestionPropertiesFactory == null)
            {
                throw new NotSupportedException("Ingestion properties factory undefined");
            }

            return _ingestionPropertiesFactory();
        }

        private static async Task<ImmutableArray<BlobContainerClient>>
            FetchIngestionStagingContainersAsync(ICslAdminProvider kustoAdminClient)
        {
            var dataReader = await kustoAdminClient.ExecuteControlCommandAsync(
                string.Empty,
                ".get ingestion resources");
            var table = dataReader.ToDataSet().Tables[0];
            var containers = table.Rows.Cast<DataRow>()
                .Where(r => (string)r["ResourceTypeName"] == "TempStorage")
                .Select(r => new Uri((string)r["StorageRoot"]))
                .Select(uri => new BlobContainerClient(uri))
                .ToImmutableArray();

            return containers;
        }
    }
}