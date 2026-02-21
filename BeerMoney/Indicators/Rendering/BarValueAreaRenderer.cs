using System;
using System.Collections.Generic;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Data;
using SharpDX.Direct2D1;

namespace NinjaTrader.NinjaScript.Indicators.BeerMoney.Rendering
{
    /// <summary>
    /// Renders per-trigger-bar POC line (gold) and value area rectangle (VAH to VAL)
    /// aligned to each trigger bar's x-position and width on the chart.
    /// </summary>
    public sealed class BarValueAreaRenderer : IDisposable
    {
        private const int MaxCacheSize = 2000;

        private readonly BrushCache _brushCache = new BrushCache();

        private readonly Dictionary<int, BarValueAreaData> _cache = new Dictionary<int, BarValueAreaData>();
        private int _minCachedIndex = int.MaxValue;

        public struct BarValueAreaData
        {
            public double POC;
            public double VAH;
            public double VAL;
        }

        public void CacheBar(int triggerBarIndex, double poc, double vah, double val)
        {
            _cache[triggerBarIndex] = new BarValueAreaData { POC = poc, VAH = vah, VAL = val };

            if (triggerBarIndex < _minCachedIndex)
                _minCachedIndex = triggerBarIndex;

            if (_cache.Count > MaxCacheSize)
                EvictOldEntries(triggerBarIndex);
        }

        private void EvictOldEntries(int currentIndex)
        {
            int threshold = currentIndex - MaxCacheSize;
            var keysToRemove = new List<int>();
            foreach (var key in _cache.Keys)
            {
                if (key < threshold)
                    keysToRemove.Add(key);
            }
            foreach (var key in keysToRemove)
                _cache.Remove(key);
            _minCachedIndex = threshold;
        }

        public void Render(
            RenderTarget renderTarget,
            ChartControl chartControl,
            ChartScale chartScale,
            ChartBars chartBars,
            Bars[] barsArray,
            int primarySeries,
            int triggerSeries,
            int[] currentBars,
            TimeSeries[] times,
            float valueAreaOpacity,
            float widthPadding = 0f,
            System.Windows.Media.Brush vaColorBrush = null,
            System.Windows.Media.Brush pocColorBrush = null)
        {
            if (renderTarget == null || chartBars == null || _cache.Count == 0)
                return;

            float halfBarWidth = (float)(chartControl.BarWidth / 2.0);
            double visibleMinPrice = chartScale.MinValue;
            double visibleMaxPrice = chartScale.MaxValue;

            var pocMediaColor = pocColorBrush != null
                ? ((System.Windows.Media.SolidColorBrush)pocColorBrush).Color
                : System.Windows.Media.Colors.Gold;
            var pocBrush = _brushCache.GetOrCreate(renderTarget, pocMediaColor.R, pocMediaColor.G, pocMediaColor.B, 255);

            var vaMediaColor = vaColorBrush != null
                ? ((System.Windows.Media.SolidColorBrush)vaColorBrush).Color
                : System.Windows.Media.Colors.CornflowerBlue;
            byte vaAlpha = (byte)(255 * Math.Max(0.05f, Math.Min(1.0f, valueAreaOpacity)));
            var vaBrush = _brushCache.GetOrCreate(renderTarget, vaMediaColor.R, vaMediaColor.G, vaMediaColor.B, vaAlpha);

            int fromIdx = Math.Max(0, chartBars.FromIndex);
            int toIdx = Math.Min(chartBars.ToIndex, currentBars[primarySeries]);
            if (fromIdx > toIdx) return;

            DateTime firstVisibleTime = times[primarySeries].GetValueAt(fromIdx);
            DateTime lastVisibleTime = times[primarySeries].GetValueAt(toIdx);

            int firstTriggerIdx = barsArray[triggerSeries].GetBar(firstVisibleTime);
            int lastTriggerIdx = barsArray[triggerSeries].GetBar(lastVisibleTime);

            firstTriggerIdx = Math.Max(0, firstTriggerIdx);
            lastTriggerIdx = Math.Min(lastTriggerIdx, currentBars[triggerSeries]);

            if (firstTriggerIdx > lastTriggerIdx)
                return;

            bool isTick = chartBars.Bars.BarsPeriod.BarsPeriodType == BarsPeriodType.Tick;

            for (int triggerBarIdx = firstTriggerIdx; triggerBarIdx <= lastTriggerIdx; triggerBarIdx++)
            {
                if (!_cache.TryGetValue(triggerBarIdx, out var data))
                    continue;

                if (data.VAL > visibleMaxPrice || data.VAH < visibleMinPrice)
                    continue;

                int primaryBarIdx;
                if (isTick)
                {
                    int offset = currentBars[primarySeries] - currentBars[triggerSeries];
                    primaryBarIdx = triggerBarIdx + offset;
                }
                else
                {
                    DateTime barTime = times[triggerSeries].GetValueAt(triggerBarIdx);
                    primaryBarIdx = barsArray[primarySeries].GetBar(barTime);
                }

                if (primaryBarIdx < fromIdx || primaryBarIdx > toIdx)
                    continue;

                float barX = chartControl.GetXByBarIndex(chartBars, primaryBarIdx);
                float left = barX - halfBarWidth - widthPadding;
                float right = barX + halfBarWidth + widthPadding;
                float barWidth = right - left;
                if (barWidth < 1f) barWidth = 1f;

                if (data.VAH > 0 && data.VAL > 0 && data.VAH > data.VAL)
                {
                    float yTop = chartScale.GetYByValue(data.VAH);
                    float yBot = chartScale.GetYByValue(data.VAL);
                    float rectHeight = Math.Max(1f, yBot - yTop);

                    var rect = new SharpDX.RectangleF(left, yTop, barWidth, rectHeight);
                    renderTarget.DrawRectangle(rect, vaBrush, 2f);
                }

                if (data.POC > 0 && data.POC >= visibleMinPrice && data.POC <= visibleMaxPrice)
                {
                    float yPoc = chartScale.GetYByValue(data.POC);
                    renderTarget.DrawLine(
                        new SharpDX.Vector2(left, yPoc),
                        new SharpDX.Vector2(right, yPoc),
                        pocBrush, 2f);
                }
            }
        }

        public void Dispose()
        {
            _brushCache.Dispose();
            _cache.Clear();
            _minCachedIndex = int.MaxValue;
        }
    }
}
