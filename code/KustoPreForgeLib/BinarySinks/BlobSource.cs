using KustoPreForgeLib.BlobEnumerables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoPreForgeLib.BinarySinks
{
    internal class BlobSource : ISource
    {
        private readonly IBlobEnumerable _blobEnumerable;

        public BlobSource(IBlobEnumerable blobEnumerable)
        {
            _blobEnumerable = blobEnumerable;
        }

        #region ISource
        async Task ISource.ProcessSourceAsync()
        {
            await foreach (var blobClient in _blobEnumerable.EnumerateBlobs())
            {
                throw new NotImplementedException();
            }
        }
        #endregion
    }
}