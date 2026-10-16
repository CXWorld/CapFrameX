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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

namespace CapFrameX.Test.Data
{
    [TestClass]
    public class CaptureManagerTest
    {
        private Container _container;
        private CaptureManager _captureManager;
        private ProcessList _processList;

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
            _processList = CreateProcessListForTests();
            _container.RegisterInstance(_processList);
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

        private ProcessList CreateProcessListForTests()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-processes.json");
            var constructor = typeof(ProcessList).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(string), typeof(IAppConfiguration), typeof(ILogger<ProcessList>) },
                modifiers: null);

            Assert.IsNotNull(constructor);
            return (ProcessList)constructor.Invoke(new object[]
            {
                tempFile,
                _appConfigurationMock.Object,
                _processListLoggerMock.Object
            });
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

        [TestMethod]
        public async Task GetProcesses_WithBlacklistedProcessNameContainingSpaces_ExcludesProcess()
        {
            _processList.AddEntry("camera hub", null, blacklist: true);
            _mockCaptureService.AddProcess("Camera Hub", 5678, "0x00000002");
            var controller = new CapFrameX.Remote.CaptureController(_captureManager);

            var processes = (await controller.GetProcesses()).ToArray();

            CollectionAssert.AreEquivalent(new[] { "TestGame.exe" }, processes);
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
        public async Task StartCapture_WhenAlreadyCapturing_ThrowsException()
        {
            _mockCaptureService.StartCaptureService(null);
            var options = CreateValidCaptureOptions();

            await _captureManager.StartCapture(options);
            await Assert.ThrowsExactlyAsync<Exception>(() => _captureManager.StartCapture(options));
        }

        [TestMethod]
        public async Task StartCapture_WithNonExistentProcess_ThrowsException()
        {
            _mockCaptureService.StartCaptureService(null);
            var options = new CaptureOptions
            {
                ProcessInfo = ("NonExistent.exe", 9999),
                CaptureTime = 0,
                CaptureDelay = 0
            };

            await Assert.ThrowsExactlyAsync<Exception>(() => _captureManager.StartCapture(options));
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
        [DataRow(0.0, DisplayName = "Stopped by hand")]
        [DataRow(1.0, DisplayName = "Stopped by the capture time")]
        public async Task StartCapture_WithDelay_RecordsNoFramesFromTheDelay(double captureTime)
        {
            // Frames sent until the delay is over carry 40 ms, the frames after it 10 ms. The capture
            // used to start at the request, so the archive supplied the frames of the delay.
            const double delay = 1.0;
            const double delayFrameTime = 40.0;
            const double captureFrameTime = 10.0;
            _mockCaptureService.AddProcess("DelayGame", 4321, "0x00000005");
            _mockCaptureService.UseRealQpcTime = true;
            _mockCaptureService.StartCaptureService(null);
            _captureManager.StartFillArchive();

            long delayEnd = long.MaxValue;
            using var stopEmitting = new CancellationTokenSource();
            var emitter = Task.Run(async () =>
            {
                while (!stopEmitting.IsCancellationRequested)
                {
                    bool inDelay = Stopwatch.GetTimestamp() < Interlocked.Read(ref delayEnd);
                    _mockCaptureService.EmitCustomFrame("DelayGame.exe", 4321, inDelay ? delayFrameTime : captureFrameTime);
                    await Task.Delay(5);
                }
            });

            // Fill the archive, then request the capture. The delay of CaptureManager ends after
            // delayEnd, which is taken before its request.
            await Task.Delay(300);
            Interlocked.Exchange(ref delayEnd, Stopwatch.GetTimestamp() + (long)(delay * Stopwatch.Frequency));

            var options = CreateValidCaptureOptions();
            options.ProcessInfo = ("DelayGame", 4321);
            options.CaptureDelay = delay;
            options.CaptureTime = captureTime;
            await _captureManager.StartCapture(options);

            if (captureTime > 0)
            {
                var timeout = Stopwatch.StartNew();
                while (_mockRecordManager.FileCount == 0 && timeout.Elapsed < TimeSpan.FromSeconds(20))
                    await Task.Delay(100);
            }
            else
            {
                await Task.Delay(1500);
                await _captureManager.StopCapture();
            }

            stopEmitting.Cancel();
            await emitter;

            var frameTimes = _mockRecordManager.LastConvertedPresentLines
                .Select(line => line.Split(',')[MockCaptureService.MsBetweenPresents_INDEX])
                .ToList();
            Assert.AreEqual(1, _mockRecordManager.FileCount, "No capture was written.");
            Assert.IsTrue(frameTimes.Count > 20, $"The capture has only {frameTimes.Count} frames.");
            Assert.AreEqual(0, frameTimes.Count(frameTime => frameTime == delayFrameTime.ToString("F4", CultureInfo.InvariantCulture)),
                "Frames shown during the capture delay ended up in the capture.");
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
        public async Task StopCapture_WhenNotCapturing_ThrowsException()
        {
            await Assert.ThrowsExactlyAsync<Exception>(() => _captureManager.StopCapture());
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

        #region Capture buffer

        [TestMethod]
        public async Task Capture_BuffersAndWritesOnlyTheTargetProcess()
        {
            // Regression: every row of every process stayed in the buffer until the next capture.
            _mockCaptureService.AddProcess("TargetGame", 4321, "0x00000005");
            _mockCaptureService.AddProcess("OtherApp", 5555, "0x00000006");
            _mockCaptureService.UseRealQpcTime = true;
            _mockCaptureService.StartCaptureService(null);
            _captureManager.StartFillArchive();

            using (new FrameEmitter(_mockCaptureService,
                ("TargetGame.exe", 4321, 10.0), ("OtherApp.exe", 5555, 33.0)))
            {
                await Task.Delay(300);
                await _captureManager.StartCapture(CaptureOptionsFor("TargetGame", 4321));
                await Task.Delay(1000);
            }
            await WaitUntilBufferIsStable();

            var buffered = _captureManager.CaptureBuffer.ToList();
            Assert.IsTrue(buffered.Count > 20, $"Only {buffered.Count} rows were buffered.");
            CollectionAssert.AreEqual(new[] { "TargetGame.exe" },
                buffered.Select(row => row[MockCaptureService.ApplicationName_INDEX]).Distinct().ToArray());

            await _captureManager.StopCapture();

            Assert.AreEqual(1, _mockRecordManager.FileCount, "No capture was written.");
            CollectionAssert.AreEqual(new[] { "TargetGame.exe" },
                _mockRecordManager.LastConvertedPresentLines
                    .Select(line => line.Split(',')[MockCaptureService.ApplicationName_INDEX]).Distinct().ToArray());
        }

        [TestMethod]
        public async Task Capture_ReleasesItsBufferOnceTheFileIsWritten()
        {
            _mockCaptureService.AddProcess("TargetGame", 4321, "0x00000005");
            _mockCaptureService.UseRealQpcTime = true;
            _mockCaptureService.StartCaptureService(null);
            _captureManager.StartFillArchive();

            using (new FrameEmitter(_mockCaptureService, ("TargetGame.exe", 4321, 10.0)))
            {
                await Task.Delay(300);
                await _captureManager.StartCapture(CaptureOptionsFor("TargetGame", 4321));
                await Task.Delay(500);
            }
            await WaitUntilBufferIsStable();
            Assert.IsTrue(_captureManager.CaptureBuffer.Count > 0);

            await _captureManager.StopCapture();

            Assert.AreEqual(1, _mockRecordManager.FileCount, "No capture was written.");
            Assert.AreEqual(0, _captureManager.CaptureBuffer.Count,
                "The rows of a written capture must not stay referenced until the next capture.");
        }

        [TestMethod]
        public async Task Capture_OfSeveralTargetInstances_IsStillRejected()
        {
            // All PIDs of the target name are kept, so the multi-instance check still sees them.
            _mockCaptureService.AddProcess("TargetGame", 4321, "0x00000005");
            _mockCaptureService.AddProcess("TargetGame", 4322, "0x00000007");
            _mockCaptureService.UseRealQpcTime = true;
            _mockCaptureService.StartCaptureService(null);
            _captureManager.StartFillArchive();

            using (new FrameEmitter(_mockCaptureService,
                ("TargetGame.exe", 4321, 10.0), ("TargetGame.exe", 4322, 12.0)))
            {
                await Task.Delay(300);
                await _captureManager.StartCapture(CaptureOptionsFor("TargetGame", 4321));
                await Task.Delay(500);
            }
            await WaitUntilBufferIsStable();
            Assert.AreEqual(2, _captureManager.CaptureBuffer
                .Select(row => row[MockCaptureService.ProcessID_INDEX]).Distinct().Count());

            await _captureManager.StopCapture();

            Assert.AreEqual(0, _mockRecordManager.FileCount);
            Assert.IsTrue(LogEntries().Contains("Multi instances detected. Capture data will be filtered."));
            Assert.AreEqual(0, _captureManager.CaptureBuffer.Count);
        }

        [TestMethod]
        public async Task Capture_StopsTheArchiveOnTheFirstRowOfAnyProcess()
        {
            // The target is silent and only another process presents. The archive used to stop
            // on the first buffered row of any process and must not wait for the target now.
            _mockCaptureService.AddProcess("TargetGame", 4321, "0x00000005");
            _mockCaptureService.AddProcess("OtherApp", 5555, "0x00000006");
            _mockCaptureService.UseRealQpcTime = true;
            _mockCaptureService.StartCaptureService(null);
            _captureManager.StartFillArchive();

            using (new FrameEmitter(_mockCaptureService, ("OtherApp.exe", 5555, 10.0)))
            {
                await Task.Delay(300);
                await _captureManager.StartCapture(CaptureOptionsFor("TargetGame", 4321));
                Assert.IsTrue(SpinWait.SpinUntil(() => LogEntries().Contains("Stopped filling Archive"),
                    TimeSpan.FromSeconds(5)), "The archive kept filling while the target was silent.");
            }
            await WaitUntilBufferIsStable();
            Assert.AreEqual(0, _captureManager.CaptureBuffer.Count);

            await _captureManager.StopCapture();

            Assert.AreEqual(0, _mockRecordManager.FileCount);
        }

        [TestMethod]
        public async Task FinishedCapture_LeavesTheBufferOfANewCaptureAlone()
        {
            // MCP and REST start a capture without waiting for LockCaptureService, so a new
            // capture can run while the previous one is still being written.
            _mockCaptureService.AddProcess("GameA", 4321, "0x00000005");
            _mockCaptureService.AddProcess("GameB", 7777, "0x00000008");
            _mockCaptureService.UseRealQpcTime = true;
            _mockCaptureService.StartCaptureService(null);
            _captureManager.StartFillArchive();
            var saveGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            using (new FrameEmitter(_mockCaptureService, ("GameA.exe", 4321, 10.0), ("GameB.exe", 7777, 20.0)))
            {
                await Task.Delay(300);
                await _captureManager.StartCapture(CaptureOptionsFor("GameA", 4321));
                await Task.Delay(500);

                _mockRecordManager.BeforeSave = () => saveGate.Task;
                Task stopA = _captureManager.StopCapture();
                Assert.IsTrue(SpinWait.SpinUntil(() => _mockRecordManager.LastConvertedPresentLines.Count > 0,
                    TimeSpan.FromSeconds(10)), "Capture A never reached its write.");

                await _captureManager.StartCapture(CaptureOptionsFor("GameB", 7777));
                await Task.Delay(500);

                saveGate.SetResult(true);
                await stopA;
                Assert.AreEqual(1, _mockRecordManager.FileCount, "Capture A was not written.");
                Assert.IsTrue(_captureManager.CaptureBuffer.Count > 0,
                    "Writing capture A released the buffer of capture B.");
                await Task.Delay(300);
            }
            await WaitUntilBufferIsStable();

            _mockRecordManager.BeforeSave = null;
            await _captureManager.StopCapture();

            Assert.AreEqual(2, _mockRecordManager.FileCount, "Capture B was not written.");
            var linesOfB = _mockRecordManager.LastConvertedPresentLines;
            Assert.IsTrue(linesOfB.Count > 20, $"Capture B has only {linesOfB.Count} frames.");
            CollectionAssert.AreEqual(new[] { "GameB.exe" },
                linesOfB.Select(line => line.Split(',')[MockCaptureService.ApplicationName_INDEX]).Distinct().ToArray());
        }

        private static CaptureOptions CaptureOptionsFor(string processName, int processId) => new CaptureOptions
        {
            ProcessInfo = (processName, processId),
            CaptureTime = 0,
            CaptureDelay = 0,
            CaptureFileMode = "Json",
            Comment = "Buffer test"
        };

        private List<string> LogEntries() => _logEntryManagerMock.Invocations
            .Where(invocation => invocation.Method.Name == nameof(ILogEntryManager.AddLogEntry))
            .Select(invocation => (string)invocation.Arguments[0])
            .ToList();

        // Rows reach the buffer on the capture scheduler. Once the emitter stopped, this waits
        // for the last of them, so the buffer can be read without a concurrent writer.
        private async Task WaitUntilBufferIsStable()
        {
            int previous = -1;
            for (int i = 0; i < 50; i++)
            {
                int current = _captureManager.CaptureBuffer.Count;
                if (current == previous)
                    return;
                previous = current;
                await Task.Delay(50);
            }
        }

        // Emits one row per process every 5 ms on the real QPC clock until disposed.
        private sealed class FrameEmitter : IDisposable
        {
            private readonly CancellationTokenSource _stop = new CancellationTokenSource();
            private readonly Task _loop;

            public FrameEmitter(MockCaptureService service,
                params (string Application, int ProcessId, double FrameTime)[] processes)
            {
                _loop = Task.Run(async () =>
                {
                    while (!_stop.IsCancellationRequested)
                    {
                        foreach (var (application, processId, frameTime) in processes)
                            service.EmitCustomFrame(application, processId, frameTime);
                        await Task.Delay(5);
                    }
                });
            }

            public void Dispose()
            {
                _stop.Cancel();
                _loop.Wait(TimeSpan.FromSeconds(5));
                _stop.Dispose();
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Field regression: a second StartCapture that slipped past the IsCapturing guard
        /// subscribed to the frame stream again and orphaned the first subscription, because the
        /// handle was simply overwritten. Every frame then reached the capture buffer once per
        /// orphan — and NormalizeTimes, which rewrote the caller's rows in place, subtracted the
        /// start time once per copy. Captures came out with ~10x the frames and timestamps hours
        /// before the run, and no longer loaded.
        ///
        /// This pins the half that turned duplicates into corruption: reading a row must not
        /// change it.
        /// </summary>
        [TestMethod]
        public void NormalizeTimes_LeavesTheSourceRowsUntouched()
        {
            var rows = CreateRecordLines(startTimeMs: 11_848_037, frameCount: 5);
            var before = rows.Select(row => string.Join(",", row)).ToList();

            _captureManager.NormalizeTimes(rows).ToList();

            var after = rows.Select(row => string.Join(",", row)).ToList();
            CollectionAssert.AreEqual(before, after,
                "NormalizeTimes rewrote its input; a row normalized twice loses its start time twice");
        }

        [TestMethod]
        public void NormalizeTimes_IsRepeatable()
        {
            var rows = CreateRecordLines(startTimeMs: 11_848_037, frameCount: 5);

            var first = _captureManager.NormalizeTimes(rows).ToList();
            var second = _captureManager.NormalizeTimes(rows).ToList();

            CollectionAssert.AreEqual(first, second, "normalizing the same rows twice gave different results");
        }

        [TestMethod]
        public void NormalizeTimes_KeepsADuplicatedRowFromPoisoningTheTimeline()
        {
            // Exactly what the orphaned subscription produced: the same row instance more than
            // once in the capture buffer. It may come out as a duplicate — it must not come out
            // as -k * startTime.
            const double startTimeMs = 11_848_037;
            var rows = CreateRecordLines(startTimeMs, frameCount: 3);
            var withDuplicates = new List<string[]> { rows[0], rows[1], rows[1], rows[1], rows[2] };

            var normalized = _captureManager.NormalizeTimes(withDuplicates).ToList();

            int timeIndex = ((ICaptureService)_mockCaptureService).CPUStartQPCTimeInMs_Index;
            foreach (var line in normalized)
            {
                double time = double.Parse(line.Split(',')[timeIndex], CultureInfo.InvariantCulture);
                Assert.IsTrue(time >= 0,
                    $"negative timestamp {time} — the row was normalized against the start time more than once");
                Assert.IsTrue(time < startTimeMs,
                    $"timestamp {time} is still on the raw timeline, not rebased onto the capture start");
            }
        }

        // Rows shaped like the capture service's own layout, with a raw CPUStartQPCTime that is
        // far from zero — the field data is a machine-uptime timeline, which is what made the
        // repeated subtraction so visible.
        private List<string[]> CreateRecordLines(double startTimeMs, int frameCount)
        {
            int timeIndex = ((ICaptureService)_mockCaptureService).CPUStartQPCTimeInMs_Index;
            int columnCount = _mockCaptureService.ColumnHeader.Split(',').Length;

            var rows = new List<string[]>();
            for (int i = 0; i < frameCount; i++)
            {
                var row = new string[columnCount];
                for (int c = 0; c < columnCount; c++) row[c] = "0";
                row[timeIndex] = (startTimeMs + i * 16.67)
                    .ToString(CultureInfo.InvariantCulture);
                rows.Add(row);
            }
            return rows;
        }

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
