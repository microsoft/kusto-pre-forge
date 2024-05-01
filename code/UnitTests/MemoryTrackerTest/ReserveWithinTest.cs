using KustoPreForgeLib.Memory;
using System.Threading.Tasks;
using Xunit.Sdk;

namespace UnitTests.MemoryTrackerTest
{
    public class ReserveWithinTest
    {
        [Fact]
        public async Task TrivialReservation()
        {
            var tracker = new MemoryTracker();
            var task = tracker.ReserveWithinAsync(new MemoryInterval(0, 10), 10);

            Assert.True(task.IsCompleted);

            var result = await task;
            
            Assert.Equal(10, result.Length);
            Assert.Equal(0, result.Offset);
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
        public async Task TrivialTwo()
        {
            var tracker = new MemoryTracker();
            var coreInterval = new MemoryInterval(0, 10);
            var task1 = tracker.ReserveWithinAsync(coreInterval, 5);
            var task2 = tracker.ReserveWithinAsync(coreInterval, 5);

            Assert.True(task1.IsCompleted);
            Assert.True(task2.IsCompleted);

            var result1 = await task1;
            var result2 = await task2;

            Assert.Equal(5, result1.Length);
            Assert.Equal(5, result2.Length);
            Assert.Equal(0, result1.Offset);
            Assert.Equal(5, result2.Offset);
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

            var result1 = await task1;
            var result2 = await task2;

            Assert.Equal(5, result1.Length);
            Assert.Equal(5, result2.Length);
            Assert.Equal(0, result1.Offset);
            Assert.Equal(5, result2.Offset);

            tracker.Release(result1);

            Assert.True(task3.IsCompleted);

            var result3 = await task3;

            Assert.Equal(result1, result3);
        }
    }
}