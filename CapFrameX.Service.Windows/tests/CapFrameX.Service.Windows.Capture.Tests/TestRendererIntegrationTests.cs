using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reactive.Linq;

namespace CapFrameX.Service.Capture.Tests;

/// <summary>
/// Integration tests using the TestRenderer application to validate
/// real-world frame capture functionality.
/// </summary>
public class TestRendererIntegrationTests : IDisposable
{
    private readonly PresentMonCaptureService _captureService;
    private Process? _testRendererProcess;
    private const string TestRendererPath = @"..\..\..\..\CapFrameX.TestRenderer\bin\Debug\net10.0-windows\win-x64\CapFrameX.TestRenderer.exe";

    public TestRendererIntegrationTests()
    {
        _captureService = TestHelpers.CreateCaptureService();
    }

    [RequiresElevationFact]
    public async Task CaptureService_WithTestRenderer_ShouldCaptureFrameData()
    {
        // Arrange
        if (!File.Exists(TestRendererPath))
        {
            Assert.Fail($"TestRenderer not found at {TestRendererPath}. Please build the TestRenderer project first.");
        }

        var frameDataReceived = new ConcurrentBag<string[]>();
        var frameDataSubscription = _captureService.FrameDataStream
            .Subscribe(data => frameDataReceived.Add(data));

        // Use redirected output configuration with default ignore list (legacy behavior)
        var ignoreList = PresentMonTestConfiguration.GetDefaultIgnoreList().ToList();
        var startInfo = PresentMonTestConfiguration.CreateRedirectedStartInfo(ignoreList);

        // Start TestRenderer
        _testRendererProcess = Process.Start(new ProcessStartInfo
        {
            FileName = TestRendererPath,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        Assert.NotNull(_testRendererProcess);

        // Wait for process to initialize
        await Task.Delay(3000);

        // Act
        var captureStarted = _captureService.StartCaptureService(startInfo);
        Assert.True(captureStarted, "Capture service should start successfully");

        // Wait for frame data
        await Task.Delay(3000);

        // Assert
        Assert.True(frameDataReceived.Count > 0,
            "PresentMon produced no data for the test renderer. Verified on 2026-09-20: PresentMon "
            + "2.4.0 and 2.5.1 both observe a D3D9 window on this machine but never the Silk.NET "
            + "OpenGL test renderer, nor vkcube. The capture pipeline itself is covered by the "
            + "unit tests; this test needs a presenting process PresentMon actually tracks.");

        // Verify data format
        var firstFrame = frameDataReceived.First();
        Assert.NotNull(firstFrame);
        Assert.NotEmpty(firstFrame);

        // Verify essential parameters are present
        var appIndex = _captureService.ParameterNameIndexMapping["ApplicationName"];
        var processIdIndex = _captureService.ParameterNameIndexMapping["ProcessID"];
        var msBetweenPresentsIndex = _captureService.ParameterNameIndexMapping["MsBetweenPresents"];

        Assert.True(firstFrame.Length > Math.Max(appIndex, Math.Max(processIdIndex, msBetweenPresentsIndex)));
        Assert.Contains("CapFrameX.TestRenderer", firstFrame[appIndex]);

        // Cleanup
        frameDataSubscription.Dispose();
        _captureService.StopCaptureService();
    }

    [RequiresElevationFact]
    public async Task CaptureService_WithTestRenderer_ShouldDetectProcess()
    {
        // Arrange
        if (!File.Exists(TestRendererPath))
        {
            Assert.Fail($"TestRenderer not found at {TestRendererPath}");
        }

        // Start TestRenderer
        _testRendererProcess = Process.Start(new ProcessStartInfo
        {
            FileName = TestRendererPath,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        Assert.NotNull(_testRendererProcess);
        await Task.Delay(1000);

        // Use redirected output configuration
        var ignoreList = PresentMonTestConfiguration.GetDefaultIgnoreList().ToList();
        var startInfo = PresentMonTestConfiguration.CreateRedirectedStartInfo(ignoreList);

        // Act
        var captureStarted = _captureService.StartCaptureService(startInfo);
        Assert.True(captureStarted);

        await Task.Delay(2000);

        var processes = _captureService.GetAllFilteredProcesses(new HashSet<string>()).ToList();

        // Assert
        Assert.True(processes.Count > 0,
            "PresentMon produced no data for the test renderer. Verified on 2026-09-20: PresentMon "
            + "2.4.0 and 2.5.1 both observe a D3D9 window on this machine but never the Silk.NET "
            + "OpenGL test renderer, nor vkcube. The capture pipeline itself is covered by the "
            + "unit tests; this test needs a presenting process PresentMon actually tracks.");
        Assert.Contains(processes, p => p.ProcessName.Contains("CapFrameX.TestRenderer"));

        // Cleanup
        _captureService.StopCaptureService();
    }

    [RequiresElevationFact]
    public async Task CaptureService_WithTestRenderer_ShouldRespectProcessFilter()
    {
        // Arrange
        if (!File.Exists(TestRendererPath))
        {
            Assert.Fail($"TestRenderer not found at {TestRendererPath}");
        }

        _testRendererProcess = Process.Start(new ProcessStartInfo
        {
            FileName = TestRendererPath,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        Assert.NotNull(_testRendererProcess);
        await Task.Delay(1000);

        // Use redirected output configuration
        var ignoreList = PresentMonTestConfiguration.GetDefaultIgnoreList().ToList();
        var startInfo = PresentMonTestConfiguration.CreateRedirectedStartInfo(ignoreList);

        // Act
        _captureService.StartCaptureService(startInfo);
        await Task.Delay(2000);

        var filter = new HashSet<string> { "CapFrameX.TestRenderer" };
        var filteredProcesses = _captureService.GetAllFilteredProcesses(filter).ToList();

        // Assert
        Assert.DoesNotContain(filteredProcesses,
            p => p.ProcessName.Contains("CapFrameX.TestRenderer"));

        // Cleanup
        _captureService.StopCaptureService();
    }

    [RequiresElevationFact]
    public async Task CaptureService_StartStop_ShouldEmitCaptureModeStates()
    {
        // Arrange
        if (!File.Exists(TestRendererPath))
        {
            Assert.Fail($"TestRenderer not found at {TestRendererPath}");
        }

        var captureStates = new ConcurrentBag<bool>();
        var stateSubscription = _captureService.IsCaptureModeActiveStream
            .Subscribe(state => captureStates.Add(state));

        _testRendererProcess = Process.Start(new ProcessStartInfo
        {
            FileName = TestRendererPath,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        Assert.NotNull(_testRendererProcess);
        await Task.Delay(1000);

        // Use redirected output configuration
        var ignoreList = PresentMonTestConfiguration.GetDefaultIgnoreList().ToList();
        var startInfo = PresentMonTestConfiguration.CreateRedirectedStartInfo(ignoreList);

        // Act
        _captureService.StartCaptureService(startInfo);
        await Task.Delay(1000);

        _captureService.StopCaptureService();
        await Task.Delay(500);

        // Assert
        Assert.Contains(true, captureStates);
        Assert.Contains(false, captureStates);

        stateSubscription.Dispose();
    }

    [RequiresElevationFact]
    public async Task CaptureService_WithTestRenderer_ShouldCaptureConsistentFrameTiming()
    {
        // Arrange
        if (!File.Exists(TestRendererPath))
        {
            Assert.Fail($"TestRenderer not found at {TestRendererPath}");
        }

        var frameTimings = new ConcurrentBag<double>();
        var frameDataSubscription = _captureService.FrameDataStream
            .Subscribe(data =>
            {
                var msBetweenPresentsIndex = _captureService.ParameterNameIndexMapping["MsBetweenPresents"];
                if (data.Length > msBetweenPresentsIndex &&
                    double.TryParse(data[msBetweenPresentsIndex], out var timing))
                {
                    frameTimings.Add(timing);
                }
            });

        _testRendererProcess = Process.Start(new ProcessStartInfo
        {
            FileName = TestRendererPath,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        Assert.NotNull(_testRendererProcess);
        await Task.Delay(1000);

        // Use redirected output configuration
        var ignoreList = PresentMonTestConfiguration.GetDefaultIgnoreList().ToList();
        var startInfo = PresentMonTestConfiguration.CreateRedirectedStartInfo(ignoreList);

        // Act
        _captureService.StartCaptureService(startInfo);
        await Task.Delay(5000); // Capture for 5 seconds

        // Assert
        Assert.True(frameTimings.Count > 0,
            "PresentMon produced no data for the test renderer. Verified on 2026-09-20: PresentMon "
            + "2.4.0 and 2.5.1 both observe a D3D9 window on this machine but never the Silk.NET "
            + "OpenGL test renderer, nor vkcube. The capture pipeline itself is covered by the "
            + "unit tests; this test needs a presenting process PresentMon actually tracks.");

        // Calculate FPS from frame timings
        var avgFrameTime = frameTimings.Average();
        var fps = 1000.0 / avgFrameTime;

        // TestRenderer should be running at reasonable FPS (>30)
        Assert.True(fps > 30, $"Expected FPS > 30, but got {fps:F2}");

        // Frame timing should be relatively consistent (CV < 0.5)
        var stdDev = Math.Sqrt(frameTimings.Average(x => Math.Pow(x - avgFrameTime, 2)));
        var coefficientOfVariation = stdDev / avgFrameTime;
        Assert.True(coefficientOfVariation < 0.5,
            $"Frame timing should be consistent. CV: {coefficientOfVariation:F3}");

        // Cleanup
        frameDataSubscription.Dispose();
        _captureService.StopCaptureService();
    }

    [RequiresElevationFact]
    public async Task CaptureService_LongRunningCapture_ShouldNotLeakMemory()
    {
        // Arrange
        if (!File.Exists(TestRendererPath))
        {
            Assert.Fail($"TestRenderer not found at {TestRendererPath}");
        }

        var initialMemory = GC.GetTotalMemory(true);
        var frameCount = 0;
        var frameDataSubscription = _captureService.FrameDataStream
            .Subscribe(_ => Interlocked.Increment(ref frameCount));

        _testRendererProcess = Process.Start(new ProcessStartInfo
        {
            FileName = TestRendererPath,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        Assert.NotNull(_testRendererProcess);
        await Task.Delay(1000);

        // Use redirected output configuration
        var ignoreList = PresentMonTestConfiguration.GetDefaultIgnoreList().ToList();
        var startInfo = PresentMonTestConfiguration.CreateRedirectedStartInfo(ignoreList);

        // Act - Run for 30 seconds
        _captureService.StartCaptureService(startInfo);
        await Task.Delay(30000);

        // Force GC and check memory
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GC.GetTotalMemory(false);
        var memoryGrowth = finalMemory - initialMemory;

        // Assert
        Assert.True(frameCount > 0,
            "PresentMon produced no data for the test renderer. Verified on 2026-09-20: PresentMon "
            + "2.4.0 and 2.5.1 both observe a D3D9 window on this machine but never the Silk.NET "
            + "OpenGL test renderer, nor vkcube. The capture pipeline itself is covered by the "
            + "unit tests; this test needs a presenting process PresentMon actually tracks.");

        // Memory growth should be reasonable (< 50MB for 30 seconds of capture)
        Assert.True(memoryGrowth < 50 * 1024 * 1024,
            $"Memory growth excessive: {memoryGrowth / 1024 / 1024}MB");

        // Cleanup
        frameDataSubscription.Dispose();
        _captureService.StopCaptureService();
    }

    public void Dispose()
    {
        _captureService.StopCaptureService();

        if (_testRendererProcess != null && !_testRendererProcess.HasExited)
        {
            _testRendererProcess.Kill();
            _testRendererProcess.Dispose();
        }
    }
}
