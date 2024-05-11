using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoPreForgeLib
{
    public class PerfCounterJournal
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
            var counterlist = new List<Reading>();

            while (_readingsQueue.TryDequeue(out var item))
            {
                counterlist.Add(item);
            }

            var counters = counterlist
                .GroupBy(i => i.perfCounterName)
                .Select(g => new
                {
                    Name = g.Key,
                    SumValue = g.Sum(i => i.value)
                })
                .OrderBy(i => i.Name);

            foreach (var perfCounter in counters)
            {
                Console.WriteLine($"{perfCounter.Name}:  {perfCounter.SumValue:#,##0}");
            }
            Console.WriteLine();
        }
    }
}