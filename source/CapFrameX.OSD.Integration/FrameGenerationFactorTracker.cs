using System.Collections.Generic;

namespace CapFrameX.OSD.Integration
{
    /// <summary>
    /// Measures the factor of a frame generation from the frame counters of the in-game hook:
    /// the frames that reached the display over the frames the application rendered in the same
    /// span. Pure: every input is passed in, the ticks on the hook's clock (GetTickCount64). Not
    /// thread-safe.
    /// </summary>
    /// <remarks>
    /// An application frame is the application's own Present to the swapchain of the runtime where
    /// the hook sees it. Where it does not (the generic route hooks no vendor swapchain, a late
    /// attach misses the one of FidelityFX), it is the call the application has to make once per
    /// frame for its frame generation: a Streamline frame token for DLSS, the prepare dispatch
    /// for FSR, the frame constants for XeSS. A counter only counts for a span it ran through:
    /// one that started or stopped inside would cover a part of the span and yield too high a
    /// factor.
    /// </remarks>
    internal sealed class FrameGenerationFactorTracker
    {
        /// <summary>The shortest span a factor is measured over.</summary>
        internal const ulong MinWindowMs = 900;
        /// <summary>The span aimed for: long enough for a low frame rate, short enough to follow a switch.</summary>
        internal const ulong TargetWindowMs = 2000;
        /// <summary>Publications older than this are forgotten; covers a slow overlay refresh.</summary>
        internal const ulong HistoryMs = 10000;
        /// <summary>
        /// The hook publishes with every displayed frame, four times per second at most. Without
        /// a publication for this long the game presents nothing and there is nothing to measure.
        /// </summary>
        internal const ulong StaleAfterMs = 1500;
        /// <summary>Fewer application frames than this say too little.</summary>
        internal const ulong MinApplicationFrames = 8;
        /// <summary>
        /// Outside these bounds the counters do not describe a frame generation: the application
        /// calls its runtime more or less often than once per frame.
        /// </summary>
        internal const double MinPlausibleFactor = 0.75;
        internal const double MaxPlausibleFactor = 8.5;
        /// <summary>
        /// While the hook's telemetry does not know whether frame generation is on, only a
        /// measurement that shows generated frames names a factor.
        /// </summary>
        internal const double MinFactorWithoutTelemetry = 1.5;

        private enum Source
        {
            None,
            ProxyPresents,
            StreamlineFrames,
            FsrPreparedFrames,
            XessFrames
        }

        private readonly List<HookFrameCounts> _samples = new List<HookFrameCounts>();

        internal void Reset() => _samples.Clear();

        /// <summary>
        /// Takes a publication of the hook and returns the factor over the last seconds, or null
        /// while it cannot be measured.
        /// </summary>
        /// <param name="sample">The counters as read; the same publication may come again.</param>
        /// <param name="nowTickMs">The current time on the hook's clock.</param>
        /// <param name="technology">The frame generation technology of the hook status block.</param>
        /// <param name="activity">
        /// The frame generation activity of the hook status block: on, or unknown. The hook only
        /// learns it from a control call it sees, and a game that switched its frame generation
        /// on before the hook attached may never make one again (XeSS-FG).
        /// </param>
        internal double? Observe(HookFrameCounts sample, ulong nowTickMs, int technology, int activity)
        {
            if (_samples.Count > 0)
            {
                HookFrameCounts last = _samples[_samples.Count - 1];
                // The hook was removed and attached again, or the PID belongs to a new process.
                if (sample.TickMs < last.TickMs || WentBack(sample, last)) _samples.Clear();
            }
            if (_samples.Count == 0 || sample.TickMs != _samples[_samples.Count - 1].TickMs)
                _samples.Add(sample);

            HookFrameCounts newest = _samples[_samples.Count - 1];
            while (newest.TickMs - _samples[0].TickMs > HistoryMs) _samples.RemoveAt(0);
            if (nowTickMs > newest.TickMs && nowTickMs - newest.TickMs > StaleAfterMs) return null;

            // The shortest span of at least the target, else the longest there is. Index 0 never
            // starts a span: the publication before it shows whether a counter was running.
            int start = -1;
            for (int i = _samples.Count - 2; i >= 1; i--)
            {
                ulong span = newest.TickMs - _samples[i].TickMs;
                if (span < MinWindowMs) continue;
                start = i;
                if (span >= TargetWindowMs) break;
            }
            if (start < 0) return null;

            ulong displayed = newest.DisplayedFrames - _samples[start].DisplayedFrames;
            if (displayed == 0) return null;
            bool active = activity == UpscalingVersionText.ActivityActive;
            Source source = ApplicationFrameSource(start, technology, active);
            if (source == Source.None) return null;
            ulong applicationFrames = Frames(newest, source) - Frames(_samples[start], source);
            if (applicationFrames < MinApplicationFrames) return null;

            double factor = (double)displayed / applicationFrames;
            if (factor < MinPlausibleFactor || factor > MaxPlausibleFactor) return null;
            return active || factor >= MinFactorWithoutTelemetry ? factor : (double?)null;
        }

        private Source ApplicationFrameSource(int start, int technology, bool active)
        {
            if (RanThrough(Source.ProxyPresents, start)) return Source.ProxyPresents;
            if (active)
            {
                Source named = SourceOf(technology);
                return named != Source.None && RanThrough(named, start) ? named : Source.None;
            }

            // Without the telemetry only a call that exists for frame generation alone counts
            // application frames. A Streamline frame token does not: a game asks for them without
            // any frame generation, and not for every Present (a loading screen).
            bool fsr = RanThrough(Source.FsrPreparedFrames, start);
            bool xess = RanThrough(Source.XessFrames, start);
            return fsr == xess ? Source.None : fsr ? Source.FsrPreparedFrames : Source.XessFrames;
        }

        // Counted before the span began and still counting at its end.
        private bool RanThrough(Source source, int start)
        {
            int last = _samples.Count - 1;
            return Frames(_samples[start], source) > Frames(_samples[start - 1], source) &&
                   Frames(_samples[last], source) > Frames(_samples[last - 1], source);
        }

        private static Source SourceOf(int technology)
        {
            switch (technology)
            {
                case UpscalingVersionText.TechnologyDlss:
                    return Source.StreamlineFrames;
                case UpscalingVersionText.TechnologyXess:
                    return Source.XessFrames;
                case UpscalingVersionText.TechnologyFsr:
                    return Source.FsrPreparedFrames;
                default:
                    return Source.None;
            }
        }

        private static ulong Frames(HookFrameCounts counts, Source source)
        {
            switch (source)
            {
                case Source.ProxyPresents:
                    return counts.ProxyPresents;
                case Source.StreamlineFrames:
                    return counts.StreamlineFrames;
                case Source.FsrPreparedFrames:
                    return counts.FsrPreparedFrames;
                case Source.XessFrames:
                    return counts.XessFrames;
                default:
                    return 0;
            }
        }

        private static bool WentBack(HookFrameCounts sample, HookFrameCounts last)
            => sample.DisplayedFrames < last.DisplayedFrames ||
               sample.ProxyPresents < last.ProxyPresents ||
               sample.StreamlineFrames < last.StreamlineFrames ||
               sample.FsrPreparedFrames < last.FsrPreparedFrames ||
               sample.XessFrames < last.XessFrames;
    }
}
