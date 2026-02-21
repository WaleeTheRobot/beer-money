using System;
using System.Text;
using BeerMoney.Core.Analysis;
using BeerMoney.Core.Analysis.Results;
using BeerMoney.Core.Models;
using BeerMoney.Core.Trading;

namespace NinjaTrader.NinjaScript.Indicators.BeerMoney
{
    /// <summary>
    /// Builds the JSON payload broadcast to the dashboard via WebSocket.
    /// Pure serialization — no NinjaTrader dependencies beyond the data passed in.
    /// </summary>
    internal static class DashboardPayloadBuilder
    {
        public static string Build(
            BarData completedBar,
            double atr,
            double slowEma,
            DateTime barTime,
            EmaTracker emaTracker,
            DataSeriesManager dataSeriesManager,
            double[] enrichedFeatures,
            VolumeProfileResult volumeProfile,
            OrderFlowMetricsResult triggerMetrics,
            OrderFlowMetricsResult biasMetrics)
        {
            var sb = new StringBuilder(4096);
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            double atrNorm = Math.Max(atr, 0.01);
            double barRange = completedBar.High - completedBar.Low;
            double closePos = barRange > 0 ? (completedBar.Close - completedBar.Low) / barRange : 0.5;
            double barRangeAtr = barRange / atrNorm;

            sb.Append("{\"type\":\"bar\",");
            sb.AppendFormat("\"timestamp\":\"{0:yyyy-MM-ddTHH:mm:ss}\",", barTime);

            AppendBarSection(sb, inv, completedBar, barRange, closePos, barRangeAtr);
            AppendFeaturesSection(sb, inv, completedBar, slowEma, atrNorm, barRange, closePos,
                emaTracker, dataSeriesManager, enrichedFeatures, volumeProfile);
            AppendDerivedSection(sb, inv, barRangeAtr, closePos, atrNorm, emaTracker, enrichedFeatures);
            AppendLevelsSection(sb, inv, completedBar, atr, slowEma, atrNorm,
                emaTracker, dataSeriesManager, volumeProfile);
            AppendMetricsSection(sb, inv, completedBar, triggerMetrics, biasMetrics);
            AppendSessionSection(sb, inv, barTime, enrichedFeatures);

            sb.Append("}");
            return sb.ToString();
        }

        private static void AppendBarSection(StringBuilder sb, IFormatProvider inv,
            BarData bar, double barRange, double closePos, double barRangeAtr)
        {
            double openPos = barRange > 0 ? (bar.Open - bar.Low) / barRange : 0.5;

            sb.Append("\"bar\":{");
            sb.AppendFormat(inv, "\"Open\":{0:F2},\"High\":{1:F2},\"Low\":{2:F2},\"Close\":{3:F2},",
                bar.Open, bar.High, bar.Low, bar.Close);
            sb.AppendFormat(inv, "\"Volume\":{0},\"BuyVolume\":{1},\"SellVolume\":{2},",
                bar.Volume, bar.BuyVolume, bar.SellVolume);
            sb.AppendFormat(inv, "\"Delta\":{0},\"CumulativeDelta\":{1},",
                bar.Delta, bar.CumulativeDelta);
            sb.AppendFormat(inv, "\"MaxDelta\":{0},\"MinDelta\":{1},",
                bar.MaxDelta, bar.MinDelta);
            sb.AppendFormat(inv, "\"POC\":{0:F2},\"BarScore\":{1:F4},\"IsDivergent\":{2},",
                bar.PointOfControl, bar.BarScore, bar.IsDivergent ? "true" : "false");
            sb.AppendFormat(inv, "\"ClosePosition\":{0:F4},\"OpenPosition\":{1:F4},\"BarRangeAtr\":{2:F4},",
                closePos, openPos, barRangeAtr);
            sb.AppendFormat(inv, "\"BullishImbalanceCount\":{0},\"BearishImbalanceCount\":{1}",
                bar.BullishImbalanceCount, bar.BearishImbalanceCount);
            sb.Append("},");
        }

