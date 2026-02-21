namespace BeerMoney.Core.Analysis.Results
{
    /// <summary>
    /// Immutable result of rolling-window order flow metrics computation.
    /// One instance per timeframe (trigger or bias).
    /// </summary>
    public sealed class OrderFlowMetricsResult
    {
        // POC Migration
        public double PocMigration { get; }
        public int PocDirection { get; }
        public double PocTrendStrength { get; }

        // Value Area
        public double VaOverlap { get; }
        public int VaMigration { get; }
        public double VaWidth { get; }
        public bool IsCompressing { get; }
        public double CompressionRate { get; }

        // Imbalance
        public double ImbalancePolarity { get; }
        public bool IsPolarized { get; }
        public double SetupDensity { get; }

        // VWAP
        public double VwapSlope { get; }
        public int VwapRegime { get; }

        // Delta
        public double RollingDelta { get; }
        public int RollingDeltaDirection { get; }
        public double RollingDeltaMomentum { get; }

        // Volume
        public double VolumeTrend { get; }

        // Agreement
        public bool PocVwapAgreement { get; }

        // Conviction
        public int ConvictionScore { get; }
        public int ConvictionDirection { get; }

        public bool IsValid { get; }

        private OrderFlowMetricsResult(
            double pocMigration, int pocDirection, double pocTrendStrength,
            double vaOverlap, int vaMigration, double vaWidth, bool isCompressing, double compressionRate,
            double imbalancePolarity, bool isPolarized, double setupDensity,
            double vwapSlope, int vwapRegime,
            double rollingDelta, int rollingDeltaDirection, double rollingDeltaMomentum,
            double volumeTrend,
            bool pocVwapAgreement,
            int convictionScore, int convictionDirection,
            bool isValid)
        {
            PocMigration = pocMigration;
            PocDirection = pocDirection;
            PocTrendStrength = pocTrendStrength;
            VaOverlap = vaOverlap;
            VaMigration = vaMigration;
            VaWidth = vaWidth;
            IsCompressing = isCompressing;
            CompressionRate = compressionRate;
            ImbalancePolarity = imbalancePolarity;
            IsPolarized = isPolarized;
            SetupDensity = setupDensity;
            VwapSlope = vwapSlope;
            VwapRegime = vwapRegime;
            RollingDelta = rollingDelta;
            RollingDeltaDirection = rollingDeltaDirection;
            RollingDeltaMomentum = rollingDeltaMomentum;
            VolumeTrend = volumeTrend;
            PocVwapAgreement = pocVwapAgreement;
            ConvictionScore = convictionScore;
            ConvictionDirection = convictionDirection;
            IsValid = isValid;
        }

        public static OrderFlowMetricsResult Create(
            double pocMigration, int pocDirection, double pocTrendStrength,
            double vaOverlap, int vaMigration, double vaWidth, bool isCompressing, double compressionRate,
            double imbalancePolarity, bool isPolarized, double setupDensity,
            double vwapSlope, int vwapRegime,
            double rollingDelta, int rollingDeltaDirection, double rollingDeltaMomentum,
            double volumeTrend,
            bool pocVwapAgreement,
            int convictionScore, int convictionDirection)
        {
            return new OrderFlowMetricsResult(
                pocMigration, pocDirection, pocTrendStrength,
                vaOverlap, vaMigration, vaWidth, isCompressing, compressionRate,
                imbalancePolarity, isPolarized, setupDensity,
                vwapSlope, vwapRegime,
                rollingDelta, rollingDeltaDirection, rollingDeltaMomentum,
                volumeTrend,
                pocVwapAgreement,
                convictionScore, convictionDirection,
                true);
        }

        public static OrderFlowMetricsResult Invalid()
        {
            return new OrderFlowMetricsResult(0, 0, 0, 0, 0, 0, false, 0, 0, false, 0, 0, 0, 0, 0, 0, 0, false, 0, 0, false);
        }
    }
}
