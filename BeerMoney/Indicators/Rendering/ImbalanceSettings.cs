using System.Windows.Media;

namespace NinjaTrader.NinjaScript.Indicators.BeerMoney.Rendering
{
    /// <summary>
    /// Immutable configuration settings for imbalance rendering.
    /// Settings are set once at construction and cannot be modified during rendering.
    /// </summary>
    public sealed class ImbalanceSettings
    {
        public int TicksPerLevel { get; }
        public double TickSize { get; }
        public double ImbalanceRatio { get; }
        public int MinImbalanceVolume { get; }
        public int ReferenceVolume { get; }
        public float ImbalanceOpacity { get; }
        public Brush BullishImbalanceColor { get; }
        public Brush BearishImbalanceColor { get; }

        public ImbalanceSettings(
            int ticksPerLevel,
            double tickSize,
            double imbalanceRatio,
            int minImbalanceVolume,
            int referenceVolume,
            float imbalanceOpacity,
            Brush bullishImbalanceColor,
            Brush bearishImbalanceColor)
        {
            TicksPerLevel = ticksPerLevel;
            TickSize = tickSize;
            ImbalanceRatio = imbalanceRatio;
            MinImbalanceVolume = minImbalanceVolume;
            ReferenceVolume = referenceVolume;
            ImbalanceOpacity = imbalanceOpacity;
            BullishImbalanceColor = bullishImbalanceColor;
            BearishImbalanceColor = bearishImbalanceColor;
        }
    }
}
