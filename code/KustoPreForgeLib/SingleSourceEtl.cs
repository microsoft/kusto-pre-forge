using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoPreForgeLib.LineBased
{
    internal class SingleSourceEtl : IEtl
    {
        private readonly ISource _source;


        public SingleSourceEtl(ISource source)
        {
            _source = source;
        }

        async Task IEtl.ProcessAsync()
        {
            await _source.ProcessSourceAsync();
        }
    }
}
