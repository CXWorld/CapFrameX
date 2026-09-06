using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;

namespace CapFrameX.OSD.Integration
{
    /// <summary>
    /// Observes the PresentMon rows that reach (or fail to reach) the hook-free overlay feed
    /// and turns them into a compact log trail for stall reports. The native core logs what it
    /// does with the samples (%TEMP%\cfx_osd.log, "[diag]" lines); this class logs what it was
    /// GIVEN: target-PID changes, rows rejected for a foreign PID, additional swapchains inside
    /// the target process, runtime-label flips, frametime outliers and gaps in either the source
    /// timeline or the delivery. Immediate lines cover rare state changes; everything that can
    /// repeat per row is aggregated into one summary per ten seconds.
    /// </summary>
    /// <remarks>
    /// Everything is gated by <see cref="Enabled"/>, the "Extended OSD logging" switch. While it
    /// is off the per-row methods return before taking the lock, and enabling it later starts
    /// from fresh references so the silent period is never reported as a gap.
    /// </remarks>
    internal sealed class HookFreeFeedDiagnostics
    {
        public const double OutlierFrametimeMs = 250.0;
        public const double SourceGapMs = 250.0;
        public const double BackdatedToleranceMs = 50.0;
        public const double ArrivalGapSeconds = 1.0;
        public const double RejectedStreakSeconds = 1.0;
        public const double SummaryPeriodSeconds = 10.0;
        public const double ImmediateLineIntervalSeconds = 2.0;
        private const int MaxRejectedPids = 5;

        private readonly Action<string> _log;
        private readonly object _gate = new object();
        private volatile bool _enabled;

        // stream state (survives summary windows)
        private int _targetPid;
        private string _runtimeLabel;
        private string _dominantSwapChain;
        private readonly HashSet<string> _knownSwapChains = new HashSet<string>(StringComparer.Ordinal);
        private double _lastAcceptedSeconds = double.NaN;
        private double _lastTimestampMs = double.NaN;
        private double _rejectedStreakStartSeconds = double.NaN;
        private bool _rejectedStreakLogged;
        private int _rejectedInStreak;
        private readonly HashSet<int> _rejectedPids = new HashSet<int>();
        private double _summaryAnchorSeconds = double.NaN;
        private double _lastImmediateSeconds = double.NegativeInfinity;

        // summary window
        private int _rows;
        private int _rejectedRows;
        private int _outliers;
        private int _sourceGaps;
        private int _arrivalGaps;
        private int _backdated;
        private double _maxFrametimeMs;
        private double _maxSourceGapMs;
        private double _maxArrivalGapSeconds;
        private string _outlierExample;
        private readonly Dictionary<string, int> _perSwapChain = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _perRuntime = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _perFrameType = new Dictionary<string, int>(StringComparer.Ordinal);

