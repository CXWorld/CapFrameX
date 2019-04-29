using System.Text;
using System.Windows.Input;

namespace CapFrameX.MVVM
{
	public class Hotkey
	{
		public Key Key { get; }

		public ModifierKeys Modifiers { get; }

		public Hotkey()
		{
			Key = Key.F12;
			Modifiers = ModifierKeys.None;
		}

		public Hotkey(Key key, ModifierKeys modifiers)
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
	}
}
