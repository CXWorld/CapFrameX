using Gma.System.MouseKeyHook;
using Gma.System.MouseKeyHook.Implementation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;

namespace CapFrameX.Hotkey
{
	public static class KeyCombinationExtensions
	{
		/// <summary>
		///     Detects a key or key combination and triggers the corresponding action.
		/// </summary>
		/// <param name="source">
		///     An instance of Global or Application hook. Use <see cref="Hook.GlobalEvents" /> or <see cref="Hook.AppEvents" /> to
		///     create it.
		/// </param>
		/// <param name="map">
		///     This map contains the list of key combinations mapped to corresponding actions. You can use a dictionary initilizer
		///     to easily create it.
		///     Whenever a listed combination will be detected a corresponding action will be triggered.
		/// </param>
		/// <param name="reset">
		///     This optional action will be executed when some key was pressed but it was not part of any wanted combinations.
		/// </param>
		public static void OnCXCombination(this IKeyboardEvents source,
			IEnumerable<KeyValuePair<CXHotkeyCombination, Action>> map, Action reset = null)
		{
			var watchlists = map.GroupBy(k => k.Key.TriggerKey)
				.ToDictionary(g => g.Key, g => g.ToArray());
			source.KeyDown += (sender, e) =>
			{
				KeyValuePair<CXHotkeyCombination, Action>[] element;
				var found = watchlists.TryGetValue(e.KeyCode, out element);
				if (!found)
				{
					reset?.Invoke();
					return;
				}
				var state = KeyboardState.GetCurrent();
				var action = reset;
				var maxLength = 0;
				foreach (var current in element)
				{
					var matches = current.Key.Chord.All(state.IsDown);
					if (!matches) continue;
					if (maxLength > current.Key.ChordLength) continue;
					maxLength = current.Key.ChordLength;
					action = current.Value;
				}
				action?.Invoke();
			};
		}
	}

    internal class Chord : IEnumerable<Keys>
    {
        private readonly Keys[] _keys;

        internal Chord(IEnumerable<Keys> additionalKeys)
        {
            _keys = additionalKeys.OrderBy(k => k).ToArray();
        }

        public int Count
        {
            get { return _keys.Length; }
        }

        public IEnumerator<Keys> GetEnumerator()
        {
            return _keys.Cast<Keys>().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public override string ToString()
        {
            return string.Join("+", _keys);
        }

        public static Chord FromString(string chord)
        {
            var parts = chord
                .Split('+')
                .Select(p => Enum.Parse(typeof(Keys), p))
                .Cast<Keys>();
            var stack = new Stack<Keys>(parts);
            return new Chord(stack);
        }

        protected bool Equals(Chord other)
        {
            if (_keys.Length != other._keys.Length) return false;
            return _keys.SequenceEqual(other._keys);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((Chord)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (_keys.Length + 13) ^
                       ((_keys.Length != 0 ? (int)_keys[0] ^ (int)_keys[_keys.Length - 1] : 0) * 397);
            }
        }
    }

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
        ///     Last key which triggers the combination.
        /// </summary>
        public Keys TriggerKey { get; }

        /// <summary>
        ///     Keys which all must be alredy down when trigger key is pressed.
        /// </summary>
        public IEnumerable<Keys> Chord
        {
            get { return _chord; }
        }

        /// <summary>
        ///     Number of chord (modifier) keys which must be already down when the trigger key is pressed.
        /// </summary>
        public int ChordLength
        {
            get { return _chord.Count; }
        }

        /// <summary>
        ///     A chainable builder method to simplify chord creation. Used along with <see cref="TriggeredBy" />,
        ///     <see cref="With" />, <see cref="Control" />, <see cref="Shift" />, <see cref="Alt" />.
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
        ///     A chainable builder method to simplify chord creation. Used along with <see cref="TriggeredBy" />,
        ///     <see cref="With" />, <see cref="Control" />, <see cref="Shift" />, <see cref="Alt" />.
        /// </summary>
        public CXHotkeyCombination Control()
        {
            return With(Keys.Control);
        }

        /// <summary>
        ///     A chainable builder method to simplify chord creation. Used along with <see cref="TriggeredBy" />,
        ///     <see cref="With" />, <see cref="Control" />, <see cref="Shift" />, <see cref="Alt" />.
        /// </summary>
        public CXHotkeyCombination Alt()
        {
            return With(Keys.Alt);
        }

        /// <summary>
        ///     A chainable builder method to simplify chord creation. Used along with <see cref="TriggeredBy" />,
        ///     <see cref="With" />, <see cref="Control" />, <see cref="Shift" />, <see cref="Alt" />.
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
        ///     TriggeredBy a chord from any string like this 'Alt+Shift+R'.
        ///     Nothe that the trigger key must be the last one.
        /// </summary>
        public static CXHotkeyCombination FromString(string trigger)
        {
            var parts = trigger
                .Split('+')
                .Select(p => ParseKeyEnum(p))
                .Cast<Keys>();
            var stack = new Stack<Keys>(parts);
            var triggerKey = stack.Pop();
            return new CXHotkeyCombination(triggerKey, stack);
        }

        /// <inheritdoc />
        protected bool Equals(CXHotkeyCombination other)
        {
            return
                TriggerKey == other.TriggerKey
                && Chord.Equals(other.Chord);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((CXHotkeyCombination)obj);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return Chord.GetHashCode() ^
                   (int)TriggerKey;
        }

        private static Keys ParseKeyEnum(string key)
        {
            var parseResult = Enum.TryParse(key, out Keys parsedKey);
            if(parseResult)
            {
                return parsedKey;
            }

            // if parsing failed, we try to determine the key by string comparison ignoring case sensitivity
            var keys = Enum.GetNames(typeof(Keys));
            var matchingEntry = keys.First(keysEntry => string.Compare(keysEntry, key, true) == 0);
            Enum.TryParse(matchingEntry, out Keys result);
            return result;
        }
    }
}
