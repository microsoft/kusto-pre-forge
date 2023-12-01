using System.Diagnostics;
using System.Reflection;
using KustoBlobSplitLib;
using KustoBlobSplitServiceBus;

namespace KustoBlobSplitConsole
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

                runSettings.WriteOutSettings();
                if (string.IsNullOrWhiteSpace(runSettings.ServiceBusQueueUrl))
                {   //  Run one ETL
                    await EtlRun.RunEtlAsync(context);
                }
                else
                {   //  Run Service Bus server picking up tasks
                    await ServiceBusServer.RunServerAsync(
                        runSettings.ServiceBusQueueUrl,
                        context);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error:  {ex.Message}");
            }
        }
    }
}