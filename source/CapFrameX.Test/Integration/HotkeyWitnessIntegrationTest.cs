using CapFrameX.Hotkey;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace CapFrameX.Test.Integration
{
    /// <summary>
    /// Drives the real global hook and the real hotkey witness with key presses injected through
    /// SendInput, and stalls the hook thread the way a starved or blocked thread stalls.
    ///
    /// The injected key is F24: no keyboard has it and no application reacts to it, but it goes
    /// to whatever window has the foreground, so run these tests on a desktop you are not typing
    /// on. They report themselves inconclusive when injected input does not arrive: on a locked
    /// or non-interactive desktop, or when the foreground window runs elevated and this process
    /// does not (UIPI).
    ///
    /// Each test writes a table of its presses and verdicts to the test output, which records how
    /// the Windows build at hand treats a hook that times out. On Windows 11 build 26300 a press
    /// made during a stall was lost or delivered late, and the hook kept working afterwards.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    [DoNotParallelize]
    public class HotkeyWitnessIntegrationTest
    {
        private const ushort VK_F24 = 0x87;
        private const int StallMs = 1500;
        private const int VerdictTimeoutMs = 5000;

        private readonly ConcurrentQueue<WitnessedHotkeyPress> _witnessed = new ConcurrentQueue<WitnessedHotkeyPress>();
        private HotkeyRegistration[] _originalRegistrations;
        private Action<WitnessedHotkeyPress> _observer;
        private int _actionRuns;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void Setup()
        {
            _originalRegistrations = GlobalHotkeyHook.Registrations;
            _observer = press =>
            {
                if (press.VirtualKey == VK_F24)
                    _witnessed.Enqueue(press);
            };
            HotkeyWitness.PressResolved += _observer;
            GlobalHotkeyHook.SetRegistrations(new[] { CreateF24Registration() });

            if (!SpinWait.SpinUntil(() => HotkeyWitness.IsActive, VerdictTimeoutMs))
                Assert.Inconclusive("The hotkey witness could not register raw keyboard input in this session.");

            // One press to prove injected input reaches this session at all.
            Press();
            if (!WaitForVerdicts(1))
            {
                Assert.Inconclusive(
                    "An injected F24 press did not arrive: locked or non-interactive desktop, or the foreground window runs elevated (UIPI).");
            }

            WaitForActionRuns(1);
            while (_witnessed.TryDequeue(out _))
            {
            }
            Interlocked.Exchange(ref _actionRuns, 0);
        }

        [TestCleanup]
        public void Cleanup()
        {
            HotkeyWitness.PressResolved -= _observer;
            GlobalHotkeyHook.SetRegistrations(_originalRegistrations);
        }

        /// <summary>
        /// The experiment of 2026-10-25 as a test: presses on a healthy hook, presses during a
        /// hook-thread stall past the hook timeout, presses after it. Whatever Windows does with
        /// the presses during the stall, none may disappear unseen: every one is dispatched or
        /// reported as not received, and the hook works again right after the stall.
        /// </summary>
        [TestMethod]
        public void EveryPressIsDispatchedOrReportedAsNotReceived_AcrossAHookThreadStall()
        {
            var phases = new List<string>();

            for (int i = 0; i < 3; i++)
            {
                PressAndRecord(phases, "healthy hook");
                Thread.Sleep(200);
            }

            GlobalHotkeyHook.RunOnHookThreadForTesting(() => Thread.Sleep(StallMs));
            // The stall starts with the next heartbeat, at most one heartbeat interval away.
            Thread.Sleep(150);
            for (int i = 0; i < 3; i++)
            {
                PressAndRecord(phases, "during the stall");
                Thread.Sleep(300);
            }

            Thread.Sleep(StallMs);
            for (int i = 0; i < 3; i++)
            {
                PressAndRecord(phases, "after the stall");
                Thread.Sleep(250);
            }

            Assert.IsTrue(WaitForVerdicts(phases.Count), $"The witness gave {_witnessed.Count} of {phases.Count} verdicts.");
            var presses = _witnessed.OrderBy(press => press.PressedMs).ToArray();
            WriteTable(phases, presses);

            Assert.IsTrue(presses.All(press => press.Verdict == HotkeyWitnessVerdict.Dispatched ||
                                               press.Verdict == HotkeyWitnessVerdict.NotReceived),
                "Every press must be dispatched or reported as not received.");
            int dispatched = presses.Count(press => press.Verdict == HotkeyWitnessVerdict.Dispatched);
            Assert.IsTrue(WaitForActionRuns(dispatched), $"{_actionRuns} action runs for {dispatched} dispatched presses.");
            Assert.AreEqual(dispatched, Volatile.Read(ref _actionRuns), "The action must run exactly for the dispatched presses.");

            for (int i = 0; i < presses.Length; i++)
            {
                if (phases[i] != "during the stall")
                {
                    Assert.AreEqual(HotkeyWitnessVerdict.Dispatched, presses[i].Verdict, $"Press {i + 1} ({phases[i]}).");
                }
                else if (presses[i].Verdict == HotkeyWitnessVerdict.NotReceived)
                {
                    Assert.IsNotNull(presses[i].Stall, $"Press {i + 1} lacks the stall that explains it.");
                    Assert.IsTrue(presses[i].Stall.GapMs >= StallMs - 100, $"Press {i + 1}: stall of {presses[i].Stall.GapMs} ms.");
                }
            }
        }

        /// <summary>
        /// A hook Windows took away is the case the witness exists for: it must report the press
        /// as never received, and the hook must work again once it is installed anew.
        /// </summary>
        [TestMethod]
        public void PressTheHookNeverReceives_IsReportedAsNotReceived()
        {
            GlobalHotkeyHook.RunOnHookThreadForTesting(GlobalHotkeyHook.RemoveHookForTesting);
            try
            {
                // The removal runs with the next heartbeat.
                Thread.Sleep(200);
                Press();

                Assert.IsTrue(WaitForVerdicts(1), "The witness gave no verdict.");
                Assert.IsTrue(_witnessed.TryDequeue(out var missed));
                WriteTable(new List<string> { "no hook" }, new[] { missed });

                Assert.AreEqual(HotkeyWitnessVerdict.NotReceived, missed.Verdict);
                Assert.AreEqual("F24", missed.Combination);
                Assert.AreEqual(0, missed.UnresponsiveMs, "The hook thread kept running.");
                Assert.IsTrue(missed.Injected);
                Assert.AreEqual(0, Volatile.Read(ref _actionRuns));
            }
            finally
            {
                GlobalHotkeyHook.RunOnHookThreadForTesting(GlobalHotkeyHook.RearmForTesting);
            }

            Thread.Sleep(200);
            Press();

            Assert.IsTrue(WaitForVerdicts(1), "The witness gave no verdict after the hook was installed again.");
            Assert.IsTrue(_witnessed.TryDequeue(out var dispatched));
            Assert.AreEqual(HotkeyWitnessVerdict.Dispatched, dispatched.Verdict);
            Assert.IsTrue(WaitForActionRuns(1));
        }

        private HotkeyRegistration CreateF24Registration()
        {
            Action action = () => Interlocked.Increment(ref _actionRuns);

            // Every modifier combination, as for a hotkey configured without modifiers, so a
            // modifier held on the physical keyboard cannot turn a press into a no-match.
            var combinations = new Dictionary<string, Action>();
            foreach (var modifiers in new[] { "", "Control+", "Shift+", "Alt+", "Control+Shift+", "Control+Alt+", "Shift+Alt+", "Control+Shift+Alt+" })
                combinations[modifiers + "F24"] = action;

            return new HotkeyRegistration(HotkeyAction.ResetMetrics, "F24", combinations, synchronizationContext: null);
        }

        private static void PressAndRecord(List<string> phases, string phase)
        {
            phases.Add(phase);
            Press();
        }

        private bool WaitForVerdicts(int count) => SpinWait.SpinUntil(() => _witnessed.Count >= count, VerdictTimeoutMs);

        private bool WaitForActionRuns(int count) =>
            SpinWait.SpinUntil(() => Volatile.Read(ref _actionRuns) >= count, VerdictTimeoutMs);

        private void WriteTable(IReadOnlyList<string> phases, IReadOnlyList<WitnessedHotkeyPress> presses)
        {
            for (int i = 0; i < presses.Count; i++)
            {
                var press = presses[i];
                TestContext.WriteLine(
                    $"{i + 1,2}. {(i < phases.Count ? phases[i] : "?"),-18} {press.Verdict,-12} " +
                    $"hook latency {press.HookLatencyMs,5} ms, heartbeat age {press.HeartbeatAgeMs,5} ms, " +
                    $"stall {(press.Stall == null ? "none" : $"{press.Stall.GapMs} ms ({(press.Stall.Rearmed ? "re-armed" : "not re-armed")})")}");
            }
        }

        private static void Press()
        {
            var inputs = new[]
            {
                new INPUT { Type = InputKeyboard, Data = new InputUnion { Keyboard = new KEYBDINPUT { VirtualKey = VK_F24 } } },
                new INPUT { Type = InputKeyboard, Data = new InputUnion { Keyboard = new KEYBDINPUT { VirtualKey = VK_F24, Flags = KeyEventKeyUp } } }
            };

            Assert.AreEqual((uint)inputs.Length, SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>()),
                $"SendInput failed (Win32 error {Marshal.GetLastWin32Error()}).");
        }

        private const uint InputKeyboard = 1;
        private const uint KeyEventKeyUp = 0x0002;

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint Type;
            public InputUnion Data;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT Mouse;
            [FieldOffset(0)] public KEYBDINPUT Keyboard;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int X;
            public int Y;
            public uint MouseData;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort VirtualKey;
            public ushort ScanCode;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint count, INPUT[] inputs, int size);
    }
}
