using System;
using BeerMoney.Core.Collections;
using BeerMoney.Core.Models;

namespace BeerMoney.Core.Trading
{
    /// <summary>
    /// Tracks fast and slow EMAs from completed bars and exposes
    /// spread, slope, cross, and bar-level features for downstream consumers.
    /// </summary>
    public sealed class EmaTracker
    {
        private const double EmaSlopeFlatThreshold = 0.01;

        private readonly Action<string> _log;
        private readonly CircularBuffer<BarData> _recentBars;
        private readonly CircularBuffer<double> _slowEmaHistory;

        private double _slowEma;
        private int _barCount;

        // Fast EMA state
        private double _fastEma;
        private readonly CircularBuffer<double> _fastEmaSlopeBuffer;
        private double _prevSpread;
        private double _lastSpreadChange;
        private int _barsSinceEmaCross;
        private int _emaCrossDirection;

        public int SlowEmaPeriod { get; set; } = 9;
        public int FastEmaPeriod { get; set; } = 5;

        // Feature properties
        public double SlowEmaValue => _slowEma;
        public double FastEmaValue => _fastEma;

        public double FastEmaSlope
        {
            get
            {
                if (_fastEmaSlopeBuffer.Count < 2) return 0.0;
                double oldest = _fastEmaSlopeBuffer[0];
                double newest = _fastEmaSlopeBuffer[_fastEmaSlopeBuffer.Count - 1];
                if (Math.Abs(oldest) < 1e-6) return 0.0;
                return ((newest - oldest) / oldest) * 100.0;
            }
        }
        public double Spread => _fastEma - _slowEma;
        public double SpreadChange => _lastSpreadChange;
        public int BarsSinceEmaCross => _barsSinceEmaCross;
        public int EmaCrossDirection => _emaCrossDirection;

        public int ImbalanceNet { get; private set; }
        public int SlowEmaSlope { get; private set; }
        public long LastBarDelta { get; private set; }

        public EmaTracker(Action<string> log = null)
        {
            _log = log ?? (_ => { });
            _recentBars = new CircularBuffer<BarData>(20);
            _slowEmaHistory = new CircularBuffer<double>(20);
            _fastEmaSlopeBuffer = new CircularBuffer<double>(15);
        }

        public void ProcessBar(BarData bar, double atr)
        {
            _barCount++;

            double slowMultiplier = 2.0 / (SlowEmaPeriod + 1);
            if (_barCount == 1)
                _slowEma = bar.Close;
            else
                _slowEma = (bar.Close - _slowEma) * slowMultiplier + _slowEma;

            double fastMultiplier = 2.0 / (FastEmaPeriod + 1);
            if (_barCount == 1)
                _fastEma = bar.Close;
            else
                _fastEma = (bar.Close - _fastEma) * fastMultiplier + _fastEma;

            _recentBars.Add(bar);
            _slowEmaHistory.Add(_slowEma);
            _fastEmaSlopeBuffer.Add(_fastEma);

            double currentSpread = _fastEma - _slowEma;
            _lastSpreadChange = _barCount > 1 ? currentSpread - _prevSpread : 0;
            int currentCrossDir = currentSpread >= 0 ? 1 : -1;
            if (_barCount <= 1)
            {
                _emaCrossDirection = currentCrossDir;
                _barsSinceEmaCross = 0;
            }
            else
            {
                if (currentCrossDir != _emaCrossDirection)
                {
                    _barsSinceEmaCross = 0;
                    _emaCrossDirection = currentCrossDir;
                }
                else
                {
                    _barsSinceEmaCross++;
                }
            }
            _prevSpread = currentSpread;

            LastBarDelta = bar.Delta;
            ImbalanceNet = bar.BullishImbalanceCount - bar.BearishImbalanceCount;
            SlowEmaSlope = ComputeSlowEmaSlope();
        }

        public void Reset()
        {
            _slowEma = 0;
            _fastEma = 0;
            _barCount = 0;
            ImbalanceNet = 0;
            SlowEmaSlope = 0;
            LastBarDelta = 0;
            _recentBars.Clear();
            _slowEmaHistory.Clear();
            _fastEmaSlopeBuffer.Clear();
            _prevSpread = 0;
            _lastSpreadChange = 0;
            _barsSinceEmaCross = 0;
            _emaCrossDirection = 0;
        }

        private int ComputeSlowEmaSlope()
        {
            if (_slowEmaHistory.Count < 3)
                return 0;

            double curr = _slowEmaHistory[_slowEmaHistory.Count - 1];
            double prev = _slowEmaHistory[_slowEmaHistory.Count - 3];
            double diff = curr - prev;

            if (Math.Abs(diff) < EmaSlopeFlatThreshold)
                return 0;
            return diff > 0 ? 1 : -1;
        }
    }
}
