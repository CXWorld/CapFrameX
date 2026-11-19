using System;

namespace CapFrameX.PresentMonInterface
{
    /// <summary>
    /// What a row did to <see cref="PresentRuntimeTracker.Runtime"/>.
    /// </summary>
    public enum PresentRuntimeObservation
    {
        /// <summary>The runtime stayed, and the row's runtime was seen before.</summary>
        Unchanged,

        /// <summary>The runtime of the process changed, the first one included.</summary>
        Changed,

        /// <summary>The runtime stayed, and the process presented through another one for the first time.</summary>
        AnotherRuntimeSeen
    }

    /// <summary>
    /// The presentation runtime of one process (PresentMon's PresentRuntime: DXGI, D3D9, Other),
    /// steady while the process presents through several. CS2 on 2026-11-17 delivered rows of a
    /// DXGI and an Other swapchain: following the latest row, the runtime changed up to 26 times
    /// per second, and every change made the in-game overlay re-evaluate the target and write a
    /// log line (1959 of the 2000 events of a crash report).
    /// The first runtime counts at once. Another one takes over once it has more than
    /// <see cref="TakeoverFactor"/> times the rows of the current one in the current and the
    /// previous second, the window of the process list's dominant present mode (PresentMon
    /// delivers rows in waves): a second swapchain that presents now and then never does, a
    /// process that really switched does within about two seconds. Not thread-safe.
    /// </summary>
    public sealed class PresentRuntimeTracker
    {
        public const int BucketMilliseconds = 1000;
        public const int TakeoverFactor = 2;

        // PresentMon knows DXGI, D3D9 and Other, so each of them has a count in every second:
        // with fewer, the current runtime lost its count in a second that began with the two
        // others, and one row of another runtime took over. A row of any further spelling is
        // dropped rather than tracked.
        private const int Slots = 3;

        // What PresentMon writes into a column it could not resolve.
        private const string ErrorRuntime = "<error>";

        // Names and counts of the current ([0]) and the previous ([1]) one-second bucket.
        private readonly long[] _bucket = { long.MinValue, long.MinValue };
        private readonly string[][] _names = { new string[Slots], new string[Slots] };
        private readonly int[][] _counts = { new int[Slots], new int[Slots] };
        // The runtimes the process presented through so far, the current one included.
        private readonly string[] _seenNames = new string[Slots];

        /// <summary>The runtime of the process, null before its first row.</summary>
        public string Runtime { get; private set; }

        /// <summary>
        /// Counts a row of the process. The times must not run backwards (both callers pass a
        /// 64-bit tick count): a row from before the current second is dropped.
        /// </summary>
        public PresentRuntimeObservation Observe(string runtime, long nowMilliseconds)
        {
            if (string.IsNullOrWhiteSpace(runtime) || runtime == ErrorRuntime)
                return PresentRuntimeObservation.Unchanged;

            long bucket = nowMilliseconds / BucketMilliseconds;
            if (bucket < _bucket[0])
                return PresentRuntimeObservation.Unchanged;

            AdvanceTo(bucket);
            Vote(runtime);
            bool firstSighting = FirstSighting(runtime);

            if (Runtime == null ||
                (!SameRuntime(runtime, Runtime) && CountOf(runtime) > TakeoverFactor * CountOf(Runtime)))
            {
                Runtime = runtime;
                return PresentRuntimeObservation.Changed;
            }

            return firstSighting
                ? PresentRuntimeObservation.AnotherRuntimeSeen
                : PresentRuntimeObservation.Unchanged;
        }

        /// <summary>Forgets the process: the next row's runtime counts at once.</summary>
        public void Reset()
        {
            _bucket[0] = _bucket[1] = long.MinValue;
            for (int b = 0; b < 2; b++)
            {
                Array.Clear(_names[b], 0, Slots);
                Array.Clear(_counts[b], 0, Slots);
            }
            Array.Clear(_seenNames, 0, Slots);
            Runtime = null;
        }

        private void AdvanceTo(long bucket)
        {
            if (bucket == _bucket[0])
                return;

            if (bucket == _bucket[0] + 1)
            {
                // Rotate: the current second becomes the previous one.
                _bucket[1] = _bucket[0];
                var names = _names[1];
                _names[1] = _names[0];
                _names[0] = names;
                var counts = _counts[1];
                _counts[1] = _counts[0];
                _counts[0] = counts;
            }
            else
            {
                // A gap of a second or more: nothing of the previous second is left.
                _bucket[1] = long.MinValue;
                Array.Clear(_names[1], 0, Slots);
                Array.Clear(_counts[1], 0, Slots);
            }

            _bucket[0] = bucket;
            Array.Clear(_names[0], 0, Slots);
            Array.Clear(_counts[0], 0, Slots);
        }

        private void Vote(string runtime)
        {
            string[] names = _names[0];
            int[] counts = _counts[0];
            for (int i = 0; i < Slots; i++)
            {
                if (names[i] == null)
                {
                    names[i] = runtime;
                    counts[i] = 1;
                    return;
                }
                if (SameRuntime(names[i], runtime))
                {
                    counts[i]++;
                    return;
                }
            }
        }

        private bool FirstSighting(string runtime)
        {
            for (int i = 0; i < Slots; i++)
            {
                if (_seenNames[i] == null)
                {
                    _seenNames[i] = runtime;
                    return true;
                }
                if (SameRuntime(_seenNames[i], runtime))
                    return false;
            }
            return false;
        }

        private int CountOf(string runtime) => CountIn(0, runtime) + CountIn(1, runtime);

        private int CountIn(int bucket, string runtime)
        {
            string[] names = _names[bucket];
            for (int i = 0; i < Slots; i++)
            {
                if (SameRuntime(names[i], runtime))
                    return _counts[bucket][i];
            }
            return 0;
        }

        private static bool SameRuntime(string a, string b)
            => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
