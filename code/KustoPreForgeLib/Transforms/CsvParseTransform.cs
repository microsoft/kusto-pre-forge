using KustoPreForgeLib.Memory;
using System.Collections.Immutable;
using System.IO.Compression;
using System.Reflection.PortableExecutable;

namespace KustoPreForgeLib.Transforms
{
    internal class CsvParseTransform : IDataSource<PartitionedTextOutput>
    {
        private readonly IDataSource<BufferFragment> _contentSource;
        private readonly int _columnIndexToExtract;
        private readonly PerfCounterJournal _journal;

        public CsvParseTransform(
            IDataSource<BufferFragment> contentSource,
            int columnIndexToExtract,
            PerfCounterJournal journal)
        {
            _contentSource = contentSource;
            _columnIndexToExtract = columnIndexToExtract;
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
            var partitionValues = new List<MemoryInterval>();

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
                        partitionValues.Add(
                            new MemoryInterval(columnStart, index - columnStart + 1));
                    }
                    columnStart = columnIndex + 1;
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
                partitionValues.ToImmutableArray());
        }
    }
}