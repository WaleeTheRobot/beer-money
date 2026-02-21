using System;
using BeerMoney.Core.Analysis;
using BeerMoney.Core.Collections;
using BeerMoney.Core.Models;

namespace BeerMoney.Core.Trading
{
    /// <summary>
    /// Computes 30 enriched features from live bar buffers.
    /// Produces identical values to the strategy-analyzer-exporter's
    /// TriggerRolling + BiasBar + BiasCluster + SessionContext features.
    /// </summary>
    public sealed class EnrichedFeatureComputer
    {
        public const int NumEnrichedFeatures = 30;
        private const double STRENGTH_NORM = 20.0;
        private const int NEAREST_THRESHOLD = 3;
        private const int NEAREST_MAX_BUCKETS = 50;
        private const double SESSION_START_MINUTES = 570.0; // 9:30 AM
        private const double SESSION_SPAN_MINUTES = 385.0;  // 9:30 to 15:55
        private const int Z_SCORE_LOOKBACK = 5;
        private const int EDGE_BARS = 3;

        private readonly CircularBuffer<double> _biasCloseHistory;
        private readonly CircularBuffer<double> _biasHighHistory;
        private readonly CircularBuffer<double> _biasLowHistory;
        private double _lastBiasSlowEma;
        private double[] _lastBiasFeatures;

        public double LastBiasOpen { get; private set; }
        public double LastBiasHigh { get; private set; }
        public double LastBiasLow { get; private set; }
        public double LastBiasClose { get; private set; }

        public EnrichedFeatureComputer()
        {
            _biasCloseHistory = new CircularBuffer<double>(Z_SCORE_LOOKBACK);
            _biasHighHistory = new CircularBuffer<double>(Z_SCORE_LOOKBACK);
            _biasLowHistory = new CircularBuffer<double>(Z_SCORE_LOOKBACK);
            _lastBiasFeatures = new double[10];
        }

        public void ResetDay()
        {
            _biasCloseHistory.Clear();
            _biasHighHistory.Clear();
            _biasLowHistory.Clear();
            _lastBiasFeatures = new double[10];
            LastBiasOpen = 0;
            LastBiasHigh = 0;
            LastBiasLow = 0;
            LastBiasClose = 0;
        }

        public void ProcessBiasBar(BarData bar, double biasSlowEma, double atr)
        {
            _lastBiasSlowEma = biasSlowEma;

            LastBiasOpen = bar.Open;
            LastBiasHigh = bar.High;
            LastBiasLow = bar.Low;
            LastBiasClose = bar.Close;

            _biasCloseHistory.Add(bar.Close);
            _biasHighHistory.Add(bar.High);
            _biasLowHistory.Add(bar.Low);

            double atrSafe = Math.Max(atr, 0.01);
            double biasRange = bar.High - bar.Low;
            double closePos = biasRange > 0.0001 ? (bar.Close - bar.Low) / biasRange : 0.5;
            double slowEmaDist = (bar.Close - biasSlowEma) / atrSafe;

            double zScore = 0, stdDev = 0;
            if (_biasCloseHistory.Count >= 2)
            {
                int n = _biasCloseHistory.Count;
                double sum = 0, sumSq = 0;
                for (int i = 0; i < n; i++)
                {
                    double val = _biasCloseHistory[i];
                    sum += val;
                    sumSq += val * val;
                }
                double mean = sum / n;
                double variance = sumSq / n - mean * mean;
                double std = variance > 0 ? Math.Sqrt(variance) : 0;
                stdDev = std / atrSafe;
                if (std > 0)
                    zScore = (bar.Close - biasSlowEma) / std;
            }

            double channelPos = 0.5, channelHighDist = 0, channelLowDist = 0;
            if (_biasHighHistory.Count >= 2 && _biasLowHistory.Count >= 2)
            {
                double chanHigh = double.MinValue, chanLow = double.MaxValue;
                for (int i = 0; i < _biasHighHistory.Count; i++)
                    if (_biasHighHistory[i] > chanHigh) chanHigh = _biasHighHistory[i];
                for (int i = 0; i < _biasLowHistory.Count; i++)
                    if (_biasLowHistory[i] < chanLow) chanLow = _biasLowHistory[i];

                double chanSpan = chanHigh - chanLow;
                if (chanSpan > 0.0001) channelPos = (bar.Close - chanLow) / chanSpan;
                channelHighDist = (bar.Close - chanHigh) / atrSafe;
                channelLowDist = (bar.Close - chanLow) / atrSafe;
            }

            int imbNet = bar.BullishImbalanceCount - bar.BearishImbalanceCount;

            _lastBiasFeatures[0] = bar.Delta / atrSafe;
            _lastBiasFeatures[1] = imbNet;
            _lastBiasFeatures[2] = closePos;
            _lastBiasFeatures[3] = slowEmaDist;
            _lastBiasFeatures[4] = zScore;
            _lastBiasFeatures[5] = stdDev;
            _lastBiasFeatures[6] = channelPos;
            _lastBiasFeatures[7] = channelHighDist;
            _lastBiasFeatures[8] = channelLowDist;
            _lastBiasFeatures[9] = biasRange / atrSafe;
        }

