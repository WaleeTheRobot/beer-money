namespace BeerMoney.Core.Analysis.Results
{
    /// <summary>
    /// Result of ATR analysis.
    /// </summary>
    public sealed class AtrResult
    {
        public double CurrentAtr { get; }
        public bool IsValid { get; }

        private AtrResult(double currentAtr, bool isValid)
        {
            CurrentAtr = currentAtr;
            IsValid = isValid;
        }

        public static AtrResult Create(double currentAtr)
        {
            return new AtrResult(currentAtr, true);
        }

        public static AtrResult Invalid()
        {
            return new AtrResult(0, false);
        }
    }
}
