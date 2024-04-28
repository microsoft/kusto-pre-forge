using System.Diagnostics;
using System.Reflection;
using KustoPreForgeLib;
using KustoPreForgeLib.BlobEnumerables;

namespace KustoPreForgeConsole
{
    internal class Program
    {
        public static string AssemblyVersion
        {
            get
            {
                var versionAttribute = typeof(Program)
                    .Assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                var version = versionAttribute == null
                    ? "<VERSION MISSING>"
                    : versionAttribute!.InformationalVersion;

                return version;
            }
        }

        private static async Task Main(string[] args)
        {
            try
            {
                Console.WriteLine($"Kusto pre-forge {AssemblyVersion}");

                var runSettings = RunSettings.FromEnvironmentVariables();
                var context = await RunningContext.CreateAsync(runSettings);
                var blobEnumerables =
                    BlobEnumerableFactory.Create(context, runSettings.SourceSettings);

                runSettings.WriteOutSettings();
                if (string.IsNullOrWhiteSpace(runSettings.SourceSettings.ServiceBusQueueUrl))
                {   //  Run one ETL
                    await EtlRun.RunEtlAsync(runSettings.Action, blobEnumerables, context);
                }
                else
                {   //  Run Service Bus server picking up tasks
                    //await ServiceBusServer.RunServerAsync(
                    //    runSettings.SourceSettings.ServiceBusQueueUrl,
                    //    context);
                    throw new NotSupportedException();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error:  {ex.Message}");
            }
        }
    }
}