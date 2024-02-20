using KustoPreForgeLib;

namespace UnitTests
{
    public class MemoryTrackerTest
    {
        [Fact]
        public void AutomaticallyTracked()
        {
            var tracker = new MemoryTracker();
            var task = tracker.TrackAsync(0, 10);

            Assert.True(task.IsCompleted);
        }
    }
}