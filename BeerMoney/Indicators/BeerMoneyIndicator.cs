using System;
using System.Collections.Generic;
using System.Windows.Media;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.BarsTypes;
using NinjaTrader.NinjaScript.Indicators;
using BeerMoney.Core.Analysis;
using BeerMoney.Core.Analysis.Results;
using BeerMoney.Core.Models;
using BeerMoney.Core.Network;
using BeerMoney.Core.Trading;
using NinjaTrader.NinjaScript.Indicators.BeerMoney.Rendering;
using SharpDX;
using SharpDX.Direct2D1;

namespace NinjaTrader.NinjaScript.Indicators.BeerMoney
{
    public enum BarTypeOptions
    {
        Minute,
        Range,
        Second,
        Tick,
        Volume
    }

    /// <summary>
    /// Beer Money - VWAP indicator with order flow analysis and dashboard WebSocket broadcast.
    /// 4 data series: Primary, Base (ATR), Bias (slow VWAP), Trigger (fast VWAP).
    /// On each completed trigger bar, computes all features and broadcasts via WebSocket.
    /// </summary>
    public partial class BeerMoneyIndicator : Indicator
    {
        // Data series indices
        private const int PrimarySeries = 0;
        private const int BaseSeries = 1;    // Tick bars for ATR
        private const int BiasSeries = 2;    // Volumetric tick bars for bias VWAP and volume profile
        private const int TriggerSeries = 3; // Volumetric tick bars for trigger VWAP

        // Data series management
        private DataSeriesManager _dataSeriesManager;

        // Bar count tracking
        private int _lastBaseBarCount = -1;
        private int _lastBiasBarCount = -1;
        private int _lastTriggerBarCount = -1;

        // Bar painting colors for divergent bars only
        private Brush _divergentBullish;
        private Brush _divergentBearish;

        // Volume profile
        private VolumeProfileResult _volumeProfile;
        private Dictionary<double, long> _profileVolumes;

        // Per-bar value area (shared dictionary, reused per call)
        private double _lastTriggerBarVah;
        private double _lastTriggerBarVal;
        private double _lastBiasBarVah;
        private double _lastBiasBarVal;
        private Dictionary<double, long> _barValueAreaVolumes;

        // Renderers
        private ImbalanceRenderer _imbalanceRenderer;
        private VolumeProfileRenderer _volumeProfileRenderer;
        private BarValueAreaRenderer _barValueAreaRenderer;

        // Feature computers
        private EmaTracker _emaTracker;
        private EnrichedFeatureComputer _enrichedComputer;
        private double[] _lastEnrichedFeatures;

        // Order flow metrics (dual timeframe)
        private OrderFlowMetricsTracker _triggerMetrics;
        private OrderFlowMetricsTracker _biasMetrics;

        // Bar extraction
        private VolumetricBarExtractor _barExtractor;

        // Dashboard WebSocket server
        private WebSocketServer _wsServer;

        // Initialization state
        private bool _isInitialized;

        protected override void OnStateChange()
        {
            switch (State)
            {
                case State.SetDefaults:
                    SetDefaults();
                    break;
                case State.Configure:
                    ConfigureDataSeries();
                    break;
                case State.DataLoaded:
                    Initialize();
                    break;
                case State.Terminated:
                    Cleanup();
                    break;
            }
        }

