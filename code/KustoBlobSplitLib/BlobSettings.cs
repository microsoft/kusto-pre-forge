using Kusto.Data.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoBlobSplitLib
{
    public class BlobSettings
    {
        #region Properties
        public DataSourceFormat Format { get; }

        public DataSourceCompressionType InputCompression { get; }

        public DataSourceCompressionType OutputCompression { get; }

        public bool HasHeaders { get; }

        public int MaxBytesPerShard { get; }
        #endregion

        public BlobSettings(
            DataSourceFormat? format,
            DataSourceCompressionType? inputCompression,
            DataSourceCompressionType? outputCompression,
            bool? hasHeaders,
            int? maxMbPerShard)
        {
            Format = format ?? DataSourceFormat.txt;
            InputCompression = inputCompression ?? DataSourceCompressionType.None;
            OutputCompression = outputCompression ?? DataSourceCompressionType.None;
            HasHeaders = hasHeaders ?? true;
            MaxBytesPerShard = (maxMbPerShard ?? 200) * 1024 * 1024;
        }

        public void WriteOutSettings()
        {
            Console.WriteLine($"Format:  {Format}");
            Console.WriteLine($"Compression:  {InputCompression}");
            Console.WriteLine($"Compression:  {OutputCompression}");
            Console.WriteLine($"HasHeaders:  {HasHeaders}");
            Console.WriteLine($"MaxBytesPerShard:  {MaxBytesPerShard}");
        }
    }
}