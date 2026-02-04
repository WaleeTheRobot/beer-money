using System;
using System.Collections;
using System.Collections.Generic;

namespace BeerMoney.Core.Collections
{
    /// <summary>
    /// Fixed-size circular buffer with O(1) add operations.
    /// When full, oldest items are automatically overwritten.
    /// </summary>
    public sealed class CircularBuffer<T> : IReadOnlyList<T>
    {
        private readonly T[] _buffer;
        private int _head;
        private int _count;

        public CircularBuffer(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");

            _buffer = new T[capacity];
            _head = 0;
            _count = 0;
        }

        public int Capacity => _buffer.Length;

        public int Count => _count;

        public bool IsFull => _count == _buffer.Length;

        public void Add(T item)
        {
            int index = (_head + _count) % _buffer.Length;

            if (_count < _buffer.Length)
            {
                _buffer[index] = item;
                _count++;
            }
            else
            {
                _buffer[_head] = item;
                _head = (_head + 1) % _buffer.Length;
            }
        }

        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= _count)
                    throw new ArgumentOutOfRangeException(nameof(index));

                int actualIndex = (_head + index) % _buffer.Length;
                return _buffer[actualIndex];
            }
        }

        public void Clear()
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _head = 0;
            _count = 0;
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < _count; i++)
            {
                yield return this[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
