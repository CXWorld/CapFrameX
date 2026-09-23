namespace CapFrameX.OSD.Integration
{
    // Status flags describe the last draw. Only counter/timestamp progress proves new draws.
    internal sealed class HookRenderProgressVerifier
    {
        private HookRenderProgress? _baseline;
        private HookRenderProgress _last;
        private ulong _started;
        private bool _confirmed;

        internal void Reset()
        {
            _baseline = null;
            _confirmed = false;
            _started = 0;
        }

        internal bool Observe(NativeHookStatusSnapshot snapshot, bool active, uint expectedFlags, ulong now)
        {
            if (!active || snapshot.Progress is not HookRenderProgress p || p.Generation == 0 ||
                p.LastDrawTickMs <= 0 || (ulong)p.LastDrawTickMs > now ||
                now - (ulong)p.LastDrawTickMs > HookStatusProbe.HeartbeatStaleAfterMs ||
                p.AppliedFlags != expectedFlags || snapshot.AppliedFlags != expectedFlags ||
                p.AppliedSequence != snapshot.AppliedSequence || p.PendingRestartFlags != 0 ||
                snapshot.PendingRestartFlags != 0)
            {
                Reset();
                return false;
            }
            if (_baseline is not HookRenderProgress start || start.Generation != p.Generation ||
                start.RouteSource != p.RouteSource || start.AppliedFlags != p.AppliedFlags ||
                start.AppliedSequence != p.AppliedSequence || p.Draws < start.Draws ||
                p.Presents < start.Presents || p.LastDrawTickMs < start.LastDrawTickMs ||
                p.Draws < _last.Draws || p.Presents < _last.Presents || p.LastDrawTickMs < _last.LastDrawTickMs)
            {
                _baseline = p;
                _last = p;
                _started = now;
                _confirmed = false;
                return false;
            }
            _last = p;
            // At least two new draws, with the last draw itself spanning the observation window.
            // Re-reading a still-fresh but frozen snapshot can never satisfy this condition.
            if (p.Draws - start.Draws >= 2 && p.Presents > start.Presents &&
                (ulong)p.LastDrawTickMs >= _started &&
                (ulong)p.LastDrawTickMs - _started >= HookCompatibilityVerdictClassifier.ProbeSuccessConfirmMs)
                _confirmed = true;
            return _confirmed;
        }
    }
}
