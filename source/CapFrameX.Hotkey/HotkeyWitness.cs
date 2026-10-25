using CapFrameX.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace CapFrameX.Hotkey
{
    /// <summary>
    /// Watches the keyboard through raw input, independently of the global hook, and logs every
    /// hotkey press the hook missed together with what explains it.
    ///
    /// A keystroke the hook never receives leaves no trace: in the log it looks exactly like no
    /// keystroke at all. "The hotkey reacted late" could not be told apart from "the first
    /// presses were lost", and none of the hook fixes could be verified. Raw input runs beside
    /// the hook on a thread and window of its own. It is a posted message, so a stalled thread
    /// receives it late but never loses it, and Windows never takes the registration away
    /// (measured on Windows 11 build 26300: a hook-thread stall past the hook timeout lost the
    /// keystrokes made during it, raw input saw every one).
    ///
    /// The hook reports every key-down of a hotkey's trigger key with what it did with it, its
    /// heartbeats and its installations. <see cref="HotkeyWitnessLedger"/> pairs them with the raw
    /// keystrokes and gives every hotkey press a verdict. Anything but a dispatch is logged as a
    /// warning; a summary follows every 10 minutes with activity and at exit
    /// (<see cref="LogSessionSummary"/>).
    ///
    /// Cost: one WM_INPUT per keystroke, one report per trigger key-down, and a timer that runs
    /// only while a verdict is outstanding.
    /// </summary>
    internal static class HotkeyWitness
    {
        private const int EvaluationIntervalMs = 100;
        private const int SummaryIntervalMs = 10 * 60 * 1000;

        private static readonly object _threadLock = new object();
        private static readonly object _ledgerLock = new object();
        private static readonly ConcurrentQueue<HookKeyReport> _hookReports = new ConcurrentQueue<HookKeyReport>();
        private static readonly ConcurrentQueue<object> _hookThreadEvents = new ConcurrentQueue<object>();
        private static readonly List<WitnessedHotkeyPress> _verdicts = new List<WitnessedHotkeyPress>();
        private static readonly HotkeyWitnessLedger _ledger = new HotkeyWitnessLedger(HotkeyClock.NowMs());

        private static long _lastHeartbeatMs = HotkeyWitnessLedger.NoHeartbeat;
        private static volatile bool _active;
        private static Thread _thread;

        /// <summary>Witness thread only.</summary>
        private static WitnessWindow _window;

        /// <summary>Witness thread only.</summary>
        private static System.Windows.Forms.Timer _evaluationTimer;

        /// <summary>Witness thread only.</summary>
        private static bool _takeoverReported;

        /// <summary>Raised for every verdict, on the witness thread. Tests observe the witness through it.</summary>
        internal static event Action<WitnessedHotkeyPress> PressResolved;

        /// <summary>Whether raw input is registered and the hook's reports are taken in.</summary>
        internal static bool IsActive => _active;

        private static ILogger Logger => GlobalHotkeyHook.Logger;

        internal static void EnsureRunning()
        {
            lock (_threadLock)
            {
                if (_thread != null)
                    return;

                _thread = new Thread(ThreadProc)
                {
                    Name = "CX Hotkey Witness",
                    IsBackground = true,
                    // Above normal so the foreground process and the modifiers are read close to
                    // the keystroke. A late witness gets every verdict right all the same: raw
                    // input queues, and every keystroke carries its input time.
                    Priority = ThreadPriority.AboveNormal
                };
                _thread.SetApartmentState(ApartmentState.STA);
                _thread.Start();
            }
        }

        /// <summary>Hook thread: one key-down of a hotkey's trigger key and what the hook did with it.</summary>
        internal static void ReportHookKeyDown(int virtualKey, int eventTime, HookKeyOutcome outcome,
            string combination, int hookLatencyMs)
        {
            if (!_active)
                return;

            _hookReports.Enqueue(new HookKeyReport
            {
                VirtualKey = virtualKey,
                EventTime = eventTime,
                Outcome = outcome,
                Combination = combination,
                HookLatencyMs = hookLatencyMs,
                ReportedMs = HotkeyClock.NowMs()
            });
        }

        /// <summary>
        /// Hook thread: one heartbeat, stamped when it started. A stall is queued before the
        /// heartbeat is published, so a verdict the heartbeat allows already knows the stall.
        /// </summary>
        internal static void ReportHeartbeat(long heartbeatMs, long gapMs, bool rearmed)
        {
            if (_active && gapMs >= HookThreadStallDetector.StallThresholdMs)
            {
                _hookThreadEvents.Enqueue(new HookThreadStall
                {
                    EndMs = heartbeatMs,
                    GapMs = gapMs,
                    Rearmed = rearmed
                });
            }

            Volatile.Write(ref _lastHeartbeatMs, heartbeatMs);
        }

        /// <summary>Hook thread: a hook was installed.</summary>
        internal static void ReportRearm(string reason)
        {
            if (_active)
                _hookThreadEvents.Enqueue(new HookRearm { AtMs = HotkeyClock.NowMs(), Reason = reason });
        }

        /// <summary>
        /// Logs what the witness counted since the start, after a last pass over what is still
        /// outstanding. Called once at exit.
        /// </summary>
        internal static void LogSessionSummary()
        {
            if (!_active)
                return;

            WitnessedHotkeyPress[] verdicts;
            HotkeyWitnessCounters session;
            long nowMs = HotkeyClock.NowMs();
            lock (_ledgerLock)
            {
                long heartbeatMs = TakeInHookReports(GlobalHotkeyHook.Registrations, nowMs);
                _ledger.ResolveByHeartbeat(nowMs, heartbeatMs, _verdicts);
                verdicts = TakeVerdicts();
                session = _ledger.Session;
            }

            Publish(verdicts);
            LogSummary(session, nowMs, "session");
        }

        private static void ThreadProc()
        {
            try
            {
                _window = new WitnessWindow();
                if (!RawKeyboardInput.Register(_window.Handle, out int error))
                {
                    Logger.LogWarning(
                        "Hotkey witness: registering raw keyboard input failed (Win32 error {error}); hotkey presses the global hook misses are not reported.",
                        error);
                    _window.DestroyHandle();
                    return;
                }

                using (var evaluationTimer = new System.Windows.Forms.Timer { Interval = EvaluationIntervalMs })
                using (var summaryTimer = new System.Windows.Forms.Timer { Interval = SummaryIntervalMs })
                {
                    _evaluationTimer = evaluationTimer;
                    evaluationTimer.Tick += (_, __) => Guarded(OnEvaluationTick);
                    summaryTimer.Tick += (_, __) => Guarded(OnSummaryTick);
                    summaryTimer.Start();

                    _active = true;
                    Logger.LogInformation(
                        "Hotkey witness: raw keyboard input registered; every hotkey press the global hook misses is logged.");

                    Application.Run(new ApplicationContext());
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Hotkey witness thread terminated; hotkey presses the global hook misses are no longer reported.");
            }
            finally
            {
                _active = false;
            }
        }

        private static void OnRawInput(IntPtr rawInput)
        {
            if (!RawKeyboardInput.TryRead(rawInput, out var keystroke))
                return;

            long arrivalMs = HotkeyClock.NowMs();
            int eventTime = RawKeyboardInput.GetMessageTime();
            var registrations = GlobalHotkeyHook.Registrations;
            int virtualKey = HotkeyWitnessLedger.NormalizeVirtualKey(keystroke.VirtualKey, keystroke.MakeCode, keystroke.IsE0);

            // Only a trigger key-down needs the modifiers and the foreground process of the moment.
            bool triggerKeyDown = !keystroke.IsBreak && HotkeyWitnessLedger.IsTriggerKey(registrations, virtualKey);
            var key = new RawKeyEvent
            {
                VirtualKey = keystroke.VirtualKey,
                MakeCode = keystroke.MakeCode,
                IsBreak = keystroke.IsBreak,
                IsE0 = keystroke.IsE0,
                Injected = keystroke.Injected,
                EventTime = eventTime,
                ArrivalMs = arrivalMs,
                ProcessingDelayMs = HotkeyLatency.ElapsedMs(eventTime, Environment.TickCount),
                AsyncModifiers = triggerKeyDown ? ModifierState.Read() : HotkeyModifiers.None,
                ForegroundProcessId = triggerKeyDown ? RawKeyboardInput.GetForegroundProcessId() : 0
            };

            WitnessedHotkeyPress[] verdicts;
            lock (_ledgerLock)
            {
                long heartbeatMs = TakeInHookReports(registrations, arrivalMs);
                _ledger.OnRawKey(key, registrations, heartbeatMs, _verdicts);
                _ledger.ResolveByHeartbeat(arrivalMs, heartbeatMs, _verdicts);
                verdicts = TakeVerdicts();
                UpdateEvaluationTimer();
            }

            Publish(verdicts);
        }

        private static void OnEvaluationTick()
        {
            WitnessedHotkeyPress[] verdicts;
            lock (_ledgerLock)
            {
                long nowMs = HotkeyClock.NowMs();
                long heartbeatMs = TakeInHookReports(GlobalHotkeyHook.Registrations, nowMs);
                _ledger.ResolveByHeartbeat(nowMs, heartbeatMs, _verdicts);
                // Only here: a timer message is retrieved after every queued WM_INPUT, so a
                // report nothing claimed by now has no raw keystroke left to pair with.
                _ledger.Expire(nowMs);
                verdicts = TakeVerdicts();
                UpdateEvaluationTimer();
            }

            Publish(verdicts);
        }

        private static void OnSummaryTick()
        {
            OnEvaluationTick();

            long nowMs = HotkeyClock.NowMs();
            HotkeyWitnessCounters interval;
            lock (_ledgerLock)
            {
                interval = _ledger.StartNewInterval(nowMs);
            }

            if (interval.HasActivity)
                LogSummary(interval, nowMs, "interval");

            if (!_takeoverReported && !RawKeyboardInput.IsRegisteredTo(_window.Handle))
            {
                _takeoverReported = true;
                Logger.LogWarning(
                    "Hotkey witness: another window of this process took over the raw keyboard input; hotkey presses the global hook misses are no longer reported.");
            }
        }

        /// <summary>
        /// Reads the hook thread's heartbeat first and takes in what the hook thread queued
        /// after: everything the hook handled before that heartbeat is in the queues by then.
        /// </summary>
        private static long TakeInHookReports(HotkeyRegistration[] registrations, long nowMs)
        {
            long heartbeatMs = Volatile.Read(ref _lastHeartbeatMs);

            while (_hookThreadEvents.TryDequeue(out var hookThreadEvent))
            {
                if (hookThreadEvent is HookThreadStall stall)
                    _ledger.OnHookStall(stall);
                else if (hookThreadEvent is HookRearm rearm)
                    _ledger.OnHookRearm(rearm);
            }

            while (_hookReports.TryDequeue(out var report))
                _ledger.OnHookReport(report, registrations, nowMs, _verdicts);

            return heartbeatMs;
        }

        private static WitnessedHotkeyPress[] TakeVerdicts()
        {
            if (_verdicts.Count == 0)
                return Array.Empty<WitnessedHotkeyPress>();

            var verdicts = _verdicts.ToArray();
            _verdicts.Clear();
            return verdicts;
        }

        /// <summary>Witness thread only.</summary>
        private static void UpdateEvaluationTimer()
        {
            var timer = _evaluationTimer;
            if (timer != null)
                timer.Enabled = _ledger.HasPendingWork;
        }

        private static void Publish(WitnessedHotkeyPress[] verdicts)
        {
            foreach (var press in verdicts)
            {
                // A dispatched hotkey is already logged by the hook, with its latencies.
                if (press.Verdict != HotkeyWitnessVerdict.Dispatched)
                    LogVerdict(press);

                try
                {
                    PressResolved?.Invoke(press);
                }
                catch (Exception e)
                {
                    Logger.LogError(e, "Hotkey witness: a verdict observer failed.");
                }
            }
        }

        private static void LogVerdict(WitnessedHotkeyPress press)
        {
            string verdict;
            switch (press.Verdict)
            {
                case HotkeyWitnessVerdict.NotReceived:
                    verdict = press.UnresponsiveMs > 0
                        ? $"was pressed, but the global keyboard hook thread had not responded for {press.UnresponsiveMs} ms"
                        : "was pressed, but the global keyboard hook never received it";
                    break;
                case HotkeyWitnessVerdict.DiscardedAsRepeat:
                    verdict = "was pressed, but the global keyboard hook discarded it as a key repeat";
                    break;
                case HotkeyWitnessVerdict.ReadWithOtherModifiers:
                    verdict = $"was pressed, but the global keyboard hook read it as {press.HookCombination}, which is no hotkey";
                    break;
                default:
                    verdict = "was held down, and the global keyboard hook dispatched an auto-repeat as another press";
                    break;
            }

            Logger.LogWarning(
                "Hotkey witness: {combination} ({hotkeyActions}) {verdict}. Hook thread: heartbeat {heartbeatAgeMs} ms old at the press, longest stall around it: {stall}, last hook installation: {rearm}. Foreground: {foregroundProcess} (PID {foregroundPid}), injected input: {injected}.",
                press.Combination,
                press.Actions == null ? "no hotkey" : string.Join(", ", press.Actions),
                verdict,
                press.HeartbeatAgeMs,
                DescribeStall(press),
                DescribeRearm(press),
                DescribeProcess(press.ForegroundProcessId),
                press.ForegroundProcessId,
                press.Injected);
        }

        private static string DescribeStall(WitnessedHotkeyPress press)
        {
            var stall = press.Stall;
            if (stall == null)
                return "none";

            long endOffset = stall.EndMs - press.PressedMs;
            string end = endOffset >= 0
                ? $"ended {endOffset} ms after the press"
                : $"ended {-endOffset} ms before the press";
            return $"{stall.GapMs} ms, {end}, {(stall.Rearmed ? "hook re-armed" : "hook not re-armed")}";
        }

        private static string DescribeRearm(WitnessedHotkeyPress press)
            => press.LastRearm == null
                ? "none"
                : $"{press.PressedMs - press.LastRearm.AtMs} ms before the press ({press.LastRearm.Reason})";

        private static string DescribeProcess(int processId)
        {
            if (processId <= 0)
                return "none";

            return ProcessHelper.TryGetImagePath(processId, out var imagePath)
                ? Path.GetFileName(imagePath)
                : "unknown";
        }

        private static void LogSummary(HotkeyWitnessCounters counters, long nowMs, string period)
        {
            Logger.LogInformation(
                "Hotkey witness, {period} of {minutes} min: {presses} hotkey presses, {dispatched} dispatched (slowest {maxHookLatencyMs} ms from keypress to hook), {notReceived} never reached the hook, {discardedAsRepeat} discarded as key repeat, {otherModifiers} read with other modifiers; {autoRepeats} auto-repeats dispatched as presses, {unwitnessed} dispatches raw input did not show. Hook thread: {stalls} stalls, longest {longestStallMs} ms, {stallsWithoutRearm} without re-arm.",
                period,
                Math.Max(0, (nowMs - counters.SinceMs) / 60_000),
                counters.Presses,
                counters.Dispatched,
                counters.MaxHookLatencyMs,
                counters.NotReceived,
                counters.DiscardedAsRepeat,
                counters.ReadWithOtherModifiers,
                counters.DispatchedAutoRepeats,
                counters.Unwitnessed,
                counters.Stalls,
                counters.LongestStallMs,
                counters.StallsWithoutRearm);
        }

        /// <summary>
        /// Nothing may escape into the message loop: WinForms would report it with a dialog on
        /// this background thread.
        /// </summary>
        private static void Guarded(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Hotkey witness callback failed.");
            }
        }

        /// <summary>
        /// The message-only window raw input is delivered to. Message-only windows receive raw
        /// input with RIDEV_INPUTSINK (verified on Windows 11 build 26300) and no broadcasts.
        /// </summary>
        private sealed class WitnessWindow : NativeWindow
        {
            private static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);

            internal WitnessWindow()
            {
                CreateHandle(new CreateParams { Caption = "CX Hotkey Witness", Parent = HWND_MESSAGE });
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == RawKeyboardInput.WM_INPUT)
                {
                    try
                    {
                        OnRawInput(m.LParam);
                    }
                    catch (Exception e)
                    {
                        Logger.LogError(e, "Hotkey witness callback failed.");
                    }
                }

                // DefWindowProc releases the raw input buffer.
                base.WndProc(ref m);
            }
        }
    }
}
