using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using CapFrameX.Capture.Contracts;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.PMD;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Overlay;
using CapFrameX.PMD.Benchlab;
using CapFrameX.PMD.Powenetics;
using CapFrameX.PresentMonInterface;
using CapFrameX.Statistics.NetStandard.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Prism.Events;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class OnlineMetricTelemetryTest
    {
        [TestMethod]
        public void TelemetryDemandComposesWithoutChangingClassicEntries()
        {
            using var service = CreateService();
            Assert.IsFalse(Evaluate(service, "EvaluateRealtimeMetrics"));
            Assert.IsFalse(Evaluate(service, "EvaluatePmdMetrics"));
            var first = service.AcquireTelemetry();
            var second = service.AcquireTelemetry();
            first.Dispose();
            first.Dispose();
            Assert.IsTrue(Evaluate(service, "EvaluateRealtimeMetrics"));
            Assert.IsTrue(Evaluate(service, "EvaluatePmdMetrics"));
            second.Dispose();
            Assert.IsFalse(Evaluate(service, "EvaluateRealtimeMetrics"));
            Assert.IsFalse(Evaluate(service, "EvaluatePmdMetrics"));
        }

        [TestMethod]
        public void PmdPreviewReadsDoNotConsumeClassicSamplesAndReturnedSnapshotIsDetached()
        {
            using var service = CreateService();
            Assert.IsNull(service.GetPmdTelemetrySnapshot());
            var samples = new List<SensorSample>
            {
                new SensorSample
                {
                    Sensors = new List<CapFrameX.PMD.Benchlab.Sensor>
                    {
                        new CapFrameX.PMD.Benchlab.Sensor { Value = 220 },
                        new CapFrameX.PMD.Benchlab.Sensor { Value = 80 },
                        new CapFrameX.PMD.Benchlab.Sensor { Value = 340 }
                    }
                }
            };
            typeof(OnlineMetricService).GetMethod("UpdatePmdMetrics", BindingFlags.NonPublic | BindingFlags.Instance,
                null, new[] { typeof(IList<SensorSample>) }, null).Invoke(service, new object[] { samples });

            var preview = service.GetPmdTelemetrySnapshot();
            Assert.AreEqual(220d, preview.GpuPowerCurrent);
            preview.GpuPowerCurrent = -1;
            Assert.AreEqual(220d, service.GetPmdTelemetrySnapshot().GpuPowerCurrent);
            var classic = service.GetPmdMetricsPowerCurrent();
            Assert.AreEqual(220d, classic.GpuPowerCurrent);
            Assert.AreEqual(80d, classic.CpuPowerCurrent);
            Assert.AreEqual(340d, classic.SystemPowerCurrent);
            Assert.AreEqual(220d, service.GetPmdTelemetrySnapshot().GpuPowerCurrent,
                "The classic consumer also must not clear the preview snapshot.");
        }

        [TestMethod]
        public void SameExecutableWithNewProcessIdInvalidatesOldFrameMeasurements()
        {
            var events = new EventAggregator();
            using var service = CreateService(events);
            events.GetEvent<PubSubEvent<ViewMessages.CurrentProcessToCapture>>()
                .Publish(new ViewMessages.CurrentProcessToCapture("game", 100));
            typeof(OnlineMetricService).GetField("_lastFrameTimestampTicks", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(service, DateTime.UtcNow.Ticks);
            events.GetEvent<PubSubEvent<ViewMessages.CurrentProcessToCapture>>()
                .Publish(new ViewMessages.CurrentProcessToCapture("game", 200));
            Assert.AreEqual(DateTime.MinValue, service.LastFrameTimestampUtc);
        }

        [TestMethod]
        public void ExpiredPmdSnapshotIsUnavailableAndMetricResetClearsFrameFreshness()
        {
            using var service = CreateService();
            typeof(OnlineMetricService).GetField("_latestPmdTelemetry", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(service, new OnlinePmdMetrics { TimestampUtc = DateTime.UtcNow.AddMinutes(-1) });
            Assert.IsNull(service.GetPmdTelemetrySnapshot());
            typeof(OnlineMetricService).GetField("_lastFrameTimestampTicks", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(service, DateTime.UtcNow.Ticks);
            Assert.IsTrue(service.LastFrameTimestampUtc > DateTime.MinValue);
            service.ResetRealtimeMetrics();
            Assert.AreEqual(DateTime.MinValue, service.LastFrameTimestampUtc);
        }

        [TestMethod]
        public void CurrentFrameSnapshotUsesOnlyRecentValidFramesAndDoesNotConsumeThem()
        {
            var events = new EventAggregator();
            var capture = new Mock<ICaptureService>();
            capture.SetupGet(c => c.FrameDataStream).Returns(Observable.Never<string[]>());
            capture.SetupGet(c => c.CPUStartQPCTimeInMs_Index).Returns(18);
            capture.SetupGet(c => c.CpuBusy_Index).Returns(20);
            capture.SetupGet(c => c.GpuBusy_Index).Returns(24);
            capture.SetupGet(c => c.ValidLineLength).Returns(31);
            using var service = CreateService(events, capture.Object);
            using var demand = service.AcquireTelemetry();
            events.GetEvent<PubSubEvent<ViewMessages.CurrentProcessToCapture>>()
                .Publish(new ViewMessages.CurrentProcessToCapture("game", 42));
            var update = typeof(OnlineMetricService).GetMethod("UpdateOnlineMetrics", BindingFlags.Instance | BindingFlags.NonPublic);
            Add(1000, 50, 50);
            Add(2000, 10, 5);
            Add(2200, 20, 0); // dropped presentation does not become a zero display duration
            var first = service.GetFrameTelemetrySnapshot();
            Assert.IsNotNull(first);
            Assert.AreEqual(15d, first.Frametime);
            Assert.AreEqual(1000d / 15, first.Framerate, 0.0001);
            Assert.AreEqual(5d, first.DisplayTime);
            Assert.AreEqual("game", first.ProcessName);
            Assert.AreEqual("DXGI", first.Runtime);
            Assert.AreEqual(first.Frametime, service.GetFrameTelemetrySnapshot().Frametime);
            events.GetEvent<PubSubEvent<ViewMessages.CurrentProcessToCapture>>()
                .Publish(new ViewMessages.CurrentProcessToCapture("game", 43));
            Assert.IsNull(service.GetFrameTelemetrySnapshot());

            void Add(double timestamp, double frametime, double displayTime)
            {
                var row = Enumerable.Repeat("0", 31).ToArray();
                row[0] = "game.exe"; row[1] = "42";
                row[PresentMonCaptureService.PresentRuntime_INDEX] = "DXGI";
                row[18] = timestamp.ToString(CultureInfo.InvariantCulture);
                row[PresentMonCaptureService.MsBetweenPresents_INDEX] = frametime.ToString(CultureInfo.InvariantCulture);
                row[PresentMonCaptureService.MsBetweenDisplayChange_INDEX] = displayTime.ToString(CultureInfo.InvariantCulture);
                update.Invoke(service, new object[] { row });
            }
        }

        [TestMethod]
        public void PresentModeIsTheModeOfMostOfTheTargetsPresentsWithinTheLastSecond()
        {
            const string IndependentFlip = "Hardware: Independent Flip", ComposedFlip = "Composed: Flip";
            var events = new EventAggregator();
            var capture = new Mock<ICaptureService>();
            capture.SetupGet(c => c.FrameDataStream).Returns(Observable.Never<string[]>());
            capture.SetupGet(c => c.CPUStartQPCTimeInMs_Index).Returns(18);
            capture.SetupGet(c => c.CpuBusy_Index).Returns(20);
            capture.SetupGet(c => c.GpuBusy_Index).Returns(24);
            using var service = CreateService(events, capture.Object);
            events.GetEvent<PubSubEvent<ViewMessages.CurrentProcessToCapture>>()
                .Publish(new ViewMessages.CurrentProcessToCapture("game", 42));
            var update = typeof(OnlineMetricService).GetMethod("UpdateOnlineMetrics", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNull(service.GetOnlinePresentMode(), "No present, no mode.");

            Add(1000, IndependentFlip);
            Add(1100, ComposedFlip);
            Add(1200, ComposedFlip);
            Add(1300, IndependentFlip);
            Assert.AreEqual(IndependentFlip, service.GetOnlinePresentMode(), "A tie goes to the mode of the newest present.");
            Add(1400, ComposedFlip);
            Assert.AreEqual(ComposedFlip, service.GetOnlinePresentMode());
            Add(1500, IndependentFlip, pid: "77");
            Add(1510, IndependentFlip, pid: "77");
            Assert.AreEqual(ComposedFlip, service.GetOnlinePresentMode(), "Another process' presents do not count.");

            // A second later only the composed present at 1.4 s is left of the earlier ones.
            Add(2350, IndependentFlip);
            Add(2380, IndependentFlip);
            Add(2390, "Unknown");
            Add(2391, "Unknown");
            Add(2392, "Unknown");
            Add(2393, "");
            Assert.AreEqual(IndependentFlip, service.GetOnlinePresentMode(),
                "Presents PresentMon could not classify do not outvote the ones it could.");

            events.GetEvent<PubSubEvent<ViewMessages.CurrentProcessToCapture>>()
                .Publish(new ViewMessages.CurrentProcessToCapture("game", 43));
            Assert.IsNull(service.GetOnlinePresentMode(), "A new target starts without a mode.");
            Add(5000, "Unknown", pid: "43");
            Assert.AreEqual("Unknown", service.GetOnlinePresentMode());

            typeof(OnlineMetricService).GetField("_lastFrameTimestampTicks", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(service, DateTime.UtcNow.AddSeconds(-3).Ticks);
            Assert.IsNull(service.GetOnlinePresentMode(), "A target that stopped presenting has no current mode.");

            void Add(double timestamp, string presentMode, string pid = "42")
            {
                var row = Enumerable.Repeat("0", 31).ToArray();
                row[0] = "game.exe"; row[1] = pid;
                row[PresentMonCaptureService.PresentMode_INDEX] = presentMode;
                row[18] = timestamp.ToString(CultureInfo.InvariantCulture);
                update.Invoke(service, new object[] { row });
            }
        }

        [TestMethod]
        public void ShownPresentModeEntryRequestsTheTargetsRows()
        {
            var core = new OverlayEntryCore();
            using var service = CreateService(overlayEntryCore: core);
            var entry = new OverlayEntryWrapper("OnlinePresentMode") { ShowOnOverlay = false };
            core.RealtimeMetricEntryDict[entry.Identifier] = entry;
            Assert.IsFalse(Evaluate(service, "EvaluateRealtimeMetrics"));
            entry.ShowOnOverlay = true;
            Assert.IsTrue(Evaluate(service, "EvaluateRealtimeMetrics"));
        }

        private static bool Evaluate(OnlineMetricService service, string method)
            => (bool)typeof(OnlineMetricService).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(service, null);

        private static OnlineMetricService CreateService(EventAggregator events = null, ICaptureService captureService = null,
            IOverlayEntryCore overlayEntryCore = null)
        {
            var capture = new Mock<ICaptureService>();
            capture.SetupGet(c => c.FrameDataStream).Returns(Observable.Never<string[]>());
            var powenetics = new Mock<IPoweneticsService>();
            powenetics.SetupGet(p => p.PmdStatusStream).Returns(Observable.Never<EPmdDriverStatus>());
            var benchlab = new Mock<IBenchlabService>();
            benchlab.SetupGet(p => p.PmdServiceStatusStream).Returns(Observable.Never<EPmdServiceStatus>());
            benchlab.SetupGet(p => p.GpuPowerSensorIndex).Returns(0);
            benchlab.SetupGet(p => p.CpuPowerSensorIndex).Returns(1);
            benchlab.SetupGet(p => p.SytemPowerSensorIndex).Returns(2);
            return new OnlineMetricService(Mock.Of<IStatisticProvider>(), captureService ?? capture.Object, events ?? new EventAggregator(),
                overlayEntryCore ?? Mock.Of<IOverlayEntryCore>(), powenetics.Object, benchlab.Object, Mock.Of<IAppConfiguration>());
        }
    }
}
