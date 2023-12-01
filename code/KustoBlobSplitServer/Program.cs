using KustoBlobSplitLib;
using KustoBlobSplitServiceBus;
using Microsoft.Extensions.Hosting;
using System.Reflection;

namespace KustoBlobSplitServer
{
    public class Program
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

        public static async Task Main(string[] args)
        {
            try
            {
                var builder = WebApplication.CreateBuilder(args);
                var app = builder.Build();

                Console.WriteLine($"Kusto pre-forge {AssemblyVersion}");

                // Configure the HTTP request pipeline.

                app.MapGet("/", (HttpContext httpContext) =>
                {
                    return string.Empty;
                });

                var webServerTask = app.RunAsync();
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

                //  Stop web server
                await Task.WhenAll(app.StopAsync(), webServerTask);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error:  {ex.Message}");
            }
        }
    }
}