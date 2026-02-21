using System;
using System.Collections.Generic;
using Xunit;
using BeerMoney.Core.Analysis;
using BeerMoney.Core.Models;

namespace BeerMoneyTests
{
    public class AtrAnalysisTests
    {
        private static BarData MakeBar(double open, double high, double low, double close)
        {
            return new BarData(DateTime.UtcNow, 0, open, high, low, close, 100);
        }

        [Fact]
        public void TrueRange_NormalBar_ReturnsHighMinusLow()
        {
            // Previous close within bar range — TR = H-L
            double tr = AtrAnalysis.TrueRange(110, 90, 100);
            Assert.Equal(20, tr);
        }

        [Fact]
        public void TrueRange_GapUp_ReturnsHighMinusPrevClose()
        {
            // Gap up: previous close below current low
            // H=120, L=110, prevClose=90 → H-L=10, H-PC=30, L-PC=20 → max=30
            double tr = AtrAnalysis.TrueRange(120, 110, 90);
            Assert.Equal(30, tr);
        }

        [Fact]
        public void TrueRange_GapDown_ReturnsLowMinusPrevClose()
        {
            // Gap down: previous close above current high
            // H=90, L=80, prevClose=120 → H-L=10, |H-PC|=30, |L-PC|=40 → max=40
            double tr = AtrAnalysis.TrueRange(90, 80, 120);
            Assert.Equal(40, tr);
        }

        [Fact]
        public void TrueRange_InsideBar_MatchesGapCalculation()
        {
            // Inside bar where H-L is smaller than gap distances
            // H=105, L=95, prevClose=90 → H-L=10, |H-PC|=15, |L-PC|=5 → max=15
            double tr = AtrAnalysis.TrueRange(105, 95, 90);
            Assert.Equal(15, tr);
        }

        [Fact]
        public void Calculate_WithKnownBars_ReturnsCorrectAtr()
        {
            // 4 bars, period=3: need TR for bars 1,2,3 relative to previous close
            var bars = new List<BarData>
            {
                MakeBar(100, 110, 90, 100),   // bar 0 (base for prev close)
                MakeBar(100, 112, 92, 105),    // TR = max(20, |112-100|=12, |92-100|=8) = 20
                MakeBar(105, 115, 95, 110),    // TR = max(20, |115-105|=10, |95-105|=10) = 20
                MakeBar(110, 125, 100, 120),   // TR = max(25, |125-110|=15, |100-110|=10) = 25
            };

            var result = AtrAnalysis.Calculate(bars, period: 3);
            Assert.True(result.IsValid);
            Assert.Equal((20.0 + 20.0 + 25.0) / 3.0, result.CurrentAtr, 6);
        }

        [Fact]
        public void Calculate_InsufficientBars_ReturnsInvalid()
        {
            var bars = new List<BarData>
            {
                MakeBar(100, 110, 90, 100),
                MakeBar(100, 112, 92, 105),
            };

            var result = AtrAnalysis.Calculate(bars, period: 14);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Calculate_NullBars_ReturnsInvalid()
        {
            var result = AtrAnalysis.Calculate(null, period: 14);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Calculate_ZeroPeriod_ReturnsInvalid()
        {
            var bars = new List<BarData> { MakeBar(100, 110, 90, 100) };
            var result = AtrAnalysis.Calculate(bars, period: 0);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Calculate_NegativePeriod_ReturnsInvalid()
        {
            var bars = new List<BarData> { MakeBar(100, 110, 90, 100) };
            var result = AtrAnalysis.Calculate(bars, period: -1);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Calculate_PeriodOne_UsesLastTrueRange()
        {
            var bars = new List<BarData>
            {
                MakeBar(100, 110, 90, 100),
                MakeBar(100, 115, 95, 110), // TR = max(20, 15, 5) = 20
            };

            var result = AtrAnalysis.Calculate(bars, period: 1);
            Assert.True(result.IsValid);
            Assert.Equal(20.0, result.CurrentAtr, 6);
        }
    }
}
