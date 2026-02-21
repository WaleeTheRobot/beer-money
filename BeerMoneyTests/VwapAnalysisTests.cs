using System;
using System.Collections.Generic;
using Xunit;
using BeerMoney.Core.Analysis;
using BeerMoney.Core.Models;

namespace BeerMoneyTests
{
    public class VwapAnalysisTests
    {
        private static BarData MakeBar(double high, double low, double close, long volume)
        {
            return new BarData(DateTime.UtcNow, 0, 100, high, low, close, volume);
        }

        [Fact]
        public void Calculate_SingleBar_ReturnsTypicalPriceAsVwap()
        {
            var bars = new List<BarData>
            {
                MakeBar(110, 90, 100, 1000)
            };
            double currentPrice = 105;

            var result = VwapAnalysis.Calculate(bars, currentPrice);
            Assert.True(result.IsValid);

            double expectedVwap = (110 + 90 + 100) / 3.0;
            Assert.Equal(expectedVwap, result.Vwap, 6);
            Assert.Equal(currentPrice - expectedVwap, result.PriceDistance, 6);
        }

        [Fact]
        public void Calculate_MultipleBars_WeightsByVolume()
        {
            var bars = new List<BarData>
            {
                // TP = (120+80+100)/3 = 100, volume = 100
                MakeBar(120, 80, 100, 100),
                // TP = (130+90+110)/3 = 110, volume = 300
                MakeBar(130, 90, 110, 300),
            };

            var result = VwapAnalysis.Calculate(bars, 105);
            Assert.True(result.IsValid);

            double expectedVwap = (100.0 * 100 + 110.0 * 300) / (100 + 300);
            Assert.Equal(expectedVwap, result.Vwap, 6);
        }

        [Fact]
        public void Calculate_NullBars_ReturnsInvalid()
        {
            var result = VwapAnalysis.Calculate(null, 100);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Calculate_EmptyBars_ReturnsInvalid()
        {
            var result = VwapAnalysis.Calculate(new List<BarData>(), 100);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Calculate_ZeroVolume_ReturnsInvalid()
        {
            var bars = new List<BarData>
            {
                MakeBar(110, 90, 100, 0)
            };

            var result = VwapAnalysis.Calculate(bars, 100);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Calculate_PriceDistance_PositiveWhenAboveVwap()
        {
            var bars = new List<BarData> { MakeBar(110, 90, 100, 500) };

            var result = VwapAnalysis.Calculate(bars, 120);
            Assert.True(result.PriceDistance > 0);
        }

        [Fact]
        public void Calculate_PriceDistance_NegativeWhenBelowVwap()
        {
            var bars = new List<BarData> { MakeBar(110, 90, 100, 500) };

            var result = VwapAnalysis.Calculate(bars, 80);
            Assert.True(result.PriceDistance < 0);
        }
    }
}
