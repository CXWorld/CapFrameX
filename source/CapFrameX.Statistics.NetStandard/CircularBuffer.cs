using System;
using System.Collections;
using System.Collections.Generic;

namespace CapFrameX.Statistics.NetStandard
{
    /// <summary>
    /// A fixed-capacity circular buffer that overwrites oldest elements when full.
    /// Provides O(1) Add operations without memory shifting, unlike List.RemoveRange().
    /// </summary>
    /// <typeparam name="T">The type of elements in the buffer.</typeparam>
    public class CircularBuffer<T> : IReadOnlyList<T>
    {
        private readonly T[] _buffer;
        private int _head;
        private int _tail;
        private int _count;

        /// <summary>
        /// Gets the maximum capacity of the buffer.
        /// </summary>
        public int Capacity => _buffer.Length;

        /// <summary>
        /// Gets the current number of elements in the buffer.
        /// </summary>
        public int Count => _count;

        /// <summary>
        /// Gets whether the buffer is empty.
        /// </summary>
        public bool IsEmpty => _count == 0;

        /// <summary>
        /// Gets whether the buffer is full.
        /// </summary>
        public bool IsFull => _count == _buffer.Length;

        /// <summary>
        /// Creates a new circular buffer with the specified capacity.
        /// </summary>
        /// <param name="capacity">The maximum number of elements the buffer can hold.</param>
        public CircularBuffer(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");

            _buffer = new T[capacity];
            _head = 0;
            _tail = 0;
            _count = 0;
        }

        /// <summary>
        /// Gets the element at the specified index (0 = oldest element).
        /// </summary>
        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= _count)
                    throw new ArgumentOutOfRangeException(nameof(index));

                return _buffer[(_head + index) % _buffer.Length];
            }
        }

        /// <summary>
        /// Adds an element to the buffer. If full, overwrites the oldest element.
        /// </summary>
        public void Add(T item)
        {
            _buffer[_tail] = item;
            _tail = (_tail + 1) % _buffer.Length;

            if (_count == _buffer.Length)
            {
                // Buffer is full, move head forward (overwrite oldest)
                _head = (_head + 1) % _buffer.Length;
            }
            else
            {
                _count++;
            }
        }

        /// <summary>
        /// Removes elements from the front of the buffer that are older than the specified time threshold.
        /// </summary>
        /// <param name="measureTimes">The corresponding measurement times buffer.</param>
        /// <param name="currentTime">The current time reference.</param>
        /// <param name="maxAge">The maximum age in seconds for elements to keep.</param>
        /// <returns>The number of elements removed.</returns>
        public int RemoveOlderThan(CircularBuffer<double> measureTimes, double currentTime, double maxAge)
        {
            int removed = 0;
            while (_count > 0 && measureTimes.Count > 0)
            {
                double oldestTime = measureTimes.PeekFirst();
                if (currentTime - oldestTime > maxAge)
                {
                    RemoveFirst();
                    measureTimes.RemoveFirst();
                    removed++;
                }
                else
                {
                    break;
                }
            }
            return removed;
        }

        /// <summary>
        /// Gets the first (oldest) element without removing it.
        /// </summary>
        public T PeekFirst()
        {
            if (_count == 0)
                throw new InvalidOperationException("Buffer is empty.");

            return _buffer[_head];
        }

        /// <summary>
        /// Gets the last (newest) element without removing it.
        /// </summary>
        public T PeekLast()
        {
            if (_count == 0)
                throw new InvalidOperationException("Buffer is empty.");

            int lastIndex = (_tail - 1 + _buffer.Length) % _buffer.Length;
            return _buffer[lastIndex];
        }

        /// <summary>
        /// Removes and returns the first (oldest) element.
        /// </summary>
        public T RemoveFirst()
        {
            if (_count == 0)
                throw new InvalidOperationException("Buffer is empty.");

            T item = _buffer[_head];
            _buffer[_head] = default;
            _head = (_head + 1) % _buffer.Length;
            _count--;
            return item;
        }

        /// <summary>
        /// Clears all elements from the buffer.
        /// </summary>
        public void Clear()
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _head = 0;
            _tail = 0;
            _count = 0;
        }

        /// <summary>
        /// Copies elements to an array for calculations that require array access.
        /// Uses the provided buffer if it has sufficient capacity to avoid allocations.
        /// </summary>
        /// <param name="destination">Optional pre-allocated buffer to use.</param>
        /// <returns>An array containing all elements in order from oldest to newest.</returns>
        public T[] ToArray(T[] destination = null)
        {
            if (destination == null || destination.Length < _count)
            {
                destination = new T[_count];
            }

            if (_count == 0)
                return destination;

            if (_head < _tail)
            {
                // Elements are contiguous
                Array.Copy(_buffer, _head, destination, 0, _count);
            }
            else
            {
                // Elements wrap around
                int firstPartLength = _buffer.Length - _head;
                Array.Copy(_buffer, _head, destination, 0, firstPartLength);
                Array.Copy(_buffer, 0, destination, firstPartLength, _tail);
            }

            return destination;
        }

        /// <summary>
        /// Copies elements to a list for calculations that require IList access.
        /// Reuses the provided list if available to avoid allocations.
        /// </summary>
        /// <param name="destination">Optional pre-allocated list to use.</param>
        /// <returns>A list containing all elements in order from oldest to newest.</returns>
        public List<T> ToList(List<T> destination = null)
        {
            if (destination == null)
            {
                destination = new List<T>(_count);
            }
            else
            {
                destination.Clear();
                if (destination.Capacity < _count)
                {
                    destination.Capacity = _count;
                }
            }

            if (_count == 0)
                return destination;

            if (_head < _tail)
            {
                // Elements are contiguous
                for (int i = _head; i < _tail; i++)
                {
                    destination.Add(_buffer[i]);
                }
            }
            else
            {
                // Elements wrap around
                for (int i = _head; i < _buffer.Length; i++)
                {
                    destination.Add(_buffer[i]);
                }
                for (int i = 0; i < _tail; i++)
                {
                    destination.Add(_buffer[i]);
                }
            }

            return destination;
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
