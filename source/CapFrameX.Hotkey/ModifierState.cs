using System;
using System.Runtime.InteropServices;
using System.Text;

namespace CapFrameX.Hotkey
{
    /// <summary>The modifiers a hotkey combination can contain.</summary>
    [Flags]
    internal enum HotkeyModifiers
    {
        None = 0,
        Control = 1,
        Shift = 2,
        Alt = 4
    }

    /// <summary>
    /// Builds the combination string a key press is matched against, e.g. <c>"Control+Alt+F12"</c>.
    ///
    /// The modifier state deliberately comes from <c>GetAsyncKeyState</c> rather than
    /// <c>GetKeyboardState</c> (which is what MouseKeyHook's <c>KeyboardState.GetCurrent()</c>
    /// uses): <c>GetKeyboardState</c> returns the *calling thread's* input state, which is only
    /// updated as that thread removes keyboard messages from its own queue. The hook thread never
    /// does — while a game owns the foreground input queue there is nothing that keeps that state
    /// current, and a modifier read as "up" silently turns a hotkey into a no-match.
    /// <c>GetAsyncKeyState</c> reads the global state instead.
    ///
    /// The documented caveat that the asynchronous state of the key being pressed is not yet
    /// updated inside the callback does not apply here: only modifiers are read this way, the
    /// trigger key comes from the hook event itself.
    /// </summary>
    internal static class ModifierState
    {
        private const int VK_SHIFT = 0x10;
        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12;

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        internal static string BuildCombination(string triggerKeyName)
            => Combine(Read(), triggerKeyName);

        /// <summary>The modifiers held down right now, from the global key state.</summary>
        internal static HotkeyModifiers Read()
        {
            var modifiers = HotkeyModifiers.None;

            if (IsDown(VK_CONTROL))
                modifiers |= HotkeyModifiers.Control;
            if (IsDown(VK_SHIFT))
                modifiers |= HotkeyModifiers.Shift;
            if (IsDown(VK_MENU))
                modifiers |= HotkeyModifiers.Alt;

            return modifiers;
        }

        /// <summary>
        /// Modifier order matches the one the stored combinations are built in
        /// (see <c>HotkeyDictionaryBuilder</c>): Control, Shift, Alt. The hotkey witness builds
        /// its combinations here too, so both sides compare the same strings.
        /// </summary>
        internal static string Combine(HotkeyModifiers modifiers, string triggerKeyName)
        {
            var combination = new StringBuilder(32);

            if ((modifiers & HotkeyModifiers.Control) != 0)
                combination.Append("Control+");
            if ((modifiers & HotkeyModifiers.Shift) != 0)
                combination.Append("Shift+");
            if ((modifiers & HotkeyModifiers.Alt) != 0)
                combination.Append("Alt+");

            combination.Append(triggerKeyName);
            return combination.ToString();
        }

        private static bool IsDown(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
    }
}
