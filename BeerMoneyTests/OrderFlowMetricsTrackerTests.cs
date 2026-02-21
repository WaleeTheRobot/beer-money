using System;
using Xunit;
using BeerMoney.Core.Analysis;
using BeerMoney.Core.Models;

namespace BeerMoneyTests
{
    public class OrderFlowMetricsTrackerTests
    {
        private static BarData MakeBar(
            double open, double high, double low, double close,
            long buyVolume = 100, long sellVolume = 100,
            double poc = 0, ImbalanceMetrics imbalance = default)
        {
            if (poc == 0) poc = (high + low) / 2.0;
            return new BarData(
                DateTime.UtcNow, 0, open, high, low, close,
                buyVolume + sellVolume, buyVolume, sellVolume,
                pointOfControl: poc, imbalance: imbalance);
        }

        #region Invalid State

        [Fact]
        public void ProcessBar_LessThanTwoBars_ReturnsInvalid()
        {
            var tracker = new OrderFlowMetricsTracker(20);
            var bar = MakeBar(100, 105, 95, 102);
            var result = tracker.ProcessBar(bar, 103, 97, 100, 5.0);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ProcessBar_ZeroAtr_ReturnsInvalid()
        {
            var tracker = new OrderFlowMetricsTracker(20);
            tracker.ProcessBar(MakeBar(100, 105, 95, 102), 103, 97, 100, 5.0);
            var result = tracker.ProcessBar(MakeBar(101, 106, 96, 103), 104, 98, 101, 0);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ProcessBar_TwoBars_ReturnsValid()
        {
            var tracker = new OrderFlowMetricsTracker(20);
            tracker.ProcessBar(MakeBar(100, 105, 95, 102), 103, 97, 100, 5.0);
            var result = tracker.ProcessBar(MakeBar(101, 106, 96, 103), 104, 98, 101, 5.0);
            Assert.True(result.IsValid);
        }

        #endregion

        #region POC Migration

        [Fact]
        public void PocMigration_RisingPoc_PositiveValue()
        {
            var tracker = new OrderFlowMetricsTracker(20);
            tracker.ProcessBar(MakeBar(100, 105, 95, 102, poc: 100), 103, 97, 100, 5.0);
            var result = tracker.ProcessBar(MakeBar(101, 106, 96, 103, poc: 103), 104, 98, 101, 5.0);

            Assert.True(result.PocMigration > 0);
            Assert.Equal(1, result.PocDirection);
        }

        [Fact]
        public void PocMigration_FallingPoc_NegativeValue()
        {
            var tracker = new OrderFlowMetricsTracker(20);
            tracker.ProcessBar(MakeBar(100, 105, 95, 102, poc: 103), 103, 97, 100, 5.0);
            var result = tracker.ProcessBar(MakeBar(99, 104, 94, 101, poc: 100), 102, 96, 99, 5.0);

            Assert.True(result.PocMigration < 0);
            Assert.Equal(-1, result.PocDirection);
        }

        [Fact]
        public void PocMigration_FlatPoc_DirectionZero()
        {
            var tracker = new OrderFlowMetricsTracker(20);
            tracker.ProcessBar(MakeBar(100, 105, 95, 102, poc: 100), 103, 97, 100, 5.0);
            var result = tracker.ProcessBar(MakeBar(100, 105, 95, 102, poc: 100), 103, 97, 100, 5.0);

            Assert.Equal(0, result.PocDirection);
        }

        #endregion

        #region POC Trend Strength

        [Fact]
        public void PocTrendStrength_ConsistentRising_NearOne()
        {
            var tracker = new OrderFlowMetricsTracker(20);
            for (int i = 0; i < 10; i++)
            {
                double poc = 100 + i * 2;
                tracker.ProcessBar(MakeBar(100 + i, 105 + i, 95 + i, 102 + i, poc: poc),
                    103 + i, 97 + i, 100 + i, 5.0);
            }

            Assert.True(tracker.CurrentMetrics.PocTrendStrength >= 0.8);
        }

        [Fact]
        public void PocTrendStrength_Alternating_NearZero()
        {
            var tracker = new OrderFlowMetricsTracker(20);
            for (int i = 0; i < 10; i++)
            {
                double poc = 100 + (i % 2 == 0 ? 5 : -5);
                tracker.ProcessBar(MakeBar(100, 105, 95, 102, poc: poc), 103, 97, 100, 5.0);
            }

            Assert.True(tracker.CurrentMetrics.PocTrendStrength <= 0.2);
        }

        #endregion

        #region Value Area Overlap

        [Fact]
        public void VaOverlap_IdenticalVAs_ReturnsOne()
        {
            var tracker = new OrderFlowMetricsTracker(20);
            tracker.ProcessBar(MakeBar(100, 105, 95, 102), 104, 96, 100, 5.0);
            var result = tracker.ProcessBar(MakeBar(100, 105, 95, 102), 104, 96, 100, 5.0);

            Assert.Equal(1.0, result.VaOverlap, 4);
        }

        [Fact]
        public void VaOverlap_NoOverlap_ReturnsZero()
        {
            var tracker = new OrderFlowMetricsTracker(20);
            tracker.ProcessBar(MakeBar(100, 105, 95, 102), 104, 96, 100, 5.0);
            var result = tracker.ProcessBar(MakeBar(110, 115, 105, 112), 115, 105, 110, 5.0);

            Assert.Equal(0.0, result.VaOverlap, 4);
        }

        [Fact]
        public void VaOverlap_PartialOverlap_BetweenZeroAndOne()
        {
            var tracker = new OrderFlowMetricsTracker(20);
            tracker.ProcessBar(MakeBar(100, 105, 95, 102), 104, 96, 100, 5.0);
            // Partial overlap: new VA is 100-108, prior was 96-104
            var result = tracker.ProcessBar(MakeBar(102, 110, 98, 106), 108, 100, 104, 5.0);

            Assert.True(result.VaOverlap > 0);
            Assert.True(result.VaOverlap < 1);
        }

        #endregion

        #region VA Migration

        [Fact]
        public void VaMigration_RisingVA_ReturnsPositive()
        {
            var tracker = new OrderFlowMetricsTracker(20);
            tracker.ProcessBar(MakeBar(100, 105, 95, 102), 104, 96, 100, 5.0);
            var result = tracker.ProcessBar(MakeBar(105, 110, 100, 107), 109, 101, 105, 5.0);

            Assert.Equal(1, result.VaMigration);
        }

        [Fact]
        public void VaMigration_FallingVA_ReturnsNegative()
        {
            var tracker = new OrderFlowMetricsTracker(20);
            tracker.ProcessBar(MakeBar(105, 110, 100, 107), 109, 101, 105, 5.0);
            var result = tracker.ProcessBar(MakeBar(100, 105, 95, 102), 104, 96, 100, 5.0);

            Assert.Equal(-1, result.VaMigration);
        }

        #endregion

        #region VA Compression

        [Fact]
        public void VaCompression_DecreasingWidths_IsCompressingTrue()
        {
            var tracker = new OrderFlowMetricsTracker(20);
            double atr = 10.0;

            for (int i = 0; i < 5; i++)
            {
                double halfWidth = 5 - i * 0.8;
                double vah = 100 + halfWidth;
                double val = 100 - halfWidth;
                tracker.ProcessBar(MakeBar(99, 101, 98, 100), vah, val, 100, atr);
            }

            Assert.True(tracker.CurrentMetrics.IsCompressing);
            Assert.True(tracker.CurrentMetrics.CompressionRate < 0);
        }

        [Fact]
        public void VaCompression_IncreasingWidths_IsCompressingFalse()
        {
            var tracker = new OrderFlowMetricsTracker(20);
            double atr = 10.0;

            for (int i = 0; i < 5; i++)
            {
                double halfWidth = 2 + i * 0.8;
                double vah = 100 + halfWidth;
                double val = 100 - halfWidth;
                tracker.ProcessBar(MakeBar(99, 101, 98, 100), vah, val, 100, atr);
            }

            Assert.False(tracker.CurrentMetrics.IsCompressing);
            Assert.True(tracker.CurrentMetrics.CompressionRate > 0);
        }

        #endregion

        #region Imbalance Polarity

        [Fact]
        public void ImbalancePolarity_AllBullish_ReturnsOne()
        {
            var tracker = new OrderFlowMetricsTracker(20);
            var imb = new ImbalanceMetrics { BullishCount = 5, BearishCount = 0 };

            tracker.ProcessBar(MakeBar(100, 105, 95, 102, imbalance: imb), 103, 97, 100, 5.0);
            var result = tracker.ProcessBar(MakeBar(101, 106, 96, 103, imbalance: imb), 104, 98, 101, 5.0);

            Assert.Equal(1.0, result.ImbalancePolarity, 4);
            Assert.True(result.IsPolarized);
        }

        [Fact]
        public void ImbalancePolarity_Equal_ReturnsZero()
        {
            var tracker = new OrderFlowMetricsTracker(20);
            var imb = new ImbalanceMetrics { BullishCount = 3, BearishCount = 3 };

            tracker.ProcessBar(MakeBar(100, 105, 95, 102, imbalance: imb), 103, 97, 100, 5.0);
            var result = tracker.ProcessBar(MakeBar(101, 106, 96, 103, imbalance: imb), 104, 98, 101, 5.0);

            Assert.Equal(0.0, result.ImbalancePolarity, 4);
            Assert.False(result.IsPolarized);
        }

        [Fact]
        public void ImbalancePolarity_NoImbalances_ReturnsZero()
        {
            var tracker = new OrderFlowMetricsTracker(20);
            var imb = new ImbalanceMetrics { BullishCount = 0, BearishCount = 0 };

            tracker.ProcessBar(MakeBar(100, 105, 95, 102, imbalance: imb), 103, 97, 100, 5.0);
            var result = tracker.ProcessBar(MakeBar(101, 106, 96, 103, imbalance: imb), 104, 98, 101, 5.0);

            Assert.Equal(0.0, result.ImbalancePolarity, 4);
        }

        #endregion

        #region Setup Density

        [Fact]
        public void SetupDensity_HighImbalanceCounts_HighDensity()
        {
            var tracker = new OrderFlowMetricsTracker(20);
            var imb = new ImbalanceMetrics { BullishCount = 10, BearishCount = 5 };

            tracker.ProcessBar(MakeBar(100, 105, 95, 102, imbalance: imb), 103, 97, 100, 5.0);
            var result = tracker.ProcessBar(MakeBar(101, 106, 96, 103, imbalance: imb), 104, 98, 101, 5.0);

            Assert.Equal(15.0, result.SetupDensity, 4);
        }

        #endregion

        #region VWAP

        [Fact]
        public void VwapSlope_RisingVwap_PositiveSlope()
        {
            var tracker = new OrderFlowMetricsTracker(20);
            tracker.ProcessBar(MakeBar(100, 105, 95, 102), 103, 97, 100, 5.0);
            var result = tracker.ProcessBar(MakeBar(102, 107, 97, 104), 105, 99, 104, 5.0);

            Assert.True(result.VwapSlope > 0);
            Assert.Equal(1, result.VwapRegime);
        }

        [Fact]
        public void VwapSlope_FallingVwap_NegativeSlope()
        {
            var tracker = new OrderFlowMetricsTracker(20);
            tracker.ProcessBar(MakeBar(105, 110, 100, 107), 108, 102, 106, 5.0);
            var result = tracker.ProcessBar(MakeBar(102, 107, 97, 104), 105, 99, 100, 5.0);

            Assert.True(result.VwapSlope < 0);
            Assert.Equal(-1, result.VwapRegime);
        }

        #endregion

        #region Rolling Delta

        [Fact]
        public void RollingDelta_AllPositive_PositiveValue()
        {
            var tracker = new OrderFlowMetricsTracker(20);
            tracker.ProcessBar(MakeBar(100, 105, 95, 102, buyVolume: 300, sellVolume: 100), 103, 97, 100, 5.0);
            var result = tracker.ProcessBar(MakeBar(101, 106, 96, 103, buyVolume: 400, sellVolume: 150), 104, 98, 101, 5.0);

            Assert.True(result.RollingDelta > 0);
            Assert.Equal(1, result.RollingDeltaDirection);
        }

        [Fact]
        public void RollingDelta_AllNegative_NegativeValue()
        {
            var tracker = new OrderFlowMetricsTracker(20);
            tracker.ProcessBar(MakeBar(100, 105, 95, 98, buyVolume: 100, sellVolume: 300), 103, 97, 100, 5.0);
            var result = tracker.ProcessBar(MakeBar(99, 104, 94, 97, buyVolume: 150, sellVolume: 400), 102, 96, 99, 5.0);

            Assert.True(result.RollingDelta < 0);
            Assert.Equal(-1, result.RollingDeltaDirection);
        }

        [Fact]
        public void RollingDeltaMomentum_Increasing_Positive()
        {
            var tracker = new OrderFlowMetricsTracker(20);
            // First two bars establish initial rolling delta
            tracker.ProcessBar(MakeBar(100, 105, 95, 102, buyVolume: 200, sellVolume: 100), 103, 97, 100, 5.0);
            tracker.ProcessBar(MakeBar(101, 106, 96, 103, buyVolume: 200, sellVolume: 100), 104, 98, 101, 5.0);
            // Third bar increases delta more
            var result = tracker.ProcessBar(MakeBar(102, 107, 97, 104, buyVolume: 500, sellVolume: 100), 105, 99, 102, 5.0);

            Assert.True(result.RollingDeltaMomentum > 0);
        }

        #endregion

        #region Volume Trend

        [Fact]
        public void VolumeTrend_IncreasingVolumes_Positive()
        {
            var tracker = new OrderFlowMetricsTracker(20);
            for (int i = 0; i < 5; i++)
            {
                long vol = (100 + i * 50);
                long half = vol / 2;
                tracker.ProcessBar(MakeBar(100, 105, 95, 102, buyVolume: half, sellVolume: vol - half),
                    103, 97, 100, 5.0);
            }

            Assert.True(tracker.CurrentMetrics.VolumeTrend > 0);
        }

        [Fact]
        public void VolumeTrend_DecreasingVolumes_Negative()
        {
            var tracker = new OrderFlowMetricsTracker(20);
            for (int i = 0; i < 5; i++)
            {
                long vol = (500 - i * 80);
                long half = vol / 2;
                tracker.ProcessBar(MakeBar(100, 105, 95, 102, buyVolume: half, sellVolume: vol - half),
                    103, 97, 100, 5.0);
            }

            Assert.True(tracker.CurrentMetrics.VolumeTrend < 0);
        }

        #endregion

        #region POC-VWAP Agreement

        [Fact]
        public void PocVwapAgreement_BothBullish_True()
        {
            var tracker = new OrderFlowMetricsTracker(20);
            // Rising POC and rising VWAP
            tracker.ProcessBar(MakeBar(100, 105, 95, 102, poc: 100), 103, 97, 100, 5.0);
            var result = tracker.ProcessBar(MakeBar(102, 107, 97, 104, poc: 104), 105, 99, 104, 5.0);

            Assert.True(result.PocVwapAgreement);
        }

        [Fact]
        public void PocVwapAgreement_Disagreement_False()
        {
            var tracker = new OrderFlowMetricsTracker(20);
            // Rising POC but falling VWAP
            tracker.ProcessBar(MakeBar(100, 105, 95, 102, poc: 100), 103, 97, 104, 5.0);
            var result = tracker.ProcessBar(MakeBar(102, 107, 97, 104, poc: 104), 105, 99, 100, 5.0);

            Assert.False(result.PocVwapAgreement);
        }

        #endregion

        #region Conviction

        [Fact]
        public void Conviction_AllBullishFactors_ScoreSix()
        {
            var tracker = new OrderFlowMetricsTracker(20);
            var imb = new ImbalanceMetrics { BullishCount = 8, BearishCount = 0 };

            // Feed bars that are strongly bullish in every dimension
            for (int i = 0; i < 5; i++)
            {
                double price = 100 + i * 3;
                tracker.ProcessBar(
                    MakeBar(price, price + 5, price - 2, price + 4,
                        buyVolume: 300 + i * 50, sellVolume: 100,
                        poc: price + 2, imbalance: imb),
                    price + 4, price - 1, price + 1, 5.0);
            }

            Assert.Equal(6, tracker.CurrentMetrics.ConvictionScore);
            Assert.Equal(1, tracker.CurrentMetrics.ConvictionDirection);
        }

        [Fact]
        public void Conviction_MixedSignals_LowerScore()
        {
            var tracker = new OrderFlowMetricsTracker(20);
            var imb = new ImbalanceMetrics { BullishCount = 3, BearishCount = 3 };

            // Bar 1: bullish
            tracker.ProcessBar(MakeBar(100, 105, 95, 103, buyVolume: 200, sellVolume: 100, poc: 100, imbalance: imb),
                104, 96, 100, 5.0);
            // Bar 2: bearish
            var result = tracker.ProcessBar(
                MakeBar(103, 108, 98, 99, buyVolume: 100, sellVolume: 200, poc: 99, imbalance: imb),
                106, 100, 99, 5.0);

            Assert.True(result.ConvictionScore < 6);
        }

        #endregion

        #region Single-Bar Flags

        [Fact]
        public void IsVolumeSkew_PocAtTop_WithBullishVolume_True()
        {
            var imb = new ImbalanceMetrics
            {
                BullishVolumeSum = 500,
                BearishVolumeSum = 100
            };
            // POC at 109 in range 100-110 → PocPosition = 0.9 (top 20%)
            var bar = MakeBar(100, 110, 100, 108, poc: 109, imbalance: imb);
            Assert.True(OrderFlowMetricsTracker.IsVolumeSkew(bar));
        }

        [Fact]
        public void IsVolumeSkew_PocAtBottom_WithBearishVolume_True()
        {
            var imb = new ImbalanceMetrics
            {
                BullishVolumeSum = 100,
                BearishVolumeSum = 500
            };
            // POC at 101 in range 100-110 → PocPosition = 0.1 (bottom 20%)
            var bar = MakeBar(108, 110, 100, 102, poc: 101, imbalance: imb);
            Assert.True(OrderFlowMetricsTracker.IsVolumeSkew(bar));
        }

        [Fact]
        public void IsVolumeSkew_PocInMiddle_False()
        {
            var imb = new ImbalanceMetrics
            {
                BullishVolumeSum = 500,
                BearishVolumeSum = 100
            };
            var bar = MakeBar(100, 110, 100, 105, poc: 105, imbalance: imb);
            Assert.False(OrderFlowMetricsTracker.IsVolumeSkew(bar));
        }

        [Fact]
        public void IsVolumeSkew_ZeroRange_False()
        {
            var bar = MakeBar(100, 100, 100, 100);
            Assert.False(OrderFlowMetricsTracker.IsVolumeSkew(bar));
        }

        [Fact]
        public void IsDivergenceConfirmed_BearishDivergenceWithImbalances_True()
        {
            var imb = new ImbalanceMetrics
            {
                BullishCount = 3,
                BullishAvgPosition = -0.6  // Bullish imbalances at the low (bottom)
            };
            // Bearish bar with positive delta (divergent), bullish imbalances stacked at low
            var bar = MakeBar(105, 110, 100, 101, buyVolume: 300, sellVolume: 100,
                imbalance: imb);
            Assert.True(OrderFlowMetricsTracker.IsDivergenceConfirmed(bar));
        }

        [Fact]
        public void IsDivergenceConfirmed_BullishDivergenceWithImbalances_True()
        {
            var imb = new ImbalanceMetrics
            {
                BearishCount = 3,
                BearishAvgPosition = 0.6  // Bearish imbalances at the high (top)
            };
            // Bullish bar with negative delta (divergent), bearish imbalances stacked at high
            var bar = MakeBar(100, 110, 95, 108, buyVolume: 100, sellVolume: 300,
                imbalance: imb);
            Assert.True(OrderFlowMetricsTracker.IsDivergenceConfirmed(bar));
        }

        [Fact]
        public void IsDivergenceConfirmed_NotDivergent_False()
        {
            // Bullish bar with positive delta — not divergent
            var bar = MakeBar(100, 110, 95, 108, buyVolume: 300, sellVolume: 100);
            Assert.False(OrderFlowMetricsTracker.IsDivergenceConfirmed(bar));
        }

        [Fact]
        public void IsDivergenceConfirmed_DivergentButNoImbalances_False()
        {
            // Bullish bar with negative delta (divergent), but no bearish imbalances
            var bar = MakeBar(100, 110, 95, 108, buyVolume: 100, sellVolume: 300);
            Assert.False(OrderFlowMetricsTracker.IsDivergenceConfirmed(bar));
        }

        #endregion

        #region VA Width

        [Fact]
        public void VaWidth_NormalizedByAtr()
        {
            var tracker = new OrderFlowMetricsTracker(20);
            double atr = 10.0;
            // VA width = 108 - 96 = 12, / 10 = 1.2
            tracker.ProcessBar(MakeBar(100, 110, 90, 105), 108, 96, 100, atr);
            var result = tracker.ProcessBar(MakeBar(101, 111, 91, 106), 108, 96, 101, atr);

            Assert.Equal(1.2, result.VaWidth, 4);
        }

        #endregion

        #region CurrentMetrics

        [Fact]
        public void CurrentMetrics_UpdatedAfterProcessBar()
        {
            var tracker = new OrderFlowMetricsTracker(20);
            Assert.False(tracker.CurrentMetrics.IsValid);

            tracker.ProcessBar(MakeBar(100, 105, 95, 102), 103, 97, 100, 5.0);
            Assert.False(tracker.CurrentMetrics.IsValid);

            tracker.ProcessBar(MakeBar(101, 106, 96, 103), 104, 98, 101, 5.0);
            Assert.True(tracker.CurrentMetrics.IsValid);
        }

        #endregion
    }
}
