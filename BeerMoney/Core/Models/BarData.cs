using System;

namespace BeerMoney.Core.Models
{
    /// <summary>
    /// Represents a single price bar with volume and volumetric order flow data.
    /// </summary>
    public sealed class BarData
    {
        public DateTime Timestamp { get; }
        public int Index { get; }
        public double Open { get; }
        public double High { get; }
        public double Low { get; }
        public double Close { get; }
        public long Volume { get; }

        // Volumetric order flow data (populated for trigger series only)
        public long BuyVolume { get; }
        public long SellVolume { get; }
        public long CumulativeDelta { get; }
        public long MaxDelta { get; }
        public long MinDelta { get; }
        public double PointOfControl { get; }

        /// <summary>
        /// Bar delta (BuyVolume - SellVolume). Positive = buyers aggressive, negative = sellers aggressive.
        /// </summary>
        public long Delta => BuyVolume - SellVolume;

        public BarData(
            DateTime timestamp,
            int index,
            double open,
            double high,
            double low,
            double close,
            long volume,
            long buyVolume = 0,
            long sellVolume = 0,
            long cumulativeDelta = 0,
            long maxDelta = 0,
            long minDelta = 0,
            double pointOfControl = 0)
        {
            Timestamp = timestamp;
            Index = index;
            Open = open;
            High = high;
            Low = low;
            Close = close;
            Volume = volume;
            BuyVolume = buyVolume;
            SellVolume = sellVolume;
            CumulativeDelta = cumulativeDelta;
            MaxDelta = maxDelta;
            MinDelta = minDelta;
            PointOfControl = pointOfControl;
        }

        /// <summary>
        /// Bar range (High - Low).
        /// </summary>
        public double Range => High - Low;

        /// <summary>
        /// Typical price (H+L+C)/3.
        /// </summary>
        public double TypicalPrice => (High + Low + Close) / 3.0;

        /// <summary>
        /// True if close is above open (bullish bar).
        /// </summary>
        public bool IsBullish => Close > Open;

        /// <summary>
        /// True if close is below open (bearish bar).
        /// </summary>
        public bool IsBearish => Close < Open;

        /// <summary>
        /// Close position within bar range (0 = closed at low, 1 = closed at high).
        /// </summary>
        public double ClosePosition => Range > 0 ? (Close - Low) / Range : 0.5;

        /// <summary>
        /// Open position within bar range (0 = opened at low, 1 = opened at high).
        /// </summary>
        public double OpenPosition => Range > 0 ? (Open - Low) / Range : 0.5;

        /// <summary>
        /// POC position within bar range (0 = POC at low, 1 = POC at high).
        /// </summary>
        public double PocPosition => Range > 0 ? (PointOfControl - Low) / Range : 0.5;

        /// <summary>
        /// Delta bias: +1 if buyers aggressive (positive delta), -1 if sellers aggressive.
        /// This is the TRUE bias of the bar, regardless of bar color.
        /// </summary>
        public int DeltaBias => Delta > 0 ? 1 : (Delta < 0 ? -1 : 0);

        /// <summary>
        /// Structural alignment (0 to 1): average of close and POC position.
        /// High value = price structure favors highs.
        /// Low value = price structure favors lows.
        /// </summary>
        public double StructuralAlignment => (ClosePosition + PocPosition) / 2.0;

        /// <summary>
        /// Combined bar score (-1 to +1).
        /// Delta determines direction (true bias), structure determines magnitude.
        /// - Delta positive + high structure = strong bullish (+0.5 to +1)
        /// - Delta positive + low structure = weak bullish (0 to +0.5) - accumulation
        /// - Delta negative + low structure = strong bearish (-1 to -0.5)
        /// - Delta negative + high structure = weak bearish (-0.5 to 0) - distribution
        /// </summary>
        public double BarScore
        {
            get
            {
                if (DeltaBias > 0)
                    return StructuralAlignment;           // 0 to +1 (bullish)
                else if (DeltaBias < 0)
                    return StructuralAlignment - 1.0;     // -1 to 0 (bearish)
                else
                    return 0;                             // neutral (no delta)
            }
        }

        /// <summary>
        /// True if delta diverges from bar direction (hidden buying/selling).
        /// Positive delta + bearish bar = hidden accumulation (buyers absorbed selling).
        /// Negative delta + bullish bar = hidden distribution (sellers absorbed buying).
        /// </summary>
        public bool IsDivergent => (Delta > 0 && IsBearish) || (Delta < 0 && IsBullish);
    }
}
