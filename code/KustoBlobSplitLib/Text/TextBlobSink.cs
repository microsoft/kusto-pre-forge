using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Kusto.Data.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

namespace KustoBlobSplitLib.LineBased
{
    internal class TextBlobSink : TextStreamSinkBase
    {
        public TextBlobSink(RunningContext context, string shardId)
            : base(context, shardId)
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

        protected override Task PostWriteAsync()
        {
            //   Do nothing as we write to blob directly

            return Task.CompletedTask;
        }
    }
}