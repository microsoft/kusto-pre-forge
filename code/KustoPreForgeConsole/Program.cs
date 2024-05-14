using System.Diagnostics;
using System.Reflection;
using KustoPreForgeLib;
using KustoPreForgeLib.BlobSources;
using KustoPreForgeLib.Settings;

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
                var journal = new PerfCounterJournal();
                var blobSource = BlobSourceFactory.Create(
                    context,
                    runSettings.SourceSettings,
                    journal);

                journal.StartReporting();
                runSettings.WriteOutSettings();
                //  Run one ETL
                await EtlRun.RunEtlAsync(runSettings, blobSource, context, journal);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error:  {ex.Message}");
            }
        }
    }
}