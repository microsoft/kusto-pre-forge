using Azure;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoBlobSplitLib
{
    internal static class AsyncPageableHelper
    {
        public static async Task<IImmutableList<T>> ToListAsync<T>(
            this AsyncPageable<T> pageable,
            Func<T, bool>? predicate = null)
            where T : notnull
        {
            var builder = ImmutableArray<T>.Empty.ToBuilder();

            predicate = predicate ?? (b => true);

            await foreach (var item in pageable)
            {
                if (predicate(item))
                {
                    builder.Add(item);
                }
            }

            return builder.ToImmutableList();
        }
    }
}