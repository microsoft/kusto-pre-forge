using KustoPreForgeLib.BlobSources;

namespace KustoPreForgeLib
{
    public class UniversalSink : ISink
    {
        private readonly IAsyncEnumerable<object> _genericEnumerable;
        private readonly PerfCounterJournal _journal;

        #region Constructors
        public static UniversalSink Create<T>(
            IDataSource<T> source,
            PerfCounterJournal journal)
        {
            return new UniversalSink(source, journal);
        }

        private UniversalSink(
            IAsyncEnumerable<object> genericEnumerable,
            PerfCounterJournal journal)
        {
            _genericEnumerable = genericEnumerable;
            _journal = journal;
        }
        #endregion

        async Task ISink.ProcessSourceAsync()
        {
            await foreach(var i in _genericEnumerable)
            {
                _journal.AddReading("UniversalSink.ItemCount", 1);
            }
        }
    }
}