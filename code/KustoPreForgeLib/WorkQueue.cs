using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KustoPreForgeLib
{
    internal class WorkQueue<T>
    {
        public int Size => throw new NotImplementedException();

        public void AddWorkItem(Task<T> task)
        {
            throw new NotImplementedException();
        }

        public async Task<T> WhenAnyAsync(Task task)
        {
            await Task.CompletedTask;

            throw new NotImplementedException();
        }
    }
}