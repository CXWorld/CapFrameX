using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace CapFrameX.Hotkey
{
    /// <summary>
    /// Key<->Keys converter: https://stackoverflow.com/questions/1153009/how-can-i-convert-system-windows-input-key-to-system-windows-forms-keys
    /// </summary>
    public class CXHotkeyCombination
    {
        private readonly Chord _chord;

        private CXHotkeyCombination(Keys triggerKey, IEnumerable<Keys> chordKeys)
            : this(triggerKey, new Chord(chordKeys))
        {
        }

        private CXHotkeyCombination(Keys triggerKey, Chord chord)
        {
            TriggerKey = triggerKey;
            _chord = chord;
        }

        /// <summary>
        /// Last key which triggers the combination.
        /// </summary>
        public Keys TriggerKey { get; }

        /// <summary>
        /// Keys which all must be alredy down when trigger key is pressed.
        /// </summary>
        public IEnumerable<Keys> Chord
        {
            get { return _chord; }
        }

        /// <summary>
        /// Number of chord (modifier) keys which must be already down when the trigger key is pressed.
        /// </summary>
        public int ChordLength
        {
            get { return _chord.Count; }
        }

        /// <summary>
        /// A chainable builder method to simplify chord creation. Used along with <see cref="TriggeredBy" />,
        /// <see cref="With" />, <see cref="Control" />, <see cref="Shift" />, <see cref="Alt" />.
        /// </summary>
        /// <param name="key"></param>
        public static CXHotkeyCombination TriggeredBy(Keys key)
        {
            return new CXHotkeyCombination(key, (IEnumerable<Keys>)new Chord(Enumerable.Empty<Keys>()));
        }

        /// <summary>
        ///     A chainable builder method to simplify chord creation. Used along with <see cref="TriggeredBy" />,
        ///     <see cref="With" />, <see cref="Control" />, <see cref="Shift" />, <see cref="Alt" />.
        /// </summary>
        /// <param name="key"></param>
        public CXHotkeyCombination With(Keys key)
        {
            return new CXHotkeyCombination(TriggerKey, Chord.Concat(Enumerable.Repeat(key, 1)));
        }

        /// <summary>
        /// A chainable builder method to simplify chord creation. Used along with <see cref="TriggeredBy" />,
        /// <see cref="With" />, <see cref="Control" />, <see cref="Shift" />, <see cref="Alt" />.
        /// </summary>
        public CXHotkeyCombination Control()
        {
            return With(Keys.Control);
        }

        /// <summary>
        /// A chainable builder method to simplify chord creation. Used along with <see cref="TriggeredBy" />,
        /// <see cref="With" />, <see cref="Control" />, <see cref="Shift" />, <see cref="Alt" />.
        /// </summary>
        public CXHotkeyCombination Alt()
        {
            return With(Keys.Alt);
        }

        /// <summary>
        /// A chainable builder method to simplify chord creation. Used along with <see cref="TriggeredBy" />,
        /// <see cref="With" />, <see cref="Control" />, <see cref="Shift" />, <see cref="Alt" />.
        /// </summary>
        public CXHotkeyCombination Shift()
        {
            return With(Keys.Shift);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return string.Join("+", Chord.Concat(Enumerable.Repeat(TriggerKey, 1)));
        }

        /// <summary>
        /// TriggeredBy a chord from any string like this 'Alt+Shift+R'.
        /// Nothe that the trigger key must be the last one.
        /// </summary>
        public static CXHotkeyCombination FromString(string trigger)
        {
            var parts = trigger
                .Split('+')
                .Select(p => Enum.Parse(typeof(Keys), p, true))
                .Cast<Keys>();
            var stack = new Stack<Keys>(parts);
            var triggerKey = stack.Pop();
            return new CXHotkeyCombination(triggerKey, stack);
        }

        protected bool Equals(CXHotkeyCombination other)
        {
            return
                TriggerKey == other.TriggerKey
                && Chord.Equals(other.Chord);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((CXHotkeyCombination)obj);
        }

        public override int GetHashCode()
        {
            return Chord.GetHashCode() ^
                   (int)TriggerKey;
        }
    }
}
