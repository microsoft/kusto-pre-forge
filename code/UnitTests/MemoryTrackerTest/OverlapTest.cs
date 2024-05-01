using KustoPreForgeLib.Memory;

namespace UnitTests.MemoryTrackerTest
{
    public class OverlapTest
    {
        [Fact]
        public void CompletelyDisjoint()
        {
            var interval1 = new MemoryInterval(0, 10);
            var interval2 = new MemoryInterval(20, 10);

            Assert.False(interval1.HasOverlap(interval2));
        }

        [Fact]
        public void CloselyDisjoint()
        {
            var interval1 = new MemoryInterval(0, 5);
            var interval2 = new MemoryInterval(5, 5);

            Assert.False(interval1.HasOverlap(interval2));
            Assert.False(interval2.HasOverlap(interval1));
        }

        [Fact]
        public void Touching()
        {
            var interval1 = new MemoryInterval(0, 5);
            var interval2 = new MemoryInterval(4, 5);

            Assert.True(interval1.HasOverlap(interval2));
        }

        [Fact]
        public void Overlapping()
        {
            var interval1 = new MemoryInterval(0, 5);
            var interval2 = new MemoryInterval(2, 5);

            Assert.True(interval1.HasOverlap(interval2));
        }

        [Fact]
        public void Identical()
        {
            var interval1 = new MemoryInterval(0, 5);
            var interval2 = new MemoryInterval(0, 5);

            Assert.True(interval1.HasOverlap(interval2));
        }
    }
}