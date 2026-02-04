using System.Collections.Generic;
using BeerMoney.Core.Analysis.Results;
using BeerMoney.Core.Models;

namespace BeerMoney.Core.Analysis
{
    /// <summary>
    /// Calculates rolling VWAP (Volume Weighted Average Price).
    /// </summary>
    public static class VwapAnalysis
    {
        /// <summary>
        /// Calculates rolling VWAP over the given bars.
        /// </summary>
        /// <param name="bars">Historical bars.</param>
        /// <param name="currentPrice">Current price for distance calculation.</param>
        /// <returns>VWAP analysis result.</returns>
        public static VwapResult Calculate(IReadOnlyList<BarData> bars, double currentPrice)
        {
            if (bars == null || bars.Count == 0)
                return VwapResult.Invalid();

            double cumulativeTPV = 0;
            long cumulativeVolume = 0;

            for (int i = 0; i < bars.Count; i++)
            {
                var bar = bars[i];
                double tp = bar.TypicalPrice;
                cumulativeTPV += tp * bar.Volume;
                cumulativeVolume += bar.Volume;
            }

            if (cumulativeVolume == 0)
                return VwapResult.Invalid();

            double vwap = cumulativeTPV / cumulativeVolume;
            double priceDistance = currentPrice - vwap;

            return VwapResult.Create(vwap, priceDistance);
        }
    }
}
