using System;
using BeerMoney.Core.Analysis;
using BeerMoney.Core.Collections;
using BeerMoney.Core.Models;

namespace NinjaTrader.NinjaScript.Indicators.BeerMoney
{
    /// <summary>
    /// Manages bar data collection and analysis for multiple data series.
    /// Handles Base (ATR), Bias (slow VWAP), and Trigger (fast VWAP) series.
    /// Enhanced with EMA tracking, cluster tracker, and trigger delta EWM for dashboard features.
    /// </summary>
    public sealed class DataSeriesManager
    {
        private const double OldestBarWeightRatio = 0.05;

        private readonly int _period;
        private readonly int _biasSmoothing;
        private readonly int _slowEmaPeriod;
        private readonly int _fastEmaPeriod;
        private readonly Action<string> _log;

        private CircularBuffer<BarData> _baseBars;
        private CircularBuffer<BarData> _biasBars;
        private CircularBuffer<BarData> _triggerBars;

        private double _emaMultiplier;
        private double _smoothedBiasVwap;
        private bool _smoothedBiasVwapInitialized;

        private double _lastBiasClose;
        private bool _biasVwapNeedsUpdate;

        // Bias EMA (slow/fast)
        private double _biasSlowEma;
        private double _biasFastEma;
        private int _biasBarCount;

        // Imbalance cluster tracking
        private ImbalanceClusterTracker _clusterTracker;
        private int _clusterLookback;
        private double _clusterBucketSize;

        // Trigger delta EWM
        private double _triggerDeltaEwm;
        private bool _triggerDeltaEwmInitialized;
        private const double EWM_ALPHA = 2.0 / (5.0 + 1.0);

        // Current trigger bar for painting
        private BarData _currentTriggerBar;

        // Bias bar tracking
        public double LastBiasBarClose { get; private set; }
        public double LastBiasBarOpen { get; private set; }
        public double LastBiasBarHigh { get; private set; }
        public double LastBiasBarLow { get; private set; }

        public DataSeriesManager(int period, int biasSmoothing, int clusterLookback = 5, double clusterBucketSize = 2.0,
            int slowEmaPeriod = 9, int fastEmaPeriod = 5, Action<string> log = null)
        {
            _period = period;
            _biasSmoothing = biasSmoothing;
            _clusterLookback = clusterLookback;
            _clusterBucketSize = clusterBucketSize;
            _slowEmaPeriod = slowEmaPeriod;
            _fastEmaPeriod = fastEmaPeriod;
            _log = log ?? (_ => { });
        }

        public double BaseAtr { get; private set; }
        public double BiasVwap { get; private set; }
        public double TriggerVwap { get; private set; }
        public double DeltaEfficiency { get; private set; }
        public double BiasSlowEma => _biasSlowEma;
        public double BiasFastEma => _biasFastEma;
        public BarData CurrentTriggerBar => _currentTriggerBar;
        public CircularBuffer<BarData> TriggerBars => _triggerBars;
        public ImbalanceClusterTracker ClusterTracker => _clusterTracker;
        public double TriggerDeltaEwm => _triggerDeltaEwm;

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
            _biasBarCount = 0;
            _biasSlowEma = 0;
            _biasFastEma = 0;

            _clusterTracker = new ImbalanceClusterTracker(_clusterLookback, _clusterBucketSize);
            _triggerDeltaEwmInitialized = false;
        }

        public void Cleanup()
        {
            _baseBars = null;
            _biasBars = null;
            _triggerBars = null;
            _currentTriggerBar = null;
            _clusterTracker = null;
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

            LastBiasBarClose = bar.Close;
            LastBiasBarOpen = bar.Open;
            LastBiasBarHigh = bar.High;
            LastBiasBarLow = bar.Low;

            // Compute Bias EMAs
            _biasBarCount++;
            double slowMult = 2.0 / (_slowEmaPeriod + 1);
            double fastMult = 2.0 / (_fastEmaPeriod + 1);
            if (_biasBarCount == 1)
            {
                _biasSlowEma = bar.Close;
                _biasFastEma = bar.Close;
            }
            else
            {
                _biasSlowEma = (bar.Close - _biasSlowEma) * slowMult + _biasSlowEma;
                _biasFastEma = (bar.Close - _biasFastEma) * fastMult + _biasFastEma;
            }

            // Feed imbalance zones to cluster tracker
            if (_clusterTracker != null)
                _clusterTracker.AddBar(bar.High, bar.Low, bar.BullishImbalanceCount, bar.BearishImbalanceCount);
        }

        public void ProcessTriggerBar(BarData bar, double currentTriggerClose, double currentBiasClose)
        {
            _triggerBars.Add(bar);
            _currentTriggerBar = bar;

            var vwapResult = VwapAnalysis.Calculate(_triggerBars, currentTriggerClose);
            if (vwapResult.IsValid)
                TriggerVwap = vwapResult.Vwap;

            const double priceChangeTolerance = 0.0001;
            if (_biasVwapNeedsUpdate || Math.Abs(currentBiasClose - _lastBiasClose) > priceChangeTolerance)
            {
                BiasVwap = CalculateSmoothedBiasVwap(currentBiasClose);
                _lastBiasClose = currentBiasClose;
                _biasVwapNeedsUpdate = false;
            }

            CalculateDeltaEfficiency();
        }

        /// <summary>
        /// Updates feature-related state from a completed trigger bar.
        /// </summary>
        public void ProcessCompletedTriggerBarFeatures(BarData completedBar)
        {
            if (!_triggerDeltaEwmInitialized)
            {
                _triggerDeltaEwm = completedBar.Delta;
                _triggerDeltaEwmInitialized = true;
            }
            else
            {
                _triggerDeltaEwm = EWM_ALPHA * completedBar.Delta + (1 - EWM_ALPHA) * _triggerDeltaEwm;
            }
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

            double decayFactor = n > 1 ? Math.Pow(OldestBarWeightRatio, 1.0 / (n - 1)) : 1.0;

            for (int i = 0; i < n; i++)
            {
                var bar = _triggerBars[i];
                if (bar != null)
                {
                    double weight = Math.Pow(decayFactor, n - 1 - i);
                    weightedSumDelta += bar.Delta * weight;
                    weightedSumAbsDelta += Math.Abs(bar.Delta) * weight;
                }
            }

            DeltaEfficiency = weightedSumAbsDelta > 0 ? Math.Abs(weightedSumDelta) / weightedSumAbsDelta * 100.0 : 0;
        }

        private double CalculateSmoothedBiasVwap(double currentBiasClose)
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

        public BarData[] GetTriggerBars()
        {
            if (_triggerBars == null || _triggerBars.Count == 0)
                return Array.Empty<BarData>();

            var bars = new BarData[_triggerBars.Count];
            for (int i = 0; i < _triggerBars.Count; i++)
                bars[i] = _triggerBars[i];
            return bars;
        }
    }
}
