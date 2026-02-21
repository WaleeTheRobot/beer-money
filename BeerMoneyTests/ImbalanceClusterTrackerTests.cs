using Xunit;
using BeerMoney.Core.Analysis;

namespace BeerMoneyTests
{
    public class ImbalanceClusterTrackerTests
    {
        [Fact]
        public void AddBar_BullishImbalance_CreatesSupport()
        {
            var tracker = new ImbalanceClusterTracker(lookback: 10, bucketSize: 1.0);
            // Bar from 100 to 110, 5 bullish imbalances
            tracker.AddBar(high: 110, low: 100, bullCount: 5, bearCount: 0);

            // Bullish in lower half (100-105 range)
            int strength = tracker.GetBullStrength(102);
            Assert.True(strength > 0);
        }

        [Fact]
        public void AddBar_BearishImbalance_CreatesResistance()
        {
            var tracker = new ImbalanceClusterTracker(lookback: 10, bucketSize: 1.0);
            tracker.AddBar(high: 110, low: 100, bullCount: 0, bearCount: 5);

            // Bearish in upper half (105-110 range)
            int strength = tracker.GetBearStrength(108);
            Assert.True(strength > 0);
        }

        [Fact]
        public void GetBullStrength_FarFromZone_ReturnsZero()
        {
            var tracker = new ImbalanceClusterTracker(lookback: 10, bucketSize: 1.0);
            tracker.AddBar(high: 110, low: 100, bullCount: 5, bearCount: 0);

            // Far away from the 100-105 support zone
            int strength = tracker.GetBullStrength(200);
            Assert.Equal(0, strength);
        }

        [Fact]
        public void MultipleBars_AccumulateStrength()
        {
            var tracker = new ImbalanceClusterTracker(lookback: 10, bucketSize: 1.0);
            // Two bars with overlapping zones
            tracker.AddBar(high: 110, low: 100, bullCount: 3, bearCount: 0);
            tracker.AddBar(high: 111, low: 101, bullCount: 4, bearCount: 0);

            int strength = tracker.GetBullStrength(103);
            // Should be greater than just one bar's contribution
            Assert.True(strength > 3);
        }

        [Fact]
        public void RollingWindow_EvictsOldBars()
        {
            var tracker = new ImbalanceClusterTracker(lookback: 2, bucketSize: 1.0);
            tracker.AddBar(high: 110, low: 100, bullCount: 10, bearCount: 0);
            tracker.AddBar(high: 210, low: 200, bullCount: 1, bearCount: 0);
            tracker.AddBar(high: 310, low: 300, bullCount: 1, bearCount: 0);

            // First bar should be evicted — support at 100-105 gone
            int strength = tracker.GetBullStrength(102);
            Assert.Equal(0, strength);
        }

        [Fact]
        public void Reset_ClearsAllState()
        {
            var tracker = new ImbalanceClusterTracker(lookback: 10, bucketSize: 1.0);
            tracker.AddBar(high: 110, low: 100, bullCount: 5, bearCount: 5);
            tracker.Reset();

            Assert.Equal(0, tracker.GetBullStrength(102));
            Assert.Equal(0, tracker.GetBearStrength(108));
        }

        [Fact]
        public void ZeroRange_NoImbalancesCreated()
        {
            var tracker = new ImbalanceClusterTracker(lookback: 10, bucketSize: 1.0);
            tracker.AddBar(high: 100, low: 100, bullCount: 5, bearCount: 5);

            // Zero range bar should not create any zones
            Assert.Equal(0, tracker.GetBullStrength(100));
            Assert.Equal(0, tracker.GetBearStrength(100));
        }

        [Fact]
        public void FindNearestSupportDist_ReturnsNegativeDistance()
        {
            var tracker = new ImbalanceClusterTracker(lookback: 10, bucketSize: 1.0);
            tracker.AddBar(high: 110, low: 100, bullCount: 5, bearCount: 0);

            // Support is below 108 at around 102-103 area
            double dist = tracker.FindNearestSupportDist(108, threshold: 3);
            Assert.True(dist <= 0);
        }

        [Fact]
        public void FindNearestResistanceDist_ReturnsPositiveDistance()
        {
            var tracker = new ImbalanceClusterTracker(lookback: 10, bucketSize: 1.0);
            tracker.AddBar(high: 110, low: 100, bullCount: 0, bearCount: 5);

            // Resistance is above 102 at around 107-108 area
            double dist = tracker.FindNearestResistanceDist(102, threshold: 3);
            Assert.True(dist >= 0);
        }
    }
}
