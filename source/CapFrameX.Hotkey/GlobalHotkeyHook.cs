using Gma.System.MouseKeyHook;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CapFrameX.Hotkey
{
    /// <summary>
    /// Owns the single global keyboard hook every CapFrameX hotkey is matched against.
    ///
    /// Three properties matter here, and each one exists because the previous design violated it:
    ///
    /// 1. The hook procedure must return fast. Windows measures it against
    ///    <c>HKCU\Control Panel\Desktop\LowLevelHooksTimeout</c> (300 ms by default, capped at
    ///    1000 ms since Windows 10 1709) and on Windows 7 and later a hook that exceeds it is
    ///    "silently removed without being called. There is no way for the application to know
    ///    whether the hook is removed"
    ///    (https://learn.microsoft.com/windows/win32/winmsg/lowlevelkeyboardproc). Registered
    ///    actions are therefore never executed here — they are posted to the
    ///    <see cref="SynchronizationContext"/> that registered them. Nothing that can block
    ///    (RTSS startup, audio device init, the synchronous Serilog file sink) may run on this
    ///    thread, which is also why the hot path does not log.
    ///
    /// 2. The hook lives on a dedicated thread with its own message loop instead of on the WPF
    ///    UI thread. That is what the documentation recommends ("run the hooks on a dedicated
    ///    thread that passes the work off to a worker thread and then immediately returns"), and
    ///    it keeps UI stalls — chart rendering, GC pauses, blocking dispatcher work — out of the
    ///    timeout budget. The thread runs at the highest priority: the timeout above is wall
    ///    time, so a game saturating every core can starve a normal-priority thread past it
    ///    without the thread doing anything wrong.
    ///
    /// 3. One hook, installed once. Changing a hotkey swaps an immutable registration snapshot
    ///    instead of disposing and re-installing every hook, so a configuration change can no
    ///    longer drop keystrokes in the gap between unhook and rehook.
    ///
    /// Because a removed hook cannot be detected, the hook is re-installed on a slow timer and,
    /// more importantly, immediately after any stall of the hook thread that was long enough to
    /// have tripped the timeout (<see cref="HookThreadStallDetector"/>). The replacement is
    /// installed before the old one is disposed so no keystroke falls through a gap; the
    /// duplicate events one keystroke then produces are absorbed by <see cref="KeyRepeatFilter"/>.
    ///
    /// Every dispatched hotkey logs how long it waited: from the keypress (the kernel stamp in
    /// the hook data) to the hook thread, and from the hook thread to the registered context
    /// running the action. A delay in the first number is this thread or Windows; a delay in
    /// the second is the receiving thread — the WPF dispatcher for every current caller.
    /// </summary>
    internal static class GlobalHotkeyHook
    {
        /// <summary>
        /// A silently removed hook is indistinguishable from an idle one. The stall detector
        /// catches the common cause right away; this slow fallback covers whatever it cannot
        /// see and costs a single SetWindowsHookEx/UnhookWindowsHookEx pair.
        /// </summary>
        private const int RearmIntervalMs = 60_000;

        /// <summary>
        /// Heartbeat cadence of the stall detector. The measured gap always includes one
        /// interval, which <see cref="HookThreadStallDetector.StallThresholdMs"/> allows for.
        /// </summary>
        private const int HeartbeatIntervalMs = 50;

        /// <summary>
        /// Hotkeys slower than this get a warning instead of the routine info line. Human
        /// perception of "pressed and nothing happened" starts around here.
        /// </summary>
        private const int LatencyWarningMs = 100;

        private static readonly object _threadLock = new object();
        private static readonly KeyRepeatFilter _repeatFilter = new KeyRepeatFilter();
        private static readonly HookThreadStallDetector _stallDetector = new HookThreadStallDetector();

        private static volatile HotkeyRegistration[] _registrations = Array.Empty<HotkeyRegistration>();
        private static Thread _hookThread;

        /// <summary>Touched on the hook thread only.</summary>
        private static IKeyboardMouseEvents _hook;

        private static ILogger _logger = NullLogger.Instance;

        internal static ILogger Logger
        {
            get => _logger;
            set => _logger = value ?? NullLogger.Instance;
        }

        /// <summary>
        /// Replaces what the hook matches against. Cheap and lock-free on the hook side: the
        /// hook itself is untouched, only the snapshot reference is swapped.
        /// </summary>
        internal static void SetRegistrations(HotkeyRegistration[] registrations)
        {
            _registrations = registrations ?? Array.Empty<HotkeyRegistration>();
            EnsureHookThreadRunning();
        }

        private static void EnsureHookThreadRunning()
        {
            lock (_threadLock)
            {
                if (_hookThread != null)
                    return;

                _hookThread = new Thread(HookThreadProc)
                {
                    Name = "CX Global Hotkey Hook",
                    IsBackground = true,
                    // See the class remarks: the hook timeout is wall time, and this thread does
                    // almost nothing per keystroke, so the priority buys responsiveness under a
                    // fully loaded CPU without taking anything from the game.
                    Priority = ThreadPriority.Highest
                };
                _hookThread.SetApartmentState(ApartmentState.STA);
                _hookThread.Start();
            }
        }

        private static void HookThreadProc()
        {
            try
            {
                InstallHook();

                using (var rearmTimer = new System.Windows.Forms.Timer { Interval = RearmIntervalMs })
                using (var heartbeatTimer = new System.Windows.Forms.Timer { Interval = HeartbeatIntervalMs })
                {
                    rearmTimer.Tick += (_, __) =>
                    {
                        InstallHook();
                        _stallDetector.NotifyRearmed(Environment.TickCount64);
                    };
                    heartbeatTimer.Tick += (_, __) => OnHeartbeat();
                    rearmTimer.Start();
                    heartbeatTimer.Start();

                    // A WH_KEYBOARD_LL hook is called by sending a message to the thread that
                    // installed it, so this thread has to pump messages for the whole lifetime of
                    // the hook. Run() returns only at process exit (background thread).
                    Application.Run(new ApplicationContext());
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Global hotkey hook thread terminated. Hotkeys stay inactive until CapFrameX is restarted.");
            }
        }

        /// <summary>
        /// The heartbeat is dispatched through the same message loop as the hook procedure, so a
        /// late heartbeat means the hook procedure would have been late by the same amount — and
        /// past the timeout, Windows has already dropped the hook.
        /// </summary>
        private static void OnHeartbeat()
        {
            var stallMs = _stallDetector.Heartbeat(Environment.TickCount64);
            if (stallMs == 0)
                return;

            InstallHook();
            // Logged after the re-arm so the file write cannot delay it. This line is the
            // evidence for "the hotkey did nothing for a while": every stall long enough to
            // have cost the hook shows up here with its length.
            Logger.LogWarning(
                "Global keyboard hook thread was unresponsive for {stallMs} ms; the hook was re-armed in case Windows dropped it (LowLevelHooksTimeout).",
                stallMs);
        }

        /// <summary>
        /// Installs a fresh hook and retires the previous one. Hook thread only — MouseKeyHook
        /// creates the underlying listener lazily when the first handler is attached, so the
        /// subscription decides which thread the hook belongs to.
        /// </summary>
        private static void InstallHook()
        {
            var previous = _hook;

            try
            {
                var hook = Hook.GlobalEvents();
                hook.KeyDown += OnKeyDown;
                hook.KeyUp += OnKeyUp;
                _hook = hook;
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Failed to install the global keyboard hook.");
                return;
            }

            // Retire the old hook only once its replacement is in place. Unhooking first would
            // leave a window in which no hook sees the keyboard at all.
            if (previous != null)
            {
                try
                {
                    previous.Dispose();
                }
                catch (Exception e)
                {
                    Logger.LogWarning(e, "Failed to dispose the previous global keyboard hook.");
                }
            }

            // Kept at debug for the re-arm case: this runs on the hook thread, where the
            // synchronous Serilog file sink would otherwise delay message dispatch.
            Logger.Log(previous == null ? LogLevel.Information : LogLevel.Debug,
                "Global keyboard hook {state}.", previous == null ? "installed" : "re-armed");
        }

        private static void OnKeyUp(object sender, KeyEventArgs e)
        {
            try
            {
                _repeatFilter.OnKeyUp(e.KeyCode);
            }
            catch
            {
                // Never let an exception escape into the native hook procedure.
            }
        }

        private static void OnKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                // Taken first so the measurement covers everything this thread does. The
                // keypress stamp is the kernel one (KBDLLHOOKSTRUCT.time); it predates any
                // scheduling delay of this thread and is the only honest reference for when
                // the user actually pressed the key.
                var hookTick = Environment.TickCount;
                var hookStamp = Stopwatch.GetTimestamp();
                var pressedTick = e is KeyEventArgsExt ext && ext.Timestamp != 0 ? ext.Timestamp : hookTick;

                // Windows repeats KeyDown while a key stays pressed. Without this, holding a
                // toggle hotkey a moment too long flips it an even number of times and looks
                // exactly like "the hotkey did not come through".
                if (!_repeatFilter.ShouldHandleKeyDown(e.KeyCode))
                    return;

                var registrations = _registrations;
                if (registrations.Length == 0)
                    return;

                // The stored trigger key is canonicalized to the name this framework's Keys enum
                // reports, which is what makes this comparison hold. See HotkeyKeyName.
                var triggerKey = e.KeyCode.ToString();
                string combination = null;

                foreach (var registration in registrations)
                {
                    if (registration.TriggerKey != triggerKey)
                        continue;

                    combination = combination ?? ModifierState.BuildCombination(triggerKey);

                    if (registration.Combinations.TryGetValue(combination, out var action))
                        Dispatch(registration, combination, action, pressedTick, hookTick, hookStamp);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Global keyboard hook callback failed.");
            }
        }

        /// <summary>
        /// Hands the action to the context it was registered from — the WPF dispatcher for every
        /// current caller — and returns immediately. Posting rather than invoking is the whole
        /// point: it is what keeps the hook procedure inside its timeout budget no matter how
        /// long the action itself takes.
        /// </summary>
        private static void Dispatch(HotkeyRegistration registration, string combination, Action action,
            int pressedTick, int hookTick, long hookStamp)
        {
            void Run()
            {
                try
                {
                    var hookLatencyMs = HotkeyLatency.ElapsedMs(pressedTick, hookTick);
                    var dispatchLatencyMs = (int)((Stopwatch.GetTimestamp() - hookStamp) * 1000 / Stopwatch.Frequency);

                    if (hookLatencyMs >= LatencyWarningMs || dispatchLatencyMs >= LatencyWarningMs)
                    {
                        Logger.LogWarning(
                            "Hotkey {combination} ({hotkeyAction}) was delayed: {hookLatencyMs} ms from the keypress to the hook thread, {dispatchLatencyMs} ms from the hook thread to the action. The first is the hook thread or Windows, the second is the thread the action runs on (the UI thread).",
                            combination, registration.HotkeyAction, hookLatencyMs, dispatchLatencyMs);
                    }
                    else
                    {
                        Logger.LogInformation(
                            "Hotkey {combination} triggered ({hotkeyAction}); {hookLatencyMs} ms keypress->hook, {dispatchLatencyMs} ms hook->action.",
                            combination, registration.HotkeyAction, hookLatencyMs, dispatchLatencyMs);
                    }

                    action();
                }
                catch (Exception e)
                {
                    Logger.LogError(e, "Hotkey action {hotkeyAction} failed.", registration.HotkeyAction);
                }
            }

            if (registration.SynchronizationContext != null)
                registration.SynchronizationContext.Post(_ => Run(), null);
            else
                Task.Run(Run);
        }
    }

    /// <summary>
    /// One hotkey as the hook matches it: the trigger key, every accepted modifier combination
    /// for it, and the context its action has to run on.
    /// </summary>
    internal sealed class HotkeyRegistration
    {
        internal HotkeyRegistration(HotkeyAction hotkeyAction, string triggerKey,
            IReadOnlyDictionary<string, Action> combinations, SynchronizationContext synchronizationContext)
        {
            HotkeyAction = hotkeyAction;
            TriggerKey = triggerKey;
            Combinations = combinations;
            SynchronizationContext = synchronizationContext;
        }

        internal HotkeyAction HotkeyAction { get; }

        internal string TriggerKey { get; }

        internal IReadOnlyDictionary<string, Action> Combinations { get; }

        internal SynchronizationContext SynchronizationContext { get; }
    }
}
