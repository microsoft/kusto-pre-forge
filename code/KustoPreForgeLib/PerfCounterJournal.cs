using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoPreForgeLib
{
    internal class PerfCounterJournal
    {
        #region Inner Types
        private record Reading(string perfCounterName, long value);
        #endregion

        private static readonly TimeSpan DELAY = TimeSpan.FromSeconds(5);

        private readonly ConcurrentQueue<Reading> _readingsQueue = new();
        private bool _reportRunning = false;
        private Task _reportingTask = Task.CompletedTask;

        public void StartReporting()
        {
            _reportRunning = true;
            _reportingTask = ReportAsync();
        }

        public async Task StopReportingAsync()
        {
            _reportRunning = false;
            await _reportingTask;
        }

        public void AddReading(string perfCounterName, long value)
        {
            _readingsQueue.Enqueue(new Reading(perfCounterName, value));
        }

        private async Task ReportAsync()
        {
            while (_reportRunning)
            {
                await Task.Delay(DELAY);
                ReportCounters();
            }
        }

        private void ReportCounters()
        {
            var counterMap = new Dictionary<string, long>();

            while (_readingsQueue.TryDequeue(out var item))
            {
                if (counterMap.TryGetValue(item.perfCounterName, out var currentValue))
                {
                    counterMap[item.perfCounterName] = currentValue + item.value;
                }
                else
                {
                    counterMap[item.perfCounterName] = item.value;
                }
            }

            foreach (var perfCounter in counterMap.OrderBy(p => p.Key))
            {
                Console.WriteLine($"{perfCounter.Key}:  {perfCounter.Value}");
            }
            Console.WriteLine();
        }
    }
}