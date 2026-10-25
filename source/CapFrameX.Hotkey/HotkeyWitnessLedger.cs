using System;
using System.Collections.Generic;

namespace CapFrameX.Hotkey
{
    /// <summary>How the global hook handled one key-down of a hotkey's trigger key.</summary>
    internal enum HookKeyOutcome
    {
        /// <summary>A hotkey combination matched and its action was posted.</summary>
        Dispatched,

        /// <summary>The key-repeat filter took the key-down for an auto-repeat.</summary>
        RepeatFiltered,

        /// <summary>The modifiers the hook read form no hotkey combination.</summary>
        NoMatch
    }

    /// <summary>What became of one hotkey press, as the witness established it.</summary>
    internal enum HotkeyWitnessVerdict
    {
        /// <summary>The hook received the press and dispatched its hotkey.</summary>
        Dispatched,

        /// <summary>The hook never received the press.</summary>
        NotReceived,

        /// <summary>The hook received the press, but its key-repeat filter discarded it.</summary>
        DiscardedAsRepeat,

        /// <summary>The hook received the press, but read modifiers that form no hotkey.</summary>
        ReadWithOtherModifiers,

        /// <summary>The key was held down, and the hook dispatched an auto-repeat as another press.</summary>
        DispatchedAutoRepeat
    }

    /// <summary>One keyboard event as raw input delivered it to the witness.</summary>
    internal readonly struct RawKeyEvent
    {
        /// <summary>The legacy virtual-key code; raw input reports Shift, Control and Alt generically.</summary>
        internal int VirtualKey { get; init; }

        internal int MakeCode { get; init; }

        internal bool IsBreak { get; init; }

        internal bool IsE0 { get; init; }

        /// <summary>The event has no device: it was synthesized, by SendInput for instance.</summary>
        internal bool Injected { get; init; }

        /// <summary>The input time stamp (GetTickCount based). The hook gets the same value for the same keystroke.</summary>
        internal int EventTime { get; init; }

        /// <summary>When the witness processed the event, on the <see cref="HotkeyClock"/>.</summary>
        internal long ArrivalMs { get; init; }

        /// <summary>How long the event waited for the witness thread.</summary>
        internal int ProcessingDelayMs { get; init; }

        /// <summary>The modifiers GetAsyncKeyState reported when the witness processed a trigger key.</summary>
        internal HotkeyModifiers AsyncModifiers { get; init; }

        /// <summary>The process owning the foreground window when the witness processed a trigger key.</summary>
        internal int ForegroundProcessId { get; init; }
    }

    /// <summary>
    /// One key-down of a hotkey's trigger key as the global hook handled it, reported from the
    /// hook thread.
    /// </summary>
    internal sealed class HookKeyReport
    {
        internal int VirtualKey { get; init; }

        /// <summary><c>KBDLLHOOKSTRUCT.time</c>; raw input reports the same value for the same keystroke.</summary>
        internal int EventTime { get; init; }

        internal HookKeyOutcome Outcome { get; init; }

        /// <summary>The combination the hook read; null for a key-down the repeat filter discarded.</summary>
        internal string Combination { get; init; }

        /// <summary>From the keypress to the hook callback.</summary>
        internal int HookLatencyMs { get; init; }

        /// <summary>When the hook handled the key-down, on the <see cref="HotkeyClock"/>.</summary>
        internal long ReportedMs { get; init; }
    }

    /// <summary>A gap between two heartbeats of the hook thread long enough to cost the hook a keystroke.</summary>
    internal sealed class HookThreadStall
    {
        /// <summary>The heartbeat that ended the stall, on the <see cref="HotkeyClock"/>.</summary>
        internal long EndMs { get; init; }

        internal long GapMs { get; init; }

        /// <summary>Whether the stall detector installed a new hook for it; it does so at most once per 2 s.</summary>
        internal bool Rearmed { get; init; }
    }

    /// <summary>One installation of the global hook: at startup, periodically, after a stall or by a test.</summary>
    internal sealed class HookRearm
    {
        internal long AtMs { get; init; }

