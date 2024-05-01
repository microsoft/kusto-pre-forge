using KustoPreForgeLib.Memory;
using Xunit.Sdk;

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

        [Fact]
        public async Task ExceedCapacity()
        {
            var tracker = new MemoryTracker();

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            {
                await tracker.ReserveWithinAsync(new MemoryInterval(0, 10), 15);
            });
        }

        [Fact]
        public void TrivialTwo()
        {
            var tracker = new MemoryTracker();
            var coreInterval = new MemoryInterval(0, 10);
            var task1 = tracker.ReserveWithinAsync(coreInterval, 5);
            var task2 = tracker.ReserveWithinAsync(coreInterval, 5);

            Assert.True(task1.IsCompleted);
            Assert.True(task2.IsCompleted);
        }

        [Fact]
        public async Task ReserveTwoWaitingOne()
        {
            var tracker = new MemoryTracker();
            var coreInterval = new MemoryInterval(0, 10);
            var task1 = tracker.ReserveWithinAsync(coreInterval, 5);
            var task2 = tracker.ReserveWithinAsync(coreInterval, 5);
            var task3 = tracker.ReserveWithinAsync(coreInterval, 5);

            Assert.True(task1.IsCompleted);
            Assert.True(task2.IsCompleted);
            Assert.False(task3.IsCompleted);

            var reservedInterval1 = await task1;

            Assert.True(task3.IsCompleted);
        }
    }
}