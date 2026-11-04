using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.CaptureHelper
{
    /// <summary>
    /// The capture hotkeys to choose from. F12 is left out: Windows reserves it for debuggers and
    /// RegisterHotKey should not take it.
    /// </summary>
    public sealed class HotkeyOption
    {
        public const uint NoRepeat = 0x4000;
        private const uint Alt = 0x1, Control = 0x2, Shift = 0x4;
        private const uint F9 = 0x78, F10 = 0x79, F11 = 0x7A, ScrollLock = 0x91;

        private HotkeyOption(string name, uint modifiers, uint key)
        {
            Name = name;
            Modifiers = modifiers;
            Key = key;
        }

        public string Name { get; }

        public uint Modifiers { get; }

        public uint Key { get; }

        public static IReadOnlyList<HotkeyOption> All { get; } = new[]
        {
            new HotkeyOption("F11", 0, F11),
            new HotkeyOption("F10", 0, F10),
            new HotkeyOption("F9", 0, F9),
            new HotkeyOption("Ctrl+F11", Control, F11),
            new HotkeyOption("Alt+F11", Alt, F11),
            new HotkeyOption("Shift+F11", Shift, F11),
            new HotkeyOption("Scroll Lock", 0, ScrollLock),
        };

        public static HotkeyOption Find(string name) => All.FirstOrDefault(option => option.Name == name) ?? All[0];

        public override string ToString() => Name;
    }

    public sealed class DurationOption
    {
        /// <summary>A capture without a set duration ends at the latest after this many seconds.</summary>
        public const int OpenEndedLimitSeconds = 30 * 60;

        private DurationOption(int seconds, string name)
        {
            Seconds = seconds;
            Name = name;
        }

        /// <summary>0 for a capture that runs until the hotkey is pressed again.</summary>
        public int Seconds { get; }

        public string Name { get; }

        public static IReadOnlyList<DurationOption> All { get; } = new[]
        {
            new DurationOption(10, "10 s"),
            new DurationOption(20, "20 s"),
            new DurationOption(30, "30 s"),
            new DurationOption(60, "60 s"),
            new DurationOption(120, "2 min"),
            new DurationOption(300, "5 min"),
            new DurationOption(0, "Until stopped (max. 30 min)"),
        };

        public static DurationOption Find(int seconds) => All.FirstOrDefault(option => option.Seconds == seconds) ?? All[1];

        public override string ToString() => Name;
    }
}
