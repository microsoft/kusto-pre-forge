using System.Diagnostics;
using KustoBlobSplitLib;
using KustoBlobSplitServiceBus;

namespace KustoBlobSplitConsole
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            var runSettings = RunSettings.FromEnvironmentVariables();
            var context = await RunningContext.CreateAsync(runSettings);

            runSettings.WriteOutSettings();
            if (string.IsNullOrWhiteSpace(runSettings.ServiceBusQueueUrl))
            {   //  Run one ETL
                await EtlRun.RunEtlAsync(context);
            }
            else
            {   //  Run Service Bus server picking up tasks
                await ServiceBusServer.RunServerAsync(runSettings.ServiceBusQueueUrl, context);
            }
        }
    }
}