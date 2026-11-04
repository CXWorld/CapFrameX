using System;
using System.Collections.Generic;

namespace CapFrameX.CaptureHelper
{
    /// <summary>A row of PresentMon's stream: its process, its present time on the QPC clock and the raw CSV line.</summary>
    internal readonly struct PresentRow
    {
        public PresentRow(int processId, long qpc, string line)
        {
            ProcessId = processId;
            Qpc = qpc;
            Line = line;
        }

        public int ProcessId { get; }
        public long Qpc { get; }
        public string Line { get; }
    }

    /// <summary>
    /// The last rows of every process, as the archive of the main app (CaptureManager, ARCHIVE_LENGTH).
    /// A fixed ring, so keeping a row costs one array write.
    /// </summary>
    internal sealed class RowArchive
    {
        private readonly PresentRow[] _rows;
        private int _next, _count;

        public RowArchive(int capacity) => _rows = new PresentRow[capacity];

        public void Add(PresentRow row)
        {
            _rows[_next] = row;
            _next = (_next + 1) % _rows.Length;
            if (_count < _rows.Length) _count++;
        }

        public void Clear()
        {
            Array.Clear(_rows, 0, _rows.Length);
            _next = _count = 0;
        }

        /// <summary>Appends the rows of one process to <paramref name="target"/>, oldest first.</summary>
        public void CopyTo(int processId, List<PresentRow> target)
        {
            int oldest = _next - _count;
            if (oldest < 0) oldest += _rows.Length;
            for (int i = 0; i < _count; i++)
            {
                var row = _rows[(oldest + i) % _rows.Length];
                if (row.ProcessId == processId) target.Add(row);
            }
        }
    }

    internal sealed class CaptureCut
    {
        public CaptureCut(IReadOnlyList<string> lines, long? referenceQpc, long firstQpc, long endQpc, bool timed)
        {
            Lines = lines;
            ReferenceQpc = referenceQpc;
            FirstQpc = firstQpc;
            EndQpc = endQpc;
            Timed = timed;
        }

        public IReadOnlyList<string> Lines { get; }

        /// <summary>
        /// The last frame before the start request, or the first frame when none came before it; null
        /// without frames.
        /// </summary>
        public long? ReferenceQpc { get; }

        /// <summary>The first frame at or after the start request; long.MaxValue without one.</summary>
        public long FirstQpc { get; }

        public long EndQpc { get; }

        /// <summary>The capture ran its set duration, rather than being stopped.</summary>
        public bool Timed { get; }
    }

    /// <summary>
    /// Chooses the frames of a capture as the main app does (CaptureManager.GetAdjustedCaptureData and
    /// DropFramesOfFirstStartTime), by their present time, whatever order and delay they arrived in. The
    /// reference is the last frame before the start request, or the first frame when none came before
    /// it. A capture that ran its set duration keeps that duration from the first frame after the
    /// reference; a stopped one keeps the frames after the first frame of the start request up to the
    /// reference plus the time it ran. The reference, resp. that first frame, is not part of the file:
    /// GetAdjustedCaptureData takes it into the interval and DropFramesOfFirstStartTime drops it again.
    /// </summary>
    internal static class CaptureWindow
    {
        /// <param name="rows">The target's rows: those of the archive, then those of the stream.</param>
        /// <param name="startQpc">The start request.</param>
        /// <param name="elapsedTicks">How long the capture ran: its duration, or until the stop request.</param>
        /// <param name="durationTicks">The set duration; used when <paramref name="timed"/>.</param>
        public static CaptureCut Cut(IReadOnlyList<PresentRow> rows, long startQpc, long elapsedTicks, long durationTicks, bool timed)
        {
            long reference = long.MinValue, first = long.MaxValue;
            foreach (var row in rows)
            {
                if (row.Qpc < startQpc)
                {
                    if (row.Qpc > reference) reference = row.Qpc;
                }
                else if (row.Qpc < first)
                {
                    first = row.Qpc;
                }
            }

            var lines = new List<string>();
            if (first == long.MaxValue)
                return new CaptureCut(lines, null, first, startQpc + elapsedTicks, timed);
            if (reference == long.MinValue)
                reference = first;

            // Timed: from the first frame after the reference for the set duration. Stopped: after the
            // first frame of the start request, up to the reference plus the time the capture ran.
            long from = timed ? Next(rows, reference) : first;
            long end = timed ? from + durationTicks : reference + elapsedTicks;
            if (from != long.MaxValue)
            {
                foreach (var row in rows)
                {
                    bool inWindow = timed ? row.Qpc >= from : row.Qpc > from;
                    if (inWindow && row.Qpc <= end)
                        lines.Add(row.Line);
                }
            }
            return new CaptureCut(lines, reference, first, end, timed);
        }

        // The earliest present after the given time.
        private static long Next(IReadOnlyList<PresentRow> rows, long qpc)
        {
            long next = long.MaxValue;
            foreach (var row in rows)
            {
                if (row.Qpc > qpc && row.Qpc < next) next = row.Qpc;
            }
            return next;
        }
    }
}
