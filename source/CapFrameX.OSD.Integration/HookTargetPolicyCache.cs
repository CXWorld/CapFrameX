using System;

namespace CapFrameX.OSD.Integration
{
    /// <summary>
    /// How long a target policy verdict stays current (<see cref="HookTargetPolicyCache"/>).
    /// A clean verdict is checked again every 2 s during the first half minute of a target, when
    /// anti-cheat modules and frame generation runtimes still load, and every 10 s after that. A
    /// denial that is final for the process (an anti-cheat module, Ricochet, the injection
    /// blacklist: <see cref="HookTargetPolicy.RequestsHookFreeFallback"/>) never expires; any
    /// other denial, a transient startup failure above all, is checked again every 2 s.
    /// </summary>
    internal static class HookTargetPolicySchedule
    {
        internal const long StartupPhaseMs = 30_000;
        internal const long StartupIntervalMs = 2_000;
        internal const long SteadyIntervalMs = 10_000;
        internal const long DeniedIntervalMs = 2_000;

        /// <summary>
        /// The lifetime of a verdict in ms, or null for one that never expires.
        /// <paramref name="sinceFirstVerdictMs"/> counts from the first verdict for the target.
        /// </summary>
        internal static long? VerdictLifetimeMs(bool allowed, string reason, long sinceFirstVerdictMs)
        {
            if (!allowed)
                return HookTargetPolicy.RequestsHookFreeFallback(reason) ? (long?)null : DeniedIntervalMs;
            return sinceFirstVerdictMs < StartupPhaseMs ? StartupIntervalMs : SteadyIntervalMs;
        }
    }

    /// <summary>
    /// The verdict cache of <see cref="HookTargetPolicy"/>. Its evaluation opens the game, reads
    /// its image path, looks for Ricochet's driver and takes a module snapshot, so it must not run
    /// on the PresentMon reader thread, where every other row consumer waits for it:
    /// <list type="bullet">
    /// <item><see cref="IsAllowed"/> evaluates only when no verdict for the PID exists yet (or
    /// <c>requireFresh</c> asks for a current one); a stale verdict is returned and refreshed in
    /// the background.</item>
    /// <item><see cref="TryGetVerdict"/> never evaluates: without a verdict it starts one in the
    /// background and returns false. The row paths use it.</item>
    /// <item><see cref="VerdictChanged"/> reports every background verdict that differs from the
    /// one before, so a reader that got none or a stale one learns the outcome.</item>
    /// </list>
    /// One PID is cached at a time, as before: every caller asks for the overlay target.
    /// </summary>
    internal sealed class HookTargetPolicyCache
    {
        internal readonly struct Verdict
        {
            public Verdict(bool allowed, string reason)
            {
                Allowed = allowed;
                Reason = reason;
            }

            public bool Allowed { get; }
            public string Reason { get; }

            public bool SameAs(Verdict other)
                => Allowed == other.Allowed && string.Equals(Reason, other.Reason, StringComparison.Ordinal);
        }

        private readonly Func<int, Verdict> _evaluate;
        private readonly Func<long> _clockMs;
        private readonly Action<Action> _runInBackground;
        private readonly object _gate = new object();

        private int _pid;
        private bool _hasVerdict;
        private Verdict _verdict;
        private long _evaluatedAtMs;
        private long? _lifetimeMs;
        private int _firstVerdictPid;
        private long _firstVerdictAtMs;
        // Bumped by Invalidate: an evaluation that started before it must not store its result.
        private long _generation;
        private int _backgroundPid;

        internal HookTargetPolicyCache(Func<int, Verdict> evaluate, Func<long> clockMs,
            Action<Action> runInBackground)
        {
            _evaluate = evaluate ?? throw new ArgumentNullException(nameof(evaluate));
            _clockMs = clockMs ?? throw new ArgumentNullException(nameof(clockMs));
            _runInBackground = runInBackground ?? throw new ArgumentNullException(nameof(runInBackground));
        }

        /// <summary>Raised with the PID after a background evaluation changed its verdict.</summary>
        internal event Action<int> VerdictChanged;

        internal bool IsAllowed(int pid, bool requireFresh, out string reason)
        {
            long generation;
            lock (_gate)
            {
                if (_hasVerdict && _pid == pid)
                {
                    bool fresh = IsFreshLocked(_clockMs());
                    if (fresh || !requireFresh)
                    {
                        if (!fresh) StartBackgroundLocked(pid);
                        reason = _verdict.Reason;
                        return _verdict.Allowed;
                    }
                }
                generation = _generation;
            }

            Verdict verdict = _evaluate(pid);
            lock (_gate)
            {
                if (generation == _generation) StoreLocked(pid, verdict, _clockMs());
            }
            reason = verdict.Reason;
            return verdict.Allowed;
        }

        internal bool TryGetVerdict(int pid, out bool allowed, out string reason)
        {
            lock (_gate)
            {
                if (_hasVerdict && _pid == pid)
                {
                    if (!IsFreshLocked(_clockMs())) StartBackgroundLocked(pid);
                    allowed = _verdict.Allowed;
                    reason = _verdict.Reason;
                    return true;
                }
                StartBackgroundLocked(pid);
            }
            allowed = false;
            reason = null;
            return false;
        }

        internal void Invalidate(int pid)
        {
            lock (_gate)
            {
                bool cached = pid <= 0 || pid == _pid;
                // A background evaluation of the PID that is still running belongs to before the
                // invalidation too (early injection invalidates a transient empty module list).
                bool pending = pid <= 0 || pid == _backgroundPid;
                if (!cached && !pending) return;
                _generation++;
                if (!cached) return;
                _pid = 0;
                _hasVerdict = false;
                _verdict = default;
                _lifetimeMs = null;
                _firstVerdictPid = 0;
            }
        }

        private bool IsFreshLocked(long nowMs)
            => _lifetimeMs == null || nowMs - _evaluatedAtMs < _lifetimeMs.Value;

        private void StoreLocked(int pid, Verdict verdict, long nowMs)
        {
            if (_firstVerdictPid != pid)
            {
                _firstVerdictPid = pid;
                _firstVerdictAtMs = nowMs;
            }
            _pid = pid;
            _hasVerdict = true;
            _verdict = verdict;
            _evaluatedAtMs = nowMs;
            _lifetimeMs = HookTargetPolicySchedule.VerdictLifetimeMs(verdict.Allowed, verdict.Reason,
                nowMs - _firstVerdictAtMs);
        }

        // One background evaluation at a time; a request for another PID waits for the next read.
        private void StartBackgroundLocked(int pid)
        {
            if (_backgroundPid != 0) return;
            _backgroundPid = pid;
            long generation = _generation;
            _runInBackground(() => RunBackground(pid, generation));
        }

        private void RunBackground(int pid, long generation)
        {
            bool changed = false;
            try
            {
                Verdict verdict = _evaluate(pid);
                lock (_gate)
                {
                    if (generation != _generation) return;
                    changed = !(_hasVerdict && _pid == pid && _verdict.SameAs(verdict));
                    StoreLocked(pid, verdict, _clockMs());
                }
            }
            finally
            {
                lock (_gate) _backgroundPid = 0;
            }
            if (changed) VerdictChanged?.Invoke(pid);
        }
    }
}