        private void SetDefaults()
        {
            Description = "Beer Money - VWAP with Order Flow Analysis + Dashboard";
            Name = "BeerMoney";
            Calculate = Calculate.OnEachTick;
            IsOverlay = true;
            DisplayInDataBox = true;
            DrawOnPricePanel = true;
            PaintPriceMarkers = false;
            ScaleJustification = ScaleJustification.Right;
            IsSuspendedWhileInactive = true;

            BaseBarsType = BarTypeOptions.Tick;
            BaseTickSize = 1000;
            VolumetricBarsType = BarTypeOptions.Tick;
            BiasTickSize = 2500;
            TriggerTickSize = 500;
            TicksPerLevel = 4;
            Period = 14;
            BiasSmoothing = 5;
            ClusterLookback = 5;
            ClusterBucketSize = 2.0;
            EnableDashboard = true;
            DashboardPort = 8422;

            ShowDivergentBars = true;
            DivergentBullishColor = Brushes.Cyan;
            DivergentBearishColor = Brushes.Magenta;

            ShowImbalances = true;
            ImbalanceRatio = 3.0;
            MinImbalanceVolume = 10;
            BullishImbalanceColor = Brushes.Green;
            BearishImbalanceColor = Brushes.Red;
            ImbalanceOpacity = 0.6f;

            ReferenceVolume = 150;

            ShowVolumeProfile = true;
            ProfileWidth = 150;
            ValueAreaPercent = 70;
            ProfileColor = Brushes.Yellow;
            ValueAreaColor = Brushes.CornflowerBlue;
            PocColor = Brushes.Red;
            ProfileOpacity = 0.6f;

            ShowBarValueArea = true;
            BarValueAreaOpacity = 0.8f;
            BarValueAreaPadding = 4;
            BarVaColor = Brushes.CornflowerBlue;
            BarPocColor = Brushes.Gold;

            FastEmaPeriod = 5;
            SlowEmaPeriod = 9;
            ShowEmaLines = true;

            AddPlot(new Stroke(Brushes.Gold, 2), PlotStyle.Line, "BiasVwap");
            AddPlot(new Stroke(Brushes.Magenta, 1), PlotStyle.Line, "TriggerVwap");
            AddPlot(new Stroke(Brushes.DodgerBlue, DashStyleHelper.Dash, 1), PlotStyle.Line, "SlowEMA");
            AddPlot(new Stroke(Brushes.LimeGreen, DashStyleHelper.Dash, 1), PlotStyle.Line, "FastEMA");
        }

        private void ConfigureDataSeries()
        {
            BarsPeriodType basePeriodType = ConvertBarType(BaseBarsType);
            BarsPeriodType volumetricPeriodType = ConvertBarType(VolumetricBarsType);

            AddDataSeries(basePeriodType, BaseTickSize);
            AddVolumetric(Instrument.FullName, volumetricPeriodType, BiasTickSize, VolumetricDeltaType.BidAsk, TicksPerLevel);
            AddVolumetric(Instrument.FullName, volumetricPeriodType, TriggerTickSize, VolumetricDeltaType.BidAsk, TicksPerLevel);
        }

        private BarsPeriodType ConvertBarType(BarTypeOptions barType)
        {
            switch (barType)
            {
                case BarTypeOptions.Minute: return BarsPeriodType.Minute;
                case BarTypeOptions.Range: return BarsPeriodType.Range;
                case BarTypeOptions.Second: return BarsPeriodType.Second;
                case BarTypeOptions.Tick: return BarsPeriodType.Tick;
                case BarTypeOptions.Volume: return BarsPeriodType.Volume;
                default: return BarsPeriodType.Tick;
            }
        }

        private void Initialize()
        {
            _dataSeriesManager = new DataSeriesManager(Period, BiasSmoothing, ClusterLookback, ClusterBucketSize,
                SlowEmaPeriod, FastEmaPeriod, msg => Print(msg));
            _dataSeriesManager.Initialize();

            _divergentBullish = DivergentBullishColor.Clone();
            _divergentBullish.Freeze();
            _divergentBearish = DivergentBearishColor.Clone();
            _divergentBearish.Freeze();

            _profileVolumes = new Dictionary<double, long>();
            _barValueAreaVolumes = new Dictionary<double, long>();

            _imbalanceRenderer = new ImbalanceRenderer();
            _volumeProfileRenderer = new VolumeProfileRenderer();
            _barValueAreaRenderer = new BarValueAreaRenderer();

            _emaTracker = new EmaTracker(_ => { });
            _emaTracker.SlowEmaPeriod = SlowEmaPeriod;
            _emaTracker.FastEmaPeriod = FastEmaPeriod;
            _enrichedComputer = new EnrichedFeatureComputer();

            _triggerMetrics = new OrderFlowMetricsTracker(20);
            _biasMetrics = new OrderFlowMetricsTracker(20);

            _barExtractor = new VolumetricBarExtractor(
                GetVolumetricBarsType,
                () => Instrument.MasterInstrument.TickSize,
                TicksPerLevel, ImbalanceRatio, MinImbalanceVolume);

            if (EnableDashboard)
            {
                _wsServer = new WebSocketServer(DashboardPort, msg => Print(msg));
                _wsServer.Start();
            }

            ValidateVolumetricSeries();
            _isInitialized = true;
        }

