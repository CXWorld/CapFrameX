using Gma.System.MouseKeyHook;
using Gma.System.MouseKeyHook.Implementation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

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
		/// <remarks>
		/// <paramref name="key" /> and the keys of <paramref name="map" /> must name the trigger
		/// key the way the running framework's <see cref="Keys" /> enum does, because both are
		/// compared against <c>e.KeyCode.ToString()</c>. <see cref="HotkeyDictionaryBuilder" />
		/// establishes that by running the configured name through
		/// <see cref="HotkeyKeyName.Canonicalize" />.
		/// </remarks>
		public static void OnCXCombination(this IKeyboardEvents source, string key, Dictionary<string, Action> map, Action reset = null)
		{
			source.KeyDown += (sender, e) =>
			{
				Action action = reset;
				if (e.KeyCode.ToString() == key)
				{
					var state = KeyboardState.GetCurrent();


					var hotkeyString = string.Empty;
					if (state.IsDown(Keys.Control))
						hotkeyString += "Control+";
					if (state.IsDown(Keys.Shift))
						hotkeyString += "Shift+";
					if (state.IsDown(Keys.Alt))
						hotkeyString += "Alt+";
					hotkeyString += e.KeyCode.ToString();

					map.TryGetValue(hotkeyString, out action);
				}
				action?.Invoke();
			};
		}
	}
}
