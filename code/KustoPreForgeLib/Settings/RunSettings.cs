using Kusto.Data.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoPreForgeLib.Settings
{
    public class RunSettings
    {
        #region Properties
        public EtlAction Action { get; }

        public AuthSettings AuthSettings { get; }

        public SourceSettings SourceSettings { get; }

        public KustoSettings? KustoSettings { get; }

        public Uri? DestinationBlobPrefix { get; }

        public BlobSettings BlobSettings { get; }
        #endregion

        #region Constructors
        public static RunSettings FromEnvironmentVariables()
        {
            var eltAction = GetEnum<EtlAction>("EtlAction", false);
            var authMode = GetEnum<AuthMode>("AuthMode", false);
            var managedIdentityResourceId = GetString("ManagedIdentityResourceId", false);
            var tenantId = GetString("TenantId", false);
            var applicationId = GetString("ApplicationId", false);
            var secret = GetString("Secret", false);
            var serviceBusQueueUrl = GetString("ServiceBusQueueUrl", false);
            var sourceBlob = GetUri("SourceBlob", false);
            var sourceBlobsPrefix = GetUri("SourceBlobsPrefix", false);
            var sourceBlobsSuffix = GetString("SourceBlobsSuffix", false);
            var destinationBlobPrefix = GetUri("DestinationBlobPrefix", false);
            var kustoIngestUri = GetUri("KustoIngestUri", false);
            var kustoDb = GetString("KustoDb", false);
            var kustoTable = GetString("KustoTable", false);
            var format = GetEnum<DataSourceFormat>("Format", false);
            var inputCompression = GetEnum<DataSourceCompressionType>("InputCompression", false);
            var outputCompression = GetEnum<DataSourceCompressionType>("OutputCompression", false);
            var hasHeaders = GetBool("CsvHeaders", false);
            var maxMbPerShard = GetInt("MaxMbPerShard", false);
            var tempDirectory = GetString("TempDirectory", false);

            return new RunSettings(
                eltAction,
                new AuthSettings(
                    authMode,
                    managedIdentityResourceId,
                    tenantId,
                    applicationId,
                    secret),
                new SourceSettings(
                    serviceBusQueueUrl,
                    sourceBlob,
                    sourceBlobsPrefix,
                    sourceBlobsSuffix),
                kustoIngestUri,
                kustoDb,
                kustoTable,
                destinationBlobPrefix,
                new BlobSettings(
                    format,
                    inputCompression,
                    outputCompression,
                    hasHeaders,
                    maxMbPerShard),
                tempDirectory);
        }

        public RunSettings(
            EtlAction? action,
            AuthSettings authSettings,
            SourceSettings sourceSettings,
            Uri? kustoIngestUri,
            string? kustoDb,
            string? kustoTable,
            Uri? destinationBlobPrefix,
            BlobSettings blobSettings,
            string? tempDirectory)
        {
            if (kustoIngestUri != null)
            {
                if (kustoDb == null || kustoTable == null)
                {
                    throw new ArgumentNullException(nameof(kustoDb));
                }
            }
            else if (destinationBlobPrefix == null)
            {
                throw new ArgumentNullException(
                    nameof(destinationBlobPrefix),
                    "No destination specified");
            }

            Action = action ?? EtlAction.Split;
            AuthSettings = authSettings;
            SourceSettings = sourceSettings;
            KustoSettings = kustoIngestUri == null
                ? null
                : new KustoSettings(
                    kustoIngestUri,
                    kustoDb!,
                    kustoTable!,
                    tempDirectory ?? Path.GetTempPath());
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
                if (Enum.TryParse<T>(value, true, out var enumValue))
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
            AuthSettings.WriteOutSettings();
            SourceSettings.WriteOutSettings();
            Console.WriteLine($"DestinationBlobPrefix:  {DestinationBlobPrefix}");
            KustoSettings?.WriteOutSettings();
            BlobSettings.WriteOutSettings();
            Console.WriteLine();
            Console.WriteLine($"Core count:  {Environment.ProcessorCount}");
            Console.WriteLine();
        }
    }
}