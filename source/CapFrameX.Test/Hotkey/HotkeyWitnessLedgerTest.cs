using System;
using System.Collections.Generic;
using System.Linq;
using CapFrameX.Hotkey;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Hotkey
{
    [TestClass]
    public class HotkeyWitnessLedgerTest
    {
        private const int VK_O = 0x4F;
        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12;
        private const int OMakeCode = 0x18;
        private const int ControlMakeCode = 0x1D;
        private const int AltMakeCode = 0x38;

        // The witness and the hook stamp the same keystroke with the same input time; the
        // witness clock (arrival, heartbeats, reports) is a different one.
        private const int AltTime = 100_000;
        private const int OTime = 100_050;
        private const long OArrivalMs = 5050;

        // Alt+O as HotkeyDictionaryBuilder registers it: the configured modifier plus every
        // combination that adds the others.
        private static readonly HotkeyRegistration[] AltO =
        {
            Registration(HotkeyAction.Overlay, "O", "Alt+O", "Control+Alt+O", "Shift+Alt+O", "Control+Shift+Alt+O")
        };

        private readonly List<WitnessedHotkeyPress> _verdicts = new List<WitnessedHotkeyPress>();

        [TestMethod]
        public void HookReportBeforeTheRawKeystroke_IsDispatched()
        {
            var ledger = new HotkeyWitnessLedger(0);
            PressAlt(ledger);
            ledger.OnHookReport(Report(HookKeyOutcome.Dispatched, "Alt+O", OArrivalMs - 1, latencyMs: 2),
                AltO, OArrivalMs - 1, _verdicts);
            ledger.OnRawKey(KeyDown(VK_O, OTime, OArrivalMs, OMakeCode, HotkeyModifiers.Alt), AltO, OArrivalMs - 10, _verdicts);

            var press = _verdicts.Single();
            Assert.AreEqual(HotkeyWitnessVerdict.Dispatched, press.Verdict);
            Assert.AreEqual("Alt+O", press.Combination);
            CollectionAssert.AreEqual(new[] { HotkeyAction.Overlay }, press.Actions.ToArray());
            Assert.AreEqual(2, press.HookLatencyMs);
            Assert.IsFalse(ledger.HasPendingWork);
        }

        /// <summary>
        /// A stalled hook thread handles the keystroke after raw input delivered it. The press
        /// waits for the report instead of being declared lost.
        /// </summary>
        [TestMethod]
        public void RawKeystrokeBeforeTheHookReport_WaitsForIt()
        {
            var ledger = new HotkeyWitnessLedger(0);
            PressAltO(ledger, heartbeatMs: OArrivalMs - 10);
            Assert.AreEqual(0, _verdicts.Count);

            // The hook thread hangs: its last heartbeat predates the press.
            ledger.ResolveByHeartbeat(OArrivalMs + 1000, OArrivalMs - 10, _verdicts);
            Assert.AreEqual(0, _verdicts.Count);
            Assert.IsTrue(ledger.HasPendingWork);

            ledger.OnHookReport(Report(HookKeyOutcome.Dispatched, "Alt+O", OArrivalMs + 1500, latencyMs: 1500),
                AltO, OArrivalMs + 1500, _verdicts);

            var press = _verdicts.Single();
            Assert.AreEqual(HotkeyWitnessVerdict.Dispatched, press.Verdict);
            Assert.AreEqual(1500, press.HookLatencyMs);
        }

        /// <summary>
        /// The verdict this exists for. A heartbeat that started after the witness saw the press
        /// proves the hook thread handled every hook call sent before, so a report missing by
        /// then never comes.
        /// </summary>
        [TestMethod]
        public void PressWithoutReport_IsNotReceivedOnceTheHookThreadCaughtUp()
        {
            var ledger = new HotkeyWitnessLedger(0);
            PressAltO(ledger, heartbeatMs: OArrivalMs - 10);

            // A heartbeat from the very moment of arrival does not prove anything yet.
            ledger.ResolveByHeartbeat(OArrivalMs + 30, OArrivalMs, _verdicts);
            Assert.AreEqual(0, _verdicts.Count);

            ledger.ResolveByHeartbeat(OArrivalMs + 60, OArrivalMs + 40, _verdicts);

            var press = _verdicts.Single();
            Assert.AreEqual(HotkeyWitnessVerdict.NotReceived, press.Verdict);
            Assert.AreEqual("Alt+O", press.Combination);
            CollectionAssert.AreEqual(new[] { HotkeyAction.Overlay }, press.Actions.ToArray());
            Assert.IsNull(press.HookCombination);
            Assert.AreEqual(-1, press.HookLatencyMs);
            Assert.AreEqual(0, press.UnresponsiveMs);
            // Pressed 1 ms before the witness saw it, 10 ms after the last heartbeat.
            Assert.AreEqual(9, press.HeartbeatAgeMs);
            Assert.AreEqual(4242, press.ForegroundProcessId);
            Assert.IsFalse(ledger.HasPendingWork);
        }

        [TestMethod]
        public void PressWhileTheHookThreadHangs_IsReportedAfterTheMaximumWait()
        {
            var ledger = new HotkeyWitnessLedger(0);
            PressAltO(ledger, heartbeatMs: OArrivalMs - 10);

            ledger.ResolveByHeartbeat(OArrivalMs + HotkeyWitnessLedger.MaxWaitMs - 1, OArrivalMs - 10, _verdicts);
            Assert.AreEqual(0, _verdicts.Count);

            ledger.ResolveByHeartbeat(OArrivalMs + HotkeyWitnessLedger.MaxWaitMs, OArrivalMs - 10, _verdicts);

            var press = _verdicts.Single();
            Assert.AreEqual(HotkeyWitnessVerdict.NotReceived, press.Verdict);
            Assert.AreEqual(HotkeyWitnessLedger.MaxWaitMs + 10, press.UnresponsiveMs);
        }

        [TestMethod]
        public void GenuinePressTheRepeatFilterDiscarded_IsReported()
        {
            var ledger = new HotkeyWitnessLedger(0);
            PressAlt(ledger);
            ledger.OnHookReport(Report(HookKeyOutcome.RepeatFiltered, null, OArrivalMs - 1), AltO, OArrivalMs - 1, _verdicts);
            ledger.OnRawKey(KeyDown(VK_O, OTime, OArrivalMs, OMakeCode, HotkeyModifiers.Alt), AltO, OArrivalMs - 10, _verdicts);

            var press = _verdicts.Single();
            Assert.AreEqual(HotkeyWitnessVerdict.DiscardedAsRepeat, press.Verdict);
            Assert.AreEqual("Alt+O", press.Combination);
            Assert.IsNull(press.HookCombination);
        }

        /// <summary>
        /// A late hook call reads the modifiers of its own moment: Alt is up again by the time a
        /// stalled hook thread gets to the O, and the press goes nowhere.
        /// </summary>
        [TestMethod]
        public void LateHookCallReadingTheReleasedModifier_IsReported()
        {
            var ledger = new HotkeyWitnessLedger(0);
            PressAltO(ledger, heartbeatMs: OArrivalMs - 10);
            ledger.OnHookReport(Report(HookKeyOutcome.NoMatch, "O", OArrivalMs + 1500), AltO, OArrivalMs + 1500, _verdicts);

            var press = _verdicts.Single();
            Assert.AreEqual(HotkeyWitnessVerdict.ReadWithOtherModifiers, press.Verdict);
            Assert.AreEqual("Alt+O", press.Combination);
            Assert.AreEqual("O", press.HookCombination);
        }

        /// <summary>
        /// The witness processed the O late, after Alt went up again. GetAsyncKeyState then
        /// describes a later moment; the order of the raw keystrokes still holds.
        /// </summary>
        [TestMethod]
        public void LateWitness_KeepsTheModifiersOfTheKeystrokeOrder()
        {
            var ledger = new HotkeyWitnessLedger(0);
            PressAlt(ledger);
            ledger.OnRawKey(KeyDown(VK_O, OTime, OArrivalMs, OMakeCode, HotkeyModifiers.None,
                delayMs: HotkeyWitnessLedger.FreshEventMs + 150), AltO, OArrivalMs - 10, _verdicts);
            ledger.ResolveByHeartbeat(OArrivalMs + 60, OArrivalMs + 40, _verdicts);

            var press = _verdicts.Single();
            Assert.AreEqual(HotkeyWitnessVerdict.NotReceived, press.Verdict);
            Assert.AreEqual("Alt+O", press.Combination);
        }

        /// <summary>A key-up the witness never saw (Ctrl+Alt+Del) must not invent hotkey presses.</summary>
        [TestMethod]
        public void ModifierWhoseKeyUpWasMissed_IsDroppedForAFreshKeystroke()
        {
            var ledger = new HotkeyWitnessLedger(0);
            PressAlt(ledger);
            // Alt went up on the secure desktop; GetAsyncKeyState knows.
            ledger.OnRawKey(KeyDown(VK_O, OTime, OArrivalMs, OMakeCode, HotkeyModifiers.None), AltO, OArrivalMs - 10, _verdicts);
            ledger.ResolveByHeartbeat(OArrivalMs + 60, OArrivalMs + 40, _verdicts);

            Assert.AreEqual(0, _verdicts.Count);
            Assert.AreEqual(0, ledger.Session.Presses);
        }

        /// <summary>An O typed in a chat is no hotkey press, and the hook rightly ignores it.</summary>
        [TestMethod]
        public void TriggerKeyWithoutItsModifiers_IsNoHotkeyPress()
        {
            var ledger = new HotkeyWitnessLedger(0);
            ledger.OnHookReport(Report(HookKeyOutcome.NoMatch, "O", OArrivalMs - 1), AltO, OArrivalMs - 1, _verdicts);
            ledger.OnRawKey(KeyDown(VK_O, OTime, OArrivalMs, OMakeCode, HotkeyModifiers.None), AltO, OArrivalMs - 10, _verdicts);
            ledger.OnRawKey(KeyUp(VK_O, OTime + 80, OArrivalMs + 80, OMakeCode), AltO, OArrivalMs + 40, _verdicts);

            // The same keystroke where the hook saw nothing at all.
            ledger.OnRawKey(KeyDown(VK_O, OTime + 200, OArrivalMs + 200, OMakeCode, HotkeyModifiers.None), AltO, OArrivalMs + 190, _verdicts);
            ledger.ResolveByHeartbeat(OArrivalMs + 260, OArrivalMs + 240, _verdicts);

            Assert.AreEqual(0, _verdicts.Count);
            Assert.IsFalse(ledger.HasPendingWork);
        }

        [TestMethod]
        public void AutoRepeatsOfAHeldHotkey_AreNoPresses()
        {
            var ledger = new HotkeyWitnessLedger(0);
            PressAlt(ledger);
            ledger.OnHookReport(Report(HookKeyOutcome.Dispatched, "Alt+O", OArrivalMs - 1), AltO, OArrivalMs - 1, _verdicts);
            ledger.OnRawKey(KeyDown(VK_O, OTime, OArrivalMs, OMakeCode, HotkeyModifiers.Alt), AltO, OArrivalMs - 10, _verdicts);

            // Windows repeats the O after the keyboard delay, then at the repeat rate.
            foreach (int offset in new[] { 500, 533, 566 })
            {
                ledger.OnHookReport(Report(HookKeyOutcome.RepeatFiltered, null, OArrivalMs + offset - 1, eventTime: OTime + offset),
                    AltO, OArrivalMs + offset - 1, _verdicts);
                ledger.OnRawKey(KeyDown(VK_O, OTime + offset, OArrivalMs + offset, OMakeCode, HotkeyModifiers.Alt),
                    AltO, OArrivalMs + offset - 10, _verdicts);
            }

            // One the hook did not report (yet) waits, then expires without a verdict.
            ledger.OnRawKey(KeyDown(VK_O, OTime + 600, OArrivalMs + 600, OMakeCode, HotkeyModifiers.Alt), AltO, OArrivalMs + 590, _verdicts);
            ledger.Expire(OArrivalMs + 600 + HotkeyWitnessLedger.RepeatRetentionMs);

            Assert.AreEqual(HotkeyWitnessVerdict.Dispatched, _verdicts.Single().Verdict);
            Assert.AreEqual(1, ledger.Session.Presses);
            Assert.IsFalse(ledger.HasPendingWork);
        }

        /// <summary>
        /// Windows starts repeating after up to 1 s (keyboard delay 3). A hook that takes the
        /// first repeat for a new press toggles the hotkey back while it is held.
        /// </summary>
        [TestMethod]
        public void AutoRepeatTheHookDispatched_IsReported()
        {
            var ledger = new HotkeyWitnessLedger(0);
            PressAlt(ledger);
            ledger.OnHookReport(Report(HookKeyOutcome.Dispatched, "Alt+O", OArrivalMs - 1), AltO, OArrivalMs - 1, _verdicts);
            ledger.OnRawKey(KeyDown(VK_O, OTime, OArrivalMs, OMakeCode, HotkeyModifiers.Alt), AltO, OArrivalMs - 10, _verdicts);

            ledger.OnRawKey(KeyDown(VK_O, OTime + 1000, OArrivalMs + 1000, OMakeCode, HotkeyModifiers.Alt), AltO, OArrivalMs + 990, _verdicts);
            ledger.OnHookReport(Report(HookKeyOutcome.Dispatched, "Alt+O", OArrivalMs + 1001, eventTime: OTime + 1000),
                AltO, OArrivalMs + 1001, _verdicts);

            Assert.AreEqual(2, _verdicts.Count);
            Assert.AreEqual(HotkeyWitnessVerdict.DispatchedAutoRepeat, _verdicts[1].Verdict);
            Assert.AreEqual("Alt+O", _verdicts[1].Combination);
            Assert.AreEqual(1, ledger.Session.Presses);
            Assert.AreEqual(1, ledger.Session.DispatchedAutoRepeats);
        }

        [TestMethod]
        public void KeyUpTheWitnessMissed_ExpiresIntoANewPress()
        {
            var ledger = new HotkeyWitnessLedger(0);
            PressAltO(ledger, heartbeatMs: OArrivalMs - 10);
            ledger.OnHookReport(Report(HookKeyOutcome.Dispatched, "Alt+O", OArrivalMs + 1), AltO, OArrivalMs + 1, _verdicts);

            // No key-up of the O ever arrived. Within the expiry another make code is a repeat...
            int repeatTime = OTime + HotkeyWitnessLedger.KeyRepeatExpiryMs - 1;
            ledger.OnRawKey(KeyDown(VK_O, repeatTime, OArrivalMs + 1499, OMakeCode, HotkeyModifiers.Alt), AltO, OArrivalMs + 1490, _verdicts);
            ledger.OnHookReport(Report(HookKeyOutcome.RepeatFiltered, null, OArrivalMs + 1500, eventTime: repeatTime),
                AltO, OArrivalMs + 1500, _verdicts);
            Assert.AreEqual(1, _verdicts.Count);

            // ...after it the key was evidently pressed again.
            int pressTime = repeatTime + HotkeyWitnessLedger.KeyRepeatExpiryMs;
            ledger.OnRawKey(KeyDown(VK_O, pressTime, OArrivalMs + 3000, OMakeCode, HotkeyModifiers.Alt), AltO, OArrivalMs + 2990, _verdicts);
            ledger.OnHookReport(Report(HookKeyOutcome.Dispatched, "Alt+O", OArrivalMs + 3001, eventTime: pressTime),
                AltO, OArrivalMs + 3001, _verdicts);

            Assert.AreEqual(2, _verdicts.Count);
            Assert.IsTrue(_verdicts.All(press => press.Verdict == HotkeyWitnessVerdict.Dispatched));
        }

        [TestMethod]
        public void DispatchWithoutARawKeystroke_CountsAsUnwitnessed()
        {
            var ledger = new HotkeyWitnessLedger(0);
            ledger.OnHookReport(Report(HookKeyOutcome.Dispatched, "Alt+O", 5000), AltO, 5000, _verdicts);

            ledger.Expire(5000 + HotkeyWitnessLedger.ReportRetentionMs - 1);
            Assert.IsTrue(ledger.HasPendingWork);

            ledger.Expire(5000 + HotkeyWitnessLedger.ReportRetentionMs);
            Assert.IsFalse(ledger.HasPendingWork);
            Assert.AreEqual(0, _verdicts.Count);
            Assert.AreEqual(1, ledger.Session.Unwitnessed);
        }

        [TestMethod]
        public void LeftAndRightModifiers_AreTrackedSeparately()
        {
            var controlO = new[] { Registration(HotkeyAction.Capture, "O", "Control+O", "Control+Shift+O") };
            var ledger = new HotkeyWitnessLedger(0);

            ledger.OnRawKey(KeyDown(VK_CONTROL, 1000, 4000, ControlMakeCode, HotkeyModifiers.Control), controlO, 3990, _verdicts);
            ledger.OnRawKey(KeyDown(VK_CONTROL, 1010, 4010, ControlMakeCode, HotkeyModifiers.Control, isE0: true), controlO, 4000, _verdicts);
            ledger.OnRawKey(KeyUp(VK_CONTROL, 1020, 4020, ControlMakeCode), controlO, 4010, _verdicts);
            // The right Control is still down.
            ledger.OnRawKey(KeyDown(VK_O, 1100, 4100, OMakeCode, HotkeyModifiers.Control), controlO, 4090, _verdicts);
            ledger.ResolveByHeartbeat(4160, 4140, _verdicts);

            var press = _verdicts.Single();
            Assert.AreEqual("Control+O", press.Combination);
            CollectionAssert.AreEqual(new[] { HotkeyAction.Capture }, press.Actions.ToArray());
        }

        [TestMethod]
        public void StallAndInstallationAroundThePress_AreItsContext()
        {
            var ledger = new HotkeyWitnessLedger(0);
            ledger.OnHookRearm(new HookRearm { AtMs = OArrivalMs - 20_000, Reason = "periodic" });
            ledger.OnHookRearm(new HookRearm { AtMs = OArrivalMs + 1600, Reason = "stall" });
            // Too long ago to explain anything.
            ledger.OnHookStall(new HookThreadStall { EndMs = OArrivalMs - 12_000, GapMs = 5000, Rearmed = true });

            PressAltO(ledger, heartbeatMs: OArrivalMs - 50);
            // The stall the press fell into, reported by the heartbeat that ended it.
            ledger.OnHookStall(new HookThreadStall { EndMs = OArrivalMs + 1500, GapMs = 1550, Rearmed = false });
            ledger.ResolveByHeartbeat(OArrivalMs + 1600, OArrivalMs + 1500, _verdicts);

            var press = _verdicts.Single();
            Assert.AreEqual(1550, press.Stall.GapMs);
            Assert.IsFalse(press.Stall.Rearmed);
            Assert.AreEqual("periodic", press.LastRearm.Reason);
            Assert.AreEqual(49, press.HeartbeatAgeMs);
        }

        [TestMethod]
        public void StartNewInterval_HandsOutTheIntervalAndKeepsTheSession()
        {
            var ledger = new HotkeyWitnessLedger(0);
            ledger.OnHookStall(new HookThreadStall { EndMs = 1000, GapMs = 400, Rearmed = false });
            PressAltO(ledger, heartbeatMs: OArrivalMs - 10);
            ledger.ResolveByHeartbeat(OArrivalMs + 60, OArrivalMs + 40, _verdicts);

            var interval = ledger.StartNewInterval(OArrivalMs + 100);

            Assert.AreEqual(1, interval.Presses);
            Assert.AreEqual(1, interval.NotReceived);
            Assert.AreEqual(1, interval.Stalls);
            Assert.AreEqual(1, interval.StallsWithoutRearm);
            Assert.AreEqual(400, interval.LongestStallMs);
            Assert.IsTrue(interval.HasActivity);
            Assert.IsFalse(ledger.Interval.HasActivity);
            Assert.AreEqual(OArrivalMs + 100, ledger.Interval.SinceMs);
            Assert.AreEqual(1, ledger.Session.NotReceived);
        }

        [TestMethod]
        [DataRow(0x10, 0x2A, false, 0xA0)]
        [DataRow(0x10, 0x36, false, 0xA1)]
        [DataRow(0x10, 0x00, false, 0xA0)]
        [DataRow(0x11, 0x1D, false, 0xA2)]
        [DataRow(0x11, 0x1D, true, 0xA3)]
        [DataRow(0x12, 0x38, false, 0xA4)]
        [DataRow(0x12, 0x38, true, 0xA5)]
        [DataRow(0x4F, 0x18, false, 0x4F)]
        public void NormalizeVirtualKey_TellsLeftAndRightModifiersApart(int virtualKey, int makeCode, bool isE0, int expected)
        {
            Assert.AreEqual(expected, HotkeyWitnessLedger.NormalizeVirtualKey(virtualKey, makeCode, isE0));
        }

        [TestMethod]
        [DataRow("O", 0x4F)]
        [DataRow("F24", 0x87)]
        [DataRow("Oem102", 0xE2)]
        [DataRow("NoSuchKey", 0)]
        public void TriggerVirtualKey_IsTheKeysValueOfTheTriggerKey(string triggerKey, int expected)
        {
            var registration = Registration(HotkeyAction.Capture, triggerKey, triggerKey);

            Assert.AreEqual(expected, registration.TriggerVirtualKey);
            Assert.AreEqual(expected != 0, HotkeyWitnessLedger.IsTriggerKey(new[] { registration }, expected));
        }

        [TestMethod]
        public void Combine_UsesTheOrderOfTheStoredCombinations()
        {
            Assert.AreEqual("Control+Shift+Alt+O",
                ModifierState.Combine(HotkeyModifiers.Alt | HotkeyModifiers.Shift | HotkeyModifiers.Control, "O"));
            Assert.AreEqual("O", ModifierState.Combine(HotkeyModifiers.None, "O"));
        }

        private void PressAlt(HotkeyWitnessLedger ledger)
        {
            ledger.OnRawKey(KeyDown(VK_MENU, AltTime, OArrivalMs - 50, AltMakeCode, HotkeyModifiers.Alt), AltO, OArrivalMs - 60, _verdicts);
        }

        private void PressAltO(HotkeyWitnessLedger ledger, long heartbeatMs)
        {
            PressAlt(ledger);
            ledger.OnRawKey(KeyDown(VK_O, OTime, OArrivalMs, OMakeCode, HotkeyModifiers.Alt), AltO, heartbeatMs, _verdicts);
        }

        private static RawKeyEvent KeyDown(int virtualKey, int eventTime, long arrivalMs, int makeCode,
            HotkeyModifiers asyncModifiers, int delayMs = 1, bool isE0 = false)
            => new RawKeyEvent
            {
                VirtualKey = virtualKey,
                MakeCode = makeCode,
                IsE0 = isE0,
                EventTime = eventTime,
                ArrivalMs = arrivalMs,
                ProcessingDelayMs = delayMs,
                AsyncModifiers = asyncModifiers,
                ForegroundProcessId = 4242
            };

        private static RawKeyEvent KeyUp(int virtualKey, int eventTime, long arrivalMs, int makeCode)
            => new RawKeyEvent
            {
                VirtualKey = virtualKey,
                MakeCode = makeCode,
                IsBreak = true,
                EventTime = eventTime,
                ArrivalMs = arrivalMs,
                ProcessingDelayMs = 1
            };

        private static HookKeyReport Report(HookKeyOutcome outcome, string combination, long reportedMs,
            int latencyMs = 1, int eventTime = OTime)
            => new HookKeyReport
            {
                VirtualKey = VK_O,
                EventTime = eventTime,
                Outcome = outcome,
                Combination = combination,
                HookLatencyMs = latencyMs,
                ReportedMs = reportedMs
            };

        private static HotkeyRegistration Registration(HotkeyAction action, string triggerKey, params string[] combinations)
            => new HotkeyRegistration(action, triggerKey,
                combinations.ToDictionary(combination => combination, combination => (Action)(() => { })), null);
    }
}
