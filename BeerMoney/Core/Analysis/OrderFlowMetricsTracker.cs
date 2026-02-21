using System;
using BeerMoney.Core.Analysis.Results;
using BeerMoney.Core.Collections;
using BeerMoney.Core.Models;

namespace BeerMoney.Core.Analysis
{
    /// <summary>
    /// Computes rolling-window order flow metrics from bar data.
    /// Instantiate once per timeframe (trigger and bias).
    /// </summary>
    public sealed class OrderFlowMetricsTracker
    {
        private struct BarSnapshot
        {
            public double Poc;
            public double Vah;
            public double Val;
            public double Close;
            public double Vwap;
            public long Volume;
            public int BullishImbalances;
            public int BearishImbalances;
            public long Delta;
        }

        private readonly CircularBuffer<BarSnapshot> _snapshots;
        private readonly int _lookback;
        private double _prevRollingDelta;

        public OrderFlowMetricsResult CurrentMetrics { get; private set; }

        public OrderFlowMetricsTracker(int lookback = 20)
        {
            _lookback = lookback;
            _snapshots = new CircularBuffer<BarSnapshot>(lookback);
            CurrentMetrics = OrderFlowMetricsResult.Invalid();
        }

        public OrderFlowMetricsResult ProcessBar(BarData bar, double vah, double val, double vwap, double atr)
        {
            var snap = new BarSnapshot
            {
                Poc = bar.PointOfControl,
                Vah = vah,
                Val = val,
                Close = bar.Close,
                Vwap = vwap,
                Volume = bar.Volume,
                BullishImbalances = bar.BullishImbalanceCount,
                BearishImbalances = bar.BearishImbalanceCount,
                Delta = bar.Delta
            };
            _snapshots.Add(snap);

            if (_snapshots.Count < 2 || atr <= 0)
            {
                CurrentMetrics = OrderFlowMetricsResult.Invalid();
                return CurrentMetrics;
            }

            double atrNorm = Math.Max(atr, 1e-9);
            int count = _snapshots.Count;
            int last = count - 1;
            int prev = count - 2;

            // POC Migration
            double pocMigration = (_snapshots[last].Poc - _snapshots[prev].Poc) / atrNorm;
            int pocDirection = ComputeDirection(pocMigration, 0.01);
            double pocTrendStrength = ComputePocTrendStrength(count);

            // Value Area
            double vaOverlap = ComputeVaOverlap(_snapshots[last], _snapshots[prev]);
            int vaMigration = ComputeVaMigration(_snapshots[last], _snapshots[prev]);
            double vaWidth = (_snapshots[last].Vah - _snapshots[last].Val) / atrNorm;
            double compressionRate = ComputeCompressionRate(atrNorm);
            bool isCompressing = compressionRate < -0.001;

            // Imbalance
            double imbalancePolarity = ComputeImbalancePolarity(count);
            bool isPolarized = Math.Abs(imbalancePolarity) >= 0.4;
            double setupDensity = ComputeSetupDensity(count);

            // VWAP
            double vwapSlope = (_snapshots[last].Vwap - _snapshots[0].Vwap) / atrNorm;
            int vwapRegime = ComputeDirection(vwapSlope, 0.05);

            // Delta
            double rollingDelta = ComputeRollingDelta(count, atrNorm);
            int rollingDeltaDirection = ComputeDirection(rollingDelta, 0.01);
            double rollingDeltaMomentum = rollingDelta - _prevRollingDelta;
            _prevRollingDelta = rollingDelta;

            // Volume
            double volumeTrend = ComputeVolumeTrend(count);

            // Agreement
            bool pocVwapAgreement = pocDirection != 0 && pocDirection == vwapRegime;

            // Conviction
            int bullPoints = 0;
            int bearPoints = 0;
            CountConviction(_snapshots[last].Close, _snapshots[last].Vwap, ref bullPoints, ref bearPoints);
            CountConvictionDirection(pocDirection, ref bullPoints, ref bearPoints);
            CountConvictionDirection(ComputeDirection(imbalancePolarity, 0.1), ref bullPoints, ref bearPoints);
            CountConvictionDirection(vaMigration, ref bullPoints, ref bearPoints);
            CountConvictionVolume(count, ref bullPoints, ref bearPoints);
            CountConvictionDirection(rollingDeltaDirection, ref bullPoints, ref bearPoints);

            int convictionScore = bullPoints + bearPoints;
            int convictionDirection = bullPoints > bearPoints ? 1 : (bearPoints > bullPoints ? -1 : 0);

            CurrentMetrics = OrderFlowMetricsResult.Create(
                pocMigration, pocDirection, pocTrendStrength,
                vaOverlap, vaMigration, vaWidth, isCompressing, compressionRate,
                imbalancePolarity, isPolarized, setupDensity,
                vwapSlope, vwapRegime,
                rollingDelta, rollingDeltaDirection, rollingDeltaMomentum,
                volumeTrend,
                pocVwapAgreement,
                convictionScore, convictionDirection);

            return CurrentMetrics;
        }

        /// <summary>
        /// Checks if POC is at an extreme (top/bottom 20%) of bar range with one-sided imbalance volume,
        /// indicating volume distribution heavily skewed to one side of the bar.
        /// </summary>
        public static bool IsVolumeSkew(BarData bar)
        {
            if (bar.Range <= 0) return false;

            double pocPosition = bar.PocPosition;
            if (pocPosition > 0.8 || pocPosition < 0.2)
            {
                // POC at extreme — check volume asymmetry
                long totalImb = bar.BullishImbVolumeSum + bar.BearishImbVolumeSum;
                if (totalImb <= 0) return false;

                if (pocPosition > 0.8)
                    return bar.BullishImbVolumeSum > bar.BearishImbVolumeSum;
                else
                    return bar.BearishImbVolumeSum > bar.BullishImbVolumeSum;
            }
            return false;
        }

