using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Gui;

namespace NinjaTrader.NinjaScript.Indicators.BeerMoney
{
    public partial class BeerMoneyIndicator
    {
        #region Properties

        [NinjaScriptProperty]
        [Display(Name = "Base Bars Type", Order = 1, GroupName = "Data Series")]
        public BarTypeOptions BaseBarsType { get; set; }

        [NinjaScriptProperty]
        [Range(1, 10000)]
        [Display(Name = "Base Period Size", Order = 2, GroupName = "Data Series")]
        public int BaseTickSize { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Volumetric Bars Type", Order = 3, GroupName = "Data Series")]
        public BarTypeOptions VolumetricBarsType { get; set; }

        [NinjaScriptProperty]
        [Range(1, 10000)]
        [Display(Name = "Bias Period Size", Order = 4, GroupName = "Data Series")]
        public int BiasTickSize { get; set; }

        [NinjaScriptProperty]
        [Range(1, 10000)]
        [Display(Name = "Trigger Period Size", Order = 5, GroupName = "Data Series")]
        public int TriggerTickSize { get; set; }

        [NinjaScriptProperty]
        [Range(1, 10)]
        [Display(Name = "Ticks Per Level", Order = 6, GroupName = "Data Series")]
        public int TicksPerLevel { get; set; }

        [NinjaScriptProperty]
        [Range(5, 50)]
        [Display(Name = "Period", Order = 1, GroupName = "Parameters")]
        public int Period { get; set; }

        [NinjaScriptProperty]
        [Range(1, 20)]
        [Display(Name = "Bias Smoothing", Order = 2, GroupName = "Parameters")]
        public int BiasSmoothing { get; set; }

        [NinjaScriptProperty]
        [Range(2, 20)]
        [Display(Name = "Cluster Lookback", Order = 3, GroupName = "Parameters")]
        public int ClusterLookback { get; set; }

        [NinjaScriptProperty]
        [Range(0.5, 10.0)]
        [Display(Name = "Cluster Bucket Size", Order = 4, GroupName = "Parameters")]
        public double ClusterBucketSize { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Enable Dashboard", Description = "Start WebSocket server for dashboard", Order = 1, GroupName = "Dashboard")]
        public bool EnableDashboard { get; set; }

        [NinjaScriptProperty]
        [Range(1024, 65535)]
        [Display(Name = "Dashboard Port", Description = "WebSocket port for dashboard", Order = 2, GroupName = "Dashboard")]
        public int DashboardPort { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Divergent Bars", Order = 0, GroupName = "Bar Colors")]
        public bool ShowDivergentBars { get; set; }

        [XmlIgnore]
        [Display(Name = "Divergent Bullish", Order = 1, GroupName = "Bar Colors")]
        public Brush DivergentBullishColor { get; set; }

        [Browsable(false)]
        public string DivergentBullishColorSerializable
        {
            get { return Serialize.BrushToString(DivergentBullishColor); }
            set { DivergentBullishColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Divergent Bearish", Order = 2, GroupName = "Bar Colors")]
        public Brush DivergentBearishColor { get; set; }

        [Browsable(false)]
        public string DivergentBearishColorSerializable
        {
            get { return Serialize.BrushToString(DivergentBearishColor); }
            set { DivergentBearishColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Display(Name = "Show Imbalances", Order = 1, GroupName = "Imbalances")]
        public bool ShowImbalances { get; set; }

        [NinjaScriptProperty]
        [Range(1.5, 10.0)]
        [Display(Name = "Imbalance Ratio", Order = 2, GroupName = "Imbalances")]
        public double ImbalanceRatio { get; set; }

        [NinjaScriptProperty]
        [Range(1, 1000)]
        [Display(Name = "Min Difference", Order = 3, GroupName = "Imbalances")]
        public int MinImbalanceVolume { get; set; }

        [NinjaScriptProperty]
        [Range(0.1f, 1.0f)]
        [Display(Name = "Opacity", Order = 4, GroupName = "Imbalances")]
        public float ImbalanceOpacity { get; set; }

        [XmlIgnore]
        [Display(Name = "Bullish Imbalance", Order = 5, GroupName = "Imbalances")]
        public Brush BullishImbalanceColor { get; set; }

        [Browsable(false)]
        public string BullishImbalanceColorSerializable
        {
            get { return Serialize.BrushToString(BullishImbalanceColor); }
            set { BullishImbalanceColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Bearish Imbalance", Order = 6, GroupName = "Imbalances")]
        public Brush BearishImbalanceColor { get; set; }

        [Browsable(false)]
        public string BearishImbalanceColorSerializable
        {
            get { return Serialize.BrushToString(BearishImbalanceColor); }
            set { BearishImbalanceColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Range(10, 10000)]
        [Display(Name = "Reference Volume", Description = "Volume level at which glows reach maximum size (continuous scaling)", Order = 7, GroupName = "Imbalances")]
        public int ReferenceVolume { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Volume Profile", Order = 1, GroupName = "Volume Profile")]
        public bool ShowVolumeProfile { get; set; }

        [NinjaScriptProperty]
        [Range(50, 400)]
        [Display(Name = "Profile Width", Order = 2, GroupName = "Volume Profile")]
        public int ProfileWidth { get; set; }

        [NinjaScriptProperty]
        [Range(50, 90)]
        [Display(Name = "Value Area %", Order = 3, GroupName = "Volume Profile")]
        public int ValueAreaPercent { get; set; }

        [NinjaScriptProperty]
        [Range(0.1f, 1.0f)]
        [Display(Name = "Profile Opacity", Order = 4, GroupName = "Volume Profile")]
        public float ProfileOpacity { get; set; }

        [XmlIgnore]
        [Display(Name = "Profile Color", Order = 5, GroupName = "Volume Profile")]
        public Brush ProfileColor { get; set; }

        [Browsable(false)]
        public string ProfileColorSerializable
        {
            get { return Serialize.BrushToString(ProfileColor); }
            set { ProfileColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Value Area Color", Order = 6, GroupName = "Volume Profile")]
        public Brush ValueAreaColor { get; set; }

        [Browsable(false)]
        public string ValueAreaColorSerializable
        {
            get { return Serialize.BrushToString(ValueAreaColor); }
            set { ValueAreaColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "POC Color", Order = 7, GroupName = "Volume Profile")]
        public Brush PocColor { get; set; }

        [Browsable(false)]
        public string PocColorSerializable
        {
            get { return Serialize.BrushToString(PocColor); }
            set { PocColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Display(Name = "Show Bar Value Area", Description = "Show per-bar POC line and VAH/VAL rectangle on each trigger bar", Order = 1, GroupName = "Bar Value Area")]
        public bool ShowBarValueArea { get; set; }

        [NinjaScriptProperty]
        [Range(0.05f, 1.0f)]
        [Display(Name = "Value Area Opacity", Description = "Opacity of the value area rectangle (0.05-1.0)", Order = 2, GroupName = "Bar Value Area")]
        public float BarValueAreaOpacity { get; set; }

        [NinjaScriptProperty]
        [Range(0, 10)]
        [Display(Name = "Width Padding", Description = "Extra pixels on each side of POC/VA lines (0-10)", Order = 3, GroupName = "Bar Value Area")]
        public int BarValueAreaPadding { get; set; }

        [XmlIgnore]
        [Display(Name = "Value Area Color", Order = 4, GroupName = "Bar Value Area")]
        public Brush BarVaColor { get; set; }

        [Browsable(false)]
        public string BarVaColorSerializable
        {
            get { return Serialize.BrushToString(BarVaColor); }
            set { BarVaColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "POC Color", Order = 5, GroupName = "Bar Value Area")]
        public Brush BarPocColor { get; set; }

        [Browsable(false)]
        public string BarPocColorSerializable
        {
            get { return Serialize.BrushToString(BarPocColor); }
            set { BarPocColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Range(2, 50)]
        [Display(Name = "Fast EMA Period", Order = 1, GroupName = "EMA")]
        public int FastEmaPeriod { get; set; }

        [NinjaScriptProperty]
        [Range(2, 50)]
        [Display(Name = "Slow EMA Period", Order = 2, GroupName = "EMA")]
        public int SlowEmaPeriod { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show EMA Lines", Description = "Plot EMA lines on chart", Order = 3, GroupName = "EMA")]
        public bool ShowEmaLines { get; set; }

        #endregion
    }
}
