using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace CapFrameX.Hotkey
{
    /// <summary>
    /// Collapses the auto-repeat KeyDown stream Windows produces while a key stays pressed down
    /// into a single trigger.
    ///
    /// A global hook sees every repeat, so holding a toggle hotkey for a moment too long used to
    /// flip it an even number of times — indistinguishable, from the outside, from a hotkey that
    /// did not fire at all. Only the capture hotkey guarded against this, with a local 500 ms
    /// lock; every other hotkey was unprotected.
    ///
    /// A key-up clears the key outright, so two deliberate presses in quick succession are both
    /// honoured. The time based expiry is only a fallback for a key-up that never arrived (a
    /// key released while the hook was being re-armed, for instance).
    /// </summary>
    public sealed class KeyRepeatFilter
    {
        /// <summary>
        /// Longer than the slowest repeat interval Windows can be configured for (500 ms at the
        /// minimum repeat rate), so a repeat is always recognized as one.
        /// </summary>
        public const long HeldExpiryMs = 700;

        private readonly Dictionary<Keys, long> _lastKeyDown = new Dictionary<Keys, long>();
        private readonly object _sync = new object();

        public bool ShouldHandleKeyDown(Keys key)
            => ShouldHandleKeyDown(key, Environment.TickCount64);

        /// <summary>
        /// Returns whether this key-down is a genuine press rather than an auto-repeat.
        /// The timestamp is a parameter so the behaviour can be tested without waiting.
        /// </summary>
        public bool ShouldHandleKeyDown(Keys key, long timestampMs)
        {
            lock (_sync)
            {
                var isRepeat = _lastKeyDown.TryGetValue(key, out var previous)
                    && timestampMs - previous < HeldExpiryMs;

                // Refreshed even for a repeat: a key held down keeps extending its own
                // suppression window instead of breaking out of it at the expiry.
                _lastKeyDown[key] = timestampMs;

                return !isRepeat;
            }
        }

        public void OnKeyUp(Keys key)
        {
            lock (_sync)
            {
                _lastKeyDown.Remove(key);
            }
        }
    }
}
