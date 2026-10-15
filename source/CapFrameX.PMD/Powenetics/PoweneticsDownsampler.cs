using System;
using System.Collections.Generic;

namespace CapFrameX.PMD.Powenetics
{
    /// <summary>
    /// Groups driver packets into bins of a fixed length on their timestamps.
    /// </summary>
    /// <remarks>
    /// Bins are aligned to whole multiples of the bin length on the QPC millisecond clock, so a bin
    /// is identified by its start alone. Grouping by count instead would assume the nominal 1 kHz
    /// of the device, which it does not run at.
    /// </remarks>
    internal sealed class PoweneticsDownsampler
    {
        private readonly int _binSizeMs;
        private readonly List<PoweneticsChannel[]> _bin = new List<PoweneticsChannel[]>();
        private long _binIndex;

        public PoweneticsDownsampler(int binSizeMs)
        {
            _binSizeMs = Math.Max(1, binSizeMs);
        }

        /// <summary>
        /// Adds a packet. When it opens a new bin, the previous bin is complete and returned.
        /// </summary>
        /// <param name="channels">The packet; all channels carry the same timestamp.</param>
        /// <param name="completedBin">The packets of the completed bin, in arrival order.</param>
        /// <param name="completedBinStart">Start of the completed bin in milliseconds.</param>
        public bool TryAdd(PoweneticsChannel[] channels, out IList<PoweneticsChannel[]> completedBin, out long completedBinStart)
        {
            long binIndex = FloorDiv(channels[0].TimeStamp, _binSizeMs);

            completedBin = null;
            completedBinStart = 0;

            // Packet times never decrease, so a packet either joins the open bin or opens a later one.
            if (_bin.Count > 0 && binIndex > _binIndex)
            {
                completedBin = _bin.ToArray();
                completedBinStart = _binIndex * _binSizeMs;
                _bin.Clear();
            }

            if (_bin.Count == 0)
                _binIndex = binIndex;

            _bin.Add(channels);
            return completedBin != null;
        }

        private static long FloorDiv(long value, long divisor)
        {
            long quotient = value / divisor;
            return (value % divisor != 0 && value < 0) ? quotient - 1 : quotient;
        }
    }
}
