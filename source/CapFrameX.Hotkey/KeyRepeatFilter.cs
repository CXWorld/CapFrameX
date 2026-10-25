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
        /// Longer than the longest wait before Windows repeats a held key, so a repeat is always
        /// recognized as one. That wait is the keyboard delay, 250 ms to about 1 s (settings 0 to
        /// 3); the repeats after it come 33 to 400 ms apart. 700 ms covered only those intervals:
        /// with delay 2 or 3 the first repeat of a held hotkey passed as a second press and
        /// flipped a toggle straight back. The stamps are taken when the hook thread handles a
        /// key-down, so the margin above 1 s also covers a short stall between a press and its
        /// first repeat.
        /// </summary>
        public const long HeldExpiryMs = 1500;

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
