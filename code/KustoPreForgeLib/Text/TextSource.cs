using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Kusto.Cloud.Platform.Utils;
using Kusto.Data.Common;
using KustoPreForgeLib.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoPreForgeLib.Text
{
    /// <summary>Reads data from Azure Blob and sends it to sink.</summary>
    internal class TextSource : ISource
    {
        private const int BUFFER_COUNT = 4;
        private const int BUFFER_SIZE = 50 * 1024 * 1024;

        private readonly BlockBlobClient _sourceBlob;
        private readonly DataSourceCompressionType _compression;
        private readonly ITextSink _sink;

        public TextSource(
            BlockBlobClient sourceBlob,
            DataSourceCompressionType compression,
            ITextSink sink)
        {
            _sourceBlob = sourceBlob;
            _compression = compression;
            _sink = sink;
        }

        async Task ISource.ProcessSourceAsync()
        {
            var readOptions = new BlobOpenReadOptions(false)
            {
                BufferSize = BUFFER_COUNT * BUFFER_SIZE
            };
            var bufferQueue = new Queue<BufferFragment>(
                Enumerable.Range(0, BUFFER_COUNT)
                .Select(i => BufferFragment.Create(BUFFER_SIZE)));
            var fragmentQueue = new WaitingQueue<BufferFragment>() as IWaitingQueue<BufferFragment>;
            var sinkTask = Task.Run(() => _sink.ProcessAsync(fragmentQueue));

            Console.WriteLine($"Reading '{_sourceBlob.Uri}'");
            using (var readStream = await _sourceBlob.OpenReadAsync(readOptions))
            using (var uncompressedStream = UncompressStream(readStream))
            {
                while (true)
                {
                    var fragment = bufferQueue.Dequeue();

                    //  Await for buffer to be released
                    await fragment.TrackAsync();

                    var size = await uncompressedStream.ReadAsync(fragment.ToMemoryBlock());

                    if (size > 0)
                    {
                        var croppedFragment = fragment.Splice(size).Left;

                        croppedFragment.Reserve();
                        fragmentQueue.Enqueue(croppedFragment);
                    }
                    if (size < fragment.Length)
                    {
                        fragmentQueue.Complete();
                        await sinkTask;
                        return;
                    }
                }
            }
        }

        private Stream UncompressStream(Stream readStream)
        {
            switch (_compression)
            {
                case DataSourceCompressionType.None:
                    return readStream;
                case DataSourceCompressionType.GZip:
                    return new GZipStream(readStream, CompressionMode.Decompress);
                case DataSourceCompressionType.Zip:
                    var archive = new ZipArchive(readStream);
                    var entries = archive
                        .Entries
                        .Where(e => !string.IsNullOrWhiteSpace(e.Name))
                        .Where(e => e.Length > 0);

                    if (!entries.Any())
                    {
                        throw new InvalidDataException(
                            $"Archive (zipped blob) doesn't contain any file");
                    }
                    else
                    {
                        return entries.First().Open();
                    }

                default:
                    throw new NotSupportedException(_compression.ToString());
            }
        }
    }
}