        private void ValidateVolumetricSeries()
        {
            if (BarsArray.Length > BiasSeries)
            {
                var biasVolumetric = BarsArray[BiasSeries].BarsType as VolumetricBarsType;
                if (biasVolumetric == null)
                    Print($"WARNING: BiasSeries (index {BiasSeries}) is not a VolumetricBarsType.");
            }

            if (BarsArray.Length > TriggerSeries)
            {
                var triggerVolumetric = BarsArray[TriggerSeries].BarsType as VolumetricBarsType;
                if (triggerVolumetric == null)
                    Print($"WARNING: TriggerSeries (index {TriggerSeries}) is not a VolumetricBarsType.");
            }
        }

        private void Cleanup()
        {
            _isInitialized = false;

            _wsServer?.Dispose();
            _wsServer = null;

            _emaTracker = null;
            _enrichedComputer = null;
            _lastEnrichedFeatures = null;
            _barExtractor = null;

            _triggerMetrics = null;
            _biasMetrics = null;

            _dataSeriesManager?.Cleanup();
            _dataSeriesManager = null;

            _profileVolumes?.Clear();
            _profileVolumes = null;
            _barValueAreaVolumes?.Clear();
            _barValueAreaVolumes = null;
            _volumeProfile = null;

            _imbalanceRenderer?.Dispose();
            _imbalanceRenderer = null;
            _volumeProfileRenderer?.Dispose();
            _volumeProfileRenderer = null;

            _barValueAreaRenderer?.Dispose();
            _barValueAreaRenderer = null;
        }

        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            base.OnRender(chartControl, chartScale);

            if (!_isInitialized || RenderTarget == null || ChartBars == null)
                return;

            float clampedProfileOpacity = Math.Max(0.05f, Math.Min(1.0f, ProfileOpacity));
            float clampedImbalanceOpacity = Math.Max(0.05f, Math.Min(1.0f, ImbalanceOpacity));
            float clampedBarVaOpacity = Math.Max(0.05f, Math.Min(1.0f, BarValueAreaOpacity));

            if (ShowVolumeProfile && _volumeProfileRenderer != null)
            {
                var profileSettings = new VolumeProfileSettings(
                    ProfileWidth, TicksPerLevel, Instrument.MasterInstrument.TickSize,
                    clampedProfileOpacity, ProfileColor, ValueAreaColor, PocColor);
                _volumeProfileRenderer.Render(RenderTarget, chartControl, chartScale, _volumeProfile, profileSettings);
            }

            if (ShowImbalances && _imbalanceRenderer != null)
            {
                var volumetricBars = GetVolumetricBarsType(TriggerSeries);
                if (volumetricBars == null) return;

                var imbalanceSettings = new ImbalanceSettings(
                    TicksPerLevel, Instrument.MasterInstrument.TickSize,
                    ImbalanceRatio, MinImbalanceVolume, ReferenceVolume,
                    clampedImbalanceOpacity, BullishImbalanceColor, BearishImbalanceColor);
                _imbalanceRenderer.Render(
                    RenderTarget, chartControl, chartScale, ChartBars, volumetricBars,
                    BarsArray, PrimarySeries, TriggerSeries, imbalanceSettings,
                    CurrentBars, Highs, Lows, Times, msg => Print(msg));
            }

            if (ShowBarValueArea && _barValueAreaRenderer != null)
            {
                _barValueAreaRenderer.Render(
                    RenderTarget, chartControl, chartScale, ChartBars,
                    BarsArray, PrimarySeries, TriggerSeries, CurrentBars, Times,
                    clampedBarVaOpacity, (float)BarValueAreaPadding,
                    BarVaColor, BarPocColor);
            }
        }

        protected override void OnBarUpdate()
        {
            if (BarsInProgress == PrimarySeries)
                ProcessPrimarySeries();
            else if (BarsInProgress == BaseSeries)
                ProcessBaseSeries();
            else if (BarsInProgress == BiasSeries)
                ProcessBiasSeries();
            else if (BarsInProgress == TriggerSeries)
                ProcessTriggerSeries();
        }

        private void ProcessPrimarySeries()
        {
            if (_dataSeriesManager == null) return;

            if (_dataSeriesManager.BiasVwap > 0)
                Values[0][0] = _dataSeriesManager.BiasVwap;
            if (_dataSeriesManager.TriggerVwap > 0)
                Values[1][0] = _dataSeriesManager.TriggerVwap;
            if (ShowEmaLines && _emaTracker != null && _emaTracker.SlowEmaValue > 0)
                Values[2][0] = _emaTracker.SlowEmaValue;
            if (ShowEmaLines && _emaTracker != null && _emaTracker.FastEmaValue > 0)
                Values[3][0] = _emaTracker.FastEmaValue;

            PaintBar();
        }

