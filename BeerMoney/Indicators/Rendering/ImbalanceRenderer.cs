using System;
using System.Windows.Media;
using NinjaTrader.Gui.Chart;
using NinjaTrader.NinjaScript.BarsTypes;
using NinjaTrader.Data;
using SharpDX.Direct2D1;

namespace NinjaTrader.NinjaScript.Indicators.BeerMoney.Rendering
{
    /// <summary>
    /// Renders diagonal imbalance glows on the chart with volume-proportional scaling.
    /// Both radius and opacity scale continuously based on actual volume at each level.
    /// </summary>
    public sealed class ImbalanceRenderer : IDisposable
    {
        private const float MinGlowMultiplier = 1.0f;
        private const float MaxGlowMultiplier = 4.5f;
        private const float MinGlowRadius = 6f;
        private const float GlowCoreRadiusRatio = 0.5f;
        private const int GlowLayers = 3;
        private const float GlowLayerExpansion = 0.4f;
        private const float GlowLayerOpacityFactor = 0.7f;
        private const float MinWeight = 0.15f;

        private readonly BrushCache _brushCache = new BrushCache();

        public void Render(
            RenderTarget renderTarget,
            ChartControl chartControl,
            ChartScale chartScale,
            ChartBars chartBars,
            VolumetricBarsType volumetricBars,
            Bars[] barsArray,
            int primarySeries,
            int triggerSeries,
            ImbalanceSettings settings,
            int[] currentBars,
            PriceSeries[] highs,
            PriceSeries[] lows,
            TimeSeries[] times,
            Action<string> logError = null)
        {
            if (volumetricBars == null || renderTarget == null)
                return;

            double levelSize = settings.TickSize * settings.TicksPerLevel;
            float halfBarWidth = (float)(chartControl.BarWidth / 2.0);

            DateTime firstVisibleTime = times[primarySeries].GetValueAt(Math.Max(0, chartBars.FromIndex));
            DateTime lastVisibleTime = times[primarySeries].GetValueAt(Math.Min(chartBars.ToIndex, currentBars[primarySeries]));

            int firstTriggerIdx = barsArray[triggerSeries].GetBar(firstVisibleTime);
            int lastTriggerIdx = barsArray[triggerSeries].GetBar(lastVisibleTime);

            firstTriggerIdx = Math.Max(0, firstTriggerIdx);
            lastTriggerIdx = Math.Min(lastTriggerIdx, currentBars[triggerSeries]);

            if (firstTriggerIdx > lastTriggerIdx)
                return;

            for (int triggerBarIdx = firstTriggerIdx; triggerBarIdx <= lastTriggerIdx; triggerBarIdx++)
            {
                if (triggerBarIdx < 0)
                    continue;

                try
                {
                    if (volumetricBars.Volumes[triggerBarIdx] == null)
                        continue;

                    double barHigh = highs[triggerSeries].GetValueAt(triggerBarIdx);
                    double barLow = lows[triggerSeries].GetValueAt(triggerBarIdx);

                    int primaryBarIdx = GetPrimaryBarIndex(chartBars, barsArray, times, triggerBarIdx,
                        primarySeries, triggerSeries, currentBars);

                    if (primaryBarIdx < 0 || primaryBarIdx > currentBars[primarySeries])
                        continue;

                    float barX = chartControl.GetXByBarIndex(chartBars, primaryBarIdx);

                    int startLevel = (int)Math.Floor(barLow / levelSize);
                    int endLevel = (int)Math.Ceiling(barHigh / levelSize);

                    for (int level = startLevel; level <= endLevel; level++)
                    {
                        double price = level * levelSize;
                        RenderImbalancesAtPrice(
                            renderTarget, chartScale, barX, halfBarWidth, price, levelSize,
                            volumetricBars, triggerBarIdx, settings);
                    }
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    logError?.Invoke($"ImbalanceRenderer index out of range at bar {triggerBarIdx} (range: {firstTriggerIdx}-{lastTriggerIdx}): {ex.Message}");
                }
                catch (Exception ex)
                {
                    logError?.Invoke($"ImbalanceRenderer error at bar {triggerBarIdx}: {ex.Message}");
                }
            }
        }

        private int GetPrimaryBarIndex(ChartBars chartBars, Bars[] barsArray, TimeSeries[] times,
            int triggerBarIdx, int primarySeries, int triggerSeries, int[] currentBars)
        {
            if (chartBars.Bars.BarsPeriod.BarsPeriodType == BarsPeriodType.Tick)
            {
                int offset = currentBars[primarySeries] - currentBars[triggerSeries];
                return triggerBarIdx + offset;
            }

            DateTime barTime = times[triggerSeries].GetValueAt(triggerBarIdx);
            return barsArray[primarySeries].GetBar(barTime);
        }

