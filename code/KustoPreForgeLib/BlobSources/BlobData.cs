using Azure.Storage.Blobs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoPreForgeLib.BlobSources
{
    public record BlobData(BlobClient BlobClient, long BlobSize);
}