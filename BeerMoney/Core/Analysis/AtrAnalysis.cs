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
            // Validate period is within reasonable range
            if (period < 1)
                return AtrResult.Invalid();

            if (bars == null || bars.Count < period + 1)
                return AtrResult.Invalid();

            var trueRanges = new List<double>();
            for (int i = 1; i < bars.Count; i++)
            {
                double tr = TrueRange(bars[i].High, bars[i].Low, bars[i - 1].Close);
                trueRanges.Add(tr);
            }

            if (trueRanges.Count < period)
                return AtrResult.Invalid();

            double currentAtr = 0;
            int startIdx = trueRanges.Count - period;
            for (int i = startIdx; i < trueRanges.Count; i++)
            {
                currentAtr += trueRanges[i];
            }
            currentAtr /= period;

            return AtrResult.Create(currentAtr);
        }
    }
}