        /// <summary>
        /// Checks if bar shows confirmed divergence: IsDivergent AND imbalances stacked against trend at extreme.
        /// </summary>
        public static bool IsDivergenceConfirmed(BarData bar)
        {
            if (!bar.IsDivergent) return false;
            if (bar.Range <= 0) return false;

            if (bar.IsBullish)
            {
                // Bullish bar with negative delta (divergent)
                // Confirm: bearish imbalances at the high (top 30%)
                return bar.BearishImbalanceCount > 0 && bar.BearishImbalanceAvgPosition > 0.4;
            }
            else if (bar.IsBearish)
            {
                // Bearish bar with positive delta (divergent)
                // Confirm: bullish imbalances at the low (bottom 30%)
                return bar.BullishImbalanceCount > 0 && bar.BullishImbalanceAvgPosition < -0.4;
            }
            return false;
        }

        private static int ComputeDirection(double value, double threshold)
        {
            if (value > threshold) return 1;
            if (value < -threshold) return -1;
            return 0;
        }

        private double ComputePocTrendStrength(int count)
        {
            if (count < 3) return 0;

            int sameDirectionCount = 0;
            int transitions = 0;

            for (int i = 2; i < count; i++)
            {
                double prevChange = _snapshots[i - 1].Poc - _snapshots[i - 2].Poc;
                double currChange = _snapshots[i].Poc - _snapshots[i - 1].Poc;

                if ((prevChange > 0 && currChange > 0) || (prevChange < 0 && currChange < 0))
                    sameDirectionCount++;

                transitions++;
            }

            return transitions > 0 ? (double)sameDirectionCount / transitions : 0;
        }

        private static double ComputeVaOverlap(BarSnapshot current, BarSnapshot prior)
        {
            double overlapLow = Math.Max(current.Val, prior.Val);
            double overlapHigh = Math.Min(current.Vah, prior.Vah);
            double intersection = Math.Max(0, overlapHigh - overlapLow);

            double unionLow = Math.Min(current.Val, prior.Val);
            double unionHigh = Math.Max(current.Vah, prior.Vah);
            double union = unionHigh - unionLow;

            return union > 0 ? intersection / union : 0;
        }

        private static int ComputeVaMigration(BarSnapshot current, BarSnapshot prior)
        {
            double currentMid = (current.Vah + current.Val) / 2.0;
            double priorMid = (prior.Vah + prior.Val) / 2.0;
            double diff = currentMid - priorMid;

            if (diff > 0.01) return 1;
            if (diff < -0.01) return -1;
            return 0;
        }

        private double ComputeCompressionRate(double atrNorm)
        {
            int count = _snapshots.Count;
            if (count < 3) return 0;

            // Linear regression slope of VA widths
            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
            for (int i = 0; i < count; i++)
            {
                double width = (_snapshots[i].Vah - _snapshots[i].Val) / atrNorm;
                sumX += i;
                sumY += width;
                sumXY += i * width;
                sumX2 += i * i;
            }

            double n = count;
            double denom = n * sumX2 - sumX * sumX;
            return denom > 0 ? (n * sumXY - sumX * sumY) / denom : 0;
        }

        private double ComputeImbalancePolarity(int count)
        {
            long sumBull = 0, sumBear = 0;
            for (int i = 0; i < count; i++)
            {
                sumBull += _snapshots[i].BullishImbalances;
                sumBear += _snapshots[i].BearishImbalances;
            }

            long total = sumBull + sumBear;
            return total > 0 ? (double)(sumBull - sumBear) / total : 0;
        }

        private double ComputeSetupDensity(int count)
        {
            long total = 0;
            for (int i = 0; i < count; i++)
                total += _snapshots[i].BullishImbalances + _snapshots[i].BearishImbalances;

            return count > 0 ? (double)total / count : 0;
        }

        private double ComputeRollingDelta(int count, double atrNorm)
        {
            long sum = 0;
            for (int i = 0; i < count; i++)
                sum += _snapshots[i].Delta;

            return sum / atrNorm;
        }

        private double ComputeVolumeTrend(int count)
        {
            if (count < 3) return 0;

            // Linear regression slope of volumes, normalized by mean
            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
            for (int i = 0; i < count; i++)
            {
                double vol = _snapshots[i].Volume;
                sumX += i;
                sumY += vol;
                sumXY += i * vol;
                sumX2 += i * i;
            }

            double n = count;
            double denom = n * sumX2 - sumX * sumX;
            if (denom <= 0) return 0;

            double slope = (n * sumXY - sumX * sumY) / denom;
            double mean = sumY / n;
            return mean > 0 ? slope / mean : 0;
        }

        private static void CountConviction(double close, double vwap, ref int bullPoints, ref int bearPoints)
        {
            if (close > vwap) bullPoints++;
            else if (close < vwap) bearPoints++;
        }

        private static void CountConvictionDirection(int direction, ref int bullPoints, ref int bearPoints)
        {
            if (direction > 0) bullPoints++;
            else if (direction < 0) bearPoints++;
        }

        private void CountConvictionVolume(int count, ref int bullPoints, ref int bearPoints)
        {
            if (count < 2) return;

            // Volume above average = conviction in current direction
            long sum = 0;
            for (int i = 0; i < count; i++)
                sum += _snapshots[i].Volume;

            double avg = (double)sum / count;
            long lastVolume = _snapshots[count - 1].Volume;

            if (lastVolume > avg)
            {
                // Volume supports the current bar's delta direction
                if (_snapshots[count - 1].Delta > 0)
                    bullPoints++;
                else if (_snapshots[count - 1].Delta < 0)
                    bearPoints++;
            }
        }
    }
}