        private void ProcessBaseSeries()
        {
            if (CurrentBars[BaseSeries] < Period) return;

            int currentBar = CurrentBars[BaseSeries];
            if (currentBar == _lastBaseBarCount) return;
            _lastBaseBarCount = currentBar;

            var bar = _barExtractor.ExtractBar(BaseSeries, 1, Times, CurrentBars, Opens, Highs, Lows, Closes, Volumes);
            _dataSeriesManager.ProcessBaseBar(bar);
        }

        private void ProcessBiasSeries()
        {
            if (CurrentBars[BiasSeries] < Period) return;

            int currentBar = CurrentBars[BiasSeries];
            if (currentBar == _lastBiasBarCount) return;
            _lastBiasBarCount = currentBar;

            var bar = _barExtractor.ExtractVolumetricBar(BiasSeries, 1, Times, CurrentBars, Opens, Highs, Lows, Closes, Volumes);
            _dataSeriesManager.ProcessBiasBar(bar, Closes[BiasSeries][0]);

            if (_enrichedComputer != null)
                _enrichedComputer.ProcessBiasBar(bar, _dataSeriesManager.BiasSlowEma, _dataSeriesManager.BaseAtr);

            ComputeBarValueArea(BiasSeries, 1, out _lastBiasBarVah, out _lastBiasBarVal);
            if (_biasMetrics != null && _dataSeriesManager.BaseAtr > 0)
                _biasMetrics.ProcessBar(bar, _lastBiasBarVah, _lastBiasBarVal,
                    _dataSeriesManager.BiasVwap, _dataSeriesManager.BaseAtr);

            if (ShowVolumeProfile)
                CalculateVolumeProfile();
        }

        private void ProcessTriggerSeries()
        {
            if (CurrentBars[TriggerSeries] < Period) return;

            int currentBar = CurrentBars[TriggerSeries];
            bool isNewBar = currentBar != _lastTriggerBarCount;

            if (isNewBar)
            {
                _lastTriggerBarCount = currentBar;

                var bar = _barExtractor.ExtractVolumetricBar(TriggerSeries, 0, Times, CurrentBars, Opens, Highs, Lows, Closes, Volumes);
                _dataSeriesManager.ProcessTriggerBar(bar, Closes[TriggerSeries][0], Closes[BiasSeries][0]);

                // Process completed bar (index 1) for features and dashboard
                if (CurrentBars[TriggerSeries] >= 1)
                {
                    var completedBar = _barExtractor.ExtractVolumetricBar(TriggerSeries, 1, Times, CurrentBars, Opens, Highs, Lows, Closes, Volumes);
                    double atr = _dataSeriesManager.BaseAtr;

                    ComputeBarValueArea(TriggerSeries, 1, out _lastTriggerBarVah, out _lastTriggerBarVal);
                    int completedTriggerIdx = CurrentBars[TriggerSeries] - 1;
                    if (_barValueAreaRenderer != null && completedTriggerIdx >= 0)
                        _barValueAreaRenderer.CacheBar(completedTriggerIdx, completedBar.PointOfControl, _lastTriggerBarVah, _lastTriggerBarVal);

                    if (_triggerMetrics != null && atr > 0)
                        _triggerMetrics.ProcessBar(completedBar, _lastTriggerBarVah, _lastTriggerBarVal,
                            _dataSeriesManager.TriggerVwap, atr);

                    if (_emaTracker != null)
                        _emaTracker.ProcessBar(completedBar, atr);

                    double slowEma = _emaTracker?.SlowEmaValue ?? 0;
                    _dataSeriesManager.ProcessCompletedTriggerBarFeatures(completedBar);

                    // Compute enriched features
                    if (_enrichedComputer != null && atr > 0)
                    {
                        int timeHHMMSS = ToTime(Times[TriggerSeries][1]);
                        _lastEnrichedFeatures = _enrichedComputer.Compute(
                            _dataSeriesManager.TriggerBars, completedBar,
                            atr, _dataSeriesManager.TriggerDeltaEwm,
                            _dataSeriesManager.ClusterTracker, timeHHMMSS);
                    }

                    // Broadcast to dashboard via WebSocket
                    if (_wsServer != null && _wsServer.IsRunning && atr > 0)
                    {
                        string payload = DashboardPayloadBuilder.Build(
                            completedBar, atr, slowEma,
                            Times[TriggerSeries][1],
                            _emaTracker, _dataSeriesManager,
                            _lastEnrichedFeatures, _volumeProfile,
                            _triggerMetrics?.CurrentMetrics,
                            _biasMetrics?.CurrentMetrics);
                        _wsServer.BroadcastBar(payload);
                    }
                }
            }
        }

