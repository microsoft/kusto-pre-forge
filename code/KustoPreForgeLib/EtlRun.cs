using Kusto.Data.Common;
using KustoPreForgeLib.LineBased;
using KustoPreForgeLib.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoPreForgeLib
{
    public static class EtlRun
    {
        public static async Task RunEtlAsync(RunningContext context)
        {
            var stopwatch = new Stopwatch();

            stopwatch.Start();

            var etl = CreateEtl(context);

            await etl.ProcessAsync();
            Console.WriteLine($"ETL completed in {stopwatch.Elapsed}");
        }

        private static IEtl CreateEtl(RunningContext context)
        {
            switch (context.BlobSettings.Format)
            {
                case DataSourceFormat.txt:
                    {
                        var subSinkFactory = GetSubSinkFactory(context);
                        var splitSink = new TextSplitSink(subSinkFactory);
                        var parsingSink = new TextLineParsingSink(
                            splitSink,
                            context.BlobSettings.HasHeaders);
                        var source = new TextSource(
                            context.SourceBlobClient,
                            context.BlobSettings.InputCompression,
                            parsingSink);

                        return new SingleSourceEtl(source);
                    }

                default:
                    throw new NotSupportedException($"Format '{context.BlobSettings.Format}'");
            }
        }

        private static Func<string, ITextSink> GetSubSinkFactory(RunningContext context)
        {
            if (context.IngestClient == null)
            {
                return (shardIndex) => new TextBlobSink(context, shardIndex);
            }
            else
            {
                var blobNamePrefix = Guid.NewGuid().ToString();

                return (shardIndex) => new TextKustoSink(context, shardIndex, blobNamePrefix);
            }
        }
    }
}