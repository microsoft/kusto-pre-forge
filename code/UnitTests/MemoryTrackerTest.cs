using KustoPreForgeLib;

namespace UnitTests
{
    public class MemoryTrackerTest
    {
        [Fact]
        public void AutomaticallyTracked()
        {
            var tracker = new MemoryTracker();
            var task = tracker.ReserveAsync(0, 10);

            Assert.True(task.IsCompleted);
        }

        [Fact]
        public void ReserveThenRelease()
        {
            var tracker = new MemoryTracker();

            tracker.Reserve(0, 10);

            var task = tracker.ReserveAsync(0, 10);

            Assert.False(task.IsCompleted);
            tracker.Release(0, 10);
            Assert.True(task.IsCompleted);
        }

        [Fact]
        public void ReserveThenReleaseIn2Parts()
        {
            var tracker = new MemoryTracker();

            tracker.Reserve(0, 10);

            var task = tracker.ReserveAsync(0, 10);

            Assert.False(task.IsCompleted);
            tracker.Release(0, 5);
            Assert.False(task.IsCompleted);
            tracker.Release(5, 5);
            Assert.True(task.IsCompleted);
        }

        [Fact]
        public void ReserveThenReleaseIn3Parts()
        {
            var tracker = new MemoryTracker();

            tracker.Reserve(0, 10);

            var task = tracker.ReserveAsync(0, 10);

            Assert.False(task.IsCompleted);
            tracker.Release(0, 3);
            Assert.False(task.IsCompleted);
            tracker.Release(7, 3);
            Assert.False(task.IsCompleted);
            tracker.Release(3, 4);
            Assert.True(task.IsCompleted);
        }
    }
}