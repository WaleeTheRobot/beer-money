using System;
using BeerMoney.Core.Models;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript.BarsTypes;

namespace NinjaTrader.NinjaScript.Indicators.BeerMoney
{
    /// <summary>
    /// Extracts BarData from NinjaTrader data arrays and volumetric bar types.
    /// Handles both plain OHLCV bars and volumetric bars with imbalance detection.
    /// </summary>
    internal sealed class VolumetricBarExtractor
    {
        private readonly Func<int, VolumetricBarsType> _getVolumetricBarsType;
        private readonly Func<double> _getTickSize;
        private readonly int _ticksPerLevel;
        private readonly double _imbalanceRatio;
        private readonly int _minImbalanceVolume;

        public VolumetricBarExtractor(
            Func<int, VolumetricBarsType> getVolumetricBarsType,
            Func<double> getTickSize,
            int ticksPerLevel,
            double imbalanceRatio,
            int minImbalanceVolume)
        {
            _getVolumetricBarsType = getVolumetricBarsType;
            _getTickSize = getTickSize;
            _ticksPerLevel = ticksPerLevel;
            _imbalanceRatio = imbalanceRatio;
            _minImbalanceVolume = minImbalanceVolume;
        }

        public BarData ExtractBar(int seriesIndex, int barIndex,
            TimeSeries[] times, int[] currentBars,
            PriceSeries[] opens, PriceSeries[] highs, PriceSeries[] lows, PriceSeries[] closes,
            VolumeSeries[] volumes)
        {
            return new BarData(
                times[seriesIndex][barIndex],
                currentBars[seriesIndex] - barIndex,
                opens[seriesIndex][barIndex],
                highs[seriesIndex][barIndex],
                lows[seriesIndex][barIndex],
                closes[seriesIndex][barIndex],
                (long)volumes[seriesIndex][barIndex]);
        }

        public BarData ExtractVolumetricBar(int seriesIndex, int barIndex,
            TimeSeries[] times, int[] currentBars,
            PriceSeries[] opens, PriceSeries[] highs, PriceSeries[] lows, PriceSeries[] closes,
            VolumeSeries[] volumes)
        {
            long buyVolume = 0, sellVolume = 0, cumulativeDelta = 0;
            long maxDelta = 0, minDelta = 0;
            double pointOfControl = 0;
            var imb = new ImbalanceMetrics();
            double bullishImbPriceSum = 0, bearishImbPriceSum = 0;

            var volumetricBars = _getVolumetricBarsType(seriesIndex);
            if (volumetricBars != null)
            {
                int volumeIndex = currentBars[seriesIndex] - barIndex;
                if (volumeIndex >= 0 && volumeIndex < volumetricBars.Volumes.Length)
                {
                    var barVolumes = volumetricBars.Volumes[volumeIndex];
                    if (barVolumes != null)
                    {
                        buyVolume = (long)barVolumes.TotalBuyingVolume;
                        sellVolume = (long)barVolumes.TotalSellingVolume;
                        cumulativeDelta = (long)barVolumes.CumulativeDelta;
                        maxDelta = (long)barVolumes.MaxSeenDelta;
                        minDelta = (long)barVolumes.MinSeenDelta;
                        barVolumes.GetMaximumVolume(null, out pointOfControl);

                        double tickSize = _getTickSize();
                        double levelSize = tickSize * _ticksPerLevel;
                        double barHigh = highs[seriesIndex].GetValueAt(volumeIndex);
                        double barLow = lows[seriesIndex].GetValueAt(volumeIndex);

                        int startLevel = (int)Math.Floor(barLow / levelSize);
                        int endLevel = (int)Math.Ceiling(barHigh / levelSize);

                        for (int level = startLevel; level <= endLevel; level++)
                        {
                            double price = level * levelSize;
                            double priceBelow = price - levelSize;
                            double priceAbove = price + levelSize;

                            long askAtPrice = barVolumes.GetAskVolumeForPrice(price);
                            long bidAtPrice = barVolumes.GetBidVolumeForPrice(price);
                            long bidBelow = barVolumes.GetBidVolumeForPrice(priceBelow);
                            long askAbove = barVolumes.GetAskVolumeForPrice(priceAbove);

                            if (askAtPrice >= _minImbalanceVolume)
                            {
                                if (bidBelow == 0 || ((askAtPrice - bidBelow) >= _minImbalanceVolume && (double)askAtPrice / bidBelow >= _imbalanceRatio))
                                {
                                    imb.BullishCount++;
                                    imb.BullishVolumeSum += askAtPrice;
                                    bullishImbPriceSum += price;
                                    if (askAtPrice > imb.MaxBullishVolume)
                                    {
                                        imb.MaxBullishVolume = askAtPrice;
                                        imb.MaxBullishPrice = price;
                                    }
                                }
                            }

                            if (bidAtPrice >= _minImbalanceVolume)
                            {
                                if (askAbove == 0 || ((bidAtPrice - askAbove) >= _minImbalanceVolume && (double)bidAtPrice / askAbove >= _imbalanceRatio))
                                {
                                    imb.BearishCount++;
                                    imb.BearishVolumeSum += bidAtPrice;
                                    bearishImbPriceSum += price;
                                    if (bidAtPrice > imb.MaxBearishVolume)
                                    {
                                        imb.MaxBearishVolume = bidAtPrice;
                                        imb.MaxBearishPrice = price;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            double barHighLocal = highs[seriesIndex][barIndex];
            double barLowLocal = lows[seriesIndex][barIndex];
            double barRangeLocal = barHighLocal - barLowLocal;

            if (imb.BullishCount > 0 && barRangeLocal > 0)
            {
                double avgPrice = bullishImbPriceSum / imb.BullishCount;
                imb.BullishAvgPosition = (avgPrice - barLowLocal) / barRangeLocal * 2.0 - 1.0;
            }

            if (imb.BearishCount > 0 && barRangeLocal > 0)
            {
                double avgPrice = bearishImbPriceSum / imb.BearishCount;
                imb.BearishAvgPosition = (avgPrice - barLowLocal) / barRangeLocal * 2.0 - 1.0;
            }

            return new BarData(
                times[seriesIndex][barIndex],
                currentBars[seriesIndex] - barIndex,
                opens[seriesIndex][barIndex],
                barHighLocal,
                barLowLocal,
                closes[seriesIndex][barIndex],
                (long)volumes[seriesIndex][barIndex],
                buyVolume, sellVolume, cumulativeDelta,
                maxDelta, minDelta, pointOfControl, imb);
        }
    }
}
