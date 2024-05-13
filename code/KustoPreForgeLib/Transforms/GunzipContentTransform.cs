using KustoPreForgeLib.Memory;
using System.IO.Compression;

namespace KustoPreForgeLib.Transforms
{
    internal class GunzipContentTransform : IDataSource<BufferFragment>
    {
        private readonly BufferFragment _buffer;
        private readonly IDataSource<BufferFragment> _contentSource;
        private readonly PerfCounterJournal _journal;

        public GunzipContentTransform(
            BufferFragment buffer,
            IDataSource<BufferFragment> contentSource,
            PerfCounterJournal journal)
        {
            _buffer = buffer;
            _contentSource = contentSource;
            _journal = journal;
        }

        async IAsyncEnumerator<SourceData<BufferFragment>>
            IAsyncEnumerable<SourceData<BufferFragment>>.GetAsyncEnumerator(
            CancellationToken cancellationToken)
        {
            await foreach (var data in _contentSource)
            {
                var inputBuffer = data.Data;
                var uncompressedSize = ComputeUncompressedSize(inputBuffer);
                var outputBuffer = await _buffer.ReserveSubBufferAsync(
                    uncompressedSize,
                    TransformHelper.CreateCancellationToken());

                UncompressContent(inputBuffer, outputBuffer);
                inputBuffer.Release();
                _journal.AddReading("Gunzip.Size", uncompressedSize);

                yield return new SourceData<BufferFragment>(
                    outputBuffer,
                    null,
                    null,
                    data);
            }
        }

        private int ComputeUncompressedSize(BufferFragment inputBuffer)
        {
            using (var inputStream = inputBuffer.ToMemoryStream())
            {
                var sizeBuffer = new byte[4];

                if (inputStream.Length < 8)
                {
                    throw new InvalidDataException("Invalid Gzipped format");
                }
                //  Go at the end of the gzipped stream to read the original file size
                inputStream.Seek(-4, SeekOrigin.End);

                // Read the last 4 bytes (uncompressed size)
                inputStream.Read(sizeBuffer);

                // Extract uncompressed size from the header (little-endian format)
                var uncompressedSize = BitConverter.ToInt32(sizeBuffer, 0);

                return uncompressedSize;
            }
        }

        private void UncompressContent(BufferFragment inputBuffer, BufferFragment outputBuffer)
        {
            using (var inputStream = inputBuffer.ToMemoryStream())
            using (var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress))
            {
                gzipStream.ReadExactly(outputBuffer.ToSpan());
            }
        }
    }
}