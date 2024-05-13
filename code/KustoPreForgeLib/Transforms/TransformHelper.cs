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
            var source = new CancellationTokenSource(delay ?? TimeSpan.FromSeconds(100));

            return source.Token;
        }
    }
}