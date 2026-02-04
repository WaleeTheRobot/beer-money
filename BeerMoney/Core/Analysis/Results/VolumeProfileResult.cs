using System.Collections.Generic;

namespace BeerMoney.Core.Analysis.Results
{
    /// <summary>
    /// Result of volume profile calculation.
    /// </summary>
    public sealed class VolumeProfileResult
    {
        public bool IsValid { get; }
        public double POC { get; }
        public double VAH { get; }
        public double VAL { get; }
        public Dictionary<double, long> PriceVolumes { get; }
        public long MaxVolume { get; }
        public long TotalVolume { get; }

        private VolumeProfileResult(bool isValid, double poc, double vah, double val,
            Dictionary<double, long> priceVolumes, long maxVolume, long totalVolume)
        {
            IsValid = isValid;
            POC = poc;
            VAH = vah;
            VAL = val;
            PriceVolumes = priceVolumes;
            MaxVolume = maxVolume;
            TotalVolume = totalVolume;
        }

        public static VolumeProfileResult Create(double poc, double vah, double val,
            Dictionary<double, long> priceVolumes, long maxVolume, long totalVolume)
        {
            return new VolumeProfileResult(true, poc, vah, val, priceVolumes, maxVolume, totalVolume);
        }

        public static VolumeProfileResult Invalid()
        {
            return new VolumeProfileResult(false, 0, 0, 0, null, 0, 0);
        }
    }
}