        private void ComputeBarValueArea(int seriesIndex, int barIndex, out double vah, out double val)
        {
            vah = 0;
            val = 0;

            var volumetricBars = GetVolumetricBarsType(seriesIndex);
            if (volumetricBars == null)
                return;

            int volumeIndex = CurrentBars[seriesIndex] - barIndex;
            if (volumeIndex < 0 || volumeIndex >= volumetricBars.Volumes.Length)
                return;

            var volumes = volumetricBars.Volumes[volumeIndex];
            if (volumes == null)
                return;

            double tickSize = Instrument.MasterInstrument.TickSize;
            double levelSize = tickSize * TicksPerLevel;
            double barHigh = Highs[seriesIndex].GetValueAt(volumeIndex);
            double barLow = Lows[seriesIndex].GetValueAt(volumeIndex);

            _barValueAreaVolumes.Clear();
            int startLevel = (int)Math.Floor(barLow / levelSize);
            int endLevel = (int)Math.Ceiling(barHigh / levelSize);

            for (int level = startLevel; level <= endLevel; level++)
            {
                double price = level * levelSize;
                long vol = volumes.GetAskVolumeForPrice(price) + volumes.GetBidVolumeForPrice(price);
                if (vol > 0)
                    _barValueAreaVolumes[price] = vol;
            }

            if (_barValueAreaVolumes.Count > 0)
            {
                var result = VolumeProfileAnalysis.Calculate(_barValueAreaVolumes, 0.70);
                if (result.IsValid)
                {
                    vah = result.VAH;
                    val = result.VAL;
                }
            }
        }

        private void CalculateVolumeProfile()
        {
            var volumetricBars = GetVolumetricBarsType(BiasSeries);
            if (volumetricBars == null) return;

            _profileVolumes.Clear();
            double tickSize = Instrument.MasterInstrument.TickSize;
            double levelSize = tickSize * TicksPerLevel;

            int startBar = Math.Max(0, CurrentBars[BiasSeries] - Period);
            int endBar = CurrentBars[BiasSeries];

            for (int barIdx = startBar; barIdx <= endBar; barIdx++)
            {
                try
                {
                    var barVolumes = volumetricBars.Volumes[barIdx];
                    if (barVolumes == null) continue;

                    double barHigh = Highs[BiasSeries].GetValueAt(barIdx);
                    double barLow = Lows[BiasSeries].GetValueAt(barIdx);

                    int startLevel = (int)Math.Floor(barLow / levelSize);
                    int endLevel = (int)Math.Ceiling(barHigh / levelSize);

                    for (int level = startLevel; level <= endLevel; level++)
                    {
                        double price = level * levelSize;
                        double roundedPrice = RoundToTickPrecision(price, tickSize);

                        long volumeAtPrice = barVolumes.GetAskVolumeForPrice(roundedPrice) + barVolumes.GetBidVolumeForPrice(roundedPrice);
                        if (volumeAtPrice > 0)
                        {
                            _profileVolumes.TryGetValue(roundedPrice, out long existingVolume);
                            _profileVolumes[roundedPrice] = existingVolume + volumeAtPrice;
                        }
                    }
                }
                catch (ArgumentOutOfRangeException) { continue; }
            }

            _volumeProfile = VolumeProfileAnalysis.Calculate(_profileVolumes, ValueAreaPercent / 100.0);
        }

        private VolumetricBarsType GetVolumetricBarsType(int seriesIndex)
        {
            if (seriesIndex < 0 || seriesIndex >= BarsArray.Length) return null;
            return BarsArray[seriesIndex].BarsType as VolumetricBarsType;
        }

        private double RoundToTickPrecision(double price, double tickSize)
        {
            return Math.Round(price / tickSize) * tickSize;
        }

        private void PaintBar()
        {
            if (!ShowDivergentBars || CurrentBar < 1) return;

            var triggerBar = _dataSeriesManager?.CurrentTriggerBar;
            if (triggerBar == null) return;

            if (triggerBar.IsDivergent)
            {
                Brush barColor = triggerBar.Delta > 0 ? _divergentBullish : _divergentBearish;
                BarBrushes[1] = barColor;
                CandleOutlineBrushes[1] = barColor;
            }
        }
    }
}
