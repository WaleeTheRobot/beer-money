using System;
using System.Collections.Generic;
using SharpDX.Direct2D1;

namespace NinjaTrader.NinjaScript.Indicators.BeerMoney.Rendering
{
    /// <summary>
    /// Caches SharpDX SolidColorBrush instances by RGBA key, auto-clearing when the render target changes.
    /// Shared by ImbalanceRenderer and BarValueAreaRenderer.
    /// </summary>
    internal sealed class BrushCache : IDisposable
    {
        private readonly Dictionary<uint, SolidColorBrush> _brushes = new Dictionary<uint, SolidColorBrush>();
        private RenderTarget _cachedRenderTarget;

        public SolidColorBrush GetOrCreate(RenderTarget renderTarget, byte r, byte g, byte b, byte a)
        {
            if (_cachedRenderTarget != renderTarget)
            {
                Clear();
                _cachedRenderTarget = renderTarget;
            }

            uint key = ((uint)r << 24) | ((uint)g << 16) | ((uint)b << 8) | a;
            if (!_brushes.TryGetValue(key, out var brush))
            {
                brush = new SolidColorBrush(renderTarget, new SharpDX.Color(r, g, b, a));
                _brushes[key] = brush;
            }
            return brush;
        }

        public void Clear()
        {
            foreach (var brush in _brushes.Values)
                brush?.Dispose();
            _brushes.Clear();
        }

        public void Dispose()
        {
            Clear();
            _cachedRenderTarget = null;
        }
    }
}
