using System;
using System.Runtime.InteropServices;

namespace CapFrameX.Hotkey
{
    /// <summary>One keystroke as WM_INPUT reports it.</summary>
    internal readonly struct RawKeystroke
    {
        internal int VirtualKey { get; init; }

        internal int MakeCode { get; init; }

        internal bool IsBreak { get; init; }

        internal bool IsE0 { get; init; }

        /// <summary>No device: SendInput and other synthesized input.</summary>
        internal bool Injected { get; init; }
    }

    /// <summary>
    /// The Win32 side of the hotkey witness: the raw keyboard input registration and reading one
    /// keystroke from WM_INPUT.
    ///
    /// Raw input is a posted message, not a hook. Nothing times it out and Windows never takes
    /// the registration away, so it keeps seeing keystrokes the global hook misses. A keystroke
    /// another program's hook swallows reaches neither of them: Windows calls the hooks first.
    /// </summary>
    internal static class RawKeyboardInput
    {
        internal const int WM_INPUT = 0x00FF;

        private const uint RID_INPUT = 0x10000003;
        private const uint RIM_TYPEKEYBOARD = 1;
        private const uint RIDEV_INPUTSINK = 0x00000100;
        private const ushort RI_KEY_BREAK = 0x01;
        private const ushort RI_KEY_E0 = 0x02;
        private const ushort HID_USAGE_PAGE_GENERIC = 0x01;
        private const ushort HID_USAGE_GENERIC_KEYBOARD = 0x06;

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICE
        {
            public ushort UsagePage;
            public ushort Usage;
            public uint Flags;
            public IntPtr Target;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTHEADER
        {
            public uint Type;
            public uint Size;
            public IntPtr Device;
            public IntPtr WParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWKEYBOARD
        {
            public ushort MakeCode;
            public ushort Flags;
            public ushort Reserved;
            public ushort VKey;
            public uint Message;
            public uint ExtraInformation;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTKEYBOARD
        {
            public RAWINPUTHEADER Header;
            public RAWKEYBOARD Keyboard;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] devices, uint count, uint size);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetRegisteredRawInputDevices([Out] RAWINPUTDEVICE[] devices, ref uint count, uint size);

        [DllImport("user32.dll")]
        private static extern uint GetRawInputData(IntPtr rawInput, uint command, out RAWINPUTKEYBOARD data,
            ref uint size, uint headerSize);

        /// <summary>The time stamp of the message being processed; for WM_INPUT the keystroke's input time.</summary>
        [DllImport("user32.dll")]
        internal static extern int GetMessageTime();

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        /// <summary>
        /// Registers <paramref name="window"/> for every keystroke of every keyboard, also while
        /// another process has the foreground (RIDEV_INPUTSINK). A process can register a single
        /// window per device type; nothing else in CapFrameX registers the keyboard.
        /// </summary>
        internal static bool Register(IntPtr window, out int error)
        {
            var devices = new[]
            {
                new RAWINPUTDEVICE
                {
                    UsagePage = HID_USAGE_PAGE_GENERIC,
                    Usage = HID_USAGE_GENERIC_KEYBOARD,
                    Flags = RIDEV_INPUTSINK,
                    Target = window
                }
            };

            bool registered = RegisterRawInputDevices(devices, (uint)devices.Length,
                (uint)Marshal.SizeOf<RAWINPUTDEVICE>());
            error = registered ? 0 : Marshal.GetLastWin32Error();
            return registered;
        }

        /// <summary>
        /// Whether the keyboard still goes to <paramref name="window"/>. Another registration of
        /// the keyboard in this process would silently take it over. An unreadable list counts as
        /// registered: this check only exists to report a takeover it can prove.
        /// </summary>
        internal static bool IsRegisteredTo(IntPtr window)
        {
            uint size = (uint)Marshal.SizeOf<RAWINPUTDEVICE>();
            uint count = 0;
            GetRegisteredRawInputDevices(null, ref count, size);
            if (count == 0)
                return false;

            var devices = new RAWINPUTDEVICE[count];
            if (GetRegisteredRawInputDevices(devices, ref count, size) == uint.MaxValue)
                return true;

            foreach (var device in devices)
            {
                if (device.UsagePage == HID_USAGE_PAGE_GENERIC && device.Usage == HID_USAGE_GENERIC_KEYBOARD)
                    return device.Target == window;
            }

            return false;
        }

        internal static bool TryRead(IntPtr rawInput, out RawKeystroke keystroke)
        {
            keystroke = default;

            uint size = (uint)Marshal.SizeOf<RAWINPUTKEYBOARD>();
            if (GetRawInputData(rawInput, RID_INPUT, out var data, ref size,
                    (uint)Marshal.SizeOf<RAWINPUTHEADER>()) == uint.MaxValue ||
                data.Header.Type != RIM_TYPEKEYBOARD)
                return false;

            keystroke = new RawKeystroke
            {
                VirtualKey = data.Keyboard.VKey,
                MakeCode = data.Keyboard.MakeCode,
                IsBreak = (data.Keyboard.Flags & RI_KEY_BREAK) != 0,
                IsE0 = (data.Keyboard.Flags & RI_KEY_E0) != 0,
                Injected = data.Header.Device == IntPtr.Zero
            };
            return true;
        }

        /// <summary>The process owning the foreground window; 0 when there is none.</summary>
        internal static int GetForegroundProcessId()
        {
            var window = GetForegroundWindow();
            if (window == IntPtr.Zero)
                return 0;

            GetWindowThreadProcessId(window, out uint processId);
            return (int)processId;
        }
    }
}
