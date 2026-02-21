using System;
using System.Collections.Generic;
using BeerMoney.Core.Collections;

namespace BeerMoney.Core.Analysis
{
    /// <summary>
    /// Tracks imbalance price zones across a rolling window of bias bars.
    /// Bullish imbalances in the lower half of a bar create "support" zones.
    /// Bearish imbalances in the upper half create "resistance" zones.
    /// Overlapping zones across multiple bars form clusters (strong S/R).
    /// </summary>
    public sealed class ImbalanceClusterTracker
    {
        private struct BarZone
        {
            public Dictionary<int, int> BullBuckets;
            public Dictionary<int, int> BearBuckets;
        }

        private readonly CircularBuffer<BarZone> _history;
        private readonly Dictionary<int, int> _bullVotes;
        private readonly Dictionary<int, int> _bearVotes;
        private readonly double _bucketSize;

        public ImbalanceClusterTracker(int lookback, double bucketSize)
        {
            _history = new CircularBuffer<BarZone>(lookback);
            _bullVotes = new Dictionary<int, int>();
            _bearVotes = new Dictionary<int, int>();
            _bucketSize = bucketSize;
        }

        public void AddBar(double high, double low, int bullCount, int bearCount)
        {
            if (_history.IsFull)
            {
                var oldest = _history[0];
                SubtractVotes(oldest);
            }

            var zone = new BarZone
            {
                BullBuckets = new Dictionary<int, int>(),
                BearBuckets = new Dictionary<int, int>()
            };

            double range = high - low;
            if (range > 0)
            {
                if (bullCount > 0)
                {
                    int bLo = PriceToBucket(low);
                    int bHi = PriceToBucket(low + range * 0.5);
                    for (int b = bLo; b <= bHi; b++)
                        zone.BullBuckets[b] = bullCount;
                }

                if (bearCount > 0)
                {
                    int bLo = PriceToBucket(high - range * 0.5);
                    int bHi = PriceToBucket(high);
                    for (int b = bLo; b <= bHi; b++)
                        zone.BearBuckets[b] = bearCount;
                }
            }

            _history.Add(zone);
            AddVotes(zone);
        }

        public int GetBullStrength(double price)
        {
            int b = PriceToBucket(price);
            return GetVotes(_bullVotes, b - 1)
                 + GetVotes(_bullVotes, b)
                 + GetVotes(_bullVotes, b + 1);
        }

        public int GetBearStrength(double price)
        {
            int b = PriceToBucket(price);
            return GetVotes(_bearVotes, b - 1)
                 + GetVotes(_bearVotes, b)
                 + GetVotes(_bearVotes, b + 1);
        }

        public double FindNearestSupportDist(double price, int threshold = 3, int maxBuckets = 50)
        {
            int centerBucket = PriceToBucket(price);
            for (int offset = 0; offset <= maxBuckets; offset++)
            {
                int bucket = centerBucket - offset;
                int strength = GetVotes(_bullVotes, bucket - 1)
                             + GetVotes(_bullVotes, bucket)
                             + GetVotes(_bullVotes, bucket + 1);
                if (strength >= threshold)
                    return bucket * _bucketSize - price;
            }
            return 0;
        }

        public double FindNearestResistanceDist(double price, int threshold = 3, int maxBuckets = 50)
        {
            int centerBucket = PriceToBucket(price);
            for (int offset = 0; offset <= maxBuckets; offset++)
            {
                int bucket = centerBucket + offset;
                int strength = GetVotes(_bearVotes, bucket - 1)
                             + GetVotes(_bearVotes, bucket)
                             + GetVotes(_bearVotes, bucket + 1);
                if (strength >= threshold)
                    return bucket * _bucketSize - price;
            }
            return 0;
        }

        public void Reset()
        {
            _history.Clear();
            _bullVotes.Clear();
            _bearVotes.Clear();
        }

        private int PriceToBucket(double price)
        {
            return (int)Math.Round(price / _bucketSize);
        }

        private int GetVotes(Dictionary<int, int> votes, int bucket)
        {
            return votes.TryGetValue(bucket, out int v) ? v : 0;
        }

        private void AddVotes(BarZone zone)
        {
            foreach (var kv in zone.BullBuckets)
            {
                _bullVotes.TryGetValue(kv.Key, out int existing);
                _bullVotes[kv.Key] = existing + kv.Value;
            }
            foreach (var kv in zone.BearBuckets)
            {
                _bearVotes.TryGetValue(kv.Key, out int existing);
                _bearVotes[kv.Key] = existing + kv.Value;
            }
        }

        private void SubtractVotes(BarZone zone)
        {
            foreach (var kv in zone.BullBuckets)
            {
                if (_bullVotes.TryGetValue(kv.Key, out int existing))
                {
                    int newVal = existing - kv.Value;
                    if (newVal <= 0)
                        _bullVotes.Remove(kv.Key);
                    else
                        _bullVotes[kv.Key] = newVal;
                }
            }
            foreach (var kv in zone.BearBuckets)
            {
                if (_bearVotes.TryGetValue(kv.Key, out int existing))
                {
                    int newVal = existing - kv.Value;
                    if (newVal <= 0)
                        _bearVotes.Remove(kv.Key);
                    else
                        _bearVotes[kv.Key] = newVal;
                }
            }
        }
    }
}
