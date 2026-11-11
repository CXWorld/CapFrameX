using CapFrameX.Data.CaptureTarget;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Reactive.Disposables;

namespace CapFrameX.Test.Data
{
    [TestClass]
    public class ForegroundProcessTrackerTest
    {
        private int _foregroundAtActivation;
        private long _now = 10_000;
        private Action<int> _hook;
        private int _hooksInstalled;

        [TestMethod]
        public void Inactive_InstallsNoHookAndKnowsNothing()
        {
            var tracker = CreateTracker();

            Assert.IsFalse(tracker.IsActive);
            Assert.AreEqual(0, _hooksInstalled);
            Assert.AreEqual(0, tracker.ForegroundProcessId);
        }

        [TestMethod]
        public void Activation_ReadsTheCurrentForegroundOnce()
        {
            var tracker = CreateTracker();
            _foregroundAtActivation = 100;

            tracker.SetActive(true);
            tracker.SetActive(true);

            Assert.AreEqual(1, _hooksInstalled);
            Assert.AreEqual(100, tracker.ForegroundProcessId);
            Assert.AreEqual(0.0, tracker.SecondsSinceForeground(100));
        }

        [TestMethod]
        public void ForegroundChange_StampsTheProcessThatLeft()
        {
            var tracker = CreateTracker();
            _foregroundAtActivation = 100;
            tracker.SetActive(true);

            _now += 5_000;
            _hook(200);
            _now += 2_000;

            Assert.AreEqual(200, tracker.ForegroundProcessId);
            Assert.AreEqual(2.0, tracker.SecondsSinceForeground(100));
            Assert.AreEqual(0.0, tracker.SecondsSinceForeground(200));
            Assert.IsNull(tracker.SecondsSinceForeground(300));
        }

        [TestMethod]
        public void ProcessThatReturns_IsInTheForegroundAgain()
        {
            var tracker = CreateTracker();
            _foregroundAtActivation = 100;
            tracker.SetActive(true);
            _hook(200);
            _now += 3_000;
            _hook(100);
            _now += 1_000;

            Assert.AreEqual(0.0, tracker.SecondsSinceForeground(100));
            Assert.AreEqual(1.0, tracker.SecondsSinceForeground(200));
        }

        [TestMethod]
        public void RepeatedEventOfTheSameProcess_KeepsItsState()
        {
            var tracker = CreateTracker();
            _foregroundAtActivation = 100;
            tracker.SetActive(true);
            _hook(200);
            _now += 3_000;

            _hook(200);

            Assert.AreEqual(3.0, tracker.SecondsSinceForeground(100));
        }

        [TestMethod]
        public void Deactivation_RemovesTheHookAndForgets()
        {
            var tracker = CreateTracker();
            _foregroundAtActivation = 100;
            tracker.SetActive(true);
            var hook = _hook;
            hook(200);

            tracker.SetActive(false);
            hook(300);

            Assert.IsNull(_hook, "The hook was not removed.");
            Assert.AreEqual(0, tracker.ForegroundProcessId, "An event queued before the removal must not count.");
            Assert.IsNull(tracker.SecondsSinceForeground(100));
            Assert.IsNull(tracker.SecondsSinceForeground(200));
        }

        [TestMethod]
        public void Dispose_RemovesTheHook()
        {
            var tracker = CreateTracker();
            tracker.SetActive(true);

            tracker.Dispose();

            Assert.IsNull(_hook);
            Assert.IsFalse(tracker.IsActive);
        }

        [TestMethod]
        public void UnknownForeground_IsNoProcess()
        {
            // The desktop switches (UAC, lock screen) have no foreground window.
            var tracker = CreateTracker();
            _foregroundAtActivation = 100;
            tracker.SetActive(true);

            _hook(0);

            Assert.AreEqual(0, tracker.ForegroundProcessId);
            Assert.IsNull(tracker.SecondsSinceForeground(0));
            Assert.AreEqual(0.0, tracker.SecondsSinceForeground(100));
        }

        [TestMethod]
        public void ManySwitches_ForgetTheOldestProcesses()
        {
            var tracker = CreateTracker();
            _foregroundAtActivation = 1;
            tracker.SetActive(true);

            for (int processId = 2; processId <= 100; processId++)
            {
                _now += 1_000;
                _hook(processId);
            }
            _now += 1_000;

            Assert.IsNull(tracker.SecondsSinceForeground(1));
            Assert.AreEqual(1.0, tracker.SecondsSinceForeground(99));
        }

        [TestMethod]
        public void FailingLookups_LeaveTheTrackerUsable()
        {
            var noHook = new ForegroundProcessTracker(() => 100,
                _ => throw new InvalidOperationException(), () => _now);
            noHook.SetActive(true);
            Assert.IsFalse(noHook.IsActive);

            var noRead = new ForegroundProcessTracker(() => throw new InvalidOperationException(),
                Subscribe, () => _now);
            noRead.SetActive(true);
            Assert.IsTrue(noRead.IsActive);
            Assert.AreEqual(0, noRead.ForegroundProcessId);
            _hook(200);
            Assert.AreEqual(200, noRead.ForegroundProcessId);
        }

        private ForegroundProcessTracker CreateTracker()
            => new ForegroundProcessTracker(() => _foregroundAtActivation, Subscribe, () => _now);

        private IDisposable Subscribe(Action<int> onForegroundChanged)
        {
            _hooksInstalled++;
            _hook = onForegroundChanged;
            return Disposable.Create(() => _hook = null);
        }
    }
}
