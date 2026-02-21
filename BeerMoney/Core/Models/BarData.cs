using System;

namespace BeerMoney.Core.Models
{
    /// <summary>
    /// Imbalance analysis data for a single bar, grouped to avoid large parameter lists.
    /// </summary>
    public struct ImbalanceMetrics
    {
        public int BullishCount;
        public int BearishCount;
        public long BullishVolumeSum;
        public long BearishVolumeSum;
        public double BullishAvgPosition;
        public double BearishAvgPosition;
        public double MaxBullishPrice;
        public long MaxBullishVolume;
        public double MaxBearishPrice;
        public long MaxBearishVolume;
    }

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

        // Imbalance metrics
        public int BullishImbalanceCount { get; }
        public int BearishImbalanceCount { get; }
        public long BullishImbVolumeSum { get; }
        public long BearishImbVolumeSum { get; }
        // Average imbalance position within bar range, normalized to [-1, +1]
        public double BullishImbalanceAvgPosition { get; }
        public double BearishImbalanceAvgPosition { get; }
        public double MaxBullishImbPrice { get; }
        public long MaxBullishImbVolume { get; }
        public double MaxBearishImbPrice { get; }
        public long MaxBearishImbVolume { get; }

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
            double pointOfControl = 0,
            ImbalanceMetrics imbalance = default)
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
            BullishImbalanceCount = imbalance.BullishCount;
            BearishImbalanceCount = imbalance.BearishCount;
            MaxBullishImbPrice = imbalance.MaxBullishPrice;
            MaxBullishImbVolume = imbalance.MaxBullishVolume;
            MaxBearishImbPrice = imbalance.MaxBearishPrice;
            MaxBearishImbVolume = imbalance.MaxBearishVolume;
            BullishImbVolumeSum = imbalance.BullishVolumeSum;
            BearishImbVolumeSum = imbalance.BearishVolumeSum;
            BullishImbalanceAvgPosition = imbalance.BullishAvgPosition;
            BearishImbalanceAvgPosition = imbalance.BearishAvgPosition;
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
        /// </summary>
        public double BarScore
        {
            get
            {
                if (DeltaBias > 0)
                    return StructuralAlignment;
                else if (DeltaBias < 0)
                    return StructuralAlignment - 1.0;
                else
                    return 0;
            }
        }

        /// <summary>
        /// Body as fraction of total range (0 = doji, 1 = full body). |Close - Open| / Range.
        /// </summary>
        public double BodyPercent => Range > 0 ? Math.Abs(Close - Open) / Range : 0;

        /// <summary>
        /// True if delta diverges from bar direction (hidden buying/selling).
        /// </summary>
        public bool IsDivergent => (Delta > 0 && IsBearish) || (Delta < 0 && IsBullish);
    }
}
