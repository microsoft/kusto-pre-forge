using Kusto.Data.Common;
using KustoPreForgeLib.Text;
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
    internal abstract class TextStreamSinkBase : ITextSink
    {
        protected const int WRITING_BUFFER_SIZE = 20 * 1024 * 1024;

        public TextStreamSinkBase(RunningContext context, string shardId)
        {
            if (context.SourceBlobClient == null)
            {
                throw new ArgumentNullException(nameof(context.SourceBlobClient));
            }

            Context = context;
            ShardId = shardId;
        }

        protected RunningContext Context { get; }

        protected string ShardId { get; }

        async Task ITextSink.ProcessAsync(
            Memory<byte>? header,
            IWaitingQueue<BufferFragment> fragmentQueue,
            IWaitingQueue<BufferFragment> releaseQueue)
        {
            var stopwatch = new Stopwatch();

            stopwatch.Start();
            //  We pre-fetch a fragment not to create an empty blob
            var fragmentResult = await fragmentQueue.DequeueAsync();

            if (!fragmentResult.IsCompleted)
            {
                await using (var blobStream = await CreateOutputStreamAsync())
                await using (var compressedStream = CompressedStream(blobStream))
                await using (var countingStream = new ByteCountingStream(compressedStream))
                {
                    if (header != null)
                    {
                        await countingStream.WriteAsync(header.Value);
                    }
                    do
                    {
                        var fragment = fragmentResult.Item!;

                        foreach (var block in fragment.GetMemoryBlocks())
                        {
                            await countingStream.WriteAsync(block);
                        }
                        releaseQueue.Enqueue(fragment);
                    }
                    while (countingStream.Position < Context.BlobSettings.MaxBytesPerShard
                    && !(fragmentResult = await fragmentQueue.DequeueAsync()).IsCompleted);
                }
                await PostWriteAsync(fragmentResult.IsCompleted);
                Console.WriteLine($"Sealed shard {ShardId} ({stopwatch.Elapsed})");
            }
        }

        protected abstract Task<Stream> CreateOutputStreamAsync();

        protected abstract Task PostWriteAsync(bool isLastShard);

        protected string GetCompressionExtension()
        {
            switch (Context.BlobSettings.OutputCompression)
            {
                case DataSourceCompressionType.None:
                    return string.Empty;
                case DataSourceCompressionType.GZip:
                    return ".gz";

                default:
                    throw new NotSupportedException(
                        Context.BlobSettings.OutputCompression.ToString());
            }
        }

        private Stream CompressedStream(Stream stream)
        {
            switch (Context.BlobSettings.OutputCompression)
            {
                case DataSourceCompressionType.None:
                    return stream;
                case DataSourceCompressionType.GZip:
                    return new GZipStream(stream, CompressionMode.Compress);

                default:
                    throw new NotSupportedException(
                        Context.BlobSettings.OutputCompression.ToString());
            }
        }
    }
}