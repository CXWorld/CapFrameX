using System.Collections.Generic;

namespace LibreHardwareMonitor.Hardware
{
    /// <summary>
    /// A buffer that maintains a fixed number of the most recent samples.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class RefreshRateBuffer<T>
    {
        private readonly int _size;
        private readonly List<T> _buffer;

        /// <summary>
        /// Gets the current samples in the buffer.
        /// </summary>
        public IEnumerable<T> RefreshRates => _buffer;

        /// <summary>
        /// Initializes a new instance of the <see cref="RefreshRateBuffer{T}"/> class.
        /// </summary>
        /// <param name="size"></param>
        public RefreshRateBuffer(int size)
        {
            _size = size;
            _buffer = new List<T>(size + 1);
        }

        /// <summary>
        /// Adds a new sample to the buffer.
        /// </summary>
        /// <param name="sample"></param>
        public void Add(T sample)
        {
            _buffer.Add(sample);
            if (_buffer.Count > _size)
            {
                _buffer.RemoveAt(0);
            }
        }

        /// <summary>
        /// Clears all samples from the buffer.
        /// </summary>
        public void Clear()
        {
            _buffer?.Clear();
        }
    }
}
