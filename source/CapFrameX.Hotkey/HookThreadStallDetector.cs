using System;

namespace CapFrameX.Hotkey
{
    /// <summary>
    /// Detects that the hook thread's message loop was unresponsive for long enough that Windows
    /// may have dropped the low-level keyboard hook, so the hook is re-armed right away instead
    /// of at the next slow periodic re-arm.
    ///
    /// Windows removes a WH_KEYBOARD_LL hook whose procedure does not return within
    /// <c>LowLevelHooksTimeout</c> (300 ms by default), and a procedure that cannot even start
    /// because its thread gets no CPU or its message loop is stuck counts the same. The removal
    /// is silent, so the only observable symptom is the stall itself: a heartbeat timer on the
    /// hook thread that fires late by more than the timeout. Nothing says a keystroke arrived
    /// during the stall, so this over-approximates; a re-arm is cheap enough for that. Without
    /// it a dropped hook stayed dead for up to a minute, which from the outside looks like a
    /// hotkey that is "executed very late" once the periodic re-arm finally restores it.
    /// </summary>
    public sealed class HookThreadStallDetector
    {
        /// <summary>
        /// Below the default LowLevelHooksTimeout (300 ms), with margin for the heartbeat's own
        /// interval and timer jitter: a gap this long means the message loop was blocked for at
        /// least ~150 ms, and a lower timeout configured by the user is still covered.
        /// </summary>
        public const long StallThresholdMs = 200;

        /// <summary>
        /// A sustained starvation episode would otherwise re-arm on every heartbeat that gets
        /// through. Each re-arm briefly runs two hooks whose duplicate events the repeat filter
        /// has to absorb, so one re-arm per episode is enough.
        /// </summary>
        public const long MinimumRearmIntervalMs = 2000;

        private long _lastHeartbeatMs;
        private long? _lastRearmMs;
        private bool _started;

        /// <summary>
        /// Records one heartbeat. Returns the gap to the previous heartbeat in milliseconds when
        /// it exceeded <see cref="StallThresholdMs"/> and a re-arm is due, otherwise 0.
        /// </summary>
        public long Heartbeat(long nowMs)
        {
            if (!_started)
            {
                _started = true;
                _lastHeartbeatMs = nowMs;
                return 0;
            }

            var gap = nowMs - _lastHeartbeatMs;
            _lastHeartbeatMs = nowMs;

            if (gap < StallThresholdMs)
                return 0;
            if (_lastRearmMs.HasValue && nowMs - _lastRearmMs.Value < MinimumRearmIntervalMs)
                return 0;

            _lastRearmMs = nowMs;
            return gap;
        }

        /// <summary>A re-arm that happened for another reason (the periodic timer) counts against the rate limit too.</summary>
        public void NotifyRearmed(long nowMs)
        {
            _lastRearmMs = nowMs;
        }
    }

    /// <summary>
    /// Tick-count arithmetic for the hotkey latency diagnostics. The keypress stamp comes from
    /// the low-level hook data (<c>KBDLLHOOKSTRUCT.time</c>, a GetTickCount value) and is
    /// compared against <see cref="Environment.TickCount"/>; both wrap after 49.7 days, which
    /// the unchecked subtraction handles as long as the real gap is below 24.8 days.
    /// </summary>
    public static class HotkeyLatency
    {
        public static int ElapsedMs(int earlierTick, int laterTick)
        {
            var elapsed = unchecked(laterTick - earlierTick);
            // Both stamps have ~16 ms granularity, so a tick that was rounded the other way can
            // come out slightly negative. That is "no measurable delay", not a negative delay.
            return elapsed < 0 ? 0 : elapsed;
        }
    }
}