        public double[] Compute(CircularBuffer<BarData> triggerBars, BarData currentBar,
            double atr, double triggerDeltaEwm,
            ImbalanceClusterTracker clusterTracker, int timeHHMMSS)
        {
            var f = new double[NumEnrichedFeatures];
            double atrSafe = Math.Max(atr, 0.01);

            int n = triggerBars != null ? triggerBars.Count : 0;

            if (n >= 2)
            {
                long deltaSum = 0;
                for (int i = 0; i < n; i++) deltaSum += triggerBars[i].Delta;
                f[0] = deltaSum / (atrSafe * n);

                if (n >= EDGE_BARS * 2)
                {
                    double firstSum = 0, lastSum = 0;
                    for (int i = 0; i < EDGE_BARS; i++)
                    {
                        firstSum += triggerBars[i].Delta;
                        lastSum += triggerBars[n - EDGE_BARS + i].Delta;
                    }
                    f[1] = (lastSum / EDGE_BARS - firstSum / EDGE_BARS) / atrSafe;
                }

                f[2] = triggerDeltaEwm / atrSafe;

                long totalBuy = 0, totalVol = 0;
                for (int i = 0; i < n; i++)
                {
                    totalBuy += triggerBars[i].BuyVolume;
                    totalVol += triggerBars[i].BuyVolume + triggerBars[i].SellVolume;
                }
                f[3] = totalVol > 0 ? (double)totalBuy / totalVol : 0.5;

                int bullSum = 0, bearSum = 0;
                for (int i = 0; i < n; i++)
                {
                    bullSum += triggerBars[i].BullishImbalanceCount;
                    bearSum += triggerBars[i].BearishImbalanceCount;
                }
                f[4] = (double)(bullSum - bearSum) / n;
                f[5] = (double)(bullSum + bearSum) / n;

                double bufHigh = double.MinValue, bufLow = double.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    if (triggerBars[i].High > bufHigh) bufHigh = triggerBars[i].High;
                    if (triggerBars[i].Low < bufLow) bufLow = triggerBars[i].Low;
                }
                double channelSpan = bufHigh - bufLow;
                double close = currentBar.Close;
                f[6] = channelSpan > 0.0001 ? (close - bufLow) / channelSpan : 0.5;
                f[7] = (close - bufHigh) / atrSafe;
                f[8] = (close - bufLow) / atrSafe;

                double currentRange = currentBar.High - currentBar.Low;
                int ranksBelow = 0;
                for (int i = 0; i < n; i++)
                {
                    double barRange = triggerBars[i].High - triggerBars[i].Low;
                    if (barRange < currentRange) ranksBelow++;
                }
                f[9] = (double)ranksBelow / n;

                long currentVol = currentBar.BuyVolume + currentBar.SellVolume;
                long bufferVolSum = 0;
                for (int i = 0; i < n; i++)
                    bufferVolSum += triggerBars[i].BuyVolume + triggerBars[i].SellVolume;
                double avgVol = (double)bufferVolSum / n;
                f[10] = avgVol > 0 ? currentVol / avgVol : 1.0;

                double cdSum = 0, cdSumSq = 0;
                for (int i = 0; i < n; i++)
                {
                    double cd = triggerBars[i].CumulativeDelta;
                    cdSum += cd;
                    cdSumSq += cd * cd;
                }
                double cdMean = cdSum / n;
                double cdVar = cdSumSq / n - cdMean * cdMean;
                double cdStd = cdVar > 0 ? Math.Sqrt(cdVar) : 0;
                f[11] = cdStd > 0 ? (currentBar.CumulativeDelta - cdMean) / cdStd : 0;
            }
            else
            {
                f[3] = 0.5;
                f[6] = 0.5;
                f[9] = 0.5;
                f[10] = 1.0;
            }

