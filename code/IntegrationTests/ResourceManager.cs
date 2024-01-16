using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntegrationTests
{
    internal class ResourceManager
    {
        private readonly int _capacity;

        public ResourceManager(int capacity)
        {
            _capacity = capacity;
        }

        internal Task PostResourceUtilizationAsync(Func<Task> resourceUtilizationFunc)
        {
            throw new NotImplementedException();
        }
    }
}