        internal string Reason { get; init; }
    }

    /// <summary>The witness' verdict on one hotkey press, with what explains a miss.</summary>
    internal sealed class WitnessedHotkeyPress
    {
        internal HotkeyWitnessVerdict Verdict { get; init; }

        internal int VirtualKey { get; init; }

        internal int EventTime { get; init; }

        /// <summary>The combination as the witness read it; for a dispatch the one the hook matched.</summary>
        internal string Combination { get; init; }

        /// <summary>The combination the hook read; null when it never received the press or discarded it.</summary>
        internal string HookCombination { get; init; }

        internal IReadOnlyList<HotkeyAction> Actions { get; init; }

        internal bool Injected { get; init; }

        internal int ForegroundProcessId { get; init; }

        /// <summary>When the key went down, on the <see cref="HotkeyClock"/>.</summary>
        internal long PressedMs { get; init; }

        internal long ResolvedMs { get; init; }

        /// <summary>From the keypress to the hook callback; -1 when the hook never received the press.</summary>
        internal int HookLatencyMs { get; init; }

        /// <summary>How old the hook thread's last heartbeat was when the key went down; -1 before the first one.</summary>
        internal long HeartbeatAgeMs { get; init; }

        /// <summary>
        /// For a press the hook thread never got to: how long the thread had been unresponsive
        /// when the verdict was given; 0 when it was responsive.
        /// </summary>
        internal long UnresponsiveMs { get; init; }

        /// <summary>
        /// The longest hook-thread stall from <see cref="HotkeyWitnessLedger.StallContextMs"/>
        /// before the press until the verdict; null if there was none.
        /// </summary>
        internal HookThreadStall Stall { get; init; }

        /// <summary>The last installation of the hook before the press; null if there was none.</summary>
        internal HookRearm LastRearm { get; init; }
    }

    /// <summary>What the witness counted over one period: a summary interval or the session.</summary>
    internal sealed class HotkeyWitnessCounters
    {
        internal HotkeyWitnessCounters(long sinceMs)
        {
            SinceMs = sinceMs;
        }

        internal long SinceMs { get; }

        /// <summary>Hotkey presses with a verdict; auto-repeats are not presses.</summary>
        internal int Presses { get; private set; }

        internal int Dispatched { get; private set; }

        internal int NotReceived { get; private set; }

        internal int DiscardedAsRepeat { get; private set; }

        internal int ReadWithOtherModifiers { get; private set; }

        internal int DispatchedAutoRepeats { get; private set; }

        /// <summary>Hotkeys the hook dispatched for a keystroke raw input never delivered: the witness was blind to it.</summary>
        internal int Unwitnessed { get; private set; }

        /// <summary>The slowest dispatched press, from the keypress to the hook.</summary>
        internal int MaxHookLatencyMs { get; private set; }

        internal int Stalls { get; private set; }

        internal int StallsWithoutRearm { get; private set; }

        internal long LongestStallMs { get; private set; }

        internal bool HasActivity => Presses + DispatchedAutoRepeats + Unwitnessed + Stalls > 0;

        internal void Record(WitnessedHotkeyPress press)
        {
            switch (press.Verdict)
            {
                case HotkeyWitnessVerdict.Dispatched:
                    Presses++;
                    Dispatched++;
                    MaxHookLatencyMs = Math.Max(MaxHookLatencyMs, press.HookLatencyMs);
                    break;
                case HotkeyWitnessVerdict.NotReceived:
                    Presses++;
                    NotReceived++;
                    break;
                case HotkeyWitnessVerdict.DiscardedAsRepeat:
                    Presses++;
                    DiscardedAsRepeat++;
                    break;
                case HotkeyWitnessVerdict.ReadWithOtherModifiers:
                    Presses++;
                    ReadWithOtherModifiers++;
                    break;
                case HotkeyWitnessVerdict.DispatchedAutoRepeat:
                    DispatchedAutoRepeats++;
                    break;
            }
        }

