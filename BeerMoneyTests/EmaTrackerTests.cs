using System;
using Xunit;
using BeerMoney.Core.Models;
using BeerMoney.Core.Trading;

namespace BeerMoneyTests
{
    public class EmaTrackerTests
    {
        private static BarData MakeBar(double close, long buyVolume = 100, long sellVolume = 100,
            ImbalanceMetrics imbalance = default)
        {
            return new BarData(
                DateTime.UtcNow, 0,
                close, close + 1, close - 1, close,
                buyVolume + sellVolume, buyVolume, sellVolume,
                imbalance: imbalance);
        }

        [Fact]
        public void FirstBar_EmaEqualsClose()
        {
            var tracker = new EmaTracker();
            var bar = MakeBar(100);
            tracker.ProcessBar(bar, 5.0);

            Assert.Equal(100, tracker.SlowEmaValue);
            Assert.Equal(100, tracker.FastEmaValue);
        }

        [Fact]
        public void Ema_ConvergesToConstantPrice()
        {
            var tracker = new EmaTracker();
            // Feed 50 bars at price 100 — EMA should converge to 100
            for (int i = 0; i < 50; i++)
                tracker.ProcessBar(MakeBar(100), 5.0);

            Assert.Equal(100, tracker.SlowEmaValue, 2);
            Assert.Equal(100, tracker.FastEmaValue, 2);
        }

        [Fact]
        public void FastEma_ReactsFasterThanSlowEma()
        {
            var tracker = new EmaTracker();
            // Initialize at 100
            for (int i = 0; i < 20; i++)
                tracker.ProcessBar(MakeBar(100), 5.0);

            // Jump to 110 — fast EMA should move faster
            tracker.ProcessBar(MakeBar(110), 5.0);

            Assert.True(tracker.FastEmaValue > tracker.SlowEmaValue);
        }

        [Fact]
        public void Spread_PositiveWhenFastAboveSlow()
        {
            var tracker = new EmaTracker();
            for (int i = 0; i < 20; i++)
                tracker.ProcessBar(MakeBar(100), 5.0);

            tracker.ProcessBar(MakeBar(110), 5.0);
            Assert.True(tracker.Spread > 0);
        }

        [Fact]
        public void EmaCrossDirection_InitializedOnFirstBar()
        {
            var tracker = new EmaTracker();
            tracker.ProcessBar(MakeBar(100), 5.0);
            // First bar: fast == slow == 100, spread = 0 → direction = 1 (>= 0)
            Assert.Equal(1, tracker.EmaCrossDirection);
            Assert.Equal(0, tracker.BarsSinceEmaCross);
        }

        [Fact]
        public void BarsSinceEmaCross_IncrementsOnSameDirection()
        {
            var tracker = new EmaTracker();
            // Feed constant price — no cross happens, bars since should increment
            for (int i = 0; i < 5; i++)
                tracker.ProcessBar(MakeBar(100), 5.0);

            Assert.True(tracker.BarsSinceEmaCross >= 3);
        }

        [Fact]
        public void EmaCrossDirection_ResetsOnCross()
        {
            var tracker = new EmaTracker();
            // Build up fast > slow
            for (int i = 0; i < 20; i++)
                tracker.ProcessBar(MakeBar(100 + i), 5.0);

            int dirBefore = tracker.EmaCrossDirection;

            // Suddenly drop price to force cross
            for (int i = 0; i < 20; i++)
                tracker.ProcessBar(MakeBar(50), 5.0);

            int dirAfter = tracker.EmaCrossDirection;
            Assert.NotEqual(dirBefore, dirAfter);
        }

        [Fact]
        public void SpreadChange_IsZeroOnFirstBar()
        {
            var tracker = new EmaTracker();
            tracker.ProcessBar(MakeBar(100), 5.0);
            Assert.Equal(0, tracker.SpreadChange);
        }

        [Fact]
        public void SlowEmaSlope_ZeroWithFewBars()
        {
            var tracker = new EmaTracker();
            tracker.ProcessBar(MakeBar(100), 5.0);
            tracker.ProcessBar(MakeBar(101), 5.0);
            Assert.Equal(0, tracker.SlowEmaSlope);
        }

        [Fact]
        public void SlowEmaSlope_PositiveWhenRising()
        {
            var tracker = new EmaTracker();
            // Rising prices should produce positive slow EMA slope
            for (int i = 0; i < 10; i++)
                tracker.ProcessBar(MakeBar(100 + i * 5), 5.0);

            Assert.Equal(1, tracker.SlowEmaSlope);
        }

        [Fact]
        public void SlowEmaSlope_NegativeWhenFalling()
        {
            var tracker = new EmaTracker();
            for (int i = 0; i < 10; i++)
                tracker.ProcessBar(MakeBar(200 - i * 5), 5.0);

            Assert.Equal(-1, tracker.SlowEmaSlope);
        }

        [Fact]
        public void LastBarDelta_UpdatedOnProcessBar()
        {
            var tracker = new EmaTracker();
            var bar = MakeBar(100, buyVolume: 300, sellVolume: 100);
            tracker.ProcessBar(bar, 5.0);
            Assert.Equal(200, tracker.LastBarDelta);
        }

        [Fact]
        public void ImbalanceNet_UpdatedOnProcessBar()
        {
            var imb = new ImbalanceMetrics { BullishCount = 5, BearishCount = 2 };
            var bar = MakeBar(100, imbalance: imb);
            var tracker = new EmaTracker();
            tracker.ProcessBar(bar, 5.0);

            Assert.Equal(3, tracker.ImbalanceNet);
        }

        [Fact]
        public void Reset_ClearsAllState()
        {
            var tracker = new EmaTracker();
            for (int i = 0; i < 10; i++)
                tracker.ProcessBar(MakeBar(100 + i), 5.0);

            tracker.Reset();

            Assert.Equal(0, tracker.SlowEmaValue);
            Assert.Equal(0, tracker.FastEmaValue);
            Assert.Equal(0, tracker.ImbalanceNet);
            Assert.Equal(0, tracker.SlowEmaSlope);
            Assert.Equal(0, tracker.LastBarDelta);
            Assert.Equal(0, tracker.BarsSinceEmaCross);
            Assert.Equal(0, tracker.EmaCrossDirection);
        }

        [Fact]
        public void FastEmaSlope_ZeroWithOnlyOneBar()
        {
            var tracker = new EmaTracker();
            tracker.ProcessBar(MakeBar(100), 5.0);
            Assert.Equal(0, tracker.FastEmaSlope);
        }

        [Fact]
        public void FastEmaSlope_PositiveWhenRising()
        {
            var tracker = new EmaTracker();
            for (int i = 0; i < 20; i++)
                tracker.ProcessBar(MakeBar(100 + i * 2), 5.0);

            Assert.True(tracker.FastEmaSlope > 0);
        }
    }
}
