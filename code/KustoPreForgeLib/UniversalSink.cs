using KustoPreForgeLib.BlobSources;

namespace KustoPreForgeLib
{
    public class UniversalSink : ISink
    {
        private readonly IAsyncEnumerable<object> _genericEnumerable;

        #region Constructors
        public static UniversalSink Create<T>(IDataSource<T> source)
        {
            return new UniversalSink(source);
        }

        private UniversalSink(IAsyncEnumerable<object> genericEnumerable)
        {
            _genericEnumerable = genericEnumerable;
        }
        #endregion

        async Task ISink.ProcessSourceAsync()
        {
            await foreach(var i in _genericEnumerable)
            {
            }
        }
    }
}