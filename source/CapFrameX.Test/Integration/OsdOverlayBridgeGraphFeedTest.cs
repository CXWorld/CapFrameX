using System;
using System.Collections;
using System.Reactive.Subjects;
using System.Reflection;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.OSD.Integration;
using CapFrameX.Overlay;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class OsdOverlayBridgeGraphFeedTest
    {
        [TestMethod]
        [DataRow(true, false, false, 1, 8d, 0d)]
        [DataRow(true, true, false, 1, 8d, 0d)]
        [DataRow(true, false, true, 1, 8d, 16d)]
        [DataRow(true, true, true, 1, 8d, 16d)]
        [DataRow(false, true, false, 1, 8d, 0d)]
        [DataRow(false, false, true, 1, 0d, 16d)]
        [DataRow(false, false, false, 0, 0d, 0d)]
        public void FrameRow_QueuesOneSampleForTheEnabledGraphs(bool fpsGraph, bool ftGraph,
            bool displayGraph, int expectedCount, double expectedFrametime, double expectedDisplayTime)
        {
            using var harness = new BridgeHarness();
            harness.Publish(fpsGraph, ftGraph, displayGraph);
            Assert.AreEqual(expectedCount > 0, harness.Frames.HasObservers);

            harness.Frames.OnNext(new[] { "8", "16", "1000" });

            Assert.AreEqual(expectedCount, harness.PendingSamples.Count);
            if (expectedCount > 0)
            {
                var sample = harness.PendingSamples[0];
                Assert.AreEqual(1000d, ReadField<double>(sample, "TimeMs"));
                Assert.AreEqual(expectedFrametime, ReadField<double>(sample, "FrametimeMs"));
                Assert.AreEqual(expectedDisplayTime, ReadField<double>(sample, "DisplayTimeMs"));
            }
            harness.Publish(fpsGraph, ftGraph, displayGraph); // the next OSD refresh
            AssertSharedValues(harness, 100d, 10d, 20d, "DXGI");
        }

        [TestMethod]
        public void TurningOffTheFpsGraph_UnsubscribesButKeepsForwardingSharedValues()
        {
            using var harness = new BridgeHarness();
            harness.Publish(true, false, false);
            harness.Frames.OnNext(new[] { "8", "16", "1000" });
            Assert.AreEqual(1, harness.PendingSamples.Count);
            harness.PendingSamples.Clear();

            harness.Publish(false, false, false);
            Assert.IsFalse(harness.Frames.HasObservers);
            harness.Frames.OnNext(new[] { "12", "16", "1012" });

            Assert.AreEqual(0, harness.PendingSamples.Count);
            harness.Publish(false, false, false, 80d, 12.5d, 25d, "Vulkan");
            AssertSharedValues(harness, 80d, 12.5d, 25d, "Vulkan");
        }

        [TestMethod]
        public void NumericOnlyEntries_UseSharedValuesWithoutSubscribingToFrames()
        {
            using var harness = new BridgeHarness();
            harness.Publish(false, false, false);
            Assert.IsFalse(harness.Frames.HasObservers);
            AssertSharedValues(harness, 100d, 10d, 20d, "DXGI");

            harness.Frames.OnNext(new[] { "8", "10", "1000" });
            harness.Frames.OnNext(new[] { "12", "20", "1012" });
            Assert.AreEqual(0, harness.PendingSamples.Count);

            harness.Publish(false, false, false, 50d, 20d, 40d, "Vulkan");
            AssertSharedValues(harness, 50d, 20d, 40d, "Vulkan");
        }

        [TestMethod]
        [DataRow(false, true)]
        [DataRow(true, false)]
        public void HiddenOrUnavailableFpsEntry_DoesNotSubscribeToFrames(bool enabled, bool visible)
        {
            using var harness = new BridgeHarness();
            harness.PublishEntries(new[] { new OverlayEntryWrapper("Framerate")
            {
                IsEntryEnabled = enabled, ShowOnOverlay = visible, ShowGraph = true
            } });

            Assert.IsFalse(harness.Frames.HasObservers);
            Assert.AreEqual(0, harness.PendingSamples.Count);
        }

        [TestMethod]
        public void EmptyProcessList_HidesTheOverlayEvenWhileItIsActive()
        {
            var processCount = new Subject<int>();
            using var harness = new BridgeHarness(processCount);
            harness.SetActive(true);
            harness.Publish(true, false, false);
            Assert.IsFalse(harness.Frames.HasObservers, "Nothing is detected yet.");
            Assert.IsTrue(harness.IsHidden);

            processCount.OnNext(1);
            Assert.IsTrue(harness.Frames.HasObservers);
            Assert.IsFalse(harness.IsHidden);

            // Several detected processes publish no target PID but still count as a filled list.
            processCount.OnNext(2);
            Assert.IsFalse(harness.IsHidden);

            processCount.OnNext(0);
            Assert.IsFalse(harness.Frames.HasObservers);
            Assert.IsTrue(harness.IsHidden);
        }

        [TestMethod]
        public void OverlayHotkey_HidesButCannotShowWhileTheProcessListIsEmpty()
        {
            var processCount = new Subject<int>();
            using var harness = new BridgeHarness(processCount);
            harness.SetActive(true);
            processCount.OnNext(1);
            Assert.IsFalse(harness.IsHidden);

            harness.SetActive(false);
            Assert.IsTrue(harness.IsHidden, "The hotkey still hides a visible overlay.");

            processCount.OnNext(0);
            harness.SetActive(true);
            Assert.IsTrue(harness.IsHidden, "The hotkey must not show it without a process.");

            processCount.OnNext(1);
            Assert.IsFalse(harness.IsHidden, "The toggled state applies once a process appears.");
        }

        [TestMethod]
        public void EntriesWhileTheOverlayIsSwitchedOff_DoNotReachTheRenderer()
        {
            using var harness = new BridgeHarness();
            harness.SetActive(true);
            harness.Publish(true, false, false);
            Assert.IsNotNull(harness.PendingEntries);
            Assert.IsTrue(harness.Frames.HasObservers);
            harness.ClearPendingEntries();

            // A remote API client keeps the entries flowing while the overlay is switched off.
            harness.SetActive(false);
            harness.Publish(true, false, false);
            Assert.IsNull(harness.PendingEntries, "The switched-off renderer must not receive entries.");
            Assert.IsFalse(harness.Frames.HasObservers);

            harness.SetActive(true);
            harness.Publish(true, false, false);
            Assert.IsNotNull(harness.PendingEntries);
            Assert.IsTrue(harness.Frames.HasObservers);
        }

        private static void AssertSharedValues(BridgeHarness harness, double fps, double ft,
            double displayTime, string group)
        {
            var entries = (IList)harness.PendingEntries;
            Assert.AreEqual(3, entries.Count);
            double[] values = { fps, ft, displayTime };
            for (int i = 0; i < values.Length; i++)
            {
                var entry = ReadField<object>(entries[i], "Entry");
                Assert.AreEqual(values[i], ReadField<double>(entry, "ValueNum"), 1e-9,
                    "Shared scalar values must reach the renderer without another aggregation.");
                Assert.AreEqual(group, ReadField<string>(entry, "Group"));
            }
        }

        // Keep the real bridge, adapter and OsdHost queue in this regression. Simulate only the
        // worker's running state so no overlay window, native DLL or render thread is needed.
        private sealed class BridgeHarness : IDisposable
        {
            private readonly Subject<bool> _active = new Subject<bool>();
            private readonly Subject<IOverlayEntry[]> _entries = new Subject<IOverlayEntry[]>();
            private readonly Subject<(string key, object value)> _configChanges =
                new Subject<(string key, object value)>();
            private readonly Mock<IOverlayService> _overlay = new Mock<IOverlayService>();
            private readonly object _host;

            public Subject<string[]> Frames { get; } = new Subject<string[]>();
            public OsdOverlayBridge Bridge { get; }
            public IList PendingSamples { get; }

            public bool IsHidden => ReadField<bool>(_host, "_hidden");

            public object PendingEntries => ReadField<object>(_host, "_pendingEntries");

            public void ClearPendingEntries() => SetField(_host, "_pendingEntries", null);

            public BridgeHarness(IObservable<int> processCount = null)
            {
                var config = new Mock<IAppConfiguration>();
                config.SetupGet(x => x.EnableHookFreeOverlay).Returns(true);
                config.SetupGet(x => x.OnValueChanged).Returns(_configChanges);
                _overlay.SetupGet(x => x.IsOverlayActiveStream).Returns(_active);
                _overlay.SetupGet(x => x.OnDictionaryUpdated).Returns(_entries);
                Bridge = new OsdOverlayBridge(_overlay.Object, config.Object, Frames,
                    frametimeColumnIndex: 0, displayChangedColumnIndex: 1,
                    startTimeIndexProvider: () => 2, processCountStream: processCount);
                SetField(Bridge, "_started", true);
                SetField(Bridge, "_active", true);
                _host = ReadField<object>(Bridge, "_osd");
                SetField(_host, "_running", true);
                PendingSamples = ReadField<IList>(_host, "_pendingSamples");
            }

            public void SetActive(bool active) => _active.OnNext(active);

            public void Publish(bool fps, bool ft, bool display, double fpsValue = 100d,
                double ftValue = 10d, double displayValue = 20d, string group = "DXGI")
            {
                PublishEntries(new[] { Entry("Framerate", fps, fpsValue, group),
                    Entry("Frametime", ft, ftValue, group),
                    Entry("DisplayTime", display, displayValue, group) });
            }

            public void PublishEntries(IOverlayEntry[] entries)
            {
                _overlay.SetupGet(x => x.CurrentOverlayEntries).Returns(entries);
                _entries.OnNext(entries);
            }

            public void Dispose()
            {
                Bridge.Dispose();
                Frames.Dispose();
                _active.Dispose();
                _entries.Dispose();
                _configChanges.Dispose();
            }

            private static IOverlayEntry Entry(string id, bool graph, double value, string group)
                => new OverlayEntryWrapper(id)
            {
                IsEntryEnabled = true, ShowOnOverlay = true, ShowGraph = graph, IsNumeric = true,
                Value = value, GroupName = group
            };
        }

        private static T ReadField<T>(object target, string name) =>
            (T)Field(target, name).GetValue(target);

        private static void SetField(object target, string name, object value) =>
            Field(target, name).SetValue(target, value);

        private static FieldInfo Field(object target, string name) => target.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Missing field: {name}");
    }
}