            // Bias Bar Features (10)
            f[12] = _lastBiasFeatures[0];
            f[13] = _lastBiasFeatures[1];
            f[14] = _lastBiasFeatures[2];
            f[15] = _lastBiasFeatures[3];
            f[16] = _lastBiasFeatures[4];
            f[17] = _lastBiasFeatures[5];
            f[18] = _lastBiasFeatures[6];
            f[19] = _lastBiasFeatures[7];
            f[20] = _lastBiasFeatures[8];
            f[21] = _lastBiasFeatures[9];

            // Bias Cluster Features (5)
            double triggerClose = currentBar != null ? currentBar.Close : 0;
            if (clusterTracker != null)
            {
                int bullStrength = clusterTracker.GetBullStrength(triggerClose);
                int bearStrength = clusterTracker.GetBearStrength(triggerClose);
                f[22] = bullStrength / STRENGTH_NORM;
                f[23] = bearStrength / STRENGTH_NORM;
                f[24] = (bullStrength - bearStrength) / STRENGTH_NORM;
                f[25] = clusterTracker.FindNearestSupportDist(
                    triggerClose, NEAREST_THRESHOLD, NEAREST_MAX_BUCKETS) / atrSafe;
                f[26] = clusterTracker.FindNearestResistanceDist(
                    triggerClose, NEAREST_THRESHOLD, NEAREST_MAX_BUCKETS) / atrSafe;
            }

            // Session Context Features (3)
            int hours = timeHHMMSS / 10000;
            int minutes = (timeHHMMSS % 10000) / 100;
            int seconds = timeHHMMSS % 100;
            double totalMinutes = hours * 60.0 + minutes + seconds / 60.0;
            f[27] = Math.Max(0, Math.Min(1,
                (totalMinutes - SESSION_START_MINUTES) / SESSION_SPAN_MINUTES));

            f[28] = currentBar != null && currentBar.MaxBullishImbPrice > 0
                ? (currentBar.Close - currentBar.MaxBullishImbPrice) / atrSafe : 0;
            f[29] = currentBar != null && currentBar.MaxBearishImbPrice > 0
                ? (currentBar.Close - currentBar.MaxBearishImbPrice) / atrSafe : 0;

            return f;
        }

        public static readonly string[] FeatureNames = new string[]
        {
            "T_DeltaSum", "T_DeltaShift", "T_DeltaMomentum", "T_BuyPct",
            "T_ImbNet", "T_ImbIntensity", "T_ChannelPos", "T_ChannelHighDist",
            "T_ChannelLowDist", "T_RangePctl", "T_VolumeRatio", "T_CumDeltaZScore",
            "B_Delta", "B_ImbNet", "B_ClosePos", "B_SlowEmaDist",
            "B_ZScore", "B_StdDev", "B_ChannelPos", "B_ChannelHighDist",
            "B_ChannelLowDist", "B_Range",
            "B_ClusterSupport", "B_ClusterResistance", "B_ClusterNet",
            "B_NearestSupportDist", "B_NearestResistanceDist",
            "SessionProgress", "MaxBullImbDist", "MaxBearImbDist"
        };
    }
}
