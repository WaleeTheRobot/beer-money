using System;
using System.Collections.Generic;
using BeerMoney.Core.Analysis.Results;
using NinjaTrader.Gui.Chart;
using SharpDX.Direct2D1;

namespace NinjaTrader.NinjaScript.Indicators.BeerMoney.Rendering
{
    /// <summary>
    /// Renders volume profile on the right side of the chart.
    /// </summary>
    public sealed class VolumeProfileRenderer : IDisposable
    {
        private const float ProfileRightMargin = 10f;

        private readonly Dictionary<uint, SolidColorBrush> _brushCache = new Dictionary<uint, SolidColorBrush>();
        private RenderTarget _cachedRenderTarget;

        public void Render(
            RenderTarget renderTarget,
            ChartControl chartControl,
            ChartScale chartScale,
            VolumeProfileResult volumeProfile,
            VolumeProfileSettings settings)
        {
            if (renderTarget == null || volumeProfile == null || !volumeProfile.IsValid || volumeProfile.PriceVolumes == null)
                return;

            if (_cachedRenderTarget != renderTarget)
            {
                ClearBrushCache();
                _cachedRenderTarget = renderTarget;
            }

            double levelSize = settings.TickSize * settings.TicksPerLevel;

            // Position profile on the right side of the chart
            float chartRight = (float)chartControl.CanvasRight;
            float profileRight = chartRight - ProfileRightMargin;
            float profileLeft = profileRight - settings.ProfileWidth;

            var profileColorValue = ((System.Windows.Media.SolidColorBrush)settings.ProfileColor).Color;
            var valueAreaColorValue = ((System.Windows.Media.SolidColorBrush)settings.ValueAreaColor).Color;
            var pocColorValue = ((System.Windows.Media.SolidColorBrush)settings.PocColor).Color;

            // Draw each price level
            foreach (var kvp in volumeProfile.PriceVolumes)
            {
                double price = kvp.Key;
                long volume = kvp.Value;

                float volumeRatio = (float)volume / volumeProfile.MaxVolume;
                float barWidth = settings.ProfileWidth * volumeRatio;

                float y1 = chartScale.GetYByValue(price + levelSize / 2);
                float y2 = chartScale.GetYByValue(price - levelSize / 2);
                float barHeight = Math.Max(1, Math.Abs(y2 - y1) - 1);

                bool isInValueArea = price >= volumeProfile.VAL && price <= volumeProfile.VAH;

                byte r, g, b;
                if (isInValueArea)
                {
                    r = (byte)(valueAreaColorValue.R * settings.ProfileOpacity);
                    g = (byte)(valueAreaColorValue.G * settings.ProfileOpacity);
                    b = (byte)(valueAreaColorValue.B * settings.ProfileOpacity);
                }
                else
                {
                    float dimOpacity = settings.ProfileOpacity * 0.5f;
                    r = (byte)(profileColorValue.R * dimOpacity);
                    g = (byte)(profileColorValue.G * dimOpacity);
                    b = (byte)(profileColorValue.B * dimOpacity);
                }

                var brush = GetOrCreateBrush(renderTarget, r, g, b, 255);
                var rect = new SharpDX.RectangleF(
                    profileRight - barWidth,
                    Math.Min(y1, y2),
                    barWidth,
                    barHeight);
                renderTarget.FillRectangle(rect, brush);
            }

            // Draw POC, VAH, VAL lines
            DrawHorizontalLine(renderTarget, chartScale, volumeProfile.POC, profileLeft, profileRight, pocColorValue, 2f);
            DrawHorizontalLine(renderTarget, chartScale, volumeProfile.VAH, profileLeft, profileRight, valueAreaColorValue, 1f);
            DrawHorizontalLine(renderTarget, chartScale, volumeProfile.VAL, profileLeft, profileRight, valueAreaColorValue, 1f);
        }

        private void DrawHorizontalLine(
            RenderTarget renderTarget,
            ChartScale chartScale,
            double price,
            float left,
            float right,
            System.Windows.Media.Color color,
            float strokeWidth)
        {
            if (price <= 0)
                return;

            float y = chartScale.GetYByValue(price);
            var brush = GetOrCreateBrush(renderTarget, color.R, color.G, color.B, 255);
            renderTarget.DrawLine(
                new SharpDX.Vector2(left, y),
                new SharpDX.Vector2(right, y),
                brush, strokeWidth);
        }

        private SolidColorBrush GetOrCreateBrush(RenderTarget renderTarget, byte r, byte g, byte b, byte a)
        {
            uint key = ((uint)r << 24) | ((uint)g << 16) | ((uint)b << 8) | a;
            if (!_brushCache.TryGetValue(key, out var brush))
            {
                brush = new SolidColorBrush(renderTarget, new SharpDX.Color(r, g, b, a));
                _brushCache[key] = brush;
            }
            return brush;
        }

        private void ClearBrushCache()
        {
            foreach (var brush in _brushCache.Values)
                brush?.Dispose();
            _brushCache.Clear();
        }

        public void Dispose()
        {
            ClearBrushCache();
            _cachedRenderTarget = null;
        }
    }
}
