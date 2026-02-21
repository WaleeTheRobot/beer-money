using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using BeerMoney.Core.Collections;

namespace BeerMoneyTests
{
    public class CircularBufferTests
    {
        [Fact]
        public void Constructor_SetsCapacity()
        {
            var buf = new CircularBuffer<int>(5);
            Assert.Equal(5, buf.Capacity);
            Assert.Empty(buf);
            Assert.False(buf.IsFull);
        }

        [Fact]
        public void Constructor_ZeroCapacity_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CircularBuffer<int>(0));
        }

        [Fact]
        public void Constructor_NegativeCapacity_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CircularBuffer<int>(-1));
        }

        [Fact]
        public void Add_IncreasesCount()
        {
            var buf = new CircularBuffer<int>(3);
            buf.Add(10);
            Assert.Single(buf);
            buf.Add(20);
            Assert.Equal(2, buf.Count);
        }

        [Fact]
        public void Add_BecomesFull()
        {
            var buf = new CircularBuffer<int>(2);
            buf.Add(1);
            Assert.False(buf.IsFull);
            buf.Add(2);
            Assert.True(buf.IsFull);
        }

        [Fact]
        public void Indexer_ReturnsItemsInOrder()
        {
            var buf = new CircularBuffer<int>(5);
            buf.Add(10);
            buf.Add(20);
            buf.Add(30);

            Assert.Equal(10, buf[0]);
            Assert.Equal(20, buf[1]);
            Assert.Equal(30, buf[2]);
        }

        [Fact]
        public void Indexer_OutOfRange_Throws()
        {
            var buf = new CircularBuffer<int>(5);
            buf.Add(1);
            Assert.Throws<ArgumentOutOfRangeException>(() => buf[1]);
            Assert.Throws<ArgumentOutOfRangeException>(() => buf[-1]);
        }

        [Fact]
        public void Wrapping_OverwritesOldest()
        {
            var buf = new CircularBuffer<int>(3);
            buf.Add(1);
            buf.Add(2);
            buf.Add(3);
            buf.Add(4); // overwrites 1

            Assert.Equal(3, buf.Count);
            Assert.Equal(2, buf[0]);
            Assert.Equal(3, buf[1]);
            Assert.Equal(4, buf[2]);
        }

        [Fact]
        public void Wrapping_MultipleOverwrites()
        {
            var buf = new CircularBuffer<int>(2);
            buf.Add(1);
            buf.Add(2);
            buf.Add(3);
            buf.Add(4);
            buf.Add(5);

            Assert.Equal(2, buf.Count);
            Assert.Equal(4, buf[0]);
            Assert.Equal(5, buf[1]);
        }

        [Fact]
        public void Clear_ResetsBuffer()
        {
            var buf = new CircularBuffer<int>(3);
            buf.Add(1);
            buf.Add(2);
            buf.Add(3);
            buf.Clear();

            Assert.Empty(buf);
            Assert.False(buf.IsFull);
        }

        [Fact]
        public void Enumeration_ReturnsAllItemsInOrder()
        {
            var buf = new CircularBuffer<string>(4);
            buf.Add("a");
            buf.Add("b");
            buf.Add("c");

            var items = buf.ToList();
            Assert.Equal(new[] { "a", "b", "c" }, items);
        }

        [Fact]
        public void Enumeration_AfterWrapping()
        {
            var buf = new CircularBuffer<int>(3);
            buf.Add(1);
            buf.Add(2);
            buf.Add(3);
            buf.Add(4);
            buf.Add(5);

            var items = buf.ToList();
            Assert.Equal(new[] { 3, 4, 5 }, items);
        }

        [Fact]
        public void CapacityOne_Works()
        {
            var buf = new CircularBuffer<int>(1);
            buf.Add(42);
            Assert.Single(buf);
            Assert.True(buf.IsFull);
            Assert.Equal(42, buf[0]);

            buf.Add(99);
            Assert.Single(buf);
            Assert.Equal(99, buf[0]);
        }

        [Fact]
        public void IReadOnlyList_CountProperty()
        {
            IReadOnlyList<int> buf = new CircularBuffer<int>(5);
            Assert.Empty(buf);
        }
    }
}