        internal void RecordUnwitnessed() => Unwitnessed++;

        internal void RecordStall(HookThreadStall stall)
        {
            Stalls++;
            if (!stall.Rearmed)
                StallsWithoutRearm++;
            LongestStallMs = Math.Max(LongestStallMs, stall.GapMs);
        }
    }

    /// <summary>
    /// Decides for every hotkey press raw input reports whether the global hook received and
    /// dispatched it. Single-threaded: <see cref="HotkeyWitness"/> feeds it from its own thread,
    /// and every input carries its time, so the rules can be tested as they are.
    ///
    /// The raw input of a keystroke and the hook's report of the same keystroke carry the same
    /// input time stamp and virtual key, which pairs them exactly. Either can come first: Windows
    /// calls the hook before it generates the raw input, but a stalled hook thread reports later.
    ///
    /// A press without a report is only declared lost once the hook thread has demonstrably
    /// caught up: a heartbeat that started after the witness saw the press. Windows hands a
    /// keystroke to the hook before it generates its raw input, and the hook thread handles every
    /// pending hook call before its message loop gets to the heartbeat timer, so a report that
    /// has not arrived by then never will.
    /// </summary>
    internal sealed class HotkeyWitnessLedger
    {
        /// <summary>The heartbeat value before the hook thread's first heartbeat.</summary>
        internal const long NoHeartbeat = long.MinValue;

        /// <summary>
        /// A make code of a key that is already down is an auto-repeat unless it comes this long
        /// after the previous one. Windows starts repeating after at most 1 s (keyboard delay 3),
        /// so a key-up the witness never saw (released on the secure desktop) costs one press at
        /// most.
        /// </summary>
        internal const int KeyRepeatExpiryMs = 1500;

        /// <summary>
        /// The witness tracks the modifiers from the raw keystrokes, in order, because that is
        /// what the hook can get wrong: a late hook call reads the modifier state of its own
        /// moment. A key-up the witness never saw (Ctrl+Alt+Del) would leave a modifier down for
        /// good, though, so an event processed within this time is checked against
        /// GetAsyncKeyState, which then still describes the same moment.
        /// </summary>
        internal const int FreshEventMs = 50;

        /// <summary>A press whose hook thread does not respond for this long is reported anyway.</summary>
        internal const long MaxWaitMs = 30_000;

        /// <summary>
        /// A hook report nothing claimed is dropped after this long. <see cref="Expire"/> runs on
        /// the witness timer only: the timer fires after every queued raw input message was
        /// handled, so the raw keystroke the report could still pair with does not exist.
        /// </summary>
        internal const long ReportRetentionMs = 1000;

        /// <summary>How long a raw auto-repeat waits for the hook's report of the same keystroke.</summary>
        internal const long RepeatRetentionMs = 2000;

        /// <summary>A verdict names the longest hook-thread stall from this long before the press until the verdict.</summary>
        internal const long StallContextMs = 10_000;

        private const int HistoryLength = 16;

        private const int VK_SHIFT = 0x10;
        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12;
        private const int VK_LSHIFT = 0xA0;
        private const int VK_RSHIFT = 0xA1;
        private const int VK_LCONTROL = 0xA2;
        private const int VK_RCONTROL = 0xA3;
        private const int VK_LMENU = 0xA4;
        private const int VK_RMENU = 0xA5;
        private const int RightShiftMakeCode = 0x36;

        private readonly bool[] _down = new bool[256];
        private readonly int[] _lastMakeTime = new int[256];
        private readonly List<PendingPress> _pending = new List<PendingPress>();
        private readonly List<HookKeyReport> _unmatchedReports = new List<HookKeyReport>();
        private readonly List<RawRepeat> _recentRepeats = new List<RawRepeat>();
        private readonly List<HookThreadStall> _stalls = new List<HookThreadStall>();
        private readonly List<HookRearm> _rearms = new List<HookRearm>();

