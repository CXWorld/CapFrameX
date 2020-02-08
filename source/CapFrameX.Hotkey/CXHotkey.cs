using System;
using System.Text;
using System.Windows.Input;

namespace CapFrameX.Hotkey
{
	public class CXHotkey
	{
		public Key Key { get; }

		public ModifierKeys Modifiers { get; }

		public CXHotkey(Key defaultKey)
		{
			Key = defaultKey;
			Modifiers = ModifierKeys.None;
		}

		public CXHotkey(Key key, ModifierKeys modifiers)
		{
			Key = key;
			Modifiers = modifiers;
		}

		public override string ToString()
		{
			var str = new StringBuilder();

			if (Modifiers.HasFlag(ModifierKeys.Control))
				str.Append("Control + ");
			if (Modifiers.HasFlag(ModifierKeys.Shift))
				str.Append("Shift + ");
			if (Modifiers.HasFlag(ModifierKeys.Alt))
				str.Append("Alt + ");
			if (Modifiers.HasFlag(ModifierKeys.Windows))
				str.Append("Win + ");

			str.Append(Key);

			return str.ToString();
		}

        public static CXHotkey Create(string[] keyStrings, Key defaultKey, ModifierKeys modifierKey = ModifierKeys.None)
        {
            CXHotkey hotkey = new CXHotkey(defaultKey, modifierKey);

            if (keyStrings.Length == 1)
            {
                var key = (Key)Enum.Parse(typeof(Key), keyStrings[0], true);
                hotkey = new CXHotkey(key, ModifierKeys.None);
            }
            else if (keyStrings.Length == 2)
            {
                var keyModifier = (ModifierKeys)Enum.Parse(typeof(ModifierKeys), keyStrings[0], true);
                var key = (Key)Enum.Parse(typeof(Key), keyStrings[1], true);

                hotkey = new CXHotkey(key, keyModifier);
            }
            else if (keyStrings.Length == 3)
            {
                var keyModifierA = (ModifierKeys)Enum.Parse(typeof(ModifierKeys), keyStrings[0], true);
                var keyModifierB = (ModifierKeys)Enum.Parse(typeof(ModifierKeys), keyStrings[1], true);
                var key = (Key)Enum.Parse(typeof(Key), keyStrings[2], true);

                hotkey = new CXHotkey(key, keyModifierA | keyModifierB);
            }

            return hotkey;
        }

        public static bool IsValidHotkey(string hotkeyString)
        {
            if (string.IsNullOrWhiteSpace(hotkeyString))
                return false;

            var keyStrings = hotkeyString.Split('+');

            bool isValid = true;
            CXHotkey captureHotkey;
            try
            {
                if (keyStrings.Length == 1)
                {
                    var key = (Key)Enum.Parse(typeof(Key), keyStrings[0], true);

                    captureHotkey = new CXHotkey(key, ModifierKeys.None);
                }
                else if (keyStrings.Length == 2)
                {
                    var keyModifier = (ModifierKeys)Enum.Parse(typeof(ModifierKeys), keyStrings[0], true);
                    var key = (Key)Enum.Parse(typeof(Key), keyStrings[1], true);

                    captureHotkey = new CXHotkey(key, keyModifier);
                }
                else if (keyStrings.Length == 3)
                {
                    var keyModifierA = (ModifierKeys)Enum.Parse(typeof(ModifierKeys), keyStrings[0], true);
                    var keyModifierB = (ModifierKeys)Enum.Parse(typeof(ModifierKeys), keyStrings[1], true);

                    var key = (Key)Enum.Parse(typeof(Key), keyStrings[2], true);

                    captureHotkey = new CXHotkey(key, keyModifierA | keyModifierB);
                }
            }
            catch { isValid = false; }

            return isValid;
        }
    }
}
