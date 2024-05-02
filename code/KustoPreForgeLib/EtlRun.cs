using Kusto.Data.Common;
using KustoPreForgeLib.BlobSources;
using KustoPreForgeLib.Memory;
using KustoPreForgeLib.Settings;
using KustoPreForgeLib.Transforms;
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
        private const int BUFFER_SIZE = 100 * 1000000;

        public static async Task RunEtlAsync(
            EtlAction action,
            IDataSource<BlobData> blobSource,
            RunningContext context)
        {
            var stopwatch = new Stopwatch();

            stopwatch.Start();

            var journal = new PerfCounterJournal();
            var etl = CreateEtl(action, blobSource, context, journal);

            journal.StartReporting();
            await etl.ProcessAsync();
            await journal.StopReportingAsync();

            Console.WriteLine($"ETL completed in {stopwatch.Elapsed}");
        }

        private static IEtl CreateEtl(
            EtlAction action,
            IDataSource<BlobData> blobSource,
            RunningContext context,
            PerfCounterJournal journal)
        {
            switch (action)
            {
                case EtlAction.Split:
                    return CreateSplitEtl(context, journal);
                case EtlAction.PrePartition:
                    return CreatePrePartitionEtl(blobSource, context, journal);

                default:
                    throw new NotSupportedException(action.ToString());
            }
        }

        private static IEtl CreateSplitEtl(RunningContext context, PerfCounterJournal journal)
        {
            switch (context.BlobSettings.Format)
            {
                //case DataSourceFormat.txt:
                //    {
                //        var streamSinkFactory = GetStreamSinkFactory(context);
                //        var lineParsingSink = new TextLineParsingSink(
                //            (header) => new TextPartitionSink(header, streamSinkFactory),
                //            context.BlobSettings.HasHeaders);
                //        var source = new TextSource(
                //            context.SourceBlobClient!,
                //            context.BlobSettings.InputCompression,
                //            lineParsingSink);

                //        return new SingleSourceEtl(source);
                //    }

                default:
                    throw new NotSupportedException($"Format '{context.BlobSettings.Format}'");
            }
        }

        private static IEtl CreatePrePartitionEtl(
            IDataSource<BlobData> blobSource,
            RunningContext context,
            PerfCounterJournal journal)
        {
            IDataSource<CsvOutput> CreateContentSource(DataSourceCompressionType type)
            {
                switch(type)
                {
                    case DataSourceCompressionType.None:
                        return new CsvParseTransform(
                            new DownloadBlobTransform(
                                new BufferFragment(BUFFER_SIZE),
                                blobSource,
                                journal),
                            journal);
                    case DataSourceCompressionType.GZip:
                        return new CsvParseTransform(
                            new GunzipContentTransform(
                                new BufferFragment(BUFFER_SIZE/2),
                                new DownloadBlobTransform(
                                    new BufferFragment(BUFFER_SIZE/2),
                                    blobSource,
                                    journal),
                                journal),
                            journal);

                    default:
                        throw new NotSupportedException();
                }
            }

            return new SingleSourceEtl(
                UniversalSink.Create(
                    CreateContentSource(context.BlobSettings.InputCompression),
                    journal));
        }

        //private static Func<Memory<byte>?, string, ITextSink> GetStreamSinkFactory(
        //    RunningContext context)
        //{
        //    if (context.IngestClient == null)
        //    {
        //        return (header, shardIndex) => new TextBlobSink(header, context, shardIndex);
        //    }
        //    else
        //    {
        //        var blobNamePrefix = Guid.NewGuid().ToString();

        //        return (header, shardIndex) => new TextKustoSink(header, context, shardIndex, blobNamePrefix);
        //    }
        //}
    }
}