        private void RenderImbalancesAtPrice(
            RenderTarget renderTarget,
            ChartScale chartScale,
            float barX,
            float halfBarWidth,
            double price,
            double levelSize,
            VolumetricBarsType volumetricBars,
            int barIndex,
            ImbalanceSettings settings)
        {
            double priceBelow = price - levelSize;
            double priceAbove = price + levelSize;

            var barVolumes = volumetricBars.Volumes[barIndex];
            long askAtPrice = barVolumes.GetAskVolumeForPrice(price);
            long bidAtPrice = barVolumes.GetBidVolumeForPrice(price);
            long bidBelow = barVolumes.GetBidVolumeForPrice(priceBelow);
            long askAbove = barVolumes.GetAskVolumeForPrice(priceAbove);

            bool bullishImbalance = CheckBullishImbalance(askAtPrice, bidBelow, settings.ImbalanceRatio, settings.MinImbalanceVolume);
            bool bearishImbalance = CheckBearishImbalance(bidAtPrice, askAbove, settings.ImbalanceRatio, settings.MinImbalanceVolume);

            if (bullishImbalance)
            {
                DrawImbalanceGlow(renderTarget, chartScale, barX, halfBarWidth, price, askAtPrice,
                    settings.ReferenceVolume, settings.ImbalanceOpacity, settings.BullishImbalanceColor);
            }

            if (bearishImbalance)
            {
                DrawImbalanceGlow(renderTarget, chartScale, barX, halfBarWidth, price, bidAtPrice,
                    settings.ReferenceVolume, settings.ImbalanceOpacity, settings.BearishImbalanceColor);
            }
        }

        private bool CheckBullishImbalance(long askAtPrice, long bidBelow, double imbalanceRatio, int minImbalanceVolume)
        {
            if (askAtPrice < minImbalanceVolume)
                return false;

            if (bidBelow == 0)
                return true;

            long bullishDiff = askAtPrice - bidBelow;
            return bullishDiff >= minImbalanceVolume && (double)askAtPrice / bidBelow >= imbalanceRatio;
        }

        private bool CheckBearishImbalance(long bidAtPrice, long askAbove, double imbalanceRatio, int minImbalanceVolume)
        {
            if (bidAtPrice < minImbalanceVolume)
                return false;

            if (askAbove == 0)
                return true;

            long bearishDiff = bidAtPrice - askAbove;
            return bearishDiff >= minImbalanceVolume && (double)bidAtPrice / askAbove >= imbalanceRatio;
        }

        private void DrawImbalanceGlow(
            RenderTarget renderTarget,
            ChartScale chartScale,
            float barX,
            float halfBarWidth,
            double price,
            long volume,
            int referenceVolume,
            float imbalanceOpacity,
            System.Windows.Media.Brush colorBrush)
        {
            float centerY = chartScale.GetYByValue(price);

            // Continuous volume weight: small volumes get small glows, large volumes get large glows
            float weight = Math.Max(MinWeight, Math.Min(1.0f, (float)volume / Math.Max(1, referenceVolume)));

            float glowMultiplier = MinGlowMultiplier + weight * (MaxGlowMultiplier - MinGlowMultiplier);
            float baseRadius = Math.Max(halfBarWidth * glowMultiplier, MinGlowRadius);

            var centerPoint = new SharpDX.Vector2(barX, centerY);
            var baseColor = ((System.Windows.Media.SolidColorBrush)colorBrush).Color;

            // Floor the effective weight so minimum is always visible
            float effectiveWeight = 0.4f + 0.6f * weight;

            // Draw multiple layers for glow effect (outer to inner), opacity scaled by volume weight
            for (int i = GlowLayers; i >= 1; i--)
            {
                float layerRadius = baseRadius * (1f + (i - 1) * GlowLayerExpansion);
                float layerOpacity = imbalanceOpacity * effectiveWeight * (1f / i) * GlowLayerOpacityFactor;

                var layerBrush = _brushCache.GetOrCreate(renderTarget, baseColor.R, baseColor.G, baseColor.B, (byte)(255 * layerOpacity));
                var ellipse = new Ellipse(centerPoint, layerRadius, layerRadius);
                renderTarget.FillEllipse(ellipse, layerBrush);
            }

            // Draw bright center core, also scaled by weight
            float coreRadius = baseRadius * GlowCoreRadiusRatio;
            float coreOpacity = imbalanceOpacity * effectiveWeight;
            var coreBrush = _brushCache.GetOrCreate(renderTarget, baseColor.R, baseColor.G, baseColor.B, (byte)(255 * coreOpacity));
            var coreEllipse = new Ellipse(centerPoint, coreRadius, coreRadius);
            renderTarget.FillEllipse(coreEllipse, coreBrush);
        }

        public void Dispose()
        {
            _brushCache.Dispose();
        }
    }
}
