using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
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

namespace KustoPreForgeLib.LineBased
{
    internal class TextSource : ISource
    {
#if DEBUG
        private const int STORAGE_BUFFER_SIZE = 10 * 1024 * 1024;
        private const int MIN_STORAGE_FETCH = 1 * 1024 * 1024;
#else
        private const int STORAGE_BUFFER_SIZE = 100 * 1024 * 1024;
        private const int MIN_STORAGE_FETCH = 1 * 1024 * 1024;
#endif
        private const int BUFFER_SIZE = 10 * 1024 * 1024;

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
                BufferSize = STORAGE_BUFFER_SIZE
            };
            var buffer = BufferFragment.Create(BUFFER_SIZE);
            var bufferAvailable = buffer;
            var fragmentList = (IEnumerable<BufferFragment>)ImmutableArray<BufferFragment>.Empty;
            var fragmentQueue = new WaitingQueue<BufferFragment>() as IWaitingQueue<BufferFragment>;
            var releaseQueue = new WaitingQueue<BufferFragment>() as IWaitingQueue<BufferFragment>;
            var sinkTask = Task.Run(() => _sink.ProcessAsync(
                null,
                fragmentQueue,
                releaseQueue));
            var lastFragment = (BufferFragment?)null;

            Console.WriteLine($"Reading '{_sourceBlob.Uri}'");
            using (var readStream = await _sourceBlob.OpenReadAsync(readOptions))
            using (var uncompressedStream = UncompressStream(readStream))
            {
                while (true)
                {
                    if (bufferAvailable.Length >= MIN_STORAGE_FETCH
                        || bufferAvailable.GetMemoryBlocks().Count() > 1)
                    {
                        var readLength = await uncompressedStream.ReadAsync(
                            bufferAvailable.GetMemoryBlocks().First());

                        if (readLength == 0)
                        {
                            fragmentQueue.Complete();
                            await sinkTask;
                            return;
                        }
                        else
                        {
                            var currentFragment = bufferAvailable.SpliceBefore(readLength);

                            fragmentQueue.Enqueue(currentFragment);
                            lastFragment = currentFragment;
                            bufferAvailable = bufferAvailable.SpliceAfter(readLength - 1);
                        }
                    }
                    while (releaseQueue.HasData || bufferAvailable.Length < MIN_STORAGE_FETCH)
                    {
                        var fragmentResult = await TaskHelper.AwaitAsync(
                            releaseQueue.DequeueAsync(),
                            sinkTask);

                        if (fragmentResult.IsCompleted)
                        {
                            throw new NotSupportedException(
                                "releaseQueue should never be observed as completed");
                        }
                        fragmentList = fragmentList.Prepend(fragmentResult.Item!);

                        var bundle = bufferAvailable.TryMerge(fragmentList);

                        if (lastFragment == null
                            //  Make sure we merge a fragment that is contiguous with last one
                            || lastFragment.IsContiguouslyBefore(bundle.Fragment))
                        {
                            bufferAvailable = bundle.Fragment;
                            fragmentList = bundle.List;
                            lastFragment = null;
                        }
                        else
                        {
                            int a = 5;
                            ++a;
                        }
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