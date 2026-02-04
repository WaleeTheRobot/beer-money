using System;
using System.Collections.Generic;
using System.Windows.Media;
using NinjaTrader.Gui.Chart;
using NinjaTrader.NinjaScript.BarsTypes;
using NinjaTrader.Data;
using SharpDX.Direct2D1;

namespace NinjaTrader.NinjaScript.Indicators.BeerMoney.Rendering
{
    /// <summary>
    /// Renders diagonal imbalance glows on the chart.
    /// </summary>
    public sealed class ImbalanceRenderer
    {
        // Glow effect constants
        private const float GlowRadiusMultiplier = 1.5f;
        private const float HighVolumeGlowMultiplier = 4.5f;
        private const float MinGlowRadius = 6f;
        private const float MinHighVolumeGlowRadius = 18f;
        private const float GlowCoreRadiusRatio = 0.5f;
        private const int GlowLayers = 3;
        /// <summary>
        /// Each outer glow layer expands by this fraction of base radius (0.4 = 40% per layer).
        /// </summary>
        private const float GlowLayerExpansion = 0.4f;
        /// <summary>
        /// Opacity reduction factor per glow layer to create fade effect (0.7 = 70% of previous layer).
        /// Combined with 1/layer index to create exponential falloff for natural glow appearance.
        /// </summary>
        private const float GlowLayerOpacityFactor = 0.7f;

        // Brush cache to reduce GC pressure during rendering
        private readonly Dictionary<uint, SharpDX.Direct2D1.SolidColorBrush> _brushCache = new Dictionary<uint, SharpDX.Direct2D1.SolidColorBrush>();
        private RenderTarget _cachedRenderTarget;

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

            // Get visible time range from primary chart
            DateTime firstVisibleTime = times[primarySeries].GetValueAt(Math.Max(0, chartBars.FromIndex));
            DateTime lastVisibleTime = times[primarySeries].GetValueAt(Math.Min(chartBars.ToIndex, currentBars[primarySeries]));

            // Find trigger bars within visible time range
            int firstTriggerIdx = barsArray[triggerSeries].GetBar(firstVisibleTime);
            int lastTriggerIdx = barsArray[triggerSeries].GetBar(lastVisibleTime);

            // Clamp indices to valid range
            firstTriggerIdx = Math.Max(0, firstTriggerIdx);
            lastTriggerIdx = Math.Min(lastTriggerIdx, currentBars[triggerSeries]);

            // Ensure we have a valid range to iterate
            if (firstTriggerIdx > lastTriggerIdx)
                return;

