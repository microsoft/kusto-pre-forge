using KustoPreForgeLib.Memory;
using System.Collections.Immutable;
using System.Text;

namespace KustoPreForgeLib.Transforms
{
    internal class PartitionedContentSink : ISink
    {
        private readonly IDataSource<SinglePartitionContent> _contentSource;
        private readonly PerfCounterJournal _journal;

        public PartitionedContentSink(
            IDataSource<SinglePartitionContent> contentSource,
            PerfCounterJournal journal)
        {
            _contentSource = contentSource;
            _journal = journal;
        }

        async Task ISink.ProcessSourceAsync()
        {
            await foreach (var data in _contentSource)
            {
                data.Data.Content.Release();
                await using (data)
                {
                }
            }
        }
    }
}