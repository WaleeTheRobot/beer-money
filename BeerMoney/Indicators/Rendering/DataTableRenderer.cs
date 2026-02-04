using System;
using NinjaTrader.Gui.Chart;
using SharpDX.Direct2D1;

namespace NinjaTrader.NinjaScript.Indicators.BeerMoney.Rendering
{
    /// <summary>
    /// Immutable data values to display in the table.
    /// </summary>
    public sealed class DataTableValues
    {
        public double BiasVwap { get; }
        public double TriggerVwap { get; }
        public double BaseAtr { get; }
        public double DeltaEfficiency { get; }

        public DataTableValues(double biasVwap, double triggerVwap, double baseAtr, double deltaEfficiency)
        {
            BiasVwap = biasVwap;
            TriggerVwap = triggerVwap;
            BaseAtr = baseAtr;
            DeltaEfficiency = deltaEfficiency;
        }
    }

    /// <summary>
    /// Renders the data table overlay showing VWAP, ATR, and delta efficiency.
    /// </summary>
    public sealed class DataTableRenderer : IDisposable
    {
        private const float TablePadding = 8f;
        private const float TableWidth = 160f;
        private const float TableLineSpacing = 4f;
        private const float ChartMargin = 10f;

        private SharpDX.DirectWrite.TextFormat _tableTextFormat;
        private SharpDX.DirectWrite.Factory _dwFactory;
        private bool _disposed;

        public void Initialize(int fontSize)
        {
            _dwFactory = new SharpDX.DirectWrite.Factory();
            _tableTextFormat = new SharpDX.DirectWrite.TextFormat(_dwFactory, "Consolas",
                SharpDX.DirectWrite.FontWeight.Normal,
                SharpDX.DirectWrite.FontStyle.Normal,
                fontSize);
        }

        public void Render(
            RenderTarget renderTarget,
            ChartControl chartControl,
            ChartScale chartScale,
            DataTableValues values,
            DataTableSettings settings)
        {
            if (renderTarget == null || _tableTextFormat == null)
                return;

            double vwapDiff = (values.BiasVwap > 0 && values.TriggerVwap > 0) ? values.TriggerVwap - values.BiasVwap : 0;

            string line1 = string.Format("VWAP Diff: {0:+0.00;-0.00;0.00}", vwapDiff);
            string line2 = string.Format("Base ATR:  {0:0.00}", values.BaseAtr);
            string line3 = string.Format("Delta Eff: {0:0}%", values.DeltaEfficiency);

            float lineHeight = settings.FontSize + TableLineSpacing;
            float tableHeight = lineHeight * 3 + TablePadding * 2;

            var (tableX, tableY) = CalculatePosition(chartControl, chartScale, settings, tableHeight);

            DrawBackground(renderTarget, tableX, tableY, tableHeight);
            DrawText(renderTarget, tableX, tableY, lineHeight, line1, line2, line3, values.DeltaEfficiency);
        }

        private (float x, float y) CalculatePosition(
            ChartControl chartControl,
            ChartScale chartScale,
            DataTableSettings settings,
            float tableHeight)
        {
            float tableX, tableY;
            float profileOffset = settings.ShowVolumeProfile ? settings.ProfileWidth + 20 : 0;

            switch (settings.Position)
            {
                case TablePosition.TopLeft:
                    tableX = ChartMargin;
                    tableY = ChartMargin;
                    break;
                case TablePosition.TopRight:
                    tableX = (float)chartControl.CanvasRight - TableWidth - ChartMargin - profileOffset;
                    tableY = ChartMargin;
                    break;
                case TablePosition.BottomLeft:
                    tableX = ChartMargin;
                    tableY = (float)chartScale.GetYByValue(chartScale.MinValue) - tableHeight - ChartMargin;
                    break;
                case TablePosition.BottomRight:
                default:
                    tableX = (float)chartControl.CanvasRight - TableWidth - ChartMargin - profileOffset;
                    tableY = (float)chartScale.GetYByValue(chartScale.MinValue) - tableHeight - ChartMargin;
                    break;
            }

            return (tableX + settings.OffsetX, tableY + settings.OffsetY);
        }

        private void DrawBackground(RenderTarget renderTarget, float tableX, float tableY, float tableHeight)
        {
            var bgRect = new SharpDX.RectangleF(tableX, tableY, TableWidth, tableHeight);

            using (var bgBrush = new SharpDX.Direct2D1.SolidColorBrush(renderTarget,
                new SharpDX.Color(0, 0, 0, 200)))
            {
                renderTarget.FillRectangle(bgRect, bgBrush);
            }

            using (var borderBrush = new SharpDX.Direct2D1.SolidColorBrush(renderTarget,
                new SharpDX.Color(80, 80, 80, 255)))
            {
                renderTarget.DrawRectangle(bgRect, borderBrush, 1f);
            }
        }

        private void DrawText(
            RenderTarget renderTarget,
            float tableX,
            float tableY,
            float lineHeight,
            string line1,
            string line2,
            string line3,
            double deltaEfficiency)
        {
            float textX = tableX + TablePadding;
            float textY = tableY + TablePadding;
            float textWidth = TableWidth - TablePadding * 2;

            // Draw lines 1 and 2 in default color
            using (var textBrush = new SharpDX.Direct2D1.SolidColorBrush(renderTarget,
                new SharpDX.Color(220, 220, 220, 255)))
            {
                var line1Rect = new SharpDX.RectangleF(textX, textY, textWidth, lineHeight);
                renderTarget.DrawText(line1, _tableTextFormat, line1Rect, textBrush);

                var line2Rect = new SharpDX.RectangleF(textX, textY + lineHeight, textWidth, lineHeight);
                renderTarget.DrawText(line2, _tableTextFormat, line2Rect, textBrush);
            }

            // Draw line 3 (Delta Eff) with color based on value
            SharpDX.Color effColor = GetEfficiencyColor(deltaEfficiency);

            using (var effBrush = new SharpDX.Direct2D1.SolidColorBrush(renderTarget, effColor))
            {
                var line3Rect = new SharpDX.RectangleF(textX, textY + lineHeight * 2, textWidth, lineHeight);
                renderTarget.DrawText(line3, _tableTextFormat, line3Rect, effBrush);
            }
        }

        private SharpDX.Color GetEfficiencyColor(double deltaEfficiency)
        {
            // Low (0-30%) = Cyan (choppy), Medium (30-60%) = Yellow, High (60%+) = Orange (trending)
            if (deltaEfficiency < 30)
                return new SharpDX.Color(0, 220, 220, 255);
            if (deltaEfficiency < 60)
                return new SharpDX.Color(220, 220, 100, 255);
            return new SharpDX.Color(255, 165, 0, 255);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                // Dispose managed resources
                if (_tableTextFormat != null)
                {
                    _tableTextFormat.Dispose();
                    _tableTextFormat = null;
                }
                if (_dwFactory != null)
                {
                    _dwFactory.Dispose();
                    _dwFactory = null;
                }
            }

            _disposed = true;
        }

        ~DataTableRenderer()
        {
            Dispose(false);
        }
    }
}
