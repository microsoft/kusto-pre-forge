using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Kusto.Cloud.Platform.Data;
using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Data.Net.Client;
using Kusto.Ingest;
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
        private readonly IImmutableList<BlobContainerClient> _ingestionStagingContainers;
        private volatile int _ingestionStagingContainerIndex = 0;

        #region Constructors
        public static async Task<RunningContext> CreateAsync(RunSettings runSettings)
        {
            var blobSettings = runSettings.BlobSettings;
            var credentials = GetCredentials(runSettings);
            var sourceBlobClient = runSettings.SourceSettings.SourceBlob != null
                ? new BlockBlobClient(runSettings.SourceSettings.SourceBlob, credentials)
                : null;
            var destinationBlobClient = runSettings.DestinationBlobPrefix != null
                ? new BlockBlobClient(runSettings.DestinationBlobPrefix, credentials)
                : null;
            var ingestClient = runSettings.KustoIngestUri != null
                ? KustoIngestFactory.CreateQueuedIngestClient(
                    new KustoConnectionStringBuilder(
                        runSettings.KustoIngestUri.ToString())
                    .WithAadAzureTokenCredentialsAuthentication(credentials))
                : null;
            var ingestionPropertiesFactory = runSettings.KustoIngestUri != null
                ? () => new KustoQueuedIngestionProperties(runSettings.KustoDb!, runSettings.KustoTable!)
                {
                    Format = runSettings.BlobSettings.Format
                }
                : (Func<KustoQueuedIngestionProperties>?)null;
            var kustoAdminClient = runSettings.KustoIngestUri != null
                ? KustoClientFactory.CreateCslAdminProvider(
                    new KustoConnectionStringBuilder(
                        runSettings.KustoIngestUri.ToString())
                    .WithAadAzureTokenCredentialsAuthentication(credentials))
                : null;
            var ingestionStagingContainers = runSettings.KustoIngestUri != null
                ? await GetIngestionStagingContainersAsync(kustoAdminClient!)
                : ImmutableArray<BlobContainerClient>.Empty;

            return new RunningContext(
                blobSettings,
                credentials,
                sourceBlobClient,
                destinationBlobClient,
                ingestClient,
                ingestionPropertiesFactory,
                ingestionStagingContainers);
        }

        public RunningContext(
            BlobSettings blobSettings,
            TokenCredential credentials,
            BlockBlobClient? sourceBlobClient,
            BlockBlobClient? destinationBlobClient,
            IKustoQueuedIngestClient? ingestClient,
            Func<KustoQueuedIngestionProperties>? ingestionPropertiesFactory,
            IImmutableList<BlobContainerClient> ingestionStagingContainers)
        {
            BlobSettings = blobSettings;
            Credentials = credentials;
            SourceBlobClient = sourceBlobClient;
            DestinationBlobClient = destinationBlobClient;
            IngestClient = ingestClient;
            _ingestionPropertiesFactory = ingestionPropertiesFactory;
            _ingestionStagingContainers = ingestionStagingContainers;
        }

        private static TokenCredential GetCredentials(RunSettings runSettings)
        {
            switch (runSettings.AuthMode)
            {
                case AuthMode.Default:
                    return new DefaultAzureCredential();
                case AuthMode.ManagedIdentity:
                    return new ManagedIdentityCredential(
                        new ResourceIdentifier(runSettings.ManagedIdentityResourceId!));

                default:
                    throw new NotSupportedException($"Auth mode:  '{runSettings.AuthMode}'");
            }
        }
        #endregion

        public BlobSettings BlobSettings { get; }

        public TokenCredential Credentials { get; }

        public BlockBlobClient? SourceBlobClient { get; }

        public BlockBlobClient? DestinationBlobClient { get; }

        public IKustoQueuedIngestClient? IngestClient { get; }

        public KustoQueuedIngestionProperties CreateIngestionProperties()
        {
            if (_ingestionPropertiesFactory == null)
            {
                throw new NotSupportedException("Ingestion properties factory undefined");
            }

            return _ingestionPropertiesFactory();
        }

        public BlobContainerClient RoundRobinIngestStagingContainer()
        {
            if (_ingestionStagingContainers.Count == 0)
            {
                throw new InvalidDataException("No ingestion staging containers are detected");
            }

            var client = _ingestionStagingContainers[
                _ingestionStagingContainerIndex % _ingestionStagingContainers.Count];

            Interlocked.Increment(ref _ingestionStagingContainerIndex);

            return client;
        }

        public RunningContext OverrideSourceBlob(Uri sourceUri)
        {
            var sourceBlobClient = new BlockBlobClient(sourceUri, Credentials);

            return new RunningContext(
                BlobSettings,
                Credentials,
                sourceBlobClient,
                DestinationBlobClient,
                IngestClient,
                _ingestionPropertiesFactory,
                _ingestionStagingContainers);
        }

        public static async Task<ImmutableArray<BlobContainerClient>> GetIngestionStagingContainersAsync(
            ICslAdminProvider kustoAdminClient)
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