        private static void AppendFeaturesSection(StringBuilder sb, IFormatProvider inv,
            BarData bar, double slowEma, double atrNorm, double barRange, double closePos,
            EmaTracker emaTracker, DataSeriesManager dataSeriesManager,
            double[] enrichedFeatures, VolumeProfileResult volumeProfile)
        {
            sb.Append("\"features\":{");

            double fastEma = emaTracker?.FastEmaValue ?? 0;
            double clv = barRange > 1e-6 ? (2.0 * bar.Close - bar.High - bar.Low) / barRange : 0.0;
            double olv = barRange > 1e-6 ? (2.0 * bar.Open - bar.High - bar.Low) / barRange : 0.0;
            double bullImbPos = bar.BullishImbalanceAvgPosition;
            double bearImbPos = bar.BearishImbalanceAvgPosition;
            double biasVwap = dataSeriesManager.BiasVwap;
            double triggerVwap = dataSeriesManager.TriggerVwap;
            double vah = volumeProfile?.VAH ?? 0;
            double val = volumeProfile?.VAL ?? 0;

            // F_ features
            sb.AppendFormat(inv, "\"F_SlowEmaDistance\":{0:F6},\"F_FastEmaDistance\":{1:F6},",
                (bar.Close - slowEma) / atrNorm, (bar.Close - fastEma) / atrNorm);
            sb.AppendFormat(inv, "\"F_EmaSpread\":{0:F6},\"F_EmaSpreadChange\":{1:F6},",
                (emaTracker?.Spread ?? 0) / atrNorm,
                (emaTracker?.SpreadChange ?? 0) / atrNorm);
            sb.AppendFormat(inv, "\"F_FastEmaSlope\":{0:F6},\"F_BarsSinceEmaCross\":{1},\"F_EmaCrossDirection\":{2},",
                emaTracker?.FastEmaSlope ?? 0, emaTracker?.BarsSinceEmaCross ?? 0,
                emaTracker?.EmaCrossDirection ?? 0);
            sb.AppendFormat(inv, "\"F_MovingAverageSlope\":{0:F6},", emaTracker?.SlowEmaSlope ?? 0);
            sb.AppendFormat(inv, "\"F_PocDistance\":{0:F6},\"F_TriggerVwapDistance\":{1:F6},\"F_VwapDiff\":{2:F6},",
                (bar.Close - bar.PointOfControl) / atrNorm,
                (bar.Close - triggerVwap) / atrNorm,
                (triggerVwap - biasVwap) / atrNorm);
            sb.AppendFormat(inv, "\"F_VahDistance\":{0:F6},\"F_ValDistance\":{1:F6},",
                (bar.Close - vah) / atrNorm, (bar.Close - val) / atrNorm);
            sb.AppendFormat(inv, "\"F_ClosePosition\":{0:F6},\"F_CloseLocationValue\":{1:F6},\"F_OpenLocationValue\":{2:F6},",
                closePos, clv, olv);
            sb.AppendFormat(inv, "\"F_DeltaEfficiency\":{0:F6},", dataSeriesManager.DeltaEfficiency);
            sb.AppendFormat(inv, "\"F_BullishImbalanceCount\":{0},\"F_BearishImbalanceCount\":{1},",
                bar.BullishImbalanceCount, bar.BearishImbalanceCount);
            sb.AppendFormat(inv, "\"F_BullishImbalancePosition\":{0:F6},\"F_BearishImbalancePosition\":{1:F6},",
                bullImbPos, bearImbPos);
            sb.AppendFormat(inv, "\"F_CloseVsBullishImbalance\":{0:F6},\"F_CloseVsBearishImbalance\":{1:F6},",
                bar.BullishImbalanceCount > 0 ? clv - bullImbPos : 0.0,
                bar.BearishImbalanceCount > 0 ? clv - bearImbPos : 0.0);

            // T_ and B_ features from enriched
            if (enrichedFeatures != null && enrichedFeatures.Length >= 30)
            {
                var ef = enrichedFeatures;
                sb.AppendFormat(inv, "\"T_DeltaSum\":{0:F6},\"T_DeltaShift\":{1:F6},\"T_DeltaMomentum\":{2:F6},\"T_BuyPct\":{3:F6},",
                    ef[0], ef[1], ef[2], ef[3]);
                sb.AppendFormat(inv, "\"T_ImbNet\":{0:F6},\"T_ImbIntensity\":{1:F6},\"T_ChannelPos\":{2:F6},\"T_ChannelHighDist\":{3:F6},",
                    ef[4], ef[5], ef[6], ef[7]);
                sb.AppendFormat(inv, "\"T_ChannelLowDist\":{0:F6},\"T_RangePctl\":{1:F6},\"T_VolumeRatio\":{2:F6},\"T_CumDeltaZScore\":{3:F6},",
                    ef[8], ef[9], ef[10], ef[11]);
                sb.AppendFormat(inv, "\"B_Delta\":{0:F6},\"B_ImbNet\":{1:F6},\"B_ClosePos\":{2:F6},\"B_SlowEmaDist\":{3:F6},",
                    ef[12], ef[13], ef[14], ef[15]);
                sb.AppendFormat(inv, "\"B_ZScore\":{0:F6},\"B_StdDev\":{1:F6},\"B_ChannelPos\":{2:F6},\"B_ChannelHighDist\":{3:F6},",
                    ef[16], ef[17], ef[18], ef[19]);
                sb.AppendFormat(inv, "\"B_ChannelLowDist\":{0:F6},\"B_Range\":{1:F6},",
                    ef[20], ef[21]);
                sb.AppendFormat(inv, "\"B_ClusterSupport\":{0:F6},\"B_ClusterResistance\":{1:F6},\"B_ClusterNet\":{2:F6},",
                    ef[22], ef[23], ef[24]);
                sb.AppendFormat(inv, "\"B_NearestSupportDist\":{0:F6},\"B_NearestResistanceDist\":{1:F6},",
                    ef[25], ef[26]);
                sb.AppendFormat(inv, "\"B_Open\":{0:F2},\"B_High\":{1:F2},\"B_Low\":{2:F2},\"B_Close\":{3:F2},",
                    dataSeriesManager.LastBiasBarOpen, dataSeriesManager.LastBiasBarHigh,
                    dataSeriesManager.LastBiasBarLow, dataSeriesManager.LastBiasBarClose);
                double biasSpread = (dataSeriesManager.BiasFastEma - dataSeriesManager.BiasSlowEma) / atrNorm;
                sb.AppendFormat(inv, "\"B_EmaSpread\":{0:F6},", biasSpread);
                sb.AppendFormat(inv, "\"SessionProgress\":{0:F6}", ef[27]);
            }
            sb.Append("},");
        }

