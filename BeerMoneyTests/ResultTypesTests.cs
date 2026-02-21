using System.Collections.Generic;
using Xunit;
using BeerMoney.Core.Analysis.Results;

namespace BeerMoneyTests
{
    public class ResultTypesTests
    {
        [Fact]
        public void AtrResult_Create_IsValid()
        {
            var result = AtrResult.Create(5.5);
            Assert.True(result.IsValid);
            Assert.Equal(5.5, result.CurrentAtr);
        }

        [Fact]
        public void AtrResult_Invalid_IsNotValid()
        {
            var result = AtrResult.Invalid();
            Assert.False(result.IsValid);
            Assert.Equal(0, result.CurrentAtr);
        }

        [Fact]
        public void VwapResult_Create_IsValid()
        {
            var result = VwapResult.Create(100.5, 2.3);
            Assert.True(result.IsValid);
            Assert.Equal(100.5, result.Vwap);
            Assert.Equal(2.3, result.PriceDistance);
        }

        [Fact]
        public void VwapResult_Invalid_IsNotValid()
        {
            var result = VwapResult.Invalid();
            Assert.False(result.IsValid);
            Assert.Equal(0, result.Vwap);
            Assert.Equal(0, result.PriceDistance);
        }

        [Fact]
        public void VolumeProfileResult_Create_IsValid()
        {
            var pv = new Dictionary<double, long> { { 100.0, 500 } };
            var result = VolumeProfileResult.Create(100, 102, 98, pv, 500, 500, null);
            Assert.True(result.IsValid);
            Assert.Equal(100, result.POC);
            Assert.Equal(102, result.VAH);
            Assert.Equal(98, result.VAL);
            Assert.Equal(500, result.MaxVolume);
            Assert.Equal(500, result.TotalVolume);
            Assert.NotNull(result.PriceVolumes);
        }

        [Fact]
        public void VolumeProfileResult_Invalid_IsNotValid()
        {
            var result = VolumeProfileResult.Invalid();
            Assert.False(result.IsValid);
            Assert.Equal(0, result.POC);
            Assert.Null(result.PriceVolumes);
        }
    }
}
