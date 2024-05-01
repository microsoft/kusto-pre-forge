using KustoPreForgeLib.Memory;

namespace UnitTests.MemoryTracker
{
    public class ReserveTest
    {
        [Fact]
        public void AutomaticallyTracked()
        {
            var tracker = new MemoryTracker();
            var task = tracker.ReserveAsync(new MemoryInterval(0, 10));

            Assert.True(task.IsCompleted);
        }

        [Fact]
        public void ReserveThenRelease()
        {
            var tracker = new MemoryTracker();

            tracker.Reserve(new MemoryInterval(0, 10));

            var task = tracker.ReserveAsync(new MemoryInterval(0, 10));

            Assert.False(task.IsCompleted);
            tracker.Release(new MemoryInterval(0, 10));
            Assert.True(task.IsCompleted);
        }

        [Fact]
        public void ReserveThenReleaseIn2Parts()
        {
            var tracker = new MemoryTracker();

            tracker.Reserve(new MemoryInterval(0, 10));

            var task = tracker.ReserveAsync(new MemoryInterval(0, 10));

            Assert.False(task.IsCompleted);
            tracker.Release(new MemoryInterval(0, 5));
            Assert.False(task.IsCompleted);
            tracker.Release(new MemoryInterval(5, 5));
            Assert.True(task.IsCompleted);
        }

        [Fact]
        public void ReserveThenReleaseIn3Parts()
        {
            var tracker = new MemoryTracker();

            tracker.Reserve(new MemoryInterval(0, 10));

            var task = tracker.ReserveAsync(new MemoryInterval(0, 10));

            Assert.False(task.IsCompleted);
            tracker.Release(new MemoryInterval(0, 3));
            Assert.False(task.IsCompleted);
            tracker.Release(new MemoryInterval(7, 3));
            Assert.False(task.IsCompleted);
            tracker.Release(new MemoryInterval(3, 4));
            Assert.True(task.IsCompleted);
        }
    }
}