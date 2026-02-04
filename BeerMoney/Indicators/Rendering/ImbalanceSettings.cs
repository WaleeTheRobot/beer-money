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
        public int HighVolumeThreshold { get; }
        public float ImbalanceOpacity { get; }
        public Brush BullishImbalanceColor { get; }
        public Brush BearishImbalanceColor { get; }
        public Brush HighVolumeBullishColor { get; }
        public Brush HighVolumeBearishColor { get; }

        public ImbalanceSettings(
            int ticksPerLevel,
            double tickSize,
            double imbalanceRatio,
            int minImbalanceVolume,
            int highVolumeThreshold,
            float imbalanceOpacity,
            Brush bullishImbalanceColor,
            Brush bearishImbalanceColor,
            Brush highVolumeBullishColor,
            Brush highVolumeBearishColor)
        {
            TicksPerLevel = ticksPerLevel;
            TickSize = tickSize;
            ImbalanceRatio = imbalanceRatio;
            MinImbalanceVolume = minImbalanceVolume;
            HighVolumeThreshold = highVolumeThreshold;
            ImbalanceOpacity = imbalanceOpacity;
            BullishImbalanceColor = bullishImbalanceColor;
            BearishImbalanceColor = bearishImbalanceColor;
            HighVolumeBullishColor = highVolumeBullishColor;
            HighVolumeBearishColor = highVolumeBearishColor;
        }
    }
}
