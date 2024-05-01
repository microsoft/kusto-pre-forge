using KustoPreForgeLib.Memory;

namespace UnitTests.MemoryTrackerTest
{
    public class ReserveWithinTest
    {
        [Fact]
        public void TrivialReservation()
        {
            var tracker = new MemoryTracker();
            var task = tracker.ReserveWithinAsync(new MemoryInterval(0, 10), 10);

            Assert.True(task.IsCompleted);
        }
    }
}