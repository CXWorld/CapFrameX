using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace CapFrameX.Data.CaptureTarget
{
    /// <summary>
    /// Follows the foreground window through a WinEvent hook while it is active, so nothing is
    /// polled: Windows calls back on every foreground change only. The capture view activates it
    /// while the process list has entries. Activation reads the current foreground once; the
    /// hook delivers its events on the activating thread, which needs a message loop.
    /// </summary>
    public sealed class ForegroundProcessTracker : IDisposable
    {
        // A process that left the foreground long ago and many switches back no longer decides.
        private const int MaximumRemembered = 64;

        private readonly Func<int> _readForegroundProcess;
        private readonly Func<Action<int>, IDisposable> _subscribe;
        private readonly Func<long> _milliseconds;
        // When each process lost the foreground; the current one is _foreground.
        private readonly Dictionary<int, long> _leftAt = new Dictionary<int, long>();
        private IDisposable _hook;
        private int _foreground;

        public ForegroundProcessTracker()
            : this(ForegroundWindow.ReadProcess, ForegroundWindow.Subscribe, () => Environment.TickCount64)
        {
        }

        internal ForegroundProcessTracker(Func<int> readForegroundProcess,
            Func<Action<int>, IDisposable> subscribe, Func<long> milliseconds)
        {
            _readForegroundProcess = readForegroundProcess;
            _subscribe = subscribe;
            _milliseconds = milliseconds;
        }

        public bool IsActive => _hook != null;

        /// <summary>
        /// The process that owns the foreground window; 0 while inactive or unknown.
        /// </summary>
        public int ForegroundProcessId => _foreground;

        /// <summary>
        /// Installs the hook or removes it. An inactive tracker forgets everything.
        /// </summary>
        public void SetActive(bool active)
        {
            if (active == IsActive)
                return;

            if (!active)
            {
                _hook.Dispose();
                _hook = null;
                _foreground = 0;
                _leftAt.Clear();
                return;
            }

            try
            {
                _hook = _subscribe(OnForegroundChanged);
            }
            catch (Exception)
            {
                return;
            }

            int current;
            try
            {
                current = _readForegroundProcess();
            }
            catch (Exception)
            {
                current = 0;
            }
            OnForegroundChanged(current);
        }

        /// <summary>
        /// 0 for the process in the foreground, else the seconds since it left the foreground;
        /// null when it was not in the foreground while the tracker was active.
        /// </summary>
        public double? SecondsSinceForeground(int processId)
        {
            if (processId == 0)
                return null;
            if (processId == _foreground)
                return 0;
            return _leftAt.TryGetValue(processId, out long leftAt)
                ? (_milliseconds() - leftAt) / 1000.0
                : (double?)null;
        }

        public void Dispose() => SetActive(false);

        private void OnForegroundChanged(int processId)
        {
            // An event that was queued before the hook was removed.
            if (_hook == null || processId == _foreground)
                return;

            if (_foreground != 0)
                _leftAt[_foreground] = _milliseconds();
            _leftAt.Remove(processId);
            _foreground = processId;

            if (_leftAt.Count > MaximumRemembered)
                _leftAt.Remove(_leftAt.OrderBy(entry => entry.Value).First().Key);
        }
    }

    internal static class ForegroundWindow
    {
        private const uint EventSystemForeground = 0x0003;
        private const uint WineventOutOfContext = 0x0000;

        private delegate void WinEventProc(IntPtr hook, uint eventType, IntPtr window, int objectId,
            int childId, uint eventThread, uint eventTime);

        private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr module,
            WinEventProc callback, uint processId, uint threadId, uint flags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWinEvent(IntPtr hook);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr window, StringBuilder className, int maxCount);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr parameter);

        public static int ReadProcess() => ProcessOf(GetForegroundWindow());

        public static IDisposable Subscribe(Action<int> onForegroundChanged) => new Hook(onForegroundChanged);

        /// <summary>
        /// The process that draws into the window. A UWP app draws into a CoreWindow of its own
        /// process inside a frame window of ApplicationFrameHost (class ApplicationFrameWindow);
        /// only such a frame has its child windows searched.
        /// </summary>
        private static int ProcessOf(IntPtr window)
        {
            if (window == IntPtr.Zero)
                return 0;

            GetWindowThreadProcessId(window, out uint owner);
            if (!IsApplicationFrame(window))
                return (int)owner;

            uint app = 0;
            EnumWindowsProc callback = (child, _) =>
            {
                GetWindowThreadProcessId(child, out uint processId);
                if (processId == 0 || processId == owner)
                    return true;
                app = processId;
                return false;
            };
            EnumChildWindows(window, callback, IntPtr.Zero);
            GC.KeepAlive(callback);

            return (int)(app != 0 ? app : owner);
        }

        private static bool IsApplicationFrame(IntPtr window)
        {
            var className = new StringBuilder(32);
            return GetClassName(window, className, className.Capacity) > 0 &&
                className.ToString() == "ApplicationFrameWindow";
        }

        private sealed class Hook : IDisposable
        {
            // Referenced for the hook's lifetime: the native side holds only a function pointer.
            private readonly WinEventProc _callback;
            private IntPtr _handle;

            public Hook(Action<int> onForegroundChanged)
            {
                _callback = (hook, eventType, window, objectId, childId, eventThread, eventTime)
                    => onForegroundChanged(ProcessOf(window));
                _handle = SetWinEventHook(EventSystemForeground, EventSystemForeground, IntPtr.Zero,
                    _callback, 0, 0, WineventOutOfContext);
            }

            public void Dispose()
            {
                if (_handle == IntPtr.Zero)
                    return;
                UnhookWinEvent(_handle);
                _handle = IntPtr.Zero;
            }
        }
    }
}
