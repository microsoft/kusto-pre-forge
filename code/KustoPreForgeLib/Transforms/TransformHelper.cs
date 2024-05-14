using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoPreForgeLib.Transforms
{
    internal static class TransformHelper
    {
        public static CancellationToken CreateCancellationToken(TimeSpan? delay = null)
        {
#if DEBUG
            var source = new CancellationTokenSource(delay ?? TimeSpan.FromSeconds(40));
#else
            var source = new CancellationTokenSource(delay ?? TimeSpan.FromSeconds(10));
#endif

            return source.Token;
        }
    }
}