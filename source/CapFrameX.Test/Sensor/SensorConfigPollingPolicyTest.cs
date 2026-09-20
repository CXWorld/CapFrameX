using System;
using System.IO;
using CapFrameX.Sensor;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Sensor
{
    [TestClass]
    public class SensorConfigPollingPolicyTest
    {
        private string _configurationDirectory;

        [TestInitialize]
        public void Setup()
        {
            _configurationDirectory = Path.Combine(
                Path.GetTempPath(),
                "CapFrameX-SensorConfigTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_configurationDirectory);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_configurationDirectory))
                Directory.Delete(_configurationDirectory, recursive: true);
        }

        [TestMethod]
        public void LoggingSelectionIsEvaluatedOnlyForCaptureOrActiveSensorWebsocket()
        {
            var config = new SensorConfig(_configurationDirectory);
            const string identifier = "/gpu-nvidia/0/load/0";
            config.SelectForLogging(identifier, true);

            Assert.IsTrue(config.GetSensorEvaluate(identifier),
                "The first evaluation is retained for sensor discovery.");
            Assert.IsFalse(config.GetSensorEvaluate(identifier),
                "A saved capture selection must not poll while no capture is running.");

            config.IsCapturing = true;
            Assert.IsFalse(config.GetSensorEvaluate(identifier),
                "Capture state alone must not poll sensors when sensor logging is disabled.");

            config.IsSensorLoggingActive = true;
            Assert.IsTrue(config.GetSensorEvaluate(identifier));

            config.IsSensorLoggingActive = false;
            config.WsActiveSensorsEnabled = true;
            Assert.IsTrue(config.GetSensorEvaluate(identifier));

            config.WsActiveSensorsEnabled = false;
            Assert.IsFalse(config.GetSensorEvaluate(identifier));
        }

        [TestMethod]
        public void AllSensorsWebsocketForcesEvaluation()
        {
            var config = new SensorConfig(_configurationDirectory);
            const string identifier = "/amdcpu/0/power/0";

            Assert.IsTrue(config.GetSensorEvaluate(identifier));
            Assert.IsFalse(config.GetSensorEvaluate(identifier));

            config.WsSensorsEnabled = true;
            Assert.IsTrue(config.GetSensorEvaluate(identifier));
        }

        [TestMethod]
        public void OverlaySensorStateIgnoresSyntheticRowsAndTracksHardwareRows()
        {
            var config = new SensorConfig(_configurationDirectory);

            config.SelectForOverlay("OnlineAverage", true);
            Assert.IsFalse(config.HasSelectedOverlaySensors);

            config.SelectForOverlay("/gpu-nvidia/0/load/0", true);
            config.SelectForOverlay("/gpu-nvidia/0/load/0", true);
            Assert.IsTrue(config.HasSelectedOverlaySensors);

            config.SelectForOverlay("/gpu-nvidia/0/load/0", false);
            Assert.IsFalse(config.HasSelectedOverlaySensors);

            config.SelectForOverlay("pmcreader/cpu/dram-bandwidth", true);
            Assert.IsTrue(config.HasSelectedOverlaySensors);
            Assert.IsTrue(config.HasSelectedPmcOverlaySensors);
            config.SelectForOverlay("pmcreader/cpu/dram-bandwidth", false);
            Assert.IsFalse(config.HasSelectedOverlaySensors);
            Assert.IsFalse(config.HasSelectedPmcOverlaySensors);

            config.SelectForOverlay("/amdcpu/0/load/0", true);
            config.ResetEvaluate();
            Assert.IsFalse(config.HasSelectedOverlaySensors);
            Assert.IsFalse(config.HasSelectedPmcOverlaySensors);
        }

        [TestMethod]
        public void PmcLoggingStateTracksOnlySelectedPmcEntries()
        {
            var config = new SensorConfig(_configurationDirectory);

            config.SelectForLogging("/amdcpu/0/load/0", true);
            Assert.IsFalse(config.HasSelectedPmcLoggingSensors);

            config.SelectForLogging("pmcreader/cpu/l3-hitrate", true);
            Assert.IsTrue(config.HasSelectedPmcLoggingSensors);

            config.SelectForLogging("pmcreader/cpu/l3-hitrate", false);
            Assert.IsFalse(config.HasSelectedPmcLoggingSensors);
        }

        [TestMethod]
        public void SensorIntervalUsesLoggingRateOnlyDuringSensorCapture()
        {
            var loggingInterval = TimeSpan.FromMilliseconds(500);
            var osdInterval = TimeSpan.FromMilliseconds(1000);

            Assert.AreEqual(osdInterval, SensorService.SelectSensorTimespan(
                loggingInterval, osdInterval, isLoggingActive: false, useSensorLogging: true));
            Assert.AreEqual(osdInterval, SensorService.SelectSensorTimespan(
                loggingInterval, osdInterval, isLoggingActive: true, useSensorLogging: false));
            Assert.AreEqual(loggingInterval, SensorService.SelectSensorTimespan(
                loggingInterval, osdInterval, isLoggingActive: true, useSensorLogging: true));
        }

        [TestMethod]
        public void PmcPollingRequiresAnActivePmcConsumer()
        {
            Assert.IsFalse(SelectPmcPolling(
                overlayActive: true,
                hasOverlayPmc: false,
                loggingActive: true,
                useSensorLogging: true,
                hasLoggingPmc: false));

            Assert.IsTrue(SelectPmcPolling(
                overlayActive: true,
                hasOverlayPmc: true,
                loggingActive: false,
                useSensorLogging: true,
                hasLoggingPmc: false));

            Assert.IsTrue(SelectPmcPolling(
                overlayActive: false,
                hasOverlayPmc: false,
                loggingActive: true,
                useSensorLogging: true,
                hasLoggingPmc: true));
        }

        private static bool SelectPmcPolling(
            bool overlayActive,
            bool hasOverlayPmc,
            bool loggingActive,
            bool useSensorLogging,
            bool hasLoggingPmc)
        {
            return SensorService.SelectPmcReaderPollingState(
                overlayActive,
                hasOverlayPmc,
                loggingActive,
                useSensorLogging,
                hasLoggingPmc,
                websocketActive: false,
                websocketAllSensors: false,
                websocketActiveSensors: false,
                evaluateAllSensors: false);
        }
    }
}
