using System;
using BeerMoney.Core.Analysis.Results;
using NinjaTrader.Gui.Chart;
using SharpDX.Direct2D1;

namespace NinjaTrader.NinjaScript.Indicators.BeerMoney.Rendering
{
    /// <summary>
    /// Renders volume profile on the right side of the chart.
    /// </summary>
    public sealed class VolumeProfileRenderer
    {
        private const float ProfileRightMargin = 10f;

        public void Render(
            RenderTarget renderTarget,
            ChartControl chartControl,
            ChartScale chartScale,
            VolumeProfileResult volumeProfile,
            VolumeProfileSettings settings)
        {
            if (renderTarget == null || volumeProfile == null || !volumeProfile.IsValid || volumeProfile.PriceVolumes == null)
                return;

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

                SharpDX.Color barColor = isInValueArea
                    ? CreateDimmedColor(valueAreaColorValue, settings.ProfileOpacity)
                    : CreateDimmedColor(profileColorValue, settings.ProfileOpacity * 0.5f);

                using (var brush = new SharpDX.Direct2D1.SolidColorBrush(renderTarget, barColor))
                {
                    var rect = new SharpDX.RectangleF(
                        profileRight - barWidth,
                        Math.Min(y1, y2),
                        barWidth,
                        barHeight);
                    renderTarget.FillRectangle(rect, brush);
                }
            }

            // Draw POC, VAH, VAL lines
            DrawHorizontalLine(renderTarget, chartScale, volumeProfile.POC, profileLeft, profileRight, pocColorValue, 2f);
            DrawHorizontalLine(renderTarget, chartScale, volumeProfile.VAH, profileLeft, profileRight, valueAreaColorValue, 1f);
            DrawHorizontalLine(renderTarget, chartScale, volumeProfile.VAL, profileLeft, profileRight, valueAreaColorValue, 1f);
        }

        private SharpDX.Color CreateDimmedColor(System.Windows.Media.Color color, float opacity)
        {
            byte r = (byte)(color.R * opacity);
            byte g = (byte)(color.G * opacity);
            byte b = (byte)(color.B * opacity);
            return new SharpDX.Color(r, g, b, (byte)255);
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
            using (var brush = new SharpDX.Direct2D1.SolidColorBrush(renderTarget,
                new SharpDX.Color(color.R, color.G, color.B, (byte)255)))
            {
                renderTarget.DrawLine(
                    new SharpDX.Vector2(left, y),
                    new SharpDX.Vector2(right, y),
                    brush, strokeWidth);
            }
        }
    }
}
