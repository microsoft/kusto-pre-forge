using KustoPreForgeLib.Memory;
using System.Collections.Immutable;
using System.IO.Compression;
using System.Reflection.PortableExecutable;
using System.Text;

namespace KustoPreForgeLib.Transforms
{
    internal class CsvParseTransform : IDataSource<PartitionedTextOutput>
    {
        private readonly IDataSource<BufferFragment> _contentSource;
        private readonly int _columnIndexToExtract;
        private readonly Func<ReadOnlyMemory<byte>, int> _partitionFunction;
        private readonly PerfCounterJournal _journal;
        private readonly StringBuilder _builder = new ();

        public CsvParseTransform(
            IDataSource<BufferFragment> contentSource,
            int columnIndexToExtract,
            Func<ReadOnlyMemory<byte>, int> partitionFunction,
            PerfCounterJournal journal)
        {
            _contentSource = contentSource;
            _columnIndexToExtract = columnIndexToExtract;
            _partitionFunction = partitionFunction;
            _journal = journal;
        }

        async IAsyncEnumerator<SourceData<PartitionedTextOutput>>
            IAsyncEnumerable<SourceData<PartitionedTextOutput>>.GetAsyncEnumerator(
            CancellationToken cancellationToken)
        {
            await foreach (var data in _contentSource)
            {
                var inputBuffer = data.Data;
                var output = Parse(inputBuffer);

                //  To remove
                inputBuffer.Release();
                yield return new SourceData<PartitionedTextOutput>(
                    output,
                    null,
                    null,
                    data);
            }
        }

        private PartitionedTextOutput Parse(BufferFragment inputBuffer)
        {
            var span = inputBuffer.ToSpan();
            //  This whole algorithm was authored by chat GPT
            //  Essentially implementing a CSV in one method
            var inQuotes = false;
            var index = 0;
            var columnIndex = 0;
            var recordStart = 0;
            var columnStart = 0;
            var recordLengths = new List<int>();
            var partitionIds = new List<int>();
            var partitionValueSamples = new Dictionary<int, string>();

            while (index < span.Length)
            {
                var currentChar = span[index];

                if (currentChar == '"')
                {
                    if (!inQuotes)
                    {
                        inQuotes = true;
                    }
                    else
                    {
                        //  Check if the next character is also a double quote (escaped quote)
                        if (index + 1 < span.Length && span[index + 1] == '"')
                        {
                            ++index;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                }
                else if (currentChar == ',' && !inQuotes)
                {
                    //  End of field, add current field to the current record
                    if (columnIndex == _columnIndexToExtract)
                    {
                        var columnMemory = inputBuffer.ToMemory()
                            .Slice(columnStart, index - columnStart);
                        var partitionId = _partitionFunction(columnMemory);

                        if (!partitionValueSamples.ContainsKey(partitionId))
                        {
                            var sample = GetSample(columnMemory);

                            partitionValueSamples.Add(partitionId, sample);
                        }
                    }
                    columnStart = index + 1;
                    ++columnIndex;
                }
                else if (currentChar == '\n' && !inQuotes)
                {
                    // End of record, add current field to the current record and finalize the record
                    recordLengths.Add(index - recordStart + 1);
                    recordStart = index + 1;
                    columnIndex = 0;
                }
                else
                {
                    //  Add character to current field
                }
                ++index;
            }

            return new PartitionedTextOutput(
                inputBuffer,
                recordLengths.ToImmutableArray(),
                partitionIds.ToImmutableArray(),
                partitionValueSamples.ToImmutableDictionary());
        }

        private string GetSample(ReadOnlyMemory<byte> columnMemory)
        {
            _builder.Clear();
            foreach(var b in columnMemory.Span)
            {
                _builder.Append((char)b);
            }

            return _builder.ToString();
        }
    }
}