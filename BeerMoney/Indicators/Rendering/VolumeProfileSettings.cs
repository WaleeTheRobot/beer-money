using System.Windows.Media;

namespace NinjaTrader.NinjaScript.Indicators.BeerMoney.Rendering
{
    /// <summary>
    /// Immutable configuration settings for volume profile rendering.
    /// Settings are set once at construction and cannot be modified during rendering.
    /// </summary>
    public sealed class VolumeProfileSettings
    {
        public int ProfileWidth { get; }
        public int TicksPerLevel { get; }
        public double TickSize { get; }
        public float ProfileOpacity { get; }
        public Brush ProfileColor { get; }
        public Brush ValueAreaColor { get; }
        public Brush PocColor { get; }

        public VolumeProfileSettings(
            int profileWidth,
            int ticksPerLevel,
            double tickSize,
            float profileOpacity,
            Brush profileColor,
            Brush valueAreaColor,
            Brush pocColor)
        {
            ProfileWidth = profileWidth;
            TicksPerLevel = ticksPerLevel;
            TickSize = tickSize;
            ProfileOpacity = profileOpacity;
            ProfileColor = profileColor;
            ValueAreaColor = valueAreaColor;
            PocColor = pocColor;
        }
    }
}
