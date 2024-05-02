using KustoPreForgeLib.Memory;
using System.Collections.Immutable;
using System.IO.Compression;
using System.Reflection.PortableExecutable;

namespace KustoPreForgeLib.Transforms
{
    internal class CsvParseTransform : IDataSource<CsvOutput>
    {
        private readonly BufferFragment _buffer;
        private readonly IDataSource<BufferFragment> _contentSource;
        private readonly PerfCounterJournal _journal;

        public CsvParseTransform(
            BufferFragment buffer,
            IDataSource<BufferFragment> contentSource,
            PerfCounterJournal journal)
        {
            _buffer = buffer;
            _contentSource = contentSource;
            _journal = journal;
        }

        async IAsyncEnumerator<SourceData<CsvOutput>>
            IAsyncEnumerable<SourceData<CsvOutput>>.GetAsyncEnumerator(
            CancellationToken cancellationToken)
        {
            await foreach (var data in _contentSource)
            {
                var inputBuffer = data.Data;
                var output = Parse(inputBuffer);

                yield return new SourceData<CsvOutput>(
                    output,
                    null,
                    null,
                    data);
            }
        }

        private CsvOutput Parse(BufferFragment inputBuffer)
        {
            var span = inputBuffer.ToSpan();
            //  This whole algorithm was authored by chat GPT
            //  Essentially implementing a CSV in one method
            var inQuotes = false;
            var index = 0;
            var recordStart = 0;
            var recordLengths = new List<int>();

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
                }
                else if (currentChar == '\n' && !inQuotes)
                {
                    // End of record, add current field to the current record and finalize the record
                    recordLengths.Add(index - recordStart + 1);
                    recordStart = index + 1;
                }
                else
                {
                    //  Add character to current field
                }
                ++index;
            }

            return new CsvOutput(inputBuffer, recordLengths.ToImmutableArray());
        }
    }
}