        public HookFreeFeedDiagnostics(Action<string> log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <summary>
        /// The "Extended OSD logging" switch. Turning it on discards the stale references of the
        /// silent period; turning it off stops all output and per-row work.
        /// </summary>
        public bool Enabled
        {
            get => _enabled;
            set
            {
                lock (_gate)
                {
                    if (_enabled == value) return;
                    _enabled = value;
                    if (value) ResetStreamLocked();
                }
            }
        }

        /// <summary>Monotonic seconds for the callers that observe in real time.</summary>
        public static double Now() => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;

        public void OnTargetPidChanged(int previousPid, int currentPid, double nowSeconds)
        {
            lock (_gate)
            {
                // The PID is stream state the summary needs even after a later enable.
                _targetPid = currentPid;
                ResetStreamLocked();
                if (!_enabled) return;
                _log(string.Format(CultureInfo.InvariantCulture,
                    "target PID {0} -> {1}{2}", previousPid, currentPid,
                    currentPid <= 0 ? " (no target: every PresentMon row is dropped until a PID is selected)" : ""));
            }
        }

        public void OnRowRejected(int rowPid, int targetPid, double nowSeconds)
        {
            if (!_enabled) return;
            lock (_gate)
            {
                if (!_enabled) return;
                _rejectedRows++;
                if (double.IsNaN(_rejectedStreakStartSeconds)) _rejectedStreakStartSeconds = nowSeconds;
                _rejectedInStreak++;
                if (_rejectedPids.Count < MaxRejectedPids) _rejectedPids.Add(rowPid);

                // Foreign rows are normal on a desktop with other presenters. What matters is the
                // target going silent WHILE foreign rows keep coming: that is either a wrong PID or
                // a game that stopped presenting.
                bool targetSilent = double.IsNaN(_lastAcceptedSeconds) ||
                                    nowSeconds - _lastAcceptedSeconds >= RejectedStreakSeconds;
                if (!_rejectedStreakLogged && targetSilent &&
                    nowSeconds - _rejectedStreakStartSeconds >= RejectedStreakSeconds)
                {
                    _rejectedStreakLogged = true;
                    _log(string.Format(CultureInfo.InvariantCulture,
                        "no rows for target PID {0} for {1:F1} s; rejected {2} rows from PIDs [{3}]",
                        targetPid, nowSeconds - _rejectedStreakStartSeconds, _rejectedInStreak,
                        string.Join(", ", _rejectedPids.OrderBy(p => p))));
                }
                FlushSummaryIfDueLocked(nowSeconds);
            }
        }

        public void OnRowAccepted(string swapChain, string runtime, string frameType,
            double frametimeMs, double timestampMs, double displayTimeMs, double nowSeconds)
        {
            if (!_enabled) return;
            lock (_gate)
            {
                if (!_enabled) return;
                _rows++;
                if (_rejectedStreakLogged)
                {
                    _log(string.Format(CultureInfo.InvariantCulture,
                        "rows for target PID {0} resumed after {1:F1} s ({2} foreign rows rejected meanwhile)",
                        _targetPid, nowSeconds - _rejectedStreakStartSeconds, _rejectedInStreak));
                }
                ResetRejectedStreakLocked();

                if (!double.IsNaN(_lastAcceptedSeconds))
                {
                    double gapSeconds = nowSeconds - _lastAcceptedSeconds;
                    if (gapSeconds >= ArrivalGapSeconds)
                    {
                        _arrivalGaps++;
                        _maxArrivalGapSeconds = Math.Max(_maxArrivalGapSeconds, gapSeconds);
                    }
                }
                _lastAcceptedSeconds = nowSeconds;

                if (timestampMs > 0)
                {
                    if (!double.IsNaN(_lastTimestampMs))
                    {
                        double gapMs = timestampMs - _lastTimestampMs;
                        if (gapMs >= SourceGapMs)
                        {
                            _sourceGaps++;
                            _maxSourceGapMs = Math.Max(_maxSourceGapMs, gapMs);
                        }
                        else if (gapMs < -BackdatedToleranceMs)
                        {
                            // A second swapchain's rows carry the CPU start of ITS previous
                            // present, i.e. they arrive dated into the past.
                            _backdated++;
                        }
                    }
                    if (double.IsNaN(_lastTimestampMs) || timestampMs > _lastTimestampMs)
                        _lastTimestampMs = timestampMs;
                }

                string swapChainKey = swapChain ?? "?";
                string runtimeKey = runtime ?? "?";
                string frameTypeKey = frameType ?? "?";
                Increment(_perSwapChain, swapChainKey);
                Increment(_perRuntime, runtimeKey);
                Increment(_perFrameType, frameTypeKey);

                if (frametimeMs >= OutlierFrametimeMs)
                {
                    _outliers++;
                    if (frametimeMs > _maxFrametimeMs)
                    {
                        _maxFrametimeMs = frametimeMs;
                        _outlierExample = string.Format(CultureInfo.InvariantCulture,
                            "{0:F0} ms swapchain={1} runtime={2} frametype={3} displaytime={4:F0} ms",
                            frametimeMs, swapChainKey, runtimeKey, frameTypeKey, displayTimeMs);
                    }
                }

                if (_knownSwapChains.Add(swapChainKey) && _knownSwapChains.Count > 1 &&
                    AllowImmediateLocked(nowSeconds))
                {
                    _log(string.Format(CultureInfo.InvariantCulture,
                        "additional swapchain {0} presents in target PID {1} (runtime={2} frametype={3} frametime={4:F1} ms); " +
                        "the hook-free feed does not filter by swapchain",
                        swapChainKey, _targetPid, runtimeKey, frameTypeKey, frametimeMs));
                }

                FlushSummaryIfDueLocked(nowSeconds);
            }
        }

        public void OnRuntimeLabelChanged(string previous, string current, double nowSeconds)
        {
            lock (_gate)
            {
                _runtimeLabel = current;
                if (!_enabled) return;
                _log(string.Format(CultureInfo.InvariantCulture,
                    "runtime label '{0}' -> '{1}' (renames the <APP> group; the OSD rebuilds its scene)",
                    previous ?? "<none>", current ?? "<none>"));
            }
        }

        /// <summary>Periodic hook (call about once per second) so a summary is flushed even without rows.</summary>
        public void Tick(double nowSeconds)
        {
            if (!_enabled) return;
            lock (_gate)
            {
                if (_enabled) FlushSummaryIfDueLocked(nowSeconds);
            }
        }

        private bool AllowImmediateLocked(double nowSeconds)
        {
            if (nowSeconds - _lastImmediateSeconds < ImmediateLineIntervalSeconds) return false;
            _lastImmediateSeconds = nowSeconds;
            return true;
        }

        private void ResetRejectedStreakLocked()
        {
            _rejectedStreakStartSeconds = double.NaN;
            _rejectedStreakLogged = false;
            _rejectedInStreak = 0;
            _rejectedPids.Clear();
        }

        // Forget every reference into the past: gaps, dominant/known swapchains, the summary
        // anchor and the current window. The target PID and runtime label are kept.
        private void ResetStreamLocked()
        {
            _knownSwapChains.Clear();
            _dominantSwapChain = null;
            _lastAcceptedSeconds = double.NaN;
            _lastTimestampMs = double.NaN;
            _summaryAnchorSeconds = double.NaN;
            _lastImmediateSeconds = double.NegativeInfinity;
            ResetRejectedStreakLocked();
            ResetWindowLocked();
        }

        private void FlushSummaryIfDueLocked(double nowSeconds)
        {
            if (double.IsNaN(_summaryAnchorSeconds))
            {
                _summaryAnchorSeconds = nowSeconds;
                return;
            }
            if (nowSeconds - _summaryAnchorSeconds < SummaryPeriodSeconds) return;
            _summaryAnchorSeconds = nowSeconds;

            string dominant = _perSwapChain.Count == 0
                ? null
                : _perSwapChain.OrderByDescending(kvp => kvp.Value).First().Key;
            if (dominant != null && _dominantSwapChain != null &&
                !string.Equals(dominant, _dominantSwapChain, StringComparison.Ordinal))
            {
                _log(string.Format(CultureInfo.InvariantCulture,
                    "dominant swapchain changed {0} -> {1} (rows this window: [{2}])",
                    _dominantSwapChain, dominant, FormatCounts(_perSwapChain)));
            }
            if (dominant != null) _dominantSwapChain = dominant;

            _log(string.Format(CultureInfo.InvariantCulture,
                "10s: pid={0} rows={1} rejected={2} swapchains=[{3}] runtimes=[{4}] frametypes=[{5}] " +
                "maxFrametime={6:F1}ms outliers={7}{8} sourceGaps={9}(max {10:F0}ms) " +
                "arrivalGaps={11}(max {12:F1}s) backdated={13} label='{14}'",
                _targetPid, _rows, _rejectedRows, FormatCounts(_perSwapChain), FormatCounts(_perRuntime),
                FormatCounts(_perFrameType), _maxFrametimeMs, _outliers,
                _outlierExample != null ? " (max " + _outlierExample + ")" : "",
                _sourceGaps, _maxSourceGapMs, _arrivalGaps, _maxArrivalGapSeconds, _backdated,
                _runtimeLabel ?? "<none>"));
            ResetWindowLocked();
        }

        private void ResetWindowLocked()
        {
            _rows = 0;
            _rejectedRows = 0;
            _outliers = 0;
            _sourceGaps = 0;
            _arrivalGaps = 0;
            _backdated = 0;
            _maxFrametimeMs = 0;
            _maxSourceGapMs = 0;
            _maxArrivalGapSeconds = 0;
            _outlierExample = null;
            _perSwapChain.Clear();
            _perRuntime.Clear();
            _perFrameType.Clear();
        }

        private static void Increment(Dictionary<string, int> counts, string key)
        {
            counts.TryGetValue(key, out int n);
            counts[key] = n + 1;
        }

        private static string FormatCounts(Dictionary<string, int> counts)
        {
            if (counts.Count == 0) return "";
            var sb = new StringBuilder();
            foreach (var kvp in counts.OrderByDescending(k => k.Value))
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(kvp.Key).Append(':').Append(kvp.Value.ToString(CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }
    }
}
