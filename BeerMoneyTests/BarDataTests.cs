using System;
using Xunit;
using BeerMoney.Core.Models;

namespace BeerMoneyTests
{
    public class BarDataTests
    {
        private static BarData MakeBar(
            double open, double high, double low, double close,
            long buyVolume = 0, long sellVolume = 0,
            ImbalanceMetrics imbalance = default)
        {
            return new BarData(
                DateTime.UtcNow, 0, open, high, low, close,
                buyVolume + sellVolume, buyVolume, sellVolume,
                imbalance: imbalance);
        }

        [Fact]
        public void Range_ReturnsHighMinusLow()
        {
            var bar = MakeBar(100, 105, 95, 102);
            Assert.Equal(10, bar.Range);
        }

        [Fact]
        public void Range_ZeroWhenHighEqualsLow()
        {
            var bar = MakeBar(100, 100, 100, 100);
            Assert.Equal(0, bar.Range);
        }

        [Fact]
        public void IsBullish_WhenCloseAboveOpen()
        {
            var bar = MakeBar(100, 105, 95, 103);
            Assert.True(bar.IsBullish);
            Assert.False(bar.IsBearish);
        }

        [Fact]
        public void IsBearish_WhenCloseBelowOpen()
        {
            var bar = MakeBar(100, 105, 95, 97);
            Assert.False(bar.IsBullish);
            Assert.True(bar.IsBearish);
        }

        [Fact]
        public void Doji_NeitherBullishNorBearish()
        {
            var bar = MakeBar(100, 105, 95, 100);
            Assert.False(bar.IsBullish);
            Assert.False(bar.IsBearish);
        }

        [Fact]
        public void ClosePosition_ClosedAtHigh_ReturnsOne()
        {
            var bar = MakeBar(100, 110, 90, 110);
            Assert.Equal(1.0, bar.ClosePosition);
        }

        [Fact]
        public void ClosePosition_ClosedAtLow_ReturnsZero()
        {
            var bar = MakeBar(100, 110, 90, 90);
            Assert.Equal(0.0, bar.ClosePosition);
        }

        [Fact]
        public void ClosePosition_ZeroRange_ReturnsHalf()
        {
            var bar = MakeBar(100, 100, 100, 100);
            Assert.Equal(0.5, bar.ClosePosition);
        }

        [Fact]
        public void OpenPosition_ZeroRange_ReturnsHalf()
        {
            var bar = MakeBar(100, 100, 100, 100);
            Assert.Equal(0.5, bar.OpenPosition);
        }

        [Fact]
        public void Delta_ReturnsBuyMinusSell()
        {
            var bar = MakeBar(100, 105, 95, 102, buyVolume: 300, sellVolume: 200);
            Assert.Equal(100, bar.Delta);
        }

        [Fact]
        public void DeltaBias_PositiveDelta_ReturnsOne()
        {
            var bar = MakeBar(100, 105, 95, 102, buyVolume: 300, sellVolume: 200);
            Assert.Equal(1, bar.DeltaBias);
        }

        [Fact]
        public void DeltaBias_NegativeDelta_ReturnsNegativeOne()
        {
            var bar = MakeBar(100, 105, 95, 102, buyVolume: 100, sellVolume: 200);
            Assert.Equal(-1, bar.DeltaBias);
        }

        [Fact]
        public void DeltaBias_ZeroDelta_ReturnsZero()
        {
            var bar = MakeBar(100, 105, 95, 102, buyVolume: 150, sellVolume: 150);
            Assert.Equal(0, bar.DeltaBias);
        }

        [Fact]
        public void BarScore_PositiveDelta_EqualsStructuralAlignment()
        {
            // Close at high, POC at 0 -> ClosePos=1.0, PocPos=0 -> SA=0.5
            var bar = MakeBar(100, 110, 90, 110, buyVolume: 300, sellVolume: 100);
            Assert.Equal(bar.StructuralAlignment, bar.BarScore);
        }

        [Fact]
        public void BarScore_NegativeDelta_EqualsAlignmentMinusOne()
        {
            var bar = MakeBar(100, 110, 90, 110, buyVolume: 100, sellVolume: 300);
            Assert.Equal(bar.StructuralAlignment - 1.0, bar.BarScore);
        }

        [Fact]
        public void BarScore_ZeroDelta_ReturnsZero()
        {
            var bar = MakeBar(100, 110, 90, 105, buyVolume: 200, sellVolume: 200);
            Assert.Equal(0, bar.BarScore);
        }

        [Fact]
        public void BodyPercent_FullBody_ReturnsOne()
        {
            var bar = MakeBar(90, 110, 90, 110);
            Assert.Equal(1.0, bar.BodyPercent);
        }

        [Fact]
        public void BodyPercent_Doji_ReturnsZero()
        {
            var bar = MakeBar(100, 110, 90, 100);
            Assert.Equal(0.0, bar.BodyPercent);
        }

        [Fact]
        public void BodyPercent_ZeroRange_ReturnsZero()
        {
            var bar = MakeBar(100, 100, 100, 100);
            Assert.Equal(0.0, bar.BodyPercent);
        }

        [Fact]
        public void IsDivergent_BuyingInBearishBar()
        {
            // Bearish bar (close < open) with positive delta
            var bar = MakeBar(100, 105, 95, 97, buyVolume: 300, sellVolume: 100);
            Assert.True(bar.IsDivergent);
        }

        [Fact]
        public void IsDivergent_SellingInBullishBar()
        {
            // Bullish bar (close > open) with negative delta
            var bar = MakeBar(100, 105, 95, 103, buyVolume: 100, sellVolume: 300);
            Assert.True(bar.IsDivergent);
        }

        [Fact]
        public void IsDivergent_AlignedBar_ReturnsFalse()
        {
            // Bullish bar with positive delta — aligned
            var bar = MakeBar(100, 105, 95, 103, buyVolume: 300, sellVolume: 100);
            Assert.False(bar.IsDivergent);
        }

        [Fact]
        public void TypicalPrice_Calculated()
        {
            var bar = MakeBar(100, 110, 90, 105);
            Assert.Equal((110 + 90 + 105) / 3.0, bar.TypicalPrice);
        }

        [Fact]
        public void ImbalanceMetrics_PassedThrough()
        {
            var imb = new ImbalanceMetrics
            {
                BullishCount = 5,
                BearishCount = 3,
                BullishVolumeSum = 1000,
                BearishVolumeSum = 600,
                BullishAvgPosition = 0.3,
                BearishAvgPosition = -0.2,
                MaxBullishPrice = 101.5,
                MaxBullishVolume = 400,
                MaxBearishPrice = 99.0,
                MaxBearishVolume = 250
            };
            var bar = MakeBar(100, 105, 95, 102, imbalance: imb);

            Assert.Equal(5, bar.BullishImbalanceCount);
            Assert.Equal(3, bar.BearishImbalanceCount);
            Assert.Equal(1000, bar.BullishImbVolumeSum);
            Assert.Equal(600, bar.BearishImbVolumeSum);
            Assert.Equal(0.3, bar.BullishImbalanceAvgPosition);
            Assert.Equal(-0.2, bar.BearishImbalanceAvgPosition);
            Assert.Equal(101.5, bar.MaxBullishImbPrice);
            Assert.Equal(400, bar.MaxBullishImbVolume);
            Assert.Equal(99.0, bar.MaxBearishImbPrice);
            Assert.Equal(250, bar.MaxBearishImbVolume);
        }
    }
}
