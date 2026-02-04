using System;
using BeerMoney.Core.Analysis;
using BeerMoney.Core.Collections;
using BeerMoney.Core.Models;

namespace NinjaTrader.NinjaScript.Indicators.BeerMoney
{
    /// <summary>
    /// Manages bar data collection and analysis for multiple data series.
    /// Handles Base (ATR), Bias (slow VWAP), and Trigger (fast VWAP) series.
    /// </summary>
    public sealed class DataSeriesManager
    {
        /// <summary>
        /// Decay factor for delta efficiency weighting (0.85 = each older bar has 85% the weight of the next newer).
        /// Higher values (closer to 1.0) = more equal weighting, lower values = stronger recency bias.
        /// </summary>
        private const double DeltaEfficiencyDecayFactor = 0.85;

        private readonly int _period;
        private readonly int _biasSmoothing;
        private readonly Action<string> _log;

        private CircularBuffer<BarData> _baseBars;
        private CircularBuffer<BarData> _biasBars;
        private CircularBuffer<BarData> _triggerBars;

        private double _emaMultiplier;
        private double _smoothedBiasVwap;
        private bool _smoothedBiasVwapInitialized;

        // Track the last bias close used for VWAP calculation to avoid redundant updates
        private double _lastBiasClose;
        private bool _biasVwapNeedsUpdate;

        // Current trigger bar for painting
        private BarData _currentTriggerBar;

        public DataSeriesManager(int period, int biasSmoothing, Action<string> log = null)
        {
            _period = period;
            _biasSmoothing = biasSmoothing;
            _log = log ?? (_ => { });
        }

        public double BaseAtr { get; private set; }
        public double BiasVwap { get; private set; }
        public double TriggerVwap { get; private set; }

        /// <summary>
        /// Gets the delta efficiency (0-100%). |Weighted sum of deltas| / Weighted sum of |deltas|.
        /// Uses exponential decay weighting so newer bars have more influence on the calculation.
        /// High values indicate trending/directional flow, low values indicate choppy/oscillating.
        /// </summary>
        public double DeltaEfficiency { get; private set; }

        /// <summary>
        /// Gets the current trigger bar data for painting decisions.
        /// </summary>
        public BarData CurrentTriggerBar => _currentTriggerBar;

        public void Initialize()
        {
            int bufferCapacity = _period + 1;
            _baseBars = new CircularBuffer<BarData>(bufferCapacity);
            _biasBars = new CircularBuffer<BarData>(bufferCapacity);
            _triggerBars = new CircularBuffer<BarData>(bufferCapacity);

            _emaMultiplier = 2.0 / (_biasSmoothing + 1);
            _smoothedBiasVwap = 0;
            _smoothedBiasVwapInitialized = false;
            _lastBiasClose = 0;
            _biasVwapNeedsUpdate = false;
        }

        public void Cleanup()
        {
            _baseBars = null;
            _biasBars = null;
            _triggerBars = null;
            _currentTriggerBar = null;
        }

        public void ProcessBaseBar(BarData bar)
        {
            _baseBars.Add(bar);

            var atrResult = AtrAnalysis.Calculate(_baseBars, _period);
            if (atrResult.IsValid)
                BaseAtr = atrResult.CurrentAtr;
        }

        public void ProcessBiasBar(BarData bar, double currentBiasClose)
        {
            _biasBars.Add(bar);
            _biasVwapNeedsUpdate = true;
            BiasVwap = CalculateSmoothedBiasVwap(currentBiasClose);
            _lastBiasClose = currentBiasClose;
        }

        public void ProcessTriggerBar(BarData bar, double currentTriggerClose, double currentBiasClose)
        {
            _triggerBars.Add(bar);
            _currentTriggerBar = bar;

            var vwapResult = VwapAnalysis.Calculate(_triggerBars, currentTriggerClose);
            if (vwapResult.IsValid)
                TriggerVwap = vwapResult.Vwap;

            // Only update bias VWAP when bias bars have changed or bias close price has changed significantly
            // This avoids recalculating with stale bias bar data
            const double priceChangeTolerance = 0.0001;
            if (_biasVwapNeedsUpdate || Math.Abs(currentBiasClose - _lastBiasClose) > priceChangeTolerance)
            {
                BiasVwap = CalculateSmoothedBiasVwap(currentBiasClose);
                _lastBiasClose = currentBiasClose;
                _biasVwapNeedsUpdate = false;
            }

            // Calculate delta efficiency
            CalculateDeltaEfficiency();
        }

        private void CalculateDeltaEfficiency()
        {
            if (_triggerBars == null || _triggerBars.Count == 0)
            {
                DeltaEfficiency = 0;
                return;
            }

            double weightedSumDelta = 0;
            double weightedSumAbsDelta = 0;
            int n = _triggerBars.Count;

            // Apply exponential decay weighting: newer bars have more influence
            // Index 0 = oldest, Index n-1 = newest
            // Weight for bar at index i = decayFactor^(n - 1 - i)
            // Newest bar gets weight 1.0, each older bar gets decayFactor times less
            for (int i = 0; i < n; i++)
            {
                var bar = _triggerBars[i];
                if (bar != null)
                {
                    // Calculate weight: newest bar (i = n-1) gets weight 1.0
                    double weight = Math.Pow(DeltaEfficiencyDecayFactor, n - 1 - i);

                    weightedSumDelta += bar.Delta * weight;
                    weightedSumAbsDelta += Math.Abs(bar.Delta) * weight;
                }
            }

            // Delta Efficiency: |weighted net movement| / weighted total activity
            // 100% = all deltas same direction (trending), 0% = deltas canceling out (choppy)
            // Newer bars contribute more to the calculation due to decay weighting
            DeltaEfficiency = weightedSumAbsDelta > 0 ? Math.Abs(weightedSumDelta) / weightedSumAbsDelta * 100.0 : 0;
        }

        public double CalculateSmoothedBiasVwap(double currentBiasClose)
        {
            if (_biasBars == null || _biasBars.Count == 0)
                return 0;

            var vwapResult = VwapAnalysis.Calculate(_biasBars, currentBiasClose);
            if (!vwapResult.IsValid)
                return 0;

            double rawVwap = vwapResult.Vwap;

            _smoothedBiasVwap = _smoothedBiasVwapInitialized
                ? (rawVwap * _emaMultiplier) + (_smoothedBiasVwap * (1 - _emaMultiplier))
                : rawVwap;
            _smoothedBiasVwapInitialized = true;

            return _smoothedBiasVwap;
        }

        /// <summary>
        /// Gets the bar data from the trigger bar collection (oldest to newest).
        /// </summary>
        public BarData[] GetTriggerBars()
        {
            if (_triggerBars == null || _triggerBars.Count == 0)
                return Array.Empty<BarData>();

            var bars = new BarData[_triggerBars.Count];
            for (int i = 0; i < _triggerBars.Count; i++)
            {
                bars[i] = _triggerBars[i];
            }
            return bars;
        }
    }
}
