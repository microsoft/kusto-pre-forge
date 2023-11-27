using Kusto.Data.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoBlobSplitLib
{
    public class RunSettings
    {
        #region Properties
        public AuthMode AuthMode { get; }

        public string? ManagedIdentityResourceId { get; }

        public string? ServiceBusQueueUrl { get; }

        public Uri? KustoIngestUri { get; }

        public string? KustoDb { get; }

        public string? KustoTable { get; }

        public Uri SourceBlob { get; }

        public Uri? DestinationBlobPrefix { get; }

        public BlobSettings BlobSettings { get; }
        #endregion

        #region Constructors
        public static RunSettings FromEnvironmentVariables()
        {
            var authMode = GetEnum<AuthMode>("AuthMode", false);
            var managedIdentityResourceId = GetString("ManagedIdentityResourceId", false);
            var serviceBusQueueUrl = GetString("ServiceBusQueueUrl", false);
            var sourceBlob = GetUri("SourceBlob");
            var destinationBlobPrefix = GetUri("DestinationBlobPrefix", false);
            var kustoIngestUri = GetUri("KustoIngestUri", false);
            var kustoDb = GetString("KustoDb", false);
            var kustoTable = GetString("KustoTable", false);
            var format = GetEnum<DataSourceFormat>("Format", false);
            var inputCompression = GetEnum<DataSourceCompressionType>("InputCompression", false);
            var outputCompression = GetEnum<DataSourceCompressionType>("OutputCompression", false);
            var hasHeaders = GetBool("CsvHeaders", false);
            var maxMbPerShard = GetInt("MaxMbPerShard", false);

            return new RunSettings(
                authMode,
                managedIdentityResourceId,
                serviceBusQueueUrl,
                kustoIngestUri,
                kustoDb,
                kustoTable,
                sourceBlob,
                destinationBlobPrefix,
                new BlobSettings(
                    format,
                    inputCompression,
                    outputCompression,
                    hasHeaders,
                    maxMbPerShard));
        }

        public RunSettings(
            AuthMode? authMode,
            string? managedIdentityResourceId,
            string? serviceBusQueueUrl,
            Uri? kustoIngestUri,
            string? kustoDb,
            string? kustoTable,
            Uri sourceBlob,
            Uri? destinationBlobPrefix,
            BlobSettings blobSettings)
        {
            if (kustoIngestUri != null && (kustoDb == null || kustoTable == null))
            {
                throw new ArgumentNullException(nameof(kustoDb));
            }
            else if (destinationBlobPrefix == null)
            {
                throw new ArgumentNullException(
                    nameof(destinationBlobPrefix),
                    "No destination specified");
            }
            if (AuthMode == AuthMode.ManagedIdentity
                && string.IsNullOrWhiteSpace(managedIdentityResourceId))
            {
                throw new ArgumentNullException(nameof(managedIdentityResourceId));
            }

            AuthMode = authMode ?? AuthMode.Default;
            ManagedIdentityResourceId = managedIdentityResourceId;
            ServiceBusQueueUrl = serviceBusQueueUrl;
            KustoIngestUri = kustoIngestUri;
            KustoDb = kustoDb;
            KustoTable = kustoTable;
            SourceBlob = sourceBlob;
            DestinationBlobPrefix = destinationBlobPrefix;
            BlobSettings = blobSettings;
        }
        #endregion

        #region Environment variables
        #region String
        private static string GetString(string variableName)
        {
            var value = GetString(variableName, true);

            return value!;
        }

        private static string? GetString(string variableName, bool mustExist)
        {
            var value = Environment.GetEnvironmentVariable(variableName);

            if (mustExist && value == null)
            {
                throw new ArgumentNullException(variableName, "Environment variable missing");
            }

            return value;
        }
        #endregion

        #region Uri
        private static Uri GetUri(string variableName)
        {
            var uri = GetUri(variableName, true);

            return uri!;
        }

        private static Uri? GetUri(string variableName, bool mustExist)
        {
            var value = Environment.GetEnvironmentVariable(variableName);

            if (mustExist && value == null)
            {
                throw new ArgumentNullException(variableName, "Environment variable missing");
            }

            if (value != null)
            {
                try
                {
                    var uri = new Uri(value, UriKind.Absolute);

                    return uri;
                }
                catch
                {
                    throw new ArgumentNullException(variableName, $"Unsupported value:  '{value}'");
                }
            }
            else
            {
                return null;
            }
        }
        #endregion

        #region Enum
        private static T? GetEnum<T>(string variableName, bool mustExist)
            where T : struct
        {
            var value = Environment.GetEnvironmentVariable(variableName);

            if (mustExist && value == null)
            {
                throw new ArgumentNullException(variableName, "Environment variable missing");
            }

            if (value != null)
            {
                if (Enum.TryParse<T>(value, out var enumValue))
                {
                    return enumValue;
                }
                else
                {
                    throw new ArgumentNullException(variableName, $"Unsupported value:  '{value}'");
                }
            }
            else
            {
                return null;
            }
        }
        #endregion

        #region Bool
        private static bool? GetBool(string variableName, bool mustExist)
        {
            var value = Environment.GetEnvironmentVariable(variableName);

            if (mustExist && value == null)
            {
                throw new ArgumentNullException(variableName, "Environment variable missing");
            }

            if (value != null)
            {
                if (bool.TryParse(value, out var boolValue))
                {
                    return boolValue;
                }
                else
                {
                    throw new ArgumentNullException(variableName, $"Unsupported value:  '{value}'");
                }
            }
            else
            {
                return null;
            }
        }
        #endregion

        #region Int
        private static int? GetInt(string variableName, bool mustExist)
        {
            var value = Environment.GetEnvironmentVariable(variableName);

            if (mustExist && value == null)
            {
                throw new ArgumentNullException(variableName, "Environment variable missing");
            }

            if (value != null)
            {
                if (int.TryParse(value, out var boolValue))
                {
                    return boolValue;
                }
                else
                {
                    throw new ArgumentNullException(variableName, $"Unsupported value:  '{value}'");
                }
            }
            else
            {
                return null;
            }
        }
        #endregion
        #endregion

        public void WriteOutSettings()
        {
            Console.WriteLine();
            Console.WriteLine($"AuthMode:  {AuthMode}");
            Console.WriteLine($"ManagedIdentityResourceId:  {ManagedIdentityResourceId}");
            Console.WriteLine($"ServiceBusQueueUrl:  {ServiceBusQueueUrl}");
            Console.WriteLine($"SourceBlob:  {SourceBlob}");
            Console.WriteLine($"DestinationBlobPrefix:  {DestinationBlobPrefix}");
            Console.WriteLine($"KustoIngestUri:  {KustoIngestUri}");
            Console.WriteLine($"KustoDb:  {KustoDb}");
            Console.WriteLine($"KustoTable:  {KustoTable}");
            BlobSettings.WriteOutSettings();
            Console.WriteLine();
            Console.WriteLine($"Core count:  {Environment.ProcessorCount}");
            Console.WriteLine();
        }
    }
}