        internal HotkeyWitnessLedger(long nowMs)
        {
            Interval = new HotkeyWitnessCounters(nowMs);
            Session = new HotkeyWitnessCounters(nowMs);
        }

        internal HotkeyWitnessCounters Interval { get; private set; }

        internal HotkeyWitnessCounters Session { get; }

        /// <summary>Whether a verdict or an expiry is outstanding, which is when the witness timer has to run.</summary>
        internal bool HasPendingWork =>
            _pending.Count > 0 || _unmatchedReports.Count > 0 || _recentRepeats.Count > 0;

        /// <summary>Hands out the counters of the interval that ends now and starts the next one.</summary>
        internal HotkeyWitnessCounters StartNewInterval(long nowMs)
        {
            var finished = Interval;
            Interval = new HotkeyWitnessCounters(nowMs);
            return finished;
        }

        internal static bool IsTriggerKey(HotkeyRegistration[] registrations, int virtualKey)
        {
            if (virtualKey <= 0)
                return false;

            for (int i = 0; i < registrations.Length; i++)
            {
                if (registrations[i].TriggerVirtualKey == virtualKey)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Raw input reports Shift, Control and Alt by their generic codes. Left and right are
        /// told apart (by the make code for Shift, the E0 prefix for the others) because
        /// releasing one of them leaves the modifier held while the other is still down.
        /// </summary>
        internal static int NormalizeVirtualKey(int virtualKey, int makeCode, bool isE0)
        {
            switch (virtualKey)
            {
                case VK_SHIFT:
                    return makeCode == RightShiftMakeCode ? VK_RSHIFT : VK_LSHIFT;
                case VK_CONTROL:
                    return isE0 ? VK_RCONTROL : VK_LCONTROL;
                case VK_MENU:
                    return isE0 ? VK_RMENU : VK_LMENU;
                default:
                    return virtualKey;
            }
        }

        internal void OnRawKey(in RawKeyEvent key, HotkeyRegistration[] registrations, long heartbeatMs,
            ICollection<WitnessedHotkeyPress> verdicts)
        {
            int virtualKey = NormalizeVirtualKey(key.VirtualKey, key.MakeCode, key.IsE0);
            // 0 carries no key, 0xFF is the fake key of an E1 sequence (Pause).
            if (virtualKey <= 0 || virtualKey >= 0xFF)
                return;

            if (key.IsBreak)
            {
                _down[virtualKey] = false;
                return;
            }

            bool isRepeat = _down[virtualKey] &&
                (uint)unchecked(key.EventTime - _lastMakeTime[virtualKey]) < KeyRepeatExpiryMs;
            _down[virtualKey] = true;
            _lastMakeTime[virtualKey] = key.EventTime;

            if (!IsTriggerKey(registrations, virtualKey))
                return;

            long pressedMs = key.ArrivalMs - key.ProcessingDelayMs;

            if (isRepeat)
            {
                var repeat = new RawRepeat
                {
                    VirtualKey = virtualKey,
                    EventTime = key.EventTime,
                    ArrivalMs = key.ArrivalMs,
                    PressedMs = pressedMs,
                    Injected = key.Injected,
                    ForegroundProcessId = key.ForegroundProcessId
                };

                var repeatReport = TakeUnmatchedReport(virtualKey, key.EventTime);
                if (repeatReport != null)
                    JudgeRepeat(repeat, repeatReport, registrations, key.ArrivalMs, verdicts);
                else
                    _recentRepeats.Add(repeat);
                return;
            }

            if (key.ProcessingDelayMs <= FreshEventMs)
                ReleaseStaleModifiers(key.AsyncModifiers);

            var actions = ActionsFor(registrations, virtualKey, HeldModifiers(), out var combination);
            var press = new PendingPress
            {
                VirtualKey = virtualKey,
                EventTime = key.EventTime,
                ArrivalMs = key.ArrivalMs,
                PressedMs = pressedMs,
                Actions = actions,
                Combination = combination,
                Injected = key.Injected,
                ForegroundProcessId = key.ForegroundProcessId,
                HeartbeatAgeMs = heartbeatMs == NoHeartbeat ? -1 : Math.Max(0, pressedMs - heartbeatMs)
            };

            var report = TakeUnmatchedReport(virtualKey, key.EventTime);
            if (report != null)
                Judge(press, report, registrations, key.ArrivalMs, verdicts);
            else
                _pending.Add(press);
        }

        internal void OnHookReport(HookKeyReport report, HotkeyRegistration[] registrations, long nowMs,
            ICollection<WitnessedHotkeyPress> verdicts)
        {
            int index = _pending.FindIndex(press =>
                press.VirtualKey == report.VirtualKey && press.EventTime == report.EventTime);
            if (index >= 0)
            {
                var press = _pending[index];
                _pending.RemoveAt(index);
                Judge(press, report, registrations, nowMs, verdicts);
                return;
            }

            index = _recentRepeats.FindIndex(repeat =>
                repeat.VirtualKey == report.VirtualKey && repeat.EventTime == report.EventTime);
            if (index >= 0)
            {
                var repeat = _recentRepeats[index];
                _recentRepeats.RemoveAt(index);
                JudgeRepeat(repeat, report, registrations, nowMs, verdicts);
                return;
            }

            _unmatchedReports.Add(report);
        }

        internal void OnHookStall(HookThreadStall stall)
        {
            AddToHistory(_stalls, stall);
            Interval.RecordStall(stall);
            Session.RecordStall(stall);
        }

        internal void OnHookRearm(HookRearm rearm) => AddToHistory(_rearms, rearm);

        /// <summary>
        /// Declares the presses lost whose hook thread has caught up without reporting them, and
        /// those it has not responded to for <see cref="MaxWaitMs"/>. <paramref name="heartbeatMs"/>
        /// must have been read before the hook reports were taken in, or a report the heartbeat
        /// already covers could still be on its way.
        /// </summary>
        internal void ResolveByHeartbeat(long nowMs, long heartbeatMs, ICollection<WitnessedHotkeyPress> verdicts)
        {
            for (int i = 0; i < _pending.Count;)
            {
                var press = _pending[i];
                bool hookCaughtUp = heartbeatMs != NoHeartbeat && heartbeatMs > press.ArrivalMs;
                if (!hookCaughtUp && nowMs - press.ArrivalMs < MaxWaitMs)
                {
                    i++;
                    continue;
                }

                _pending.RemoveAt(i);

                // A keystroke that forms no hotkey was none of the hook's business.
                if (press.Actions == null)
                    continue;

                Record(verdicts, new WitnessedHotkeyPress
                {
                    Verdict = HotkeyWitnessVerdict.NotReceived,
                    VirtualKey = press.VirtualKey,
                    EventTime = press.EventTime,
                    Combination = press.Combination,
                    Actions = press.Actions,
                    Injected = press.Injected,
                    ForegroundProcessId = press.ForegroundProcessId,
                    PressedMs = press.PressedMs,
                    ResolvedMs = nowMs,
                    HookLatencyMs = -1,
                    HeartbeatAgeMs = press.HeartbeatAgeMs,
                    UnresponsiveMs = hookCaughtUp ? 0
                        : nowMs - (heartbeatMs == NoHeartbeat ? press.ArrivalMs : heartbeatMs),
                    Stall = FindStall(press.PressedMs, nowMs),
                    LastRearm = FindRearm(press.PressedMs)
                });
            }
        }

        /// <summary>Drops what can no longer pair. Witness timer only, see <see cref="ReportRetentionMs"/>.</summary>
        internal void Expire(long nowMs)
        {
            for (int i = _unmatchedReports.Count - 1; i >= 0; i--)
            {
                var report = _unmatchedReports[i];
                if (nowMs - report.ReportedMs < ReportRetentionMs)
                    continue;

                _unmatchedReports.RemoveAt(i);

                // Raw input never delivered a keystroke the hook dispatched a hotkey for.
                if (report.Outcome == HookKeyOutcome.Dispatched)
                {
                    Interval.RecordUnwitnessed();
                    Session.RecordUnwitnessed();
                }
            }

            _recentRepeats.RemoveAll(repeat => nowMs - repeat.ArrivalMs >= RepeatRetentionMs);
        }

        private void Judge(PendingPress press, HookKeyReport report, HotkeyRegistration[] registrations,
            long nowMs, ICollection<WitnessedHotkeyPress> verdicts)
        {
            HotkeyWitnessVerdict verdict;
            switch (report.Outcome)
            {
                case HookKeyOutcome.Dispatched:
                    verdict = HotkeyWitnessVerdict.Dispatched;
                    break;
                case HookKeyOutcome.RepeatFiltered:
                    verdict = HotkeyWitnessVerdict.DiscardedAsRepeat;
                    break;
                default:
                    verdict = HotkeyWitnessVerdict.ReadWithOtherModifiers;
                    break;
            }

            // Typing the trigger key without its modifiers (an O in a chat) is no hotkey press,
            // and the hook rightly did nothing with it.
            bool dispatched = verdict == HotkeyWitnessVerdict.Dispatched;
            if (!dispatched && press.Actions == null)
                return;

            Record(verdicts, new WitnessedHotkeyPress
            {
                Verdict = verdict,
                VirtualKey = press.VirtualKey,
                EventTime = press.EventTime,
                Combination = dispatched ? report.Combination : press.Combination,
                HookCombination = report.Combination,
                Actions = dispatched ? ActionsFor(registrations, press.VirtualKey, report.Combination) : press.Actions,
                Injected = press.Injected,
                ForegroundProcessId = press.ForegroundProcessId,
                PressedMs = press.PressedMs,
                ResolvedMs = nowMs,
                HookLatencyMs = report.HookLatencyMs,
                HeartbeatAgeMs = press.HeartbeatAgeMs,
                Stall = FindStall(press.PressedMs, nowMs),
                LastRearm = FindRearm(press.PressedMs)
            });
        }

        private void JudgeRepeat(RawRepeat repeat, HookKeyReport report, HotkeyRegistration[] registrations,
            long nowMs, ICollection<WitnessedHotkeyPress> verdicts)
        {
            // Discarding auto-repeats is exactly what the repeat filter is for.
            if (report.Outcome != HookKeyOutcome.Dispatched)
                return;

            Record(verdicts, new WitnessedHotkeyPress
            {
                Verdict = HotkeyWitnessVerdict.DispatchedAutoRepeat,
                VirtualKey = repeat.VirtualKey,
                EventTime = repeat.EventTime,
                Combination = report.Combination,
                HookCombination = report.Combination,
                Actions = ActionsFor(registrations, repeat.VirtualKey, report.Combination),
                Injected = repeat.Injected,
                ForegroundProcessId = repeat.ForegroundProcessId,
                PressedMs = repeat.PressedMs,
                ResolvedMs = nowMs,
                HookLatencyMs = report.HookLatencyMs,
                HeartbeatAgeMs = -1,
                Stall = FindStall(repeat.PressedMs, nowMs),
                LastRearm = FindRearm(repeat.PressedMs)
            });
        }

        private void Record(ICollection<WitnessedHotkeyPress> verdicts, WitnessedHotkeyPress press)
        {
            verdicts.Add(press);
            Interval.Record(press);
            Session.Record(press);
        }

        private HookKeyReport TakeUnmatchedReport(int virtualKey, int eventTime)
        {
            int index = _unmatchedReports.FindIndex(report =>
                report.VirtualKey == virtualKey && report.EventTime == eventTime);
            if (index < 0)
                return null;

            var found = _unmatchedReports[index];
            _unmatchedReports.RemoveAt(index);
            return found;
        }

        private HotkeyModifiers HeldModifiers()
        {
            var modifiers = HotkeyModifiers.None;

            if (_down[VK_LCONTROL] || _down[VK_RCONTROL])
                modifiers |= HotkeyModifiers.Control;
            if (_down[VK_LSHIFT] || _down[VK_RSHIFT])
                modifiers |= HotkeyModifiers.Shift;
            if (_down[VK_LMENU] || _down[VK_RMENU])
                modifiers |= HotkeyModifiers.Alt;

            return modifiers;
        }

        private void ReleaseStaleModifiers(HotkeyModifiers asyncModifiers)
        {
            if ((asyncModifiers & HotkeyModifiers.Control) == 0)
                _down[VK_LCONTROL] = _down[VK_RCONTROL] = false;
            if ((asyncModifiers & HotkeyModifiers.Shift) == 0)
                _down[VK_LSHIFT] = _down[VK_RSHIFT] = false;
            if ((asyncModifiers & HotkeyModifiers.Alt) == 0)
                _down[VK_LMENU] = _down[VK_RMENU] = false;
        }

        /// <summary>The hotkeys the held modifiers and the trigger key form, null when they form none.</summary>
        private static List<HotkeyAction> ActionsFor(HotkeyRegistration[] registrations, int virtualKey,
            HotkeyModifiers modifiers, out string combination)
        {
            combination = null;
            List<HotkeyAction> actions = null;

            foreach (var registration in registrations)
            {
                if (registration.TriggerVirtualKey != virtualKey)
                    continue;

                combination = ModifierState.Combine(modifiers, registration.TriggerKey);
                if (registration.Combinations.ContainsKey(combination))
                    (actions ??= new List<HotkeyAction>()).Add(registration.HotkeyAction);
            }

            return actions;
        }

        private static List<HotkeyAction> ActionsFor(HotkeyRegistration[] registrations, int virtualKey,
            string combination)
        {
            if (combination == null)
                return null;

            List<HotkeyAction> actions = null;
            foreach (var registration in registrations)
            {
                if (registration.TriggerVirtualKey == virtualKey && registration.Combinations.ContainsKey(combination))
                    (actions ??= new List<HotkeyAction>()).Add(registration.HotkeyAction);
            }

            return actions;
        }

        private HookThreadStall FindStall(long pressedMs, long untilMs)
        {
            HookThreadStall longest = null;
            foreach (var stall in _stalls)
            {
                bool overlaps = stall.EndMs >= pressedMs - StallContextMs && stall.EndMs - stall.GapMs <= untilMs;
                if (overlaps && (longest == null || stall.GapMs > longest.GapMs))
                    longest = stall;
            }

            return longest;
        }

        private HookRearm FindRearm(long pressedMs)
        {
            HookRearm last = null;
            foreach (var rearm in _rearms)
            {
                if (rearm.AtMs <= pressedMs && (last == null || rearm.AtMs > last.AtMs))
                    last = rearm;
            }

            return last;
        }

        private static void AddToHistory<T>(List<T> history, T item)
        {
            if (history.Count == HistoryLength)
                history.RemoveAt(0);
            history.Add(item);
        }

        /// <summary>A trigger key press waiting for the hook's report.</summary>
        private sealed class PendingPress
        {
            internal int VirtualKey { get; init; }

            internal int EventTime { get; init; }

            internal long ArrivalMs { get; init; }

            internal long PressedMs { get; init; }

            internal string Combination { get; init; }

            /// <summary>The hotkeys the witness read; null when the keystroke forms none.</summary>
            internal List<HotkeyAction> Actions { get; init; }

            internal bool Injected { get; init; }

            internal int ForegroundProcessId { get; init; }

            internal long HeartbeatAgeMs { get; init; }
        }

        /// <summary>An auto-repeat of a trigger key, kept until the hook's report of it arrives.</summary>
        private sealed class RawRepeat
        {
            internal int VirtualKey { get; init; }

            internal int EventTime { get; init; }

            internal long ArrivalMs { get; init; }

            internal long PressedMs { get; init; }

            internal bool Injected { get; init; }

            internal int ForegroundProcessId { get; init; }
        }
    }
}
