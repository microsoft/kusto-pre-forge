using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

namespace KustoPreForgeLib.LineBased
{
    internal class TextBlobSink : TextStreamSinkBase
    {
        public TextBlobSink(Memory<byte>? header, RunningContext context, string shardId)
            : base(header, context, shardId)
        {
        }

        protected override async Task<Stream> CreateOutputStreamAsync()
        {
            var writeOptions = new BlobOpenWriteOptions
            {
                BufferSize = WRITING_BUFFER_SIZE
            };

            var shardName =
                $"{Context.DestinationBlobClient!.Name}-{ShardId}.txt{GetCompressionExtension()}";
            var shardBlobClient = Context
                .DestinationBlobClient!
                .GetParentBlobContainerClient()
                .GetBlobClient(shardName);
            var blobStream = await shardBlobClient.OpenWriteAsync(true, writeOptions);

            return blobStream;
        }

        protected override Task PostWriteAsync(bool isLastShard)
        {
            //   Do nothing as we write to blob directly

            return Task.CompletedTask;
        }
    }
}