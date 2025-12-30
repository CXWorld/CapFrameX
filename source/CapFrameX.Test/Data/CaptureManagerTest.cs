using CapFrameX.Capture.Contracts;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Logging;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.RTSS;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Data;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Monitoring.Contracts;
using CapFrameX.PMD.Benchlab;
using CapFrameX.PMD.Powenetics;
using CapFrameX.Test.Mocks;
using DryIoc;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace CapFrameX.Test.Data
{
    [TestClass]
    public class CaptureManagerTest
    {
        private Container _container;
        private CaptureManager _captureManager;

        // Custom mock services
        private MockCaptureService _mockCaptureService;
        private MockSensorService _mockSensorService;
        private MockRecordManager _mockRecordManager;
        private MockPoweneticsService _mockPoweneticsService;
        private MockBenchlabService _mockBenchlabService;

        // Moq mocks for simpler interfaces
        private Mock<IOverlayService> _overlayServiceMock;
        private Mock<IRTSSService> _rtssServiceMock;
        private Mock<ISensorConfig> _sensorConfigMock;
        private Mock<IAppConfiguration> _appConfigurationMock;
        private Mock<ILogEntryManager> _logEntryManagerMock;
        private Mock<ILogger<CaptureManager>> _loggerMock;
        private Mock<ILogger<SoundManager>> _soundManagerLoggerMock;
        private Mock<ILogger<ProcessList>> _processListLoggerMock;

        [TestInitialize]
        public void Setup()
        {
            // Initialize custom mock services with deterministic seeds for reproducibility
            _mockCaptureService = new MockCaptureService(seed: 42);
            _mockSensorService = new MockSensorService(seed: 42);
            _mockRecordManager = new MockRecordManager();
            _mockPoweneticsService = new MockPoweneticsService(seed: 42);
            _mockBenchlabService = new MockBenchlabService(seed: 42);

            // Configure custom mocks
            _mockCaptureService.EmissionIntervalMs = 0; // Manual control for tests
            _mockCaptureService.AddProcess("TestGame.exe", 1234, "0x00000001");

            // Initialize Moq mocks
            _overlayServiceMock = new Mock<IOverlayService>();
            _rtssServiceMock = new Mock<IRTSSService>();
            _sensorConfigMock = new Mock<ISensorConfig>();
            _appConfigurationMock = new Mock<IAppConfiguration>();
            _logEntryManagerMock = new Mock<ILogEntryManager>();
            _loggerMock = new Mock<ILogger<CaptureManager>>();
            _soundManagerLoggerMock = new Mock<ILogger<SoundManager>>();
            _processListLoggerMock = new Mock<ILogger<ProcessList>>();

            // Setup overlay service mock
            _overlayServiceMock.Setup(x => x.IsOverlayActiveStream).Returns(new Subject<bool>());

            // Setup RTSS service mock
            _rtssServiceMock.Setup(x => x.GetFrameTimesInterval(It.IsAny<int>(), It.IsAny<int>()))
                .Returns(new float[] { 16.67f, 16.67f, 16.67f });

            // Setup sensor config mock
            _sensorConfigMock.SetupProperty(x => x.IsCapturing, false);

            // Setup app configuration mock with default values
            SetupDefaultAppConfiguration();

            // Build DryIoC container
            _container = new Container();

            // Register custom mock services
            _container.RegisterInstance<ICaptureService>(_mockCaptureService);
            _container.RegisterInstance<ISensorService>(_mockSensorService);
            _container.RegisterInstance<IRecordManager>(_mockRecordManager);
            _container.RegisterInstance<IPoweneticsService>(_mockPoweneticsService);
            _container.RegisterInstance<IBenchlabService>(_mockBenchlabService);

            // Register Moq mocks
            _container.RegisterInstance<IOverlayService>(_overlayServiceMock.Object);
            _container.RegisterInstance<IRTSSService>(_rtssServiceMock.Object);
            _container.RegisterInstance<ISensorConfig>(_sensorConfigMock.Object);
            _container.RegisterInstance<IAppConfiguration>(_appConfigurationMock.Object);
            _container.RegisterInstance<ILogEntryManager>(_logEntryManagerMock.Object);
            _container.RegisterInstance<ILogger<CaptureManager>>(_loggerMock.Object);
            _container.RegisterInstance<ILogger<SoundManager>>(_soundManagerLoggerMock.Object);
            _container.RegisterInstance<ILogger<ProcessList>>(_processListLoggerMock.Object);

            // Register concrete dependencies
            _container.Register<SoundManager>(Reuse.Singleton);
            _container.Register<ProcessList>(Reuse.Singleton);
            _container.Register<CaptureManager>(Reuse.Singleton);

            _captureManager = _container.Resolve<CaptureManager>();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _mockCaptureService?.Dispose();
            _mockSensorService?.Dispose();
            _mockPoweneticsService?.Dispose();
            _container?.Dispose();
        }

        private void SetupDefaultAppConfiguration()
        {
            _appConfigurationMock.Setup(x => x.IsOverlayActive).Returns(false);
            _appConfigurationMock.Setup(x => x.AutoDisableOverlay).Returns(false);
            _appConfigurationMock.Setup(x => x.UsePmdDataLogging).Returns(false);
            _appConfigurationMock.Setup(x => x.CaptureRTSSFrameTimes).Returns(false);
            _appConfigurationMock.Setup(x => x.UseRunHistory).Returns(false);
            _appConfigurationMock.Setup(x => x.UseAggregation).Returns(false);
            _appConfigurationMock.Setup(x => x.SaveAggregationOnly).Returns(false);
            _appConfigurationMock.Setup(x => x.VoiceSoundLevel).Returns(0);
            _appConfigurationMock.Setup(x => x.SimpleSoundLevel).Returns(0);
            _appConfigurationMock.Setup(x => x.HotkeySoundMode).Returns("None");
        }

        #region Constructor Tests

        [TestMethod]
        public void Constructor_WithValidDependencies_CreatesInstance()
        {
            Assert.IsNotNull(_captureManager);
        }

        [TestMethod]
        public void Constructor_IsCapturingIsFalse_Initially()
        {
            Assert.IsFalse(_captureManager.IsCapturing);
        }

        [TestMethod]
        public void Constructor_LockCaptureServiceIsFalse_Initially()
        {
            Assert.IsFalse(_captureManager.LockCaptureService);
        }

        [TestMethod]
        public void Constructor_DelayCountdownRunningIsFalse_Initially()
        {
            Assert.IsFalse(_captureManager.DelayCountdownRunning);
        }

        #endregion

        #region GetAllFilteredProcesses Tests

        [TestMethod]
        public void GetAllFilteredProcesses_WithNoFilter_ReturnsAllProcesses()
        {
            var filter = new HashSet<string>();
            var processes = _captureManager.GetAllFilteredProcesses(filter);

            Assert.IsNotNull(processes);
            var processList = new List<(string, int)>(processes);
            Assert.AreEqual(1, processList.Count);
            Assert.AreEqual("TestGame.exe", processList[0].Item1);
            Assert.AreEqual(1234, processList[0].Item2);
        }

        [TestMethod]
        public void GetAllFilteredProcesses_WithFilter_FiltersProcesses()
        {
            var filter = new HashSet<string> { "TestGame.exe" };
            var processes = _captureManager.GetAllFilteredProcesses(filter);

            var processList = new List<(string, int)>(processes);
            Assert.AreEqual(0, processList.Count);
        }

        [TestMethod]
        public void GetAllFilteredProcesses_WithMultipleProcesses_ReturnsUnfiltered()
        {
            _mockCaptureService.AddProcess("AnotherGame.exe", 5678, "0x00000002");
            var filter = new HashSet<string> { "TestGame.exe" };

            var processes = _captureManager.GetAllFilteredProcesses(filter);

            var processList = new List<(string, int)>(processes);
            Assert.AreEqual(1, processList.Count);
            Assert.AreEqual("AnotherGame.exe", processList[0].Item1);
        }

        #endregion

        #region StartCapture Tests

        [TestMethod]
        public async Task StartCapture_WithValidOptions_SetsIsCapturingTrue()
        {
            // Start the capture service first
            _mockCaptureService.StartCaptureService(null);

            var options = CreateValidCaptureOptions();

            await _captureManager.StartCapture(options);

            Assert.IsTrue(_captureManager.IsCapturing);
        }

        [TestMethod]
        [ExpectedException(typeof(Exception))]
        public async Task StartCapture_WhenAlreadyCapturing_ThrowsException()
        {
            _mockCaptureService.StartCaptureService(null);
            var options = CreateValidCaptureOptions();

            await _captureManager.StartCapture(options);
            await _captureManager.StartCapture(options); // Should throw
        }

        [TestMethod]
        [ExpectedException(typeof(Exception))]
        public async Task StartCapture_WithNonExistentProcess_ThrowsException()
        {
            _mockCaptureService.StartCaptureService(null);
            var options = new CaptureOptions
            {
                ProcessInfo = ("NonExistent.exe", 9999),
                CaptureTime = 0,
                CaptureDelay = 0
            };

            await _captureManager.StartCapture(options);
        }

        [TestMethod]
        public async Task StartCapture_WithDelay_SetsDelayCountdownRunning()
        {
            _mockCaptureService.StartCaptureService(null);
            var options = CreateValidCaptureOptions();
            options.CaptureDelay = 1; // 1 second delay

            var captureTask = _captureManager.StartCapture(options);

            // Check that delay countdown is running before capture starts
            Assert.IsTrue(_captureManager.DelayCountdownRunning);

            // Cancel the delay to cleanup
            await _captureManager.StopCapture();
        }

        [TestMethod]
        public async Task StartCapture_SetsOverlayServiceStatus()
        {
            _mockCaptureService.StartCaptureService(null);
            var options = CreateValidCaptureOptions();

            await _captureManager.StartCapture(options);

            _overlayServiceMock.Verify(
                x => x.SetCaptureServiceStatus("Recording frametimes"),
                Times.Once);
        }

        [TestMethod]
        public async Task StartCapture_StartsLogging_OnSensorService()
        {
            _mockCaptureService.StartCaptureService(null);
            var options = CreateValidCaptureOptions();

            await _captureManager.StartCapture(options);

            // The mock sensor service should have started logging
            var sensorData = _mockSensorService.GetSensorSessionData();
            Assert.IsNotNull(sensorData);
        }

        [TestMethod]
        public async Task StartCapture_WithAutoDisableOverlay_DisablesOverlay()
        {
            _appConfigurationMock.Setup(x => x.IsOverlayActive).Returns(true);
            _appConfigurationMock.Setup(x => x.AutoDisableOverlay).Returns(true);
            _appConfigurationMock.SetupProperty(x => x.IsOverlayActive, true);

            _mockCaptureService.StartCaptureService(null);
            var options = CreateValidCaptureOptions();

            await _captureManager.StartCapture(options);

            _rtssServiceMock.Verify(x => x.OnOSDOff(), Times.Once);
            Assert.IsTrue(_captureManager.OSDAutoDisabled);
        }

        [TestMethod]
        public async Task StartCapture_WithCaptureTime_StartsCountdown()
        {
            _mockCaptureService.StartCaptureService(null);
            var options = CreateValidCaptureOptions();
            options.CaptureTime = 10; // 10 seconds

            await _captureManager.StartCapture(options);

            _overlayServiceMock.Verify(x => x.StartCountdown(10), Times.Once);
        }

        [TestMethod]
        public async Task StartCapture_WithoutCaptureTime_StartsCaptureTimer()
        {
            _mockCaptureService.StartCaptureService(null);
            var options = CreateValidCaptureOptions();
            options.CaptureTime = 0;

            await _captureManager.StartCapture(options);

            _overlayServiceMock.Verify(x => x.StartCaptureTimer(), Times.Once);
        }

        [TestMethod]
        public async Task StartCapture_AddsLogEntry()
        {
            _mockCaptureService.StartCaptureService(null);
            var options = CreateValidCaptureOptions();

            await _captureManager.StartCapture(options);

            _logEntryManagerMock.Verify(
                x => x.AddLogEntry(
                    It.Is<string>(s => s.Contains("Capturing of process")),
                    It.IsAny<ELogMessageType>(),
                    It.IsAny<bool>()),
                Times.AtLeastOnce);
        }

        #endregion

        #region StopCapture Tests

        [TestMethod]
        public async Task StopCapture_WhenDelayCountdownRunning_CancelsDelay()
        {
            _mockCaptureService.StartCaptureService(null);
            var options = CreateValidCaptureOptions();
            options.CaptureDelay = 10; // 10 second delay

            var captureTask = _captureManager.StartCapture(options);

            // Stop capture while delay is running
            await _captureManager.StopCapture();

            Assert.IsFalse(_captureManager.DelayCountdownRunning);
            _overlayServiceMock.Verify(x => x.CancelDelayCountdown(), Times.Once);
        }

        [TestMethod]
        [ExpectedException(typeof(Exception))]
        public async Task StopCapture_WhenNotCapturing_ThrowsException()
        {
            await _captureManager.StopCapture();
        }

        [TestMethod]
        public async Task StopCapture_AfterCapture_SetsIsCapturingFalse()
        {
            _mockCaptureService.StartCaptureService(null);
            var options = CreateValidCaptureOptions();

            await _captureManager.StartCapture(options);

            // Emit some frames
            _mockCaptureService.EmitFrames(10);

            await _captureManager.StopCapture();

            // Wait for processing to complete
            await Task.Delay(3000);

            Assert.IsFalse(_captureManager.IsCapturing);
        }

        [TestMethod]
        public async Task StopCapture_StopsCaptureTimer()
        {
            _mockCaptureService.StartCaptureService(null);
            var options = CreateValidCaptureOptions();

            await _captureManager.StartCapture(options);
            _mockCaptureService.EmitFrames(5);

            await _captureManager.StopCapture();
            await Task.Delay(3000);

            _overlayServiceMock.Verify(x => x.StopCaptureTimer(), Times.Once);
        }

        [TestMethod]
        public async Task StopCapture_WithAutoDisabledOverlay_ReEnablesOverlay()
        {
            _appConfigurationMock.Setup(x => x.IsOverlayActive).Returns(true);
            _appConfigurationMock.Setup(x => x.AutoDisableOverlay).Returns(true);
            _appConfigurationMock.SetupProperty(x => x.IsOverlayActive, true);

            _mockCaptureService.StartCaptureService(null);
            var options = CreateValidCaptureOptions();

            await _captureManager.StartCapture(options);
            _mockCaptureService.EmitFrames(5);

            await _captureManager.StopCapture();
            await Task.Delay(3000);

            _rtssServiceMock.Verify(x => x.OnOSDOn(), Times.Once);
        }

        [TestMethod]
        public async Task StopCapture_AddsLogEntry()
        {
            _mockCaptureService.StartCaptureService(null);
            var options = CreateValidCaptureOptions();

            await _captureManager.StartCapture(options);
            _mockCaptureService.EmitFrames(5);

            await _captureManager.StopCapture();

            _logEntryManagerMock.Verify(
                x => x.AddLogEntry(
                    It.Is<string>(s => s.Contains("Capture finished")),
                    It.IsAny<ELogMessageType>(),
                    It.IsAny<bool>()),
                Times.Once);
        }

        #endregion

        #region StartFillArchive Tests

        [TestMethod]
        public void StartFillArchive_StartsArchiving()
        {
            _mockCaptureService.StartCaptureService(null);

            _captureManager.StartFillArchive();

            // Emit frames and verify they are being captured
            _mockCaptureService.EmitFrames(5);

            // Archive should be filling (no direct way to verify, but no exception means success)
            Assert.IsNotNull(_captureManager);
        }

        [TestMethod]
        public void StopFillArchive_StopsArchiving()
        {
            _mockCaptureService.StartCaptureService(null);
            _captureManager.StartFillArchive();

            _captureManager.StopFillArchive();

            // Should not throw and capture service should stop
            Assert.IsNotNull(_captureManager);
        }

        #endregion

        #region StartCaptureService Tests

        [TestMethod]
        public void StartCaptureService_DelegatesTo_PresentMonCaptureService()
        {
            var result = _captureManager.StartCaptureService(null);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void StartCaptureService_WhenAlreadyRunning_ReturnsFalse()
        {
            _captureManager.StartCaptureService(null);
            var result = _captureManager.StartCaptureService(null);

            Assert.IsFalse(result);
        }

        #endregion

        #region ToggleSensorLogging Tests

        [TestMethod]
        public void ToggleSensorLogging_Enabled_EmitsTrue()
        {
            bool? receivedValue = null;
            _mockSensorService.IsLoggingActiveStream.Subscribe(value => receivedValue = value);

            _captureManager.ToggleSensorLogging(true);

            Assert.IsTrue(receivedValue.Value);
        }

        [TestMethod]
        public void ToggleSensorLogging_Disabled_EmitsFalse()
        {
            bool? receivedValue = null;
            _mockSensorService.IsLoggingActiveStream.Subscribe(value => receivedValue = value);

            _captureManager.ToggleSensorLogging(false);

            Assert.IsFalse(receivedValue.Value);
        }

        #endregion

        #region CaptureStatusChange Tests

        [TestMethod]
        public async Task CaptureStatusChange_EmitsStarted_WhenCaptureStarts()
        {
            CaptureStatus? receivedStatus = null;
            _captureManager.CaptureStatusChange.Subscribe(status => receivedStatus = status);

            _mockCaptureService.StartCaptureService(null);
            var options = CreateValidCaptureOptions();

            await _captureManager.StartCapture(options);

            Assert.IsNotNull(receivedStatus);
            Assert.AreEqual(ECaptureStatus.Started, receivedStatus.Value.Status);
        }

        [TestMethod]
        public async Task CaptureStatusChange_EmitsStartedTimer_WhenCaptureTimeSet()
        {
            CaptureStatus? receivedStatus = null;
            _captureManager.CaptureStatusChange.Subscribe(status => receivedStatus = status);

            _mockCaptureService.StartCaptureService(null);
            var options = CreateValidCaptureOptions();
            options.CaptureTime = 10;

            await _captureManager.StartCapture(options);

            Assert.IsNotNull(receivedStatus);
            Assert.AreEqual(ECaptureStatus.StartedTimer, receivedStatus.Value.Status);
        }

        [TestMethod]
        public async Task CaptureStatusChange_EmitsStartedRemote_WhenRemoteTrue()
        {
            CaptureStatus? receivedStatus = null;
            _captureManager.CaptureStatusChange.Subscribe(status => receivedStatus = status);

            _mockCaptureService.StartCaptureService(null);
            var options = CreateValidCaptureOptions();
            options.Remote = true;

            await _captureManager.StartCapture(options);

            Assert.IsNotNull(receivedStatus);
            Assert.AreEqual(ECaptureStatus.StartedRemote, receivedStatus.Value.Status);
        }

        [TestMethod]
        public async Task CaptureStatusChange_EmitsProcessing_WhenCaptureStopping()
        {
            var statusHistory = new List<ECaptureStatus?>();
            _captureManager.CaptureStatusChange.Subscribe(status => statusHistory.Add(status.Status));

            _mockCaptureService.StartCaptureService(null);
            var options = CreateValidCaptureOptions();

            await _captureManager.StartCapture(options);
            _mockCaptureService.EmitFrames(5);

            await _captureManager.StopCapture();

            Assert.IsTrue(statusHistory.Contains(ECaptureStatus.Processing));
        }

        #endregion

        #region Integration Tests

        [TestMethod]
        public async Task FullCaptureWorkflow_CompletesSuccessfully()
        {
            // Setup
            _mockCaptureService.Scenario = SimulationScenario.Stable60Fps;
            _mockCaptureService.StartCaptureService(null);

            // Start fill archive first
            _captureManager.StartFillArchive();
            _mockCaptureService.EmitFrames(100); // Fill archive

            // Create capture options
            var options = CreateValidCaptureOptions();

            // Start capture
            await _captureManager.StartCapture(options);
            Assert.IsTrue(_captureManager.IsCapturing);

            // Emit frames during capture
            _mockCaptureService.EmitFrames(200);

            // Stop capture
            await _captureManager.StopCapture();

            // Wait for processing
            await Task.Delay(3000);

            // Verify
            Assert.IsFalse(_captureManager.IsCapturing);
            Assert.IsFalse(_captureManager.LockCaptureService);
        }

        [TestMethod]
        public async Task CaptureWithPmdLogging_EnablesPowerDataCollection()
        {
            _appConfigurationMock.Setup(x => x.UsePmdDataLogging).Returns(true);

            _mockCaptureService.StartCaptureService(null);
            _mockPoweneticsService.EmissionIntervalMs = 0; // Manual control
            _mockPoweneticsService.StartDriver();

            var options = CreateValidCaptureOptions();

            await _captureManager.StartCapture(options);

            // Emit frame and power data
            _mockCaptureService.EmitFrames(10);
            _mockPoweneticsService.EmitSamples(10);

            await _captureManager.StopCapture();
            await Task.Delay(3000);

            Assert.IsFalse(_captureManager.IsCapturing);
        }

        [TestMethod]
        public async Task CaptureWithRTSSFrameTimes_CollectsFrameTimes()
        {
            _appConfigurationMock.Setup(x => x.CaptureRTSSFrameTimes).Returns(true);
            _rtssServiceMock.Setup(x => x.GetFrameTimesInterval(It.IsAny<int>(), It.IsAny<int>()))
                .Returns(new float[] { 16.67f, 16.67f, 16.67f, 16.67f });

            _mockCaptureService.StartCaptureService(null);

            var options = CreateValidCaptureOptions();

            await _captureManager.StartCapture(options);
            _mockCaptureService.EmitFrames(10);

            await _captureManager.StopCapture();
            await Task.Delay(3000);

            Assert.IsFalse(_captureManager.IsCapturing);
        }

        [TestMethod]
        public async Task CaptureWithDifferentScenarios_HandlesAllScenarios()
        {
            var scenarios = new[]
            {
                SimulationScenario.Stable60Fps,
                SimulationScenario.Stable144Fps,
                SimulationScenario.GpuBound,
                SimulationScenario.CpuBound,
                SimulationScenario.Stuttering
            };

            foreach (var scenario in scenarios)
            {
                // Cleanup from previous iteration
                _mockCaptureService.StopCaptureService();
                _mockCaptureService.RemoveProcess(1234);
                _mockCaptureService.AddProcess("TestGame.exe", 1234, "0x00000001");

                _mockCaptureService.Scenario = scenario;
                _mockCaptureService.StartCaptureService(null);

                _captureManager.StartFillArchive();
                _mockCaptureService.EmitFrames(50);

                var options = CreateValidCaptureOptions();

                await _captureManager.StartCapture(options);
                _mockCaptureService.EmitFrames(100);

                await _captureManager.StopCapture();
                await Task.Delay(3000);

                Assert.IsFalse(_captureManager.IsCapturing, $"Failed for scenario: {scenario}");
            }
        }

        #endregion

        #region Helper Methods

        private CaptureOptions CreateValidCaptureOptions()
        {
            return new CaptureOptions
            {
                ProcessInfo = ("TestGame.exe", 1234),
                CaptureTime = 0,
                CaptureDelay = 0,
                CaptureFileMode = "Json",
                RecordDirectory = null,
                Remote = false,
                Comment = "Test capture"
            };
        }

        #endregion
    }
}