        private static void AppendDerivedSection(StringBuilder sb, IFormatProvider inv,
            double barRangeAtr, double closePos, double atrNorm,
            EmaTracker emaTracker, double[] enrichedFeatures)
        {
            double sessionProgress = enrichedFeatures != null && enrichedFeatures.Length > 27
                ? enrichedFeatures[27] : 0;
            double signedSpread = (emaTracker?.Spread ?? 0) / atrNorm;
            double signedSlope = emaTracker?.FastEmaSlope ?? 0;
            double signedDeltaZ = enrichedFeatures != null && enrichedFeatures.Length > 11
                ? enrichedFeatures[11] : 0;
            double signedImbNet = enrichedFeatures != null && enrichedFeatures.Length > 4
                ? enrichedFeatures[4] : 0;
            double signedChannelPos = enrichedFeatures != null && enrichedFeatures.Length > 6
                ? enrichedFeatures[6] : 0;

            sb.Append("\"derived\":{");
            sb.AppendFormat(inv, "\"session_progress\":{0:F4},\"bar_range_atr\":{1:F4},\"close_position\":{2:F4},",
                sessionProgress, barRangeAtr, closePos);
            sb.AppendFormat(inv, "\"signed_spread\":{0:F4},\"signed_slope\":{1:F4},\"signed_delta_z\":{2:F4},",
                signedSpread, signedSlope, signedDeltaZ);
            sb.AppendFormat(inv, "\"signed_imb_net\":{0:F4},\"signed_channel_pos\":{1:F4}",
                signedImbNet, signedChannelPos);
            sb.Append("},");
        }

        private static void AppendLevelsSection(StringBuilder sb, IFormatProvider inv,
            BarData bar, double atr, double slowEma, double atrNorm,
            EmaTracker emaTracker, DataSeriesManager dataSeriesManager,
            VolumeProfileResult volumeProfile)
        {
            double fastEma = emaTracker?.FastEmaValue ?? 0;
            double biasVwap = dataSeriesManager.BiasVwap;
            double triggerVwap = dataSeriesManager.TriggerVwap;
            double vah = volumeProfile?.VAH ?? 0;
            double val = volumeProfile?.VAL ?? 0;

            sb.Append("\"levels\":{");
            sb.AppendFormat(inv, "\"BiasVwap\":{0:F2},\"TriggerVwap\":{1:F2},\"Atr\":{2:F4},",
                biasVwap, triggerVwap, atr);
            sb.AppendFormat(inv, "\"SlowEMA\":{0:F2},\"FastEMA\":{1:F2},", slowEma, fastEma);
            sb.AppendFormat(inv, "\"BarPOC\":{0:F2},\"VAH\":{1:F2},\"VAL\":{2:F2},\"POC\":{3:F2},",
                bar.PointOfControl, vah, val, volumeProfile?.POC ?? 0);
            sb.AppendFormat(inv, "\"DeltaEfficiency\":{0:F2},\"BiasSlowEma\":{1:F2},\"BiasFastEma\":{2:F2},",
                dataSeriesManager.DeltaEfficiency, dataSeriesManager.BiasSlowEma, dataSeriesManager.BiasFastEma);

            sb.Append("\"HVN\":[");
            var hvns = volumeProfile?.HighValueNodes;
            if (hvns != null && hvns.Count > 0)
            {
                for (int i = 0; i < hvns.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.AppendFormat(inv, "{{\"Price\":{0:F2},\"Volume\":{1}}}", hvns[i].Price, hvns[i].Volume);
                }
            }
            sb.Append("]},");
        }

        private static void AppendMetricsSection(StringBuilder sb, IFormatProvider inv, BarData bar,
            OrderFlowMetricsResult triggerMetrics, OrderFlowMetricsResult biasMetrics)
        {
            sb.Append("\"metrics\":{");

            AppendSingleTimeframeMetrics(sb, inv, "trigger", triggerMetrics);
            sb.Append(",");
            AppendSingleTimeframeMetrics(sb, inv, "bias", biasMetrics);

            bool volumeSkew = OrderFlowMetricsTracker.IsVolumeSkew(bar);
            bool divergenceConfirmed = OrderFlowMetricsTracker.IsDivergenceConfirmed(bar);
            sb.AppendFormat(",\"volume_skew\":{0},\"divergence_confirmed\":{1}",
                volumeSkew ? "true" : "false", divergenceConfirmed ? "true" : "false");

            sb.Append("},");
        }

        private static void AppendSingleTimeframeMetrics(StringBuilder sb, IFormatProvider inv,
            string name, OrderFlowMetricsResult m)
        {
            sb.AppendFormat("\"{0}\":{{", name);

            if (m != null && m.IsValid)
            {
                sb.AppendFormat(inv, "\"poc_migration\":{0:F4},\"poc_direction\":{1},\"poc_trend_strength\":{2:F4},",
                    m.PocMigration, m.PocDirection, m.PocTrendStrength);
                sb.AppendFormat(inv, "\"va_overlap\":{0:F4},\"va_migration\":{1},\"va_width\":{2:F4},",
                    m.VaOverlap, m.VaMigration, m.VaWidth);
                sb.AppendFormat(inv, "\"is_compressing\":{0},\"compression_rate\":{1:F4},",
                    m.IsCompressing ? "true" : "false", m.CompressionRate);
                sb.AppendFormat(inv, "\"imbalance_polarity\":{0:F4},\"is_polarized\":{1},\"setup_density\":{2:F4},",
                    m.ImbalancePolarity, m.IsPolarized ? "true" : "false", m.SetupDensity);
                sb.AppendFormat(inv, "\"vwap_slope\":{0:F4},\"vwap_regime\":{1},",
                    m.VwapSlope, m.VwapRegime);
                sb.AppendFormat(inv, "\"rolling_delta\":{0:F4},\"rolling_delta_direction\":{1},\"rolling_delta_momentum\":{2:F4},",
                    m.RollingDelta, m.RollingDeltaDirection, m.RollingDeltaMomentum);
                sb.AppendFormat(inv, "\"volume_trend\":{0:F4},\"poc_vwap_agreement\":{1},",
                    m.VolumeTrend, m.PocVwapAgreement ? "true" : "false");
                sb.AppendFormat(inv, "\"conviction_score\":{0},\"conviction_direction\":{1}",
                    m.ConvictionScore, m.ConvictionDirection);
            }
            else
            {
                sb.Append("\"poc_migration\":0,\"poc_direction\":0,\"poc_trend_strength\":0,");
                sb.Append("\"va_overlap\":0,\"va_migration\":0,\"va_width\":0,");
                sb.Append("\"is_compressing\":false,\"compression_rate\":0,");
                sb.Append("\"imbalance_polarity\":0,\"is_polarized\":false,\"setup_density\":0,");
                sb.Append("\"vwap_slope\":0,\"vwap_regime\":0,");
                sb.Append("\"rolling_delta\":0,\"rolling_delta_direction\":0,\"rolling_delta_momentum\":0,");
                sb.Append("\"volume_trend\":0,\"poc_vwap_agreement\":false,");
                sb.Append("\"conviction_score\":0,\"conviction_direction\":0");
            }

            sb.Append("}");
        }

        private static void AppendSessionSection(StringBuilder sb, IFormatProvider inv,
            DateTime barTime, double[] enrichedFeatures)
        {
            double sessionProgress = enrichedFeatures != null && enrichedFeatures.Length > 27
                ? enrichedFeatures[27] : 0;
            int dayInt = barTime.Year * 10000 + barTime.Month * 100 + barTime.Day;
            int timeInt = barTime.Hour * 10000 + barTime.Minute * 100 + barTime.Second;

            sb.Append("\"session\":{");
            sb.AppendFormat(inv, "\"day\":{0},\"time\":{1},\"progress\":{2:F4}",
                dayInt, timeInt, sessionProgress);
            sb.Append("}");
        }
    }
}
