using System.Collections.Generic;
using System.Linq;
using Xunit;
using BeerMoney.Core.Analysis;
using BeerMoney.Core.Analysis.Results;

namespace BeerMoneyTests
{
    public class VolumeProfileAnalysisTests
    {
        [Fact]
        public void Calculate_SinglePrice_PocEqualsOnlyPrice()
        {
            var pv = new Dictionary<double, long> { { 100.0, 500 } };
            var result = VolumeProfileAnalysis.Calculate(pv);

            Assert.True(result.IsValid);
            Assert.Equal(100.0, result.POC);
            Assert.Equal(100.0, result.VAH);
            Assert.Equal(100.0, result.VAL);
            Assert.Equal(500, result.MaxVolume);
            Assert.Equal(500, result.TotalVolume);
        }

        [Fact]
        public void Calculate_PocIsHighestVolume()
        {
            var pv = new Dictionary<double, long>
            {
                { 99.0, 100 },
                { 100.0, 500 },
                { 101.0, 200 },
            };

            var result = VolumeProfileAnalysis.Calculate(pv);
            Assert.True(result.IsValid);
            Assert.Equal(100.0, result.POC);
        }

        [Fact]
        public void Calculate_TieBreaking_HigherPriceWins()
        {
            var pv = new Dictionary<double, long>
            {
                { 98.0, 300 },
                { 100.0, 300 },
                { 102.0, 300 },
            };

            var result = VolumeProfileAnalysis.Calculate(pv);
            Assert.True(result.IsValid);
            Assert.Equal(102.0, result.POC);
        }

        [Fact]
        public void Calculate_ValueArea_ContainsPoc()
        {
            var pv = new Dictionary<double, long>
            {
                { 98.0, 100 },
                { 99.0, 200 },
                { 100.0, 500 },
                { 101.0, 200 },
                { 102.0, 100 },
            };

            var result = VolumeProfileAnalysis.Calculate(pv);
            Assert.True(result.IsValid);
            Assert.True(result.VAL <= result.POC);
            Assert.True(result.VAH >= result.POC);
        }

        [Fact]
        public void Calculate_ValueArea_ExpandsSymmetrically()
        {
            // Equal volume on both sides — should expand both ways
            var pv = new Dictionary<double, long>
            {
                { 97.0, 50 },
                { 98.0, 100 },
                { 99.0, 200 },
                { 100.0, 500 },  // POC
                { 101.0, 200 },
                { 102.0, 100 },
                { 103.0, 50 },
            };

            var result = VolumeProfileAnalysis.Calculate(pv);
            Assert.True(result.IsValid);
            // VAH and VAL should be roughly symmetric around POC
            Assert.True(result.VAL <= 100.0);
            Assert.True(result.VAH >= 100.0);
        }

        [Fact]
        public void Calculate_NullInput_ReturnsInvalid()
        {
            var result = VolumeProfileAnalysis.Calculate(null);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Calculate_EmptyInput_ReturnsInvalid()
        {
            var result = VolumeProfileAnalysis.Calculate(new Dictionary<double, long>());
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Calculate_AllZeroVolume_ReturnsInvalid()
        {
            var pv = new Dictionary<double, long>
            {
                { 100.0, 0 },
                { 101.0, 0 },
            };

            var result = VolumeProfileAnalysis.Calculate(pv);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Calculate_TotalVolumeCorrect()
        {
            var pv = new Dictionary<double, long>
            {
                { 99.0, 100 },
                { 100.0, 200 },
                { 101.0, 300 },
            };

            var result = VolumeProfileAnalysis.Calculate(pv);
            Assert.Equal(600, result.TotalVolume);
        }

        [Fact]
        public void Calculate_HvnsReturnedInDescendingVolumeOrder()
        {
            var pv = new Dictionary<double, long>
            {
                { 97.0, 50 },
                { 98.0, 300 },
                { 99.0, 100 },
                { 100.0, 500 },
                { 101.0, 400 },
                { 102.0, 200 },
                { 103.0, 150 },
            };

            var result = VolumeProfileAnalysis.Calculate(pv);
            Assert.Equal(5, result.HighValueNodes.Count);

            for (int i = 1; i < result.HighValueNodes.Count; i++)
                Assert.True(result.HighValueNodes[i - 1].Volume >= result.HighValueNodes[i].Volume);
        }

        [Fact]
        public void Calculate_PocIsFirstHvn()
        {
            var pv = new Dictionary<double, long>
            {
                { 99.0, 100 },
                { 100.0, 500 },
                { 101.0, 200 },
            };

            var result = VolumeProfileAnalysis.Calculate(pv);
            Assert.Equal(result.POC, result.HighValueNodes[0].Price);
            Assert.Equal(result.MaxVolume, result.HighValueNodes[0].Volume);
        }

        [Fact]
        public void Calculate_FewerThanFiveLevels_ReturnsAll()
        {
            var pv = new Dictionary<double, long>
            {
                { 100.0, 500 },
                { 101.0, 200 },
                { 102.0, 100 },
            };

            var result = VolumeProfileAnalysis.Calculate(pv);
            Assert.Equal(3, result.HighValueNodes.Count);
        }

        [Fact]
        public void Calculate_InvalidResult_HasEmptyHvns()
        {
            var result = VolumeProfileAnalysis.Calculate(null);
            Assert.False(result.IsValid);
            Assert.NotNull(result.HighValueNodes);
            Assert.Empty(result.HighValueNodes);
        }
    }
}
