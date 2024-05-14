using KustoPreForgeLib.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoPreForgeLib.BlobSources
{
    public class BlobSourceFactory
    {
        public static IDataSource<BlobData> Create(
            RunningContext runningContext,
            SourceSettings sourceSettings,
            PerfCounterJournal journal)
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
                return new ListBlobSource(
                    sourceSettings.SourceBlobsPrefix,
                    sourceSettings.SourceBlobsSuffix,
                    runningContext.Credentials,
                    journal);
            }
            else
            {
                throw new NotSupportedException();
            }
        }
    }
}