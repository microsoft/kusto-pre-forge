using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Kusto.Ingest;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

namespace KustoPreForgeLib.LineBased
{
    internal class TextKustoSink : TextStreamSinkBase
    {
        private readonly BlobClient _shardBlobClient;

        public TextKustoSink(
            RunningContext context,
            string shardId,
            string blobNamePrefix)
            : base(context, shardId)
        {
            var shardName =
                $"{blobNamePrefix}-{shardId}.txt{GetCompressionExtension()}";
            
            _shardBlobClient = Context
                .RoundRobinIngestStagingContainer()
                .GetBlobClient(shardName);
        }

        protected override async Task<Stream> CreateOutputStreamAsync()
        {
            var writeOptions = new BlobOpenWriteOptions
            {
                BufferSize = WRITING_BUFFER_SIZE
            };

            var blobStream = await _shardBlobClient.OpenWriteAsync(true, writeOptions);

            return blobStream;
        }

        protected override async Task PostWriteAsync()
        {
            var properties = Context.CreateIngestionProperties();
            var tagValue = $"{Context.SourceBlobClient.Uri}-{ShardId}";

            properties.IngestByTags = new[] { tagValue };
            properties.IngestIfNotExists = new[] { tagValue };
            properties.AdditionalTags = new[] { $"original-blob:{Context.SourceBlobClient.Uri}" };

            await Context.IngestClient!.IngestFromStorageAsync(
                _shardBlobClient.Uri.ToString(),
                properties,
                new StorageSourceOptions
                {
                    CompressionType = Context.BlobSettings.OutputCompression
                });
        }
    }
}