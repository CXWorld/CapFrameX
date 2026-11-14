using System;
using System.Collections;
using System.Collections.Generic;

namespace CapFrameX.Data
{
    /// <summary>
    /// The last <see cref="Capacity"/> PresentMon rows before a capture (CaptureManager's
    /// archive), oldest first. A ring: the list it replaces shifted all 500 rows on every row
    /// (<c>RemoveAt(0)</c>), at 1000 rows per second and more.
    /// </summary>
    internal sealed class FrameRowArchive : IEnumerable<string[]>
    {
        private readonly string[][] _rows;
        private int _start;

        public FrameRowArchive(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _rows = new string[capacity][];
        }

        public int Capacity => _rows.Length;

        public int Count { get; private set; }

        /// <summary>Adds a row; a full archive drops its oldest.</summary>
        public void Add(string[] row)
        {
            if (Count < _rows.Length)
            {
                _rows[(_start + Count) % _rows.Length] = row;
                Count++;
            }
            else
            {
                _rows[_start] = row;
                _start = (_start + 1) % _rows.Length;
            }
        }

        /// <summary>The newest row, or null while the archive is empty.</summary>
        public string[] Last => Count == 0 ? null : _rows[(_start + Count - 1) % _rows.Length];

        public void Clear()
        {
            Array.Clear(_rows, 0, _rows.Length);
            _start = 0;
            Count = 0;
        }

        public IEnumerator<string[]> GetEnumerator()
        {
            for (int i = 0; i < Count; i++)
                yield return _rows[(_start + i) % _rows.Length];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
