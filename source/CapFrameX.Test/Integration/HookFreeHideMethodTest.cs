using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Reflection;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CapFrameX.Test.Integration
{
    /// <summary>
    /// How the hook-free window hides follows the target's present mode. In Mafia: The Old Country
    /// (RX 9070 XT, VRR, FSR FG, 2026-10-31) every capture began with 1-2 s of lost displays after the
    /// auto-hide cloaked the window, and ShowWindow did the same, while the game stayed on its MPO
    /// plane; with the window emptied but still composed, three captures began clean. A composed game
    /// is still cloaked, so the capture measures it without the window. Whatever the method, a hidden
    /// overlay keeps reading the frame rows: shown again, its charts continue on the current window
    /// instead of playing the samples from before the hide and then jumping over the gap.
    /// </summary>
    [TestClass]
    public class HookFreeHideMethodTest
    {
        private const int TargetPid = 4321;

        [TestMethod]
        [DataRow("Hardware: Independent Flip", true)]
        [DataRow("Hardware Composed: Independent Flip", true)]
        [DataRow("Hardware: Legacy Flip", true)]
        [DataRow("Hardware: Legacy Copy to front buffer", true)]
        [DataRow("Composed: Flip", false)]
        [DataRow("Composed: Copy with GPU GDI", false)]
        [DataRow("Composed: Composition Atlas", false)]
        [DataRow("Unknown", false)]
        [DataRow("Other", false)]
        [DataRow("", false)]
        [DataRow(null, false)]
        public void Choose_KeepsTheWindowComposedOnlyOverAGameOnItsOwnPlane(string presentMode, bool transparent)
        {
            Assert.AreEqual(transparent ? HookFreeHideMethod.Transparent : HookFreeHideMethod.Cloak,
                HookFreeHideMethods.Choose(presentMode));
        }

        [TestMethod]
        public void Hide_TakesItsMethodFromTheTargetsPresentModeAtThatMoment()
        {
            using var harness = new Harness();
            harness.SetTarget(TargetPid);
            harness.SetActive(true);
            Assert.AreEqual(0, harness.Applied.Count, "A show chooses no hide method.");

            harness.PresentModes[TargetPid] = "Hardware Composed: Independent Flip";
            harness.SetActive(false);
            CollectionAssert.AreEqual(new[] { HookFreeHideMethod.Transparent }, harness.Applied);
            Assert.IsTrue(harness.IsHidden, "The overlay must still hide.");

            harness.SetActive(false);
            Assert.AreEqual(1, harness.Applied.Count, "An overlay that is already hidden chooses nothing.");

            harness.SetActive(true);
            harness.PresentModes[TargetPid] = "Composed: Flip";
            harness.SetActive(false);
            CollectionAssert.AreEqual(new[] { HookFreeHideMethod.Transparent, HookFreeHideMethod.Cloak },
                harness.Applied, "A composed game is cloaked.");
        }

        [TestMethod]
        public void Hide_WithoutAKnownPresentMode_Cloaks()
        {
            using var harness = new Harness();
            harness.SetActive(true);
            harness.SetActive(false);
            CollectionAssert.AreEqual(new[] { HookFreeHideMethod.Cloak }, harness.Applied,
                "Without a target nothing says the game is on a plane.");

            harness.SetTarget(TargetPid);
            harness.SetActive(true);
            harness.SetActive(false);
            CollectionAssert.AreEqual(new[] { HookFreeHideMethod.Cloak, HookFreeHideMethod.Cloak },
                harness.Applied, "A target the process list does not know is cloaked.");
        }

        [TestMethod]
        public void Hide_WithACoreWithoutHideMethods_StillHides()
        {
            using var harness = new Harness(applyResult: false);
            harness.SetTarget(TargetPid);
            harness.PresentModes[TargetPid] = "Hardware: Independent Flip";
            harness.SetActive(true);
            harness.SetActive(false);
            Assert.IsTrue(harness.IsHidden);
        }

        [TestMethod]
        public void Hide_KeepsTheFrameFeedForTheCharts()
        {
            using var harness = new Harness();
            harness.SetTarget(TargetPid);
            harness.SetActive(true);
            harness.FeedFramesToAChart();
            Assert.IsTrue(harness.Frames.HasObservers, "A shown overlay with a chart reads the rows.");

            harness.PresentModes[TargetPid] = "Hardware: Independent Flip";
            harness.SetActive(false);
            Assert.IsTrue(harness.IsHidden);
            Assert.IsTrue(harness.Frames.HasObservers,
                "A hidden overlay must keep reading the rows, or its charts resume on stale samples.");

            harness.SetActive(true);
            Assert.IsTrue(harness.Frames.HasObservers);
        }

        // The real bridge and OsdHost without a native handle: SetHidden only records the state, so
        // no window, native DLL or render thread is needed.
        private sealed class Harness : IDisposable
        {
            private readonly Subject<bool> _active = new Subject<bool>();
            private readonly Subject<IOverlayEntry[]> _entries = new Subject<IOverlayEntry[]>();
            private readonly Subject<string[]> _frames = new Subject<string[]>();
            private readonly Subject<int> _processIds = new Subject<int>();
            private readonly Subject<(string key, object value)> _configChanges =
                new Subject<(string key, object value)>();
            private readonly OsdOverlayBridge _bridge;
            private readonly object _host;

            public Dictionary<int, string> PresentModes { get; } = new Dictionary<int, string>();
            public Subject<string[]> Frames => _frames;
            public List<HookFreeHideMethod> Applied { get; } = new List<HookFreeHideMethod>();
            public bool IsHidden => (bool)Field(_host, "_hidden").GetValue(_host);

            public Harness(bool applyResult = true)
            {
                var config = new Mock<IAppConfiguration>();
                config.SetupGet(x => x.EnableHookFreeOverlay).Returns(true);
                config.SetupGet(x => x.OnValueChanged).Returns(_configChanges);
                var overlay = new Mock<IOverlayService>();
                overlay.SetupGet(x => x.IsOverlayActiveStream).Returns(_active);
                overlay.SetupGet(x => x.OnDictionaryUpdated).Returns(_entries);
                _bridge = new OsdOverlayBridge(overlay.Object, config.Object,
                    frameDataStream: _frames, frametimeColumnIndex: 1,
                    processIdStream: _processIds, processIdColumnIndex: 0,
                    presentModeOfProcess: pid => PresentModes.TryGetValue(pid, out var mode) ? mode : null);
                _bridge.ApplyHideMethod = method => { Applied.Add(method); return applyResult; };
                Field(_bridge, "_started").SetValue(_bridge, true);
                _host = Field(_bridge, "_osd").GetValue(_bridge);
            }

            public void SetActive(bool active) => _active.OnNext(active);

            public void SetTarget(int processId) => _processIds.OnNext(processId);

            // What the start does once the layout's frametime chart is known (UpdateFrameFeedRequirements).
            public void FeedFramesToAChart()
            {
                Field(_bridge, "_frameFeedRequirements").SetValue(_bridge, 1);
                _bridge.GetType().GetMethod("UpdateFrameSubscription",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(_bridge, new object[] { true });
            }

            public void Dispose()
            {
                _bridge.Dispose();
                _active.Dispose();
                _entries.Dispose();
                _frames.Dispose();
                _processIds.Dispose();
                _configChanges.Dispose();
            }

            private static FieldInfo Field(object target, string name) => target.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"Missing field: {name}");
        }
    }
}
