using Gma.System.MouseKeyHook;
using Gma.System.MouseKeyHook.Implementation;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.Hotkey
{
	public static class KeyCombinationExtensions
	{
		/// <summary>
		/// Detects a key or key combination and triggers the corresponding action.
		/// </summary>
		/// <param name="source">
		/// An instance of Global or Application hook. Use <see cref="Hook.GlobalEvents" /> or <see cref="Hook.AppEvents" /> to
		/// create it.
		/// </param>
		/// <param name="map">
		/// This map contains the list of key combinations mapped to corresponding actions. You can use a dictionary initilizer
		/// to easily create it.
		/// Whenever a listed combination will be detected a corresponding action will be triggered.
		/// </param>
		/// <param name="reset">
		/// This optional action will be executed when some key was pressed but it was not part of any wanted combinations.
		/// </param>
		public static void OnCXCombination(this IKeyboardEvents source,
			IEnumerable<KeyValuePair<CXHotkeyCombination, Action>> map, Action reset = null)
		{
			var watchlists = map.GroupBy(k => k.Key.TriggerKey)
				.ToDictionary(g => g.Key, g => g.ToArray());
			source.KeyDown += (sender, e) =>
			{
				if (!watchlists.TryGetValue(e.KeyCode, out KeyValuePair<CXHotkeyCombination, Action>[] element))
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
}
