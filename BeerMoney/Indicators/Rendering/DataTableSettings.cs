namespace NinjaTrader.NinjaScript.Indicators.BeerMoney.Rendering
{
    /// <summary>
    /// Immutable configuration settings for data table rendering.
    /// Settings are set once at construction and cannot be modified during rendering.
    /// </summary>
    public sealed class DataTableSettings
    {
        public TablePosition Position { get; }
        public int FontSize { get; }
        public int OffsetX { get; }
        public int OffsetY { get; }
        public bool ShowVolumeProfile { get; }
        public int ProfileWidth { get; }

        public DataTableSettings(
            TablePosition position,
            int fontSize,
            int offsetX,
            int offsetY,
            bool showVolumeProfile,
            int profileWidth)
        {
            Position = position;
            FontSize = fontSize;
            OffsetX = offsetX;
            OffsetY = offsetY;
            ShowVolumeProfile = showVolumeProfile;
            ProfileWidth = profileWidth;
        }
    }
}
