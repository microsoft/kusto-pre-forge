using KustoPreForgeLib.Memory;
using System.Collections.Immutable;
using System.Text;

namespace KustoPreForgeLib.Transforms
{
    internal class PartitioningTextTransform : IDataSource<SinglePartitionContent>
    {
        private readonly BufferFragment _buffer;
        private readonly IDataSource<PartitionedTextContent> _contentSource;
        private readonly PerfCounterJournal _journal;

        public PartitioningTextTransform(
            BufferFragment buffer,
            IDataSource<PartitionedTextContent> contentSource,
            PerfCounterJournal journal)
        {
            _buffer = buffer;
            _contentSource = contentSource;
            _journal = journal;
        }

        async IAsyncEnumerator<SourceData<SinglePartitionContent>>
            IAsyncEnumerable<SourceData<SinglePartitionContent>>.GetAsyncEnumerator(
            CancellationToken cancellationToken)
        {
            await foreach (var data in _contentSource)
            {
                var input = data.Data;
                var partitionSizes = ComputePartitionSizes(input);
                var sourceMemory = input.Content.ToMemory();
                var partitionDisposables = ReferenceCounterDisposable.Create(
                    partitionSizes.Count,
                    data)
                    .Zip(partitionSizes.Keys)
                    .ToImmutableDictionary(p => p.Second, p => p.First);
                var unitId = Guid.NewGuid();

                foreach (var partitionId in partitionSizes.Keys)
                {
                    var subBuffer =
                        await _buffer.ReserveSubBufferAsync(partitionSizes[partitionId] + 1);
                    var subBufferMemory = subBuffer.ToMemory();
                    var output = new SinglePartitionContent(
                        subBuffer,
                        unitId,
                        partitionId,
                        input.PartitionValueSamples[partitionId]);
                    var recordLengths = input.RecordLengths;
                    var partitionIds = input.PartitionIds;
                    var sourceOffset = 0;
                    var destinationOffset = 0;

                    for (int i = 0; i != input.RecordLengths.Count; i++)
                    {
                        if (partitionIds[i] == partitionId)
                        {
                            var length = recordLengths[i];
                            var source = sourceMemory.Slice(sourceOffset, length);
                            var destination = subBufferMemory.Slice(sourceOffset, length);

                            source.CopyTo(destination);
                            sourceOffset += length;
                            destinationOffset += length;
                        }
                    }
                    //  Add return carriage to make sure we can concatenate those buffers
                    subBuffer.ToSpan()[subBuffer.Length - 1] = (byte)'\n';

                    yield return new SourceData<SinglePartitionContent>(
                        output,
                        null,
                        null,
                        partitionDisposables[partitionId]);
                }
                //  Release the input buffer
                input.Content.Release();
            }
        }

        private IImmutableDictionary<int, int> ComputePartitionSizes(PartitionedTextContent input)
        {
            var partitionSizes = new Dictionary<int, int>();

            foreach (var pair in input.PartitionIds.Zip(input.RecordLengths))
            {
                var partitionId = pair.First;
                var recordLength = pair.Second;

                if (partitionSizes.TryGetValue(partitionId, out var totalLength))
                {
                    partitionSizes[partitionId] = totalLength + recordLength;
                }
                else
                {
                    partitionSizes[partitionId] = recordLength;
                }
            }

            return partitionSizes.ToImmutableDictionary();
        }
    }
}