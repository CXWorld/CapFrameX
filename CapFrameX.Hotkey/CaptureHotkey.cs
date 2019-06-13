using System;
using System.Text;
using System.Windows.Input;

namespace CapFrameX.Hotkey
{
	public class CaptureHotkey
	{
		public Key Key { get; }

		public ModifierKeys Modifiers { get; }

		public CaptureHotkey()
		{
			Key = Key.F12;
			Modifiers = ModifierKeys.None;
		}

		public CaptureHotkey(Key key, ModifierKeys modifiers)
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

        public static bool IsValidHotkey(string hotkeyString)
        {
            if (string.IsNullOrWhiteSpace(hotkeyString))
                return false;

            var keyStrings = hotkeyString.Split('+');

            bool isValid = true;
            CaptureHotkey captureHotkey;
            try
            {
                if (keyStrings.Length == 1)
                {
                    var key = (Key)Enum.Parse(typeof(Key), keyStrings[0], true);

                    if (!((key >= Key.A && key <= Key.Z) || (key >= Key.F1 && key <= Key.F12)))
                        isValid = false;

                    captureHotkey = new CaptureHotkey(key, ModifierKeys.None);
                }
                else if (keyStrings.Length == 2)
                {
                    var keyModifier = (ModifierKeys)Enum.Parse(typeof(ModifierKeys), keyStrings[0], true);
                    var key = (Key)Enum.Parse(typeof(Key), keyStrings[1], true);

                    if (!((key >= Key.A && key <= Key.Z) || (key >= Key.F1 && key <= Key.F12)))
                        isValid = false;

                    if (!(keyModifier == ModifierKeys.Alt || keyModifier == ModifierKeys.Shift || keyModifier == ModifierKeys.Control))
                        isValid = false;

                    captureHotkey = new CaptureHotkey(key, keyModifier);
                }
                else if (keyStrings.Length == 3)
                {
                    var keyModifierA = (ModifierKeys)Enum.Parse(typeof(ModifierKeys), keyStrings[0], true);

                    if (!(keyModifierA == ModifierKeys.Alt || keyModifierA == ModifierKeys.Shift || keyModifierA == ModifierKeys.Control))
                        isValid = false;

                    var keyModifierB = (ModifierKeys)Enum.Parse(typeof(ModifierKeys), keyStrings[1], true);

                    if (!(keyModifierB == ModifierKeys.Alt || keyModifierB == ModifierKeys.Shift || keyModifierB == ModifierKeys.Control))
                        isValid = false;

                    var key = (Key)Enum.Parse(typeof(Key), keyStrings[2], true);

                    if (!((key >= Key.A && key <= Key.Z) || (key >= Key.F1 && key <= Key.F12)))
                        isValid = false;

                    captureHotkey = new CaptureHotkey(key, keyModifierA | keyModifierB);
                }
            }
            catch { isValid = false; }

            return isValid;
        }
    }
}
