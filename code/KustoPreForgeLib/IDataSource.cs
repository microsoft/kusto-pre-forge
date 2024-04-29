using KustoPreForgeLib.BlobSources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoPreForgeLib
{
    public interface IDataSource<T> : IAsyncEnumerable<SourceData<T>>
    {
    }
}