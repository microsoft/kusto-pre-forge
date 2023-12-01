using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoPreForgeLib
{
    /// <summary>
    /// Exposes method to catch errors on background tasks so they do not fail silently and
    /// halt the process.
    /// </summary>
    internal static class TaskHelper
    {
        public static async Task<T> AwaitAsync<T>(
            Task<T> mainTask,
            params Task?[] backgroundTasks)
        {
            var realBackgroundTasks = backgroundTasks
                .Where(t => t != null)
                .Select(t => t!);

            await Task.WhenAny(realBackgroundTasks.Prepend(mainTask));

            //  Scan background tasks for faults
            foreach (var task in realBackgroundTasks)
            {
                if (task.IsFaulted)
                {   //  Observe exception
                    await task;
                }
            }

            return await mainTask;
        }

        public static async ValueTask<T> AwaitAsync<T>(
            ValueTask<T> mainTask,
            params Task?[] backgroundTasks)
        {
            return await AwaitAsync(mainTask.AsTask(), backgroundTasks);
        }
    }
}