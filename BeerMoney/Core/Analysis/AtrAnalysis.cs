using System;
using System.Collections.Generic;
using BeerMoney.Core.Analysis.Results;
using BeerMoney.Core.Models;

namespace BeerMoney.Core.Analysis
{
    /// <summary>
    /// Calculates ATR (Average True Range).
    /// </summary>
    public static class AtrAnalysis
    {
        /// <summary>
        /// Calculates true range for a single bar.
        /// </summary>
        public static double TrueRange(double high, double low, double previousClose)
        {
            double hl = high - low;
            double hpc = Math.Abs(high - previousClose);
            double lpc = Math.Abs(low - previousClose);
            return Math.Max(hl, Math.Max(hpc, lpc));
        }

        /// <summary>
        /// Calculates ATR from bar data.
        /// </summary>
        /// <param name="bars">Historical bars.</param>
        /// <param name="period">ATR period. Must be at least 1.</param>
        /// <returns>ATR result with just the current ATR value.</returns>
        public static AtrResult Calculate(IReadOnlyList<BarData> bars, int period = 14)
        {
            if (period < 1)
                return AtrResult.Invalid();

            if (bars == null || bars.Count < period + 1)
                return AtrResult.Invalid();

            double sum = 0;
            int startIdx = bars.Count - period;
            for (int i = startIdx; i < bars.Count; i++)
            {
                sum += TrueRange(bars[i].High, bars[i].Low, bars[i - 1].Close);
            }

            return AtrResult.Create(sum / period);
        }
    }
}
