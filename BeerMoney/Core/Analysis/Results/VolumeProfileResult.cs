using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace BeerMoney.Core.Analysis.Results
{
    public sealed class HighValueNode
    {
        public double Price { get; }
        public long Volume { get; }

        public HighValueNode(double price, long volume)
        {
            Price = price;
            Volume = volume;
        }
    }

    /// <summary>
    /// Result of volume profile calculation.
    /// </summary>
    public sealed class VolumeProfileResult
    {
        public bool IsValid { get; }
        public double POC { get; }
        public double VAH { get; }
        public double VAL { get; }
        public IReadOnlyDictionary<double, long> PriceVolumes { get; }
        public long MaxVolume { get; }
        public long TotalVolume { get; }
        public IReadOnlyList<HighValueNode> HighValueNodes { get; }

        private static readonly IReadOnlyList<HighValueNode> EmptyHvns = new List<HighValueNode>().AsReadOnly();

        private VolumeProfileResult(bool isValid, double poc, double vah, double val,
            IReadOnlyDictionary<double, long> priceVolumes, long maxVolume, long totalVolume,
            IReadOnlyList<HighValueNode> highValueNodes)
        {
            IsValid = isValid;
            POC = poc;
            VAH = vah;
            VAL = val;
            PriceVolumes = priceVolumes;
            MaxVolume = maxVolume;
            TotalVolume = totalVolume;
            HighValueNodes = highValueNodes;
        }

        public static VolumeProfileResult Create(double poc, double vah, double val,
            Dictionary<double, long> priceVolumes, long maxVolume, long totalVolume,
            IReadOnlyList<HighValueNode> highValueNodes)
        {
            return new VolumeProfileResult(true, poc, vah, val,
                new ReadOnlyDictionary<double, long>(priceVolumes), maxVolume, totalVolume,
                highValueNodes ?? EmptyHvns);
        }

        public static VolumeProfileResult Invalid()
        {
            return new VolumeProfileResult(false, 0, 0, 0, null, 0, 0, EmptyHvns);
        }
    }
}
