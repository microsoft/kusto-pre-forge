using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoPreForgeLib
{
    internal class SingleSourceEtl : IEtl
    {
        private readonly ISink _source;


        public SingleSourceEtl(ISink source)
        {
            _source = source;
        }

        async Task IEtl.ProcessAsync()
        {
            await _source.ProcessSourceAsync();
        }
    }
}
