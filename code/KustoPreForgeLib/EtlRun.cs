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
            if (context.SourceBlobClient == null)
            {
                throw new ArgumentNullException(nameof(context.SourceBlobClient));
            }

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
                        var streamSinkFactory = GetStreamSinkFactory(context);
                        var lineParsingSink = new TextLineParsingSink(
                            (header) => new TextPartitionSink(header, streamSinkFactory),
                            context.BlobSettings.HasHeaders);
                        var source = new TextSource(
                            context.SourceBlobClient!,
                            context.BlobSettings.InputCompression,
                            lineParsingSink);

                        return new SingleSourceEtl(source);
                    }

                default:
                    throw new NotSupportedException($"Format '{context.BlobSettings.Format}'");
            }
        }

        private static Func<Memory<byte>?, string, ITextSink> GetStreamSinkFactory(
            RunningContext context)
        {
            if (context.IngestClient == null)
            {
                return (header, shardIndex) => new TextBlobSink(header, context, shardIndex);
            }
            else
            {
                var blobNamePrefix = Guid.NewGuid().ToString();

                return (header, shardIndex) => new TextKustoSink(header, context, shardIndex, blobNamePrefix);
            }
        }
    }
}