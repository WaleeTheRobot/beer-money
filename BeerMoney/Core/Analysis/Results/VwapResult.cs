namespace BeerMoney.Core.Analysis.Results
{
    /// <summary>
    /// Result of VWAP analysis.
    /// </summary>
    public sealed class VwapResult
    {
        public double Vwap { get; }
        public double PriceDistance { get; }
        public bool IsValid { get; }

        private VwapResult(double vwap, double priceDistance, bool isValid)
        {
            Vwap = vwap;
            PriceDistance = priceDistance;
            IsValid = isValid;
        }

        public static VwapResult Create(double vwap, double priceDistance)
        {
            return new VwapResult(vwap, priceDistance, true);
        }

        public static VwapResult Invalid()
        {
            return new VwapResult(0, 0, false);
        }
    }
}
