using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Logging;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.RTSS;
using CapFrameX.Data;
using CapFrameX.Data.Session.Classes;
using CapFrameX.Monitoring.Contracts;
using CapFrameX.PresentMonInterface;
using CapFrameX.Test.Mocks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    [TestCategory("Integration")]
    public class PresentMonIntegrationTest
    {
        private PresentMonCaptureService _captureService;
        private CaptureManager _captureManager;
        private MockRecordManager _mockRecordManager;
        private MockSensorService _mockSensorService;
        private MockPoweneticsService _mockPoweneticsService;
        private MockBenchlabService _mockBenchlabService;

        private Mock<IAppConfiguration> _appConfigMock;
        private Mock<IOverlayService> _overlayServiceMock;
        private Mock<IRTSSService> _rtssServiceMock;
        private Mock<ISensorConfig> _sensorConfigMock;
        private Mock<ILogEntryManager> _logEntryManagerMock;

        private Process _vkcubeProcess;
        private string _previousWorkingDir;
        private string _appOutputDir;

        [TestInitialize]
        public void Setup()
        {
            // Check prerequisites - each test gets Inconclusive if not met
            using (var identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
                    Assert.Inconclusive("Integration tests require administrator privileges.");
            }

            // Resolve the CapFrameX app output directory
            var testAssemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            _appOutputDir = Path.GetFullPath(Path.Combine(testAssemblyDir, "..", "..", "..", "..", "CapFrameX", "bin", "x64", "Release"));

            var presentMonPath = Path.Combine(_appOutputDir, "PresentMon", CaptureServiceConfiguration.PresentMonAppName + ".exe");
            if (!File.Exists(presentMonPath))
                Assert.Inconclusive($"PresentMon not found at: {presentMonPath}. Build the main application first.");

            var vkcubePath = Path.Combine(_appOutputDir, "3d-test-app", "vkcube.exe");
            if (!File.Exists(vkcubePath))
                Assert.Inconclusive($"vkcube.exe not found at: {vkcubePath}. Build the main application first.");

            // CaptureServiceConfiguration uses relative path "PresentMon\..." so set working directory
            _previousWorkingDir = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(_appOutputDir);

            _appConfigMock = new Mock<IAppConfiguration>();
            _appConfigMock.SetupAllProperties();
            _appConfigMock.Object.UsePcLatency = true;
            _appConfigMock.Object.IsOverlayActive = false;
            _appConfigMock.Object.AutoDisableOverlay = false;
            _appConfigMock.Object.UsePmdDataLogging = false;
            _appConfigMock.Object.CaptureRTSSFrameTimes = false;
            _appConfigMock.Object.UseRunHistory = false;
            _appConfigMock.Object.UseAggregation = false;
            _appConfigMock.Object.SaveAggregationOnly = false;
            _appConfigMock.Object.VoiceSoundLevel = 0;
            _appConfigMock.Object.SimpleSoundLevel = 0;
            _appConfigMock.Object.HotkeySoundMode = "None";
            _appConfigMock.Object.CaptureFileMode = "Json";

            // Real PresentMon capture service
            var captureServiceLogger = new Mock<ILogger<PresentMonCaptureService>>();
            _captureService = new PresentMonCaptureService(captureServiceLogger.Object, _appConfigMock.Object);

            // Mock services
            _mockSensorService = new MockSensorService(seed: 42);
            _mockRecordManager = new MockRecordManager();
            _mockPoweneticsService = new MockPoweneticsService(seed: 42);
            _mockBenchlabService = new MockBenchlabService(seed: 42);

            _overlayServiceMock = new Mock<IOverlayService>();
            _overlayServiceMock.Setup(x => x.IsOverlayActiveStream).Returns(new Subject<bool>());

            _rtssServiceMock = new Mock<IRTSSService>();
            _sensorConfigMock = new Mock<ISensorConfig>();
            _sensorConfigMock.SetupProperty(x => x.IsCapturing, false);

            _logEntryManagerMock = new Mock<ILogEntryManager>();
            _logEntryManagerMock.Setup(x => x.AddLogEntry(It.IsAny<string>(), It.IsAny<ELogMessageType>(), It.IsAny<bool>()));

            var captureManagerLogger = new Mock<ILogger<CaptureManager>>();
            var soundManagerLogger = new Mock<ILogger<SoundManager>>();
            var processListLogger = new Mock<ILogger<ProcessList>>();

            var soundManager = new SoundManager(_appConfigMock.Object, soundManagerLogger.Object);

            // ProcessList via reflection (private constructor)
            string tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-processes.json");
            var ctor = typeof(ProcessList).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(string), typeof(IAppConfiguration), typeof(ILogger<ProcessList>) },
                modifiers: null);
            var processList = (ProcessList)ctor.Invoke(new object[]
            {
                tempFile,
                _appConfigMock.Object,
                processListLogger.Object
            });

            // CaptureManager with REAL capture service
            _captureManager = new CaptureManager(
                _captureService,
                _mockSensorService,
                _overlayServiceMock.Object,
                soundManager,
                _mockRecordManager,
                captureManagerLogger.Object,
                _appConfigMock.Object,
                _rtssServiceMock.Object,
                _sensorConfigMock.Object,
                processList,
                _mockPoweneticsService,
                _mockBenchlabService,
                _logEntryManagerMock.Object);
        }

        [TestCleanup]
        public void Cleanup()
        {
            try
            {
                if (_vkcubeProcess != null && !_vkcubeProcess.HasExited)
                {
                    _vkcubeProcess.Kill();
                    _vkcubeProcess.WaitForExit(3000);
                }
            }
            catch { }
            _vkcubeProcess?.Dispose();

            try { _captureService?.StopCaptureService(); } catch { }
            try { PresentMonCaptureService.TryKillPresentMon(); } catch { }

            _mockSensorService?.Dispose();
            _mockPoweneticsService?.Dispose();
            _captureManager?.StopFillArchive();

            if (_previousWorkingDir != null)
            {
                Directory.SetCurrentDirectory(_previousWorkingDir);
            }
        }

        [TestMethod]
        [Timeout(30000)]
        public async Task ProcessDetection_VkcubeDetectedByPresentMon()
        {
            bool started = StartPresentMonService();
            Assert.IsTrue(started, "PresentMon service failed to start");
            _captureManager.StartFillArchive();

            _vkcubeProcess = StartVkcube();
            await Task.Delay(2000);

            var processInfo = await WaitForProcessDetection("vkcube");

            Assert.IsTrue(processInfo.Item1.IndexOf("vkcube", StringComparison.OrdinalIgnoreCase) >= 0,
                $"Expected process name containing 'vkcube', got '{processInfo.Item1}'");
            Assert.IsTrue(processInfo.Item2 > 0, "Process ID should be positive");
        }

        [TestMethod]
        [Timeout(30000)]
        public async Task FrameDataStream_EmitsRealFrameData()
        {
            bool started = StartPresentMonService();
            Assert.IsTrue(started, "PresentMon service failed to start");
            _captureManager.StartFillArchive();

            _vkcubeProcess = StartVkcube();
            await Task.Delay(2000);

            // Collect raw frames for 5 seconds
            var frames = new List<string[]>();
            var subscription = _captureService.FrameDataStream
                .Subscribe(lineSplit =>
                {
                    if (lineSplit[PresentMonCaptureService.ApplicationName_INDEX]
                        .IndexOf("vkcube", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        frames.Add(lineSplit);
                    }
                });

            await Task.Delay(5000);
            subscription.Dispose();

            // Must have captured frames
            Assert.IsTrue(frames.Count > 50,
                $"Expected at least 50 frames, got {frames.Count}");

            // Each frame must have correct column count
            int expectedColumns = _captureService.ValidLineLength;
            foreach (var frame in frames)
            {
                Assert.AreEqual(expectedColumns, frame.Length,
                    $"Frame should have {expectedColumns} columns, got {frame.Length}");
            }

            // Frame times must be in plausible range
            var frameTimes = frames
                .Select(f => double.Parse(f[PresentMonCaptureService.MsBetweenPresents_INDEX], CultureInfo.InvariantCulture))
                .ToList();

            foreach (var ft in frameTimes)
            {
                Assert.IsTrue(ft > 0.5 && ft < 200.0,
                    $"Frame time {ft}ms is outside expected range [0.5, 200]ms");
            }

            double avgFrameTime = frameTimes.Average();
            double stdDev = Math.Sqrt(frameTimes.Select(ft => Math.Pow(ft - avgFrameTime, 2)).Average());

            Console.WriteLine($"Raw stream: {frames.Count} frames, " +
                $"Avg={avgFrameTime:F2}ms, StdDev={stdDev:F2}ms, " +
                $"Min={frameTimes.Min():F2}ms, Max={frameTimes.Max():F2}ms, " +
                $"~{1000.0 / avgFrameTime:F1} FPS");
        }

        [TestMethod]
        [Timeout(60000)]
        public async Task ShortCapture_CapturesAndSavesSession()
        {
            bool started = StartPresentMonService();
            Assert.IsTrue(started, "PresentMon service failed to start");
            _captureManager.StartFillArchive();

            _vkcubeProcess = StartVkcube();
            await Task.Delay(2000);

            var processInfo = await WaitForProcessDetection("vkcube");
            await Task.Delay(3000); // let archive fill

            var options = new CaptureOptions
            {
                ProcessInfo = processInfo,
                CaptureTime = 5,
                CaptureDelay = 0,
                CaptureFileMode = "Json",
                RecordDirectory = null,
                Remote = false,
                Comment = "Integration test"
            };

            await _captureManager.StartCapture(options);
            Assert.IsTrue(_captureManager.IsCapturing, "Should be capturing after StartCapture");

            // Wait for auto-stop (5s capture + ~2.5s processing offset + buffer)
            await Task.Delay(12000);

            Assert.IsFalse(_captureManager.IsCapturing, "Capture should have auto-stopped");
            Assert.IsFalse(_captureManager.LockCaptureService, "Lock should be released");
            Assert.IsTrue(_mockRecordManager.FileCount > 0,
                "MockRecordManager should have received a capture file");

            // Verify saved data
            var firstFile = _mockRecordManager.InMemoryFiles.First();
            Assert.IsFalse(string.IsNullOrWhiteSpace(firstFile.Value),
                "Saved session JSON should not be empty");

            var session = JsonConvert.DeserializeObject<Session>(firstFile.Value);
            Assert.IsNotNull(session, "Session should deserialize");
            Assert.IsTrue(session.Runs.Count > 0, "Should have at least 1 run");
            Assert.IsNotNull(session.Runs[0].CaptureData, "Run should have CaptureData");
            Assert.IsTrue(session.Runs[0].CaptureData.MsBetweenPresents.Length > 10,
                $"Expected >10 frame times, got {session.Runs[0].CaptureData.MsBetweenPresents.Length}");

            Console.WriteLine($"Captured session: {session.Runs[0].CaptureData.MsBetweenPresents.Length} frames");
        }

        [TestMethod]
        [Timeout(60000)]
        public async Task CapturedFrameTimes_ArePhysicallyPlausible()
        {
            bool started = StartPresentMonService();
            Assert.IsTrue(started);
            _captureManager.StartFillArchive();

            _vkcubeProcess = StartVkcube();
            await Task.Delay(2000);

            var processInfo = await WaitForProcessDetection("vkcube");
            await Task.Delay(3000);

            double captureTimeSec = 5;
            var options = new CaptureOptions
            {
                ProcessInfo = processInfo,
                CaptureTime = captureTimeSec,
                CaptureDelay = 0,
                CaptureFileMode = "Json",
                RecordDirectory = null,
                Remote = false,
                Comment = "Plausibility test"
            };

            await _captureManager.StartCapture(options);
            await Task.Delay(12000);

            Assert.IsTrue(_mockRecordManager.FileCount > 0, "Should have saved a file");

            var json = _mockRecordManager.InMemoryFiles.First().Value;
            var session = JsonConvert.DeserializeObject<Session>(json);
            var run = session.Runs[0];
            var frameTimes = run.CaptureData.MsBetweenPresents;
            var timeInSeconds = run.CaptureData.TimeInSeconds;

            // All frame times must be positive
            var validFrameTimes = frameTimes.Where(ft => ft > 0).ToArray();
            Assert.IsTrue(validFrameTimes.Length > 10,
                $"Expected >10 positive frame times, got {validFrameTimes.Length}");

            // Average frame time between 1ms (1000 FPS) and 100ms (10 FPS)
            double avg = validFrameTimes.Average();
            Assert.IsTrue(avg > 1.0 && avg < 100.0,
                $"Average frame time {avg:F2}ms is outside [1, 100]ms range");

            // TimeInSeconds must be monotonically increasing
            for (int i = 1; i < timeInSeconds.Length; i++)
            {
                Assert.IsTrue(timeInSeconds[i] >= timeInSeconds[i - 1],
                    $"TimeInSeconds not monotonic at index {i}: {timeInSeconds[i - 1]} -> {timeInSeconds[i]}");
            }

            // Total capture duration should be roughly near configured capture time
            double totalDurationSec = timeInSeconds.Last();
            Assert.IsTrue(totalDurationSec > captureTimeSec * 0.5 && totalDurationSec < captureTimeSec * 3.0,
                $"Total duration {totalDurationSec:F2}s is too far from configured {captureTimeSec}s");

            Console.WriteLine($"Plausibility: {validFrameTimes.Length} frames, " +
                $"Avg={avg:F2}ms (~{1000.0 / avg:F1} FPS), " +
                $"Duration={totalDurationSec:F2}s");
        }

        [TestMethod]
        [Timeout(120000)]
        public async Task MultipleCaptureRuns_AllSucceed()
        {
            bool started = StartPresentMonService();
            Assert.IsTrue(started);
            _captureManager.StartFillArchive();

            _vkcubeProcess = StartVkcube();
            await Task.Delay(2000);

            var processInfo = await WaitForProcessDetection("vkcube");
            await Task.Delay(3000);

            int runCount = 3;
            for (int run = 0; run < runCount; run++)
            {
                _mockRecordManager.Clear();

                var options = new CaptureOptions
                {
                    ProcessInfo = processInfo,
                    CaptureTime = 2,
                    CaptureDelay = 0,
                    CaptureFileMode = "Json",
                    RecordDirectory = null,
                    Remote = false,
                    Comment = $"Multi-run #{run + 1}"
                };

                await _captureManager.StartCapture(options);
                Assert.IsTrue(_captureManager.IsCapturing, $"Run {run + 1}: Should be capturing");

                // 2s capture + ~2.5s processing + buffer
                await Task.Delay(8000);

                Assert.IsFalse(_captureManager.IsCapturing, $"Run {run + 1}: Should have stopped");
                Assert.IsFalse(_captureManager.LockCaptureService, $"Run {run + 1}: Lock should be released");
                Assert.IsTrue(_mockRecordManager.FileCount > 0,
                    $"Run {run + 1}: Should have saved a file");

                Console.WriteLine($"Run {run + 1}: Saved {_mockRecordManager.FileCount} file(s)");

                await Task.Delay(1000);
            }
        }

        #region Helpers

        private Process StartVkcube()
        {
            var vkcubePath = Path.Combine(_appOutputDir, "3d-test-app", "vkcube.exe");
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = vkcubePath,
                Arguments = "--present_mode 2 --width 520 --height 820",
                UseShellExecute = false
            });

            Assert.IsNotNull(process, "Failed to start vkcube.exe");
            Thread.Sleep(500); // let it initialize

            Assert.IsFalse(process.HasExited,
                "vkcube.exe exited immediately - Vulkan GPU may not be available");

            return process;
        }

        private bool StartPresentMonService()
        {
            var serviceConfig = new PresentMonServiceConfiguration
            {
                RedirectOutputStream = true,
                ExcludeProcesses = new List<string>(),
                TrackPcLatency = _appConfigMock.Object.UsePcLatency
            };

            var startInfo = CaptureServiceConfiguration.GetServiceStartInfo(
                serviceConfig.ConfigParameterToArguments());

            return _captureManager.StartCaptureService(startInfo);
        }

        private async Task<(string, int)> WaitForProcessDetection(string processName, int timeoutMs = 15000)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                var processes = _captureManager.GetAllFilteredProcesses(new HashSet<string>());
                if (processes != null)
                {
                    var match = processes.FirstOrDefault(p =>
                        p.Item1 != null &&
                        p.Item1.IndexOf(processName, StringComparison.OrdinalIgnoreCase) >= 0);

                    if (!string.IsNullOrEmpty(match.Item1))
                        return match;
                }

                await Task.Delay(500);
            }

            Assert.Fail($"Process '{processName}' was not detected within {timeoutMs}ms");
            return default;
        }

        #endregion
    }
}
