using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using System.Xml.Serialization;
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
    /// Beer Money - VWAP indicator with bar painting based on order flow metrics.
    /// 3 data series: Base (ATR), Bias (slow VWAP), Trigger (fast VWAP volumetric).
    /// Bars are painted based on BarScore gradient with special divergent highlighting.
    /// </summary>
    public class BeerMoneyIndicator : Indicator
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
        private System.Windows.Media.Brush _divergentBullish;  // Positive delta + bearish bar (hidden accumulation)
        private System.Windows.Media.Brush _divergentBearish;  // Negative delta + bullish bar (hidden distribution)

        // Volume profile
        private VolumeProfileResult _volumeProfile;
        private Dictionary<double, long> _profileVolumes;

        // Renderers
        private ImbalanceRenderer _imbalanceRenderer;
        private VolumeProfileRenderer _volumeProfileRenderer;
        private DataTableRenderer _dataTableRenderer;

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
            Description = "Beer Money - VWAP with Order Flow Bar Painting";
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

            // Divergent colors - special highlighting for hidden activity
            DivergentBullishColor = Brushes.Cyan;      // Hidden accumulation (positive delta, bearish bar)
            DivergentBearishColor = Brushes.Magenta;   // Hidden distribution (negative delta, bullish bar)

            // Imbalance heatmap settings
            ShowImbalances = true;
            ImbalanceRatio = 3.0;           // 300% ratio threshold
            MinImbalanceVolume = 10;        // Minimum volume to consider
            BullishImbalanceColor = Brushes.Green;
            BearishImbalanceColor = Brushes.Red;
            ImbalanceOpacity = 0.6f;

            // High volume imbalance settings
            HighVolumeThreshold = 100;      // Minimum volume at price level to use high volume color
            HighVolumeBullishColor = Brushes.White;
            HighVolumeBearishColor = Brushes.Orange;

            // Volume profile settings
            ShowVolumeProfile = true;
            ProfileWidth = 150;             // Width in pixels
            ValueAreaPercent = 70;          // 70% value area
            ProfileColor = Brushes.Yellow;
            ValueAreaColor = Brushes.CornflowerBlue;
            PocColor = Brushes.Red;
            ProfileOpacity = 0.6f;

            // Data table settings
            ShowDataTable = true;
            DataTablePosition = TablePosition.BottomLeft;
            TableFontSize = 12;
            TableOffsetX = 0;
            TableOffsetY = -15;

            AddPlot(new Stroke(Brushes.Gold, 2), PlotStyle.Line, "BiasVwap");
            AddPlot(new Stroke(Brushes.Magenta, 1), PlotStyle.Line, "TriggerVwap");
        }

        private void ConfigureDataSeries()
        {
            BarsPeriodType basePeriodType = ConvertBarType(BaseBarsType);
            BarsPeriodType volumetricPeriodType = ConvertBarType(VolumetricBarsType);

            AddDataSeries(basePeriodType, BaseTickSize);     // BaseSeries - ATR source
            AddVolumetric(Instrument.FullName, volumetricPeriodType, BiasTickSize, VolumetricDeltaType.BidAsk, TicksPerLevel);   // BiasSeries - volumetric for VWAP + volume profile
            AddVolumetric(Instrument.FullName, volumetricPeriodType, TriggerTickSize, VolumetricDeltaType.BidAsk, TicksPerLevel);  // TriggerSeries - volumetric
        }

        private BarsPeriodType ConvertBarType(BarTypeOptions barType)
        {
            switch (barType)
            {
                case BarTypeOptions.Minute:
                    return BarsPeriodType.Minute;
                case BarTypeOptions.Range:
                    return BarsPeriodType.Range;
                case BarTypeOptions.Second:
                    return BarsPeriodType.Second;
                case BarTypeOptions.Tick:
                    return BarsPeriodType.Tick;
                case BarTypeOptions.Volume:
                    return BarsPeriodType.Volume;
                default:
                    return BarsPeriodType.Tick;
            }
        }

        private void Initialize()
        {
            _dataSeriesManager = new DataSeriesManager(Period, BiasSmoothing, msg => Print(msg));
            _dataSeriesManager.Initialize();

            // Freeze brushes for performance
            _divergentBullish = DivergentBullishColor.Clone();
            _divergentBullish.Freeze();
            _divergentBearish = DivergentBearishColor.Clone();
            _divergentBearish.Freeze();

            // Initialize volume profile
            _profileVolumes = new Dictionary<double, long>();

            // Initialize renderers
            _imbalanceRenderer = new ImbalanceRenderer();
            _volumeProfileRenderer = new VolumeProfileRenderer();
            _dataTableRenderer = new DataTableRenderer();
            _dataTableRenderer.Initialize(TableFontSize);

            // Validate volumetric data series configuration
            ValidateVolumetricSeries();

            _isInitialized = true;
        }

        private void ValidateVolumetricSeries()
        {
            // Validate that bias and trigger series are actually volumetric
            if (BarsArray.Length > BiasSeries)
            {
                var biasVolumetric = BarsArray[BiasSeries].BarsType as VolumetricBarsType;
                if (biasVolumetric == null)
                {
                    Print($"WARNING: BiasSeries (index {BiasSeries}) is not a VolumetricBarsType. Volume profile will not function correctly.");
                }
            }

            if (BarsArray.Length > TriggerSeries)
            {
                var triggerVolumetric = BarsArray[TriggerSeries].BarsType as VolumetricBarsType;
                if (triggerVolumetric == null)
                {
                    Print($"WARNING: TriggerSeries (index {TriggerSeries}) is not a VolumetricBarsType. Imbalance rendering and volumetric analysis will not function correctly.");
                }
            }
        }

        private void Cleanup()
        {
            _isInitialized = false;

            _dataSeriesManager?.Cleanup();
            _dataSeriesManager = null;

            _profileVolumes?.Clear();
            _profileVolumes = null;
            _volumeProfile = null;

            _imbalanceRenderer?.Dispose();
            _imbalanceRenderer = null;
            _volumeProfileRenderer = null;

            _dataTableRenderer?.Dispose();
            _dataTableRenderer = null;
        }

        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            base.OnRender(chartControl, chartScale);

            if (!_isInitialized || RenderTarget == null || ChartBars == null)
                return;

            // Render volume profile on the right side
            if (ShowVolumeProfile && _volumeProfileRenderer != null)
            {
                var profileSettings = new VolumeProfileSettings(
                    ProfileWidth,
                    TicksPerLevel,
                    Instrument.MasterInstrument.TickSize,
                    ProfileOpacity,
                    ProfileColor,
                    ValueAreaColor,
                    PocColor);
                _volumeProfileRenderer.Render(RenderTarget, chartControl, chartScale, _volumeProfile, profileSettings);
            }

            // Render imbalances
            if (ShowImbalances && _imbalanceRenderer != null)
            {
                var volumetricBars = GetVolumetricBarsType(TriggerSeries);
                if (volumetricBars == null)
                {
                    // Skip imbalance rendering if volumetric data not available
                    return;
                }
                var imbalanceSettings = new ImbalanceSettings(
                    TicksPerLevel,
                    Instrument.MasterInstrument.TickSize,
                    ImbalanceRatio,
                    MinImbalanceVolume,
                    HighVolumeThreshold,
                    ImbalanceOpacity,
                    BullishImbalanceColor,
                    BearishImbalanceColor,
                    HighVolumeBullishColor,
                    HighVolumeBearishColor);
                _imbalanceRenderer.Render(
                    RenderTarget, chartControl, chartScale, ChartBars, volumetricBars,
                    BarsArray, PrimarySeries, TriggerSeries, imbalanceSettings,
                    CurrentBars, Highs, Lows, Times, msg => Print(msg));
            }

            // Render data table last so it's on top
            if (ShowDataTable && _dataTableRenderer != null && _dataSeriesManager != null)
            {
                var tableValues = new DataTableValues(
                    _dataSeriesManager.BiasVwap,
                    _dataSeriesManager.TriggerVwap,
                    _dataSeriesManager.BaseAtr,
                    _dataSeriesManager.DeltaEfficiency);
                var tableSettings = new DataTableSettings(
                    DataTablePosition,
                    TableFontSize,
                    TableOffsetX,
                    TableOffsetY,
                    ShowVolumeProfile,
                    ProfileWidth);
                _dataTableRenderer.Render(RenderTarget, chartControl, chartScale, tableValues, tableSettings);
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
            if (_dataSeriesManager == null)
                return;

            if (_dataSeriesManager.BiasVwap > 0)
                Values[0][0] = _dataSeriesManager.BiasVwap;
            if (_dataSeriesManager.TriggerVwap > 0)
                Values[1][0] = _dataSeriesManager.TriggerVwap;

            // Paint using current trigger bar data
            PaintBar();
        }

        private void ProcessBaseSeries()
        {
            if (CurrentBars[BaseSeries] < Period)
                return;

            int currentBar = CurrentBars[BaseSeries];
            if (currentBar == _lastBaseBarCount)
                return;
            _lastBaseBarCount = currentBar;

            var bar = ExtractBar(BaseSeries, 1);
            _dataSeriesManager.ProcessBaseBar(bar);
        }

        private void ProcessBiasSeries()
        {
            if (CurrentBars[BiasSeries] < Period)
                return;

            int currentBar = CurrentBars[BiasSeries];
            if (currentBar == _lastBiasBarCount)
                return;
            _lastBiasBarCount = currentBar;

            var bar = ExtractBar(BiasSeries, 1);
            _dataSeriesManager.ProcessBiasBar(bar, Closes[BiasSeries][0]);

            // Calculate volume profile from rolling window of bias bars
            if (ShowVolumeProfile)
            {
                CalculateVolumeProfile();
            }
        }

        private void CalculateVolumeProfile()
        {
            var volumetricBars = GetVolumetricBarsType(BiasSeries);
            if (volumetricBars == null)
                return;

            _profileVolumes.Clear();
            double tickSize = Instrument.MasterInstrument.TickSize;
            double levelSize = tickSize * TicksPerLevel;

            // Aggregate volume across the rolling window (Period bars)
            int startBar = Math.Max(0, CurrentBars[BiasSeries] - Period);
            int endBar = CurrentBars[BiasSeries];

            for (int barIdx = startBar; barIdx <= endBar; barIdx++)
            {
                try
                {
                    var barVolumes = volumetricBars.Volumes[barIdx];
                    if (barVolumes == null)
                        continue;

                    double barHigh = Highs[BiasSeries].GetValueAt(barIdx);
                    double barLow = Lows[BiasSeries].GetValueAt(barIdx);

                    // Use integer-based iteration to avoid floating-point precision issues
                    int startLevel = (int)Math.Floor(barLow / levelSize);
                    int endLevel = (int)Math.Ceiling(barHigh / levelSize);

                    for (int level = startLevel; level <= endLevel; level++)
                    {
                        double price = level * levelSize;
                        // Round to tick precision for consistent dictionary key lookup
                        double roundedPrice = RoundToTickPrecision(price, tickSize);

                        long volumeAtPrice = barVolumes.GetAskVolumeForPrice(roundedPrice) + barVolumes.GetBidVolumeForPrice(roundedPrice);
                        if (volumeAtPrice > 0)
                        {
                            if (_profileVolumes.ContainsKey(roundedPrice))
                                _profileVolumes[roundedPrice] += volumeAtPrice;
                            else
                                _profileVolumes[roundedPrice] = volumeAtPrice;
                        }
                    }
                }
                catch (ArgumentOutOfRangeException)
                {
                    // Bar index out of range - expected during chart initialization
                }
            }

            // Calculate POC, VAH, VAL
            _volumeProfile = VolumeProfileAnalysis.Calculate(_profileVolumes, ValueAreaPercent / 100.0);
        }

        /// <summary>
        /// Safely retrieves VolumetricBarsType for the specified series index.
        /// </summary>
        private VolumetricBarsType GetVolumetricBarsType(int seriesIndex)
        {
            if (seriesIndex < 0 || seriesIndex >= BarsArray.Length)
                return null;

            return BarsArray[seriesIndex].BarsType as VolumetricBarsType;
        }

        /// <summary>
        /// Rounds a price to the nearest tick precision to ensure consistent dictionary key lookup.
        /// </summary>
        private double RoundToTickPrecision(double price, double tickSize)
        {
            return Math.Round(price / tickSize) * tickSize;
        }

        private void ProcessTriggerSeries()
        {
            if (CurrentBars[TriggerSeries] < Period)
                return;

            int currentBar = CurrentBars[TriggerSeries];
            if (currentBar == _lastTriggerBarCount)
                return;
            _lastTriggerBarCount = currentBar;

            // Extract current trigger bar (index 0) for both VWAP and painting
            var bar = ExtractVolumetricBar(TriggerSeries, 0);
            _dataSeriesManager.ProcessTriggerBar(bar, Closes[TriggerSeries][0], Closes[BiasSeries][0]);
        }

        private void PaintBar()
        {
            if (CurrentBar < 1)
                return;

            var triggerBar = _dataSeriesManager?.CurrentTriggerBar;
            if (triggerBar == null)
                return;

            // Only paint divergent bars (hidden accumulation/distribution)
            // Paint previous bar (index 1) since trigger data is one bar behind
            if (triggerBar.IsDivergent)
            {
                // Positive delta + bearish bar = hidden accumulation (cyan)
                // Negative delta + bullish bar = hidden distribution (magenta)
                System.Windows.Media.Brush barColor = triggerBar.Delta > 0 ? _divergentBullish : _divergentBearish;
                BarBrushes[1] = barColor;
                CandleOutlineBrushes[1] = barColor;
            }
        }

        private BarData ExtractBar(int seriesIndex, int barIndex)
        {
            return new BarData(
                Times[seriesIndex][barIndex],
                CurrentBars[seriesIndex] - barIndex,
                Opens[seriesIndex][barIndex],
                Highs[seriesIndex][barIndex],
                Lows[seriesIndex][barIndex],
                Closes[seriesIndex][barIndex],
                (long)Volumes[seriesIndex][barIndex]);
        }

        private BarData ExtractVolumetricBar(int seriesIndex, int barIndex)
        {
            long buyVolume = 0;
            long sellVolume = 0;
            long cumulativeDelta = 0;
            long maxDelta = 0;
            long minDelta = 0;
            double pointOfControl = 0;

            var volumetricBars = GetVolumetricBarsType(seriesIndex);
            if (volumetricBars != null)
            {
                int volumeIndex = CurrentBars[seriesIndex] - barIndex;
                if (volumeIndex >= 0 && volumeIndex < volumetricBars.Volumes.Length)
                {
                    var volumes = volumetricBars.Volumes[volumeIndex];
                    if (volumes != null)
                    {
                        buyVolume = (long)volumes.TotalBuyingVolume;
                        sellVolume = (long)volumes.TotalSellingVolume;
                        cumulativeDelta = (long)volumes.CumulativeDelta;
                        maxDelta = (long)volumes.MaxSeenDelta;
                        minDelta = (long)volumes.MinSeenDelta;
                        volumes.GetMaximumVolume(null, out pointOfControl);
                    }
                }
            }

            return new BarData(
                Times[seriesIndex][barIndex],
                CurrentBars[seriesIndex] - barIndex,
                Opens[seriesIndex][barIndex],
                Highs[seriesIndex][barIndex],
                Lows[seriesIndex][barIndex],
                Closes[seriesIndex][barIndex],
                (long)Volumes[seriesIndex][barIndex],
                buyVolume,
                sellVolume,
                cumulativeDelta,
                maxDelta,
                minDelta,
                pointOfControl);
        }

        #region Properties

        [NinjaScriptProperty]
        [Display(Name = "Base Bars Type", Description = "The type of bars for the base data series (ATR)", Order = 1, GroupName = "Data Series")]
        public BarTypeOptions BaseBarsType { get; set; }

        [NinjaScriptProperty]
        [Range(1, 10000)]
        [Display(Name = "Base Period Size", Description = "Period size for base data series (e.g., ticks, seconds, range)", Order = 2, GroupName = "Data Series")]
        public int BaseTickSize { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Volumetric Bars Type", Description = "The type of bars for the volumetric data series", Order = 3, GroupName = "Data Series")]
        public BarTypeOptions VolumetricBarsType { get; set; }

        [NinjaScriptProperty]
        [Range(1, 10000)]
        [Display(Name = "Bias Period Size", Description = "Period size for bias series (slower VWAP)", Order = 4, GroupName = "Data Series")]
        public int BiasTickSize { get; set; }

        [NinjaScriptProperty]
        [Range(1, 10000)]
        [Display(Name = "Trigger Period Size", Description = "Period size for trigger series (faster VWAP)", Order = 5, GroupName = "Data Series")]
        public int TriggerTickSize { get; set; }

        [NinjaScriptProperty]
        [Range(1, 10)]
        [Display(Name = "Ticks Per Level", Description = "Tick aggregation for volumetric data (must match chart)", Order = 6, GroupName = "Data Series")]
        public int TicksPerLevel { get; set; }

        [NinjaScriptProperty]
        [Range(5, 50)]
        [Display(Name = "Period", Description = "Lookback period for ATR/VWAP", Order = 1, GroupName = "Parameters")]
        public int Period { get; set; }

        [NinjaScriptProperty]
        [Range(1, 20)]
        [Display(Name = "Bias Smoothing", Description = "EMA period for smoothing Bias VWAP (1=no smoothing)", Order = 2, GroupName = "Parameters")]
        public int BiasSmoothing { get; set; }

        [XmlIgnore]
        [Display(Name = "Divergent Bullish", Description = "Color for hidden accumulation (positive delta + bearish bar)", Order = 1, GroupName = "Bar Colors")]
        public System.Windows.Media.Brush DivergentBullishColor { get; set; }

        [Browsable(false)]
        public string DivergentBullishColorSerializable
        {
            get { return Serialize.BrushToString(DivergentBullishColor); }
            set { DivergentBullishColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Divergent Bearish", Description = "Color for hidden distribution (negative delta + bullish bar)", Order = 2, GroupName = "Bar Colors")]
        public System.Windows.Media.Brush DivergentBearishColor { get; set; }

        [Browsable(false)]
        public string DivergentBearishColorSerializable
        {
            get { return Serialize.BrushToString(DivergentBearishColor); }
            set { DivergentBearishColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Display(Name = "Show Imbalances", Description = "Enable diagonal imbalance heatmap", Order = 1, GroupName = "Imbalances")]
        public bool ShowImbalances { get; set; }

        [NinjaScriptProperty]
        [Range(1.5, 10.0)]
        [Display(Name = "Imbalance Ratio", Description = "Minimum ratio for diagonal imbalance (e.g., 3.0 = 300%)", Order = 2, GroupName = "Imbalances")]
        public double ImbalanceRatio { get; set; }

        [NinjaScriptProperty]
        [Range(1, 1000)]
        [Display(Name = "Min Difference", Description = "Minimum volume difference between diagonal levels", Order = 3, GroupName = "Imbalances")]
        public int MinImbalanceVolume { get; set; }

        [NinjaScriptProperty]
        [Range(0.1f, 1.0f)]
        [Display(Name = "Opacity", Description = "Opacity of imbalance highlighting (0.1-1.0)", Order = 4, GroupName = "Imbalances")]
        public float ImbalanceOpacity { get; set; }

        [XmlIgnore]
        [Display(Name = "Bullish Imbalance", Description = "Color for bullish diagonal imbalances", Order = 5, GroupName = "Imbalances")]
        public System.Windows.Media.Brush BullishImbalanceColor { get; set; }

        [Browsable(false)]
        public string BullishImbalanceColorSerializable
        {
            get { return Serialize.BrushToString(BullishImbalanceColor); }
            set { BullishImbalanceColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Bearish Imbalance", Description = "Color for bearish diagonal imbalances", Order = 6, GroupName = "Imbalances")]
        public System.Windows.Media.Brush BearishImbalanceColor { get; set; }

        [Browsable(false)]
        public string BearishImbalanceColorSerializable
        {
            get { return Serialize.BrushToString(BearishImbalanceColor); }
            set { BearishImbalanceColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Range(10, 10000)]
        [Display(Name = "High Volume Threshold", Description = "Volume threshold for high volume imbalance color", Order = 7, GroupName = "Imbalances")]
        public int HighVolumeThreshold { get; set; }

        [XmlIgnore]
        [Display(Name = "High Vol Bullish", Description = "Color for high volume bullish imbalances", Order = 8, GroupName = "Imbalances")]
        public System.Windows.Media.Brush HighVolumeBullishColor { get; set; }

        [Browsable(false)]
        public string HighVolumeBullishColorSerializable
        {
            get { return Serialize.BrushToString(HighVolumeBullishColor); }
            set { HighVolumeBullishColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "High Vol Bearish", Description = "Color for high volume bearish imbalances", Order = 9, GroupName = "Imbalances")]
        public System.Windows.Media.Brush HighVolumeBearishColor { get; set; }

        [Browsable(false)]
        public string HighVolumeBearishColorSerializable
        {
            get { return Serialize.BrushToString(HighVolumeBearishColor); }
            set { HighVolumeBearishColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Display(Name = "Show Volume Profile", Description = "Enable volume profile on right side", Order = 1, GroupName = "Volume Profile")]
        public bool ShowVolumeProfile { get; set; }

        [NinjaScriptProperty]
        [Range(50, 400)]
        [Display(Name = "Profile Width", Description = "Width of volume profile in pixels", Order = 2, GroupName = "Volume Profile")]
        public int ProfileWidth { get; set; }

        [NinjaScriptProperty]
        [Range(50, 90)]
        [Display(Name = "Value Area %", Description = "Percentage of volume for value area (typically 70%)", Order = 3, GroupName = "Volume Profile")]
        public int ValueAreaPercent { get; set; }

        [NinjaScriptProperty]
        [Range(0.1f, 1.0f)]
        [Display(Name = "Profile Opacity", Description = "Opacity of volume profile bars (0.1-1.0)", Order = 4, GroupName = "Volume Profile")]
        public float ProfileOpacity { get; set; }

        [XmlIgnore]
        [Display(Name = "Profile Color", Description = "Color for volume profile bars outside value area", Order = 5, GroupName = "Volume Profile")]
        public System.Windows.Media.Brush ProfileColor { get; set; }

        [Browsable(false)]
        public string ProfileColorSerializable
        {
            get { return Serialize.BrushToString(ProfileColor); }
            set { ProfileColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Value Area Color", Description = "Color for volume profile bars inside value area", Order = 6, GroupName = "Volume Profile")]
        public System.Windows.Media.Brush ValueAreaColor { get; set; }

        [Browsable(false)]
        public string ValueAreaColorSerializable
        {
            get { return Serialize.BrushToString(ValueAreaColor); }
            set { ValueAreaColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "POC Color", Description = "Color for Point of Control", Order = 7, GroupName = "Volume Profile")]
        public System.Windows.Media.Brush PocColor { get; set; }

        [Browsable(false)]
        public string PocColorSerializable
        {
            get { return Serialize.BrushToString(PocColor); }
            set { PocColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Display(Name = "Show Data Table", Description = "Enable data table display", Order = 1, GroupName = "Data Table")]
        public bool ShowDataTable { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Position", Description = "Position of data table on chart", Order = 2, GroupName = "Data Table")]
        public TablePosition DataTablePosition { get; set; }

        [NinjaScriptProperty]
        [Range(8, 24)]
        [Display(Name = "Font Size", Description = "Font size for data table", Order = 3, GroupName = "Data Table")]
        public int TableFontSize { get; set; }

        [NinjaScriptProperty]
        [Range(-500, 500)]
        [Display(Name = "Offset X", Description = "Horizontal offset in pixels (positive = right)", Order = 4, GroupName = "Data Table")]
        public int TableOffsetX { get; set; }

        [NinjaScriptProperty]
        [Range(-500, 500)]
        [Display(Name = "Offset Y", Description = "Vertical offset in pixels (positive = down)", Order = 5, GroupName = "Data Table")]
        public int TableOffsetY { get; set; }

        #endregion
    }
}