            for (int triggerBarIdx = firstTriggerIdx; triggerBarIdx <= lastTriggerIdx; triggerBarIdx++)
            {
                if (triggerBarIdx < 0)
                    continue;

                try
                {
                    var barVolumes = volumetricBars.Volumes[triggerBarIdx];
                    if (barVolumes == null)
                        continue;

                    double barHigh = highs[triggerSeries].GetValueAt(triggerBarIdx);
                    double barLow = lows[triggerSeries].GetValueAt(triggerBarIdx);

                    int primaryBarIdx = GetPrimaryBarIndex(chartBars, barsArray, times, triggerBarIdx,
                        primarySeries, triggerSeries, currentBars);

                    if (primaryBarIdx < 0 || primaryBarIdx > currentBars[primarySeries])
                        continue;

                    float barX = chartControl.GetXByBarIndex(chartBars, primaryBarIdx);

                    // Use integer-based iteration to avoid floating-point precision issues
                    int startLevel = (int)Math.Floor(barLow / levelSize);
                    int endLevel = (int)Math.Ceiling(barHigh / levelSize);

                    for (int level = startLevel; level <= endLevel; level++)
                    {
                        double price = level * levelSize;
                        RenderImbalancesAtPrice(
                            renderTarget, chartScale, barX, halfBarWidth, price, levelSize,
                            barVolumes, settings);
                    }
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    // Bar index out of range - expected during chart updates, log with context for debugging
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
            dynamic barVolumes,
            ImbalanceSettings settings)
        {
            double priceBelow = price - levelSize;
            double priceAbove = price + levelSize;

            long askAtPrice = barVolumes.GetAskVolumeForPrice(price);
            long bidAtPrice = barVolumes.GetBidVolumeForPrice(price);
            long bidBelow = barVolumes.GetBidVolumeForPrice(priceBelow);
            long askAbove = barVolumes.GetAskVolumeForPrice(priceAbove);

            bool bullishImbalance = CheckBullishImbalance(askAtPrice, bidBelow, settings.ImbalanceRatio, settings.MinImbalanceVolume);
            bool bearishImbalance = CheckBearishImbalance(bidAtPrice, askAbove, settings.ImbalanceRatio, settings.MinImbalanceVolume);

            if (bullishImbalance)
            {
                bool isHighVolume = askAtPrice >= settings.HighVolumeThreshold;
                var color = isHighVolume ? settings.HighVolumeBullishColor : settings.BullishImbalanceColor;
                DrawImbalanceGlow(renderTarget, chartScale, barX, halfBarWidth, price, isHighVolume,
                    settings.ImbalanceOpacity, color);
            }

            if (bearishImbalance)
            {
                bool isHighVolume = bidAtPrice >= settings.HighVolumeThreshold;
                var color = isHighVolume ? settings.HighVolumeBearishColor : settings.BearishImbalanceColor;
                DrawImbalanceGlow(renderTarget, chartScale, barX, halfBarWidth, price, isHighVolume,
                    settings.ImbalanceOpacity, color);
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
            bool isHighVolume,
            float imbalanceOpacity,
            System.Windows.Media.Brush colorBrush)
        {
            // Clear brush cache if render target changed (device reset, etc.)
            if (_cachedRenderTarget != renderTarget)
            {
                ClearBrushCache();
                _cachedRenderTarget = renderTarget;
            }

            float centerY = chartScale.GetYByValue(price);

            float glowMultiplier = isHighVolume ? HighVolumeGlowMultiplier : GlowRadiusMultiplier;
            float minRadius = isHighVolume ? MinHighVolumeGlowRadius : MinGlowRadius;
            float baseRadius = Math.Max(halfBarWidth * glowMultiplier, minRadius);

            var centerPoint = new SharpDX.Vector2(barX, centerY);
            var baseColor = ((System.Windows.Media.SolidColorBrush)colorBrush).Color;

            // Draw multiple layers for glow effect (outer to inner)
            for (int i = GlowLayers; i >= 1; i--)
            {
                float layerRadius = baseRadius * (1f + (i - 1) * GlowLayerExpansion);
                float layerOpacity = imbalanceOpacity * (1f / i) * GlowLayerOpacityFactor;

                var layerBrush = GetOrCreateBrush(renderTarget, baseColor.R, baseColor.G, baseColor.B, (byte)(255 * layerOpacity));
                var ellipse = new SharpDX.Direct2D1.Ellipse(centerPoint, layerRadius, layerRadius);
                renderTarget.FillEllipse(ellipse, layerBrush);
            }

            // Draw bright center core
            float coreRadius = baseRadius * GlowCoreRadiusRatio;
            var coreBrush = GetOrCreateBrush(renderTarget, baseColor.R, baseColor.G, baseColor.B, (byte)(255 * imbalanceOpacity));
            var coreEllipse = new SharpDX.Direct2D1.Ellipse(centerPoint, coreRadius, coreRadius);
            renderTarget.FillEllipse(coreEllipse, coreBrush);
        }

        private SharpDX.Direct2D1.SolidColorBrush GetOrCreateBrush(RenderTarget renderTarget, byte r, byte g, byte b, byte a)
        {
            // Create a unique key from RGBA values
            uint key = ((uint)r << 24) | ((uint)g << 16) | ((uint)b << 8) | a;

            if (!_brushCache.TryGetValue(key, out var brush))
            {
                brush = new SharpDX.Direct2D1.SolidColorBrush(renderTarget, new SharpDX.Color(r, g, b, a));
                _brushCache[key] = brush;
            }
            return brush;
        }

        private void ClearBrushCache()
        {
            foreach (var brush in _brushCache.Values)
            {
                brush?.Dispose();
            }
            _brushCache.Clear();
        }

        /// <summary>
        /// Disposes cached resources. Call when the renderer is no longer needed.
        /// </summary>
        public void Dispose()
        {
            ClearBrushCache();
            _cachedRenderTarget = null;
        }
    }
}
