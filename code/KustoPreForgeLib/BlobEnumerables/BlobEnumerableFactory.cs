using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoPreForgeLib.BlobEnumerables
{
    public class BlobEnumerableFactory
    {
        public static IBlobEnumerable Create(
            RunningContext runningContext,
            SourceSettings sourceSettings)
        {
            if (sourceSettings.SourceBlob != null)
            {
                throw new NotSupportedException();
            }
            else if (sourceSettings.ServiceBusQueueUrl != null)
            {
                throw new NotSupportedException();
            }
            else if (sourceSettings.SourceBlobsPrefix != null)
            {
                return new ListBlobEnumerable(
                    sourceSettings.SourceBlobsPrefix,
                    sourceSettings.SourceBlobsSuffix,
                    runningContext.Credentials);
            }
            else
            {
                throw new NotSupportedException();
            }
        }
    }
}