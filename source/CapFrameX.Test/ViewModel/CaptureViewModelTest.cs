using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Logging;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.RTSS;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Data;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Monitoring.Contracts;
using CapFrameX.Overlay;
using CapFrameX.PresentMonInterface;
using CapFrameX.Statistics.NetStandard.Contracts;
using CapFrameX.Test.Mocks;
using CapFrameX.ViewModel;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Prism.Events;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Windows.Threading;

namespace CapFrameX.Test.ViewModel
{
    [TestClass]
    public class CaptureViewModelTest
    {
        private const string VkcubeProcess = "vkcube.exe";
        private const int VkcubeProcessId = 1337;

        private MockCaptureService _mockCaptureService;
        private MockSensorService _mockSensorService;
        private MockRecordManager _mockRecordManager;
        private MockPoweneticsService _mockPoweneticsService;
        private MockBenchlabService _mockBenchlabService;

        private Mock<IAppConfiguration> _appConfigurationMock;
        private Mock<IOverlayService> _overlayServiceMock;
        private Mock<IOverlayEntryProvider> _overlayEntryProviderMock;
        private Mock<IRTSSService> _rtssServiceMock;
        private Mock<IOnlineMetricService> _onlineMetricServiceMock;
        private Mock<IStatisticProvider> _statisticProviderMock;
        private Mock<ILogEntryManager> _logEntryManagerMock;
        private Mock<ISensorConfig> _sensorConfigMock;
        private Mock<ILogger<CaptureViewModel>> _captureVmLoggerMock;
        private Mock<ILogger<CaptureManager>> _captureManagerLoggerMock;
        private Mock<ILogger<SoundManager>> _soundManagerLoggerMock;
        private Mock<ILogger<ProcessList>> _processListLoggerMock;
        private Mock<IOverlayEntry> _runHistoryOverlayEntryMock;

        private Subject<int> _processIdStream;
        private EventAggregator _eventAggregator;
        private ProcessList _processList;
        private SoundManager _soundManager;
        private CaptureManager _captureManager;

        [TestInitialize]
        public void Setup()
        {
            _ = Dispatcher.CurrentDispatcher;

            _mockCaptureService = new MockCaptureService(seed: 42) { EmissionIntervalMs = 0 };
            _mockSensorService = new MockSensorService(seed: 42);
            _mockRecordManager = new MockRecordManager();
            _mockPoweneticsService = new MockPoweneticsService(seed: 42);
            _mockBenchlabService = new MockBenchlabService(seed: 42);

            _appConfigurationMock = new Mock<IAppConfiguration>();
            _overlayServiceMock = new Mock<IOverlayService>();
            _overlayEntryProviderMock = new Mock<IOverlayEntryProvider>();
            _rtssServiceMock = new Mock<IRTSSService>();
            _onlineMetricServiceMock = new Mock<IOnlineMetricService>();
            _statisticProviderMock = new Mock<IStatisticProvider>();
            _logEntryManagerMock = new Mock<ILogEntryManager>();
            _sensorConfigMock = new Mock<ISensorConfig>();
            _captureVmLoggerMock = new Mock<ILogger<CaptureViewModel>>();
            _captureManagerLoggerMock = new Mock<ILogger<CaptureManager>>();
            _soundManagerLoggerMock = new Mock<ILogger<SoundManager>>();
            _processListLoggerMock = new Mock<ILogger<ProcessList>>();
            _runHistoryOverlayEntryMock = new Mock<IOverlayEntry>();

            _eventAggregator = new EventAggregator();
            _processIdStream = new Subject<int>();

            SetupAppConfiguration();
            SetupServices();

            _processList = CreateProcessListForTests();
            _soundManager = new SoundManager(_appConfigurationMock.Object, _soundManagerLoggerMock.Object);

            _captureManager = new CaptureManager(
                _mockCaptureService,
                _mockSensorService,
                _overlayServiceMock.Object,
                _soundManager,
                _mockRecordManager,
                _captureManagerLoggerMock.Object,
                _appConfigurationMock.Object,
                _rtssServiceMock.Object,
                _sensorConfigMock.Object,
                _processList,
                _mockPoweneticsService,
                _mockBenchlabService,
                _logEntryManagerMock.Object);

            _mockCaptureService.AddProcess(VkcubeProcess, VkcubeProcessId);
            _processList.AddEntry(VkcubeProcess, null, false, 30d);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _mockCaptureService?.Dispose();
            _mockSensorService?.Dispose();
            _mockPoweneticsService?.Dispose();
            _captureManager?.StopFillArchive();
        }

        [TestMethod]
        public void Constructor_InitializesFrametimeModelAndReadyState()
        {
            var sut = CreateSut();

            Assert.IsNotNull(sut.FrametimeModel);
            Assert.IsTrue(sut.CaptureStateInfo.IndexOf("Service ready", StringComparison.Ordinal) >= 0);
            Assert.IsTrue(sut.CaptureStateInfo.IndexOf(_appConfigurationMock.Object.CaptureHotKey, StringComparison.Ordinal) >= 0);
            Assert.AreEqual(_appConfigurationMock.Object.CaptureTime.ToString(CultureInfo.InvariantCulture), sut.CaptureTimeString);

            DisposeHeartbeat(sut);
        }

        [TestMethod]
        public void UpdateProcessToCaptureList_WithVkcube_AutoDetectsAndPublishesProcessId()
        {
            var sut = CreateSut();
            ViewMessages.CurrentProcessToCapture published = null;
            int? lastProcessId = null;

            _eventAggregator.GetEvent<PubSubEvent<ViewMessages.CurrentProcessToCapture>>()
                .Subscribe(msg => published = msg);
            _processIdStream.Subscribe(id => lastProcessId = id);

            InvokePrivate(sut, "UpdateProcessToCaptureList");

            Assert.IsTrue(sut.ProcessesToCapture.Any(p => p == VkcubeProcess));
            Assert.IsTrue(sut.CaptureStateInfo.IndexOf("\"vkcube\" auto-detected", StringComparison.Ordinal) >= 0);
            Assert.IsNotNull(published);
            Assert.AreEqual(VkcubeProcess, published.Process);
            Assert.AreEqual(VkcubeProcessId, published.ProcessId);
            Assert.AreEqual(VkcubeProcessId, lastProcessId);

            DisposeHeartbeat(sut);
        }

        [TestMethod]
        public void UpdateProcessToCaptureList_WithMultipleProcesses_ShowsMultipleProcessesDetected()
        {
            var sut = CreateSut();
            _mockCaptureService.AddProcess("second.exe", 4242);

            InvokePrivate(sut, "UpdateProcessToCaptureList");

            Assert.IsTrue(sut.CaptureStateInfo.IndexOf("Multiple processes detected", StringComparison.Ordinal) >= 0);
            _overlayServiceMock.Verify(x => x.SetCaptureServiceStatus("Multiple processes detected"), Times.AtLeastOnce);

            DisposeHeartbeat(sut);
        }

        [TestMethod]
        public void SelectedNumberOfRuns_SetToOne_DisablesAggregationAndUpdatesOverlay()
        {
            var sut = CreateSut();
            sut.UseAggregation = true;

            sut.SelectedNumberOfRuns = 1;

            Assert.IsFalse(sut.UseAggregation);
            _overlayServiceMock.Verify(x => x.UpdateNumberOfRuns(1), Times.Once);

            DisposeHeartbeat(sut);
        }

        [TestMethod]
        public void UseRunHistory_SetFalse_DisablesAggregationAndEnablesRunHistoryEntry()
        {
            var sut = CreateSut();
            sut.UseAggregation = true;

            sut.UseRunHistory = false;

            Assert.IsFalse(sut.UseAggregation);
            Assert.IsTrue(_runHistoryOverlayEntryMock.Object.ShowOnOverlayIsEnabled);
            _rtssServiceMock.Verify(x => x.SetShowRunHistory(false), Times.AtLeastOnce);

            DisposeHeartbeat(sut);
        }

        [TestMethod]
        public void SelectedRelatedMetric_Set_UpdatesConfigAndResetsHistory()
        {
            var sut = CreateSut();

            sut.SelectedRelatedMetric = "Third";

            Assert.AreEqual("Third", _appConfigurationMock.Object.RelatedMetricOverlay);
            _overlayServiceMock.Verify(x => x.ResetHistory(), Times.Once);

            DisposeHeartbeat(sut);
        }

        [TestMethod]
        public void CaptureTimeString_WhenUseGlobalCaptureTime_UpdatesAppConfiguration()
        {
            var sut = CreateSut();
            sut.UseGlobalCaptureTime = true;

            sut.CaptureTimeString = "42.5";

            Assert.AreEqual(42.5d, _appConfigurationMock.Object.CaptureTime, 0.001d);

            DisposeHeartbeat(sut);
        }

        [TestMethod]
        public void OnSaveCaptureTime_WithVkcube_UpdatesProcessListEntry()
        {
            var sut = CreateSut();

            sut.OnSaveCaptureTime("55.0", "vkcube");

            var processEntry = _processList.FindProcessByName("vkcube");
            Assert.IsNotNull(processEntry);
            Assert.AreEqual(55.0d, processEntry.LastCaptureTime.Value, 0.001d);

            DisposeHeartbeat(sut);
        }

        private CaptureViewModel CreateSut()
        {
            var vm = new CaptureViewModel(
                _appConfigurationMock.Object,
                _eventAggregator,
                _mockRecordManager,
                _overlayServiceMock.Object,
                _overlayEntryProviderMock.Object,
                _mockSensorService,
                _onlineMetricServiceMock.Object,
                _statisticProviderMock.Object,
                _captureVmLoggerMock.Object,
                _processList,
                _soundManager,
                _captureManager,
                _sensorConfigMock.Object,
                _rtssServiceMock.Object,
                _logEntryManagerMock.Object);

            return vm;
        }

        private void SetupAppConfiguration()
        {
            _appConfigurationMock.SetupAllProperties();
            _appConfigurationMock.Object.CaptureHotKey = "F11";
            _appConfigurationMock.Object.OverlayHotKey = "Alt+O";
            _appConfigurationMock.Object.OverlayConfigHotKey = "Alt+C";
            _appConfigurationMock.Object.ResetHistoryHotkey = "F10";
            _appConfigurationMock.Object.ThreadAffinityHotkey = "Control+A";
            _appConfigurationMock.Object.ResetMetricsHotkey = "Alt+M";
            _appConfigurationMock.Object.HotkeySoundMode = "None";
            _appConfigurationMock.Object.CaptureTime = 30d;
            _appConfigurationMock.Object.CaptureDelay = 0d;
            _appConfigurationMock.Object.UseGlobalCaptureTime = true;
            _appConfigurationMock.Object.RunHistorySecondMetric = EMetric.P1.ToString();
            _appConfigurationMock.Object.RunHistoryThirdMetric = EMetric.Average.ToString();
            _appConfigurationMock.Object.UseRunHistory = true;
            _appConfigurationMock.Object.UseAggregation = true;
            _appConfigurationMock.Object.SelectedHistoryRuns = 3;
            _appConfigurationMock.Object.OutlierPercentageOverlay = 2;
            _appConfigurationMock.Object.OutlierHandling = EOutlierHandling.Ignore.ToString();
            _appConfigurationMock.Object.RelatedMetricOverlay = "Average";
            _appConfigurationMock.Object.CaptureFileMode = "Json";
            _appConfigurationMock.Object.ShareProcessListEntries = false;
            _appConfigurationMock.Object.AutoUpdateProcessList = false;
        }

        private void SetupServices()
        {
            _overlayServiceMock.Setup(x => x.IsOverlayActiveStream).Returns(new Subject<bool>());
            _overlayServiceMock.Setup(x => x.OnDictionaryUpdated).Returns(new Subject<IOverlayEntry[]>());
            _overlayServiceMock.SetupProperty(x => x.SecondMetric);
            _overlayServiceMock.SetupProperty(x => x.ThirdMetric);
            _overlayServiceMock.Setup(x => x.SetCaptureServiceStatus(It.IsAny<string>()));
            _overlayServiceMock.Setup(x => x.UpdateNumberOfRuns(It.IsAny<int>()));
            _overlayServiceMock.Setup(x => x.ResetHistory());

            _rtssServiceMock.Setup(x => x.ProcessIdStream).Returns(_processIdStream);
            _rtssServiceMock.Setup(x => x.GetFrameTimesInterval(It.IsAny<int>(), It.IsAny<int>()))
                .Returns(new[] { 16.67f, 16.67f, 16.67f });

            _logEntryManagerMock.SetupProperty(x => x.ShowBasicInfo, true);
            _logEntryManagerMock.SetupProperty(x => x.ShowAdvancedInfo, true);
            _logEntryManagerMock.SetupProperty(x => x.ShowErrors, true);
            _logEntryManagerMock.Setup(x => x.LogEntryOutput).Returns(new ObservableCollection<ILogEntry>());

            _sensorConfigMock.SetupProperty(x => x.IsCapturing, false);

            _runHistoryOverlayEntryMock.SetupProperty(x => x.ShowOnOverlayIsEnabled, false);
            _overlayEntryProviderMock.Setup(x => x.GetOverlayEntry("RunHistory")).Returns(_runHistoryOverlayEntryMock.Object);
        }

        private ProcessList CreateProcessListForTests()
        {
            string tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid():N}-processes.json");
            var constructor = typeof(ProcessList).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(string), typeof(IAppConfiguration), typeof(ILogger<ProcessList>) },
                modifiers: null);

            return (ProcessList)constructor.Invoke(new object[]
            {
                tempFile,
                _appConfigurationMock.Object,
                _processListLoggerMock.Object
            });
        }

        private static void InvokePrivate(object instance, string methodName)
        {
            var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Private method '{methodName}' was not found.");
            method.Invoke(instance, null);
        }

        private static void DisposeHeartbeat(CaptureViewModel viewModel)
        {
            var field = typeof(CaptureViewModel).GetField("_disposableHeartBeat", BindingFlags.Instance | BindingFlags.NonPublic);
            var disposable = field?.GetValue(viewModel) as IDisposable;
            disposable?.Dispose();
        }
    }
}
