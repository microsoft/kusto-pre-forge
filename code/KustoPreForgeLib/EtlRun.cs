using Kusto.Cloud.Platform.Data;
using Kusto.Data.Common;
using KustoPreForgeLib.BlobSources;
using KustoPreForgeLib.Memory;
using KustoPreForgeLib.Settings;
using KustoPreForgeLib.Transforms;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoPreForgeLib
{
    public static class EtlRun
    {
        #region Inner Types
        private record PartitioningConfig(
            int ColumnIndex,
            int MaxPartitionCount,
            int Seed);
        #endregion

        private const int BUFFER_SIZE = 100 * 1000000;

        public static async Task RunEtlAsync(
            RunSettings runSettings,
            IDataSource<BlobData> blobSource,
            RunningContext context,
            PerfCounterJournal journal)
        {
            var stopwatch = new Stopwatch();

            stopwatch.Start();

            var etl = await CreateEtlAsync(runSettings, blobSource, context, journal);

            await etl.ProcessAsync();
            await journal.StopReportingAsync();

            Console.WriteLine($"ETL completed in {stopwatch.Elapsed}");
        }

        private static async Task<IEtl> CreateEtlAsync(
            RunSettings runSettings,
            IDataSource<BlobData> blobSource,
            RunningContext context,
            PerfCounterJournal journal)
        {
            switch (runSettings.Action)
            {
                case EtlAction.Split:
                    return CreateSplitEtl(context, journal);
                case EtlAction.PrePartition:
                    return await CreatePrePartitionEtlAsync(
                        blobSource,
                        runSettings.KustoSettings!,
                        context,
                        journal);

                default:
                    throw new NotSupportedException(runSettings.Action.ToString());
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

        private static async Task<IEtl> CreatePrePartitionEtlAsync(
            IDataSource<BlobData> blobSource,
            KustoSettings kustoSettings,
            RunningContext context,
            PerfCounterJournal journal)
        {
            IDataSource<BufferFragment> CreateContentSource(DataSourceCompressionType type)
            {
                switch (type)
                {
                    case DataSourceCompressionType.None:
                        return new DownloadBlobTransform(
                            new BufferFragment(BUFFER_SIZE),
                            blobSource,
                            journal);
                    case DataSourceCompressionType.GZip:
                        return new GunzipContentTransform(
                            new BufferFragment(BUFFER_SIZE / 2),
                            new DownloadBlobTransform(
                                new BufferFragment(BUFFER_SIZE / 2),
                                blobSource,
                                journal),
                            journal);

                    default:
                        throw new NotSupportedException();
                }
            }

            var partitionConfig = await FetchPartitioningConfig(kustoSettings, context);

            return new SingleSourceEtl(
                new PartitionedContentSink(
                    new PartitioningTextTransform(
                        new BufferFragment(BUFFER_SIZE),
                        new CsvParseTransform(
                            CreateContentSource(context.BlobSettings.InputCompression),
                            partitionConfig.ColumnIndex,
                            PartitioningHelper.GetPartitionStringFunction(
                                partitionConfig.MaxPartitionCount,
                                partitionConfig.Seed),
                            journal),
                        journal),
                    context.StagingContainers!,
                    TimeSpan.FromMinutes(1),
                    kustoSettings.TempDirectory,
                    journal));
        }

        private static async Task<PartitioningConfig> FetchPartitioningConfig(
            KustoSettings kustoSettings,
            RunningContext context)
        {
            if (context.AdminEngineClient == null)
            {
                throw new NotSupportedException(
                    "Kusto must be destination for pre partitioning");
            }
            var policyReaderTask = context.AdminEngineClient!.ExecuteControlCommandAsync(
                kustoSettings.Database!,
                @"
.show table Logs policy partitioning
| project Keys=todynamic(Policy).PartitionKeys
| mv-expand Keys
| where Keys.Kind==""Hash""
| project
    ColumnName=tostring(Keys.ColumnName),
    MaxPartitionCount = toint(Keys.Properties.MaxPartitionCount),
    Seed = toint(Keys.Properties.Seed)");
            var tableReader = await context.AdminEngineClient!.ExecuteControlCommandAsync(
                kustoSettings.Database!,
                @"
.show table Logs
| project AttributeName");
            var policyReader = await policyReaderTask;
            var policyRow = policyReader.ToDataSet().Tables[0].Rows[0];
            var columnName = (string)policyRow["ColumnName"];
            var maxPartitionCount = (int)policyRow["MaxPartitionCount"];
            var seed = (int)policyRow["Seed"];
            var columnNames = tableReader.ToDataSet().Tables[0].Rows
                .Cast<DataRow>()
                .Select(r => r[0].ToString())
                .ToImmutableArray();
            var hashPartitionKeyColumnIndex = columnNames.IndexOf(columnName);

            return new PartitioningConfig(
                hashPartitionKeyColumnIndex,
                maxPartitionCount,
                seed);
        }
    }
}