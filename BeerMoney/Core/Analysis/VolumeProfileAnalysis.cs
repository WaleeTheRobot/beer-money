using System;
using System.Collections.Generic;
using BeerMoney.Core.Analysis.Results;

namespace BeerMoney.Core.Analysis
{
    /// <summary>
    /// Calculates volume profile with POC, VAH, and VAL from volumetric bar data.
    /// </summary>
    public static class VolumeProfileAnalysis
    {
        /// <summary>
        /// Calculates volume profile from price/volume data.
        /// </summary>
        /// <param name="priceVolumes">Dictionary of price level to total volume.</param>
        /// <param name="valueAreaPercent">Percentage of volume for value area (default 70%).</param>
        /// <returns>Volume profile result with POC, VAH, VAL.</returns>
        public static VolumeProfileResult Calculate(Dictionary<double, long> priceVolumes, double valueAreaPercent = 0.70)
        {
            if (priceVolumes == null || priceVolumes.Count == 0)
                return VolumeProfileResult.Invalid();

            // Find POC (price with maximum volume)
            // When multiple prices have the same maximum volume, select the highest price for deterministic behavior
            double poc = 0;
            long maxVolume = 0;
            long totalVolume = 0;

            foreach (var kvp in priceVolumes)
            {
                totalVolume += kvp.Value;
                // Use >= with price comparison to ensure deterministic tie-breaking (higher price wins)
                if (kvp.Value > maxVolume || (kvp.Value == maxVolume && kvp.Key > poc))
                {
                    maxVolume = kvp.Value;
                    poc = kvp.Key;
                }
            }

            if (totalVolume == 0)
                return VolumeProfileResult.Invalid();

            // Sort prices for value area calculation
            var sortedPrices = new List<double>(priceVolumes.Keys);
            sortedPrices.Sort();

            // Find POC index
            int pocIndex = sortedPrices.IndexOf(poc);
            if (pocIndex < 0)
                return VolumeProfileResult.Invalid();

            // Calculate value area (70% of volume centered around POC)
            long valueAreaTarget = (long)(totalVolume * valueAreaPercent);
            long valueAreaVolume = priceVolumes[poc];

            int lowIndex = pocIndex;
            int highIndex = pocIndex;

            // Expand outward from POC until we reach target volume
            while (valueAreaVolume < valueAreaTarget && (lowIndex > 0 || highIndex < sortedPrices.Count - 1))
            {
                long volumeBelow = lowIndex > 0 ? priceVolumes[sortedPrices[lowIndex - 1]] : 0;
                long volumeAbove = highIndex < sortedPrices.Count - 1 ? priceVolumes[sortedPrices[highIndex + 1]] : 0;

                // Add the side with more volume (or the only available side)
                if (lowIndex == 0)
                {
                    highIndex++;
                    valueAreaVolume += volumeAbove;
                }
                else if (highIndex == sortedPrices.Count - 1)
                {
                    lowIndex--;
                    valueAreaVolume += volumeBelow;
                }
                else if (volumeBelow >= volumeAbove)
                {
                    lowIndex--;
                    valueAreaVolume += volumeBelow;
                }
                else
                {
                    highIndex++;
                    valueAreaVolume += volumeAbove;
                }
            }

            double val = sortedPrices[lowIndex];
            double vah = sortedPrices[highIndex];

            // Compute top 5 high value nodes sorted by volume descending
            var hvnList = new List<KeyValuePair<double, long>>(priceVolumes);
            hvnList.Sort((a, b) => b.Value.CompareTo(a.Value));
            int hvnCount = Math.Min(5, hvnList.Count);
            var hvns = new List<HighValueNode>(hvnCount);
            for (int i = 0; i < hvnCount; i++)
                hvns.Add(new HighValueNode(hvnList[i].Key, hvnList[i].Value));

            return VolumeProfileResult.Create(poc, vah, val, priceVolumes, maxVolume, totalVolume, hvns.AsReadOnly());
        }
    }
}
