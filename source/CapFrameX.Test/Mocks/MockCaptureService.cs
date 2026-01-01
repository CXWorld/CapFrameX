using CapFrameX.Capture.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;

namespace CapFrameX.Test.Mocks
{
    /// <summary>
    /// Mock implementation of ICaptureService for unit testing.
    /// Generates realistic frame timing data with configurable scenarios.
    /// </summary>
    public class MockCaptureService : ICaptureService, IDisposable
    {
        // Column indices matching PresentMonCaptureService
        public const int ApplicationName_INDEX = 0;
        public const int ProcessID_INDEX = 1;
        public const int SwapChainAddress_INDEX = 2;
        public const int PresentRuntime_INDEX = 3;
        public const int SyncInterval_INDEX = 4;
        public const int PresentFlags_INDEX = 5;
        public const int AllowsTearing_INDEX = 6;
        public const int PresentMode_INDEX = 7;
        public const int TimeInSeconds_INDEX = 8;
        public const int MsBetweenSimulationStart_INDEX = 9;
        public const int MsBetweenPresents_INDEX = 10;
        public const int MsBetweenDisplayChange_INDEX = 11;
        public const int MsInPresentAPI_INDEX = 12;
        public const int MsRenderPresentLatency_INDEX = 13;
        public const int MsUntilDisplayed_INDEX = 14;
        public const int MsPCLatency_INDEX = 15;
        public const int StartTimeInMs_INDEX = 16;
        public const int MsBetweenAppStart_INDEX = 17;
        public const int CpuBusy_INDEX = 18;
        public const int MsCpuWait_INDEX = 19;
        public const int MsGpuLatency_INDEX = 20;
        public const int MsGpuTime_INDEX = 21;
        public const int GpuBusy_INDEX = 22;
        public const int MsGpuWait_INDEX = 23;
        public const int MsAnimationError_INDEX = 24;
        public const int AnimationTime_INDEX = 25;
        public const int MsFlipDelay_INDEX = 26;
        public const int EtwBufferFillPct_INDEX = 27;
        public const int EtwBuffersInUse_INDEX = 28;
        public const int EtwTotalBuffers_INDEX = 29;
        public const int EtwEventsLost_INDEX = 30;
        public const int EtwBuffersLost_INDEX = 31;
        public const int VALID_LINE_LENGTH = 32;

        public string ColumnHeader => string.Join(",",
            Enumerable.Range(0, VALID_LINE_LENGTH).Select(i => $"Column{i}"));

        private readonly Subject<string[]> _frameDataSubject;
        private readonly Random _random;
        private readonly List<MockProcess> _processes;
        private readonly object _lock = new object();

        private Timer _emissionTimer;
        private bool _isRunning;
        private double _currentTimeInSeconds;
        private double _currentQpcTimeMs;
        private SimulationScenario _scenario;

        // State for realistic MsBetweenDisplayChange simulation
        private double _pendingDisplayDebt;  // Accumulated display time debt from stutters
        private double _lastFrameWasStutter;
        private const double DISPLAY_REFRESH_60HZ = 16.6667;
        private const double DISPLAY_REFRESH_144HZ = 6.9444;

        public Dictionary<string, int> ParameterNameIndexMapping { get; }
        public IObservable<string[]> FrameDataStream => _frameDataSubject.AsObservable();
        public Subject<bool> IsCaptureModeActiveStream { get; }

        // ICaptureService dynamic index properties (mock uses fixed 32-column format)
        int ICaptureService.CPUStartQPCTimeInMs_Index => StartTimeInMs_INDEX;
        int ICaptureService.CpuBusy_Index => CpuBusy_INDEX;
        int ICaptureService.GpuBusy_Index => GpuBusy_INDEX;
        int ICaptureService.EtwBufferFillPct_Index => EtwBufferFillPct_INDEX;
        int ICaptureService.EtwBuffersInUse_Index => EtwBuffersInUse_INDEX;
        int ICaptureService.EtwTotalBuffers_Index => EtwTotalBuffers_INDEX;
        int ICaptureService.EtwEventsLost_Index => EtwEventsLost_INDEX;
        int ICaptureService.EtwBuffersLost_Index => EtwBuffersLost_INDEX;
        int ICaptureService.ValidLineLength => VALID_LINE_LENGTH;

        /// <summary>
        /// Current simulation scenario. Can be changed during capture.
        /// </summary>
        public SimulationScenario Scenario
        {
            get => _scenario;
            set => _scenario = value;
        }

        /// <summary>
        /// Interval in milliseconds between frame emissions.
        /// Set to 0 for immediate emission (useful for synchronous tests).
        /// </summary>
        public int EmissionIntervalMs { get; set; } = 16;

        /// <summary>
        /// Creates a new MockCaptureService with a specific random seed for reproducible tests.
        /// </summary>
        public MockCaptureService(int? seed = null)
        {
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
            _frameDataSubject = new Subject<string[]>();
            IsCaptureModeActiveStream = new Subject<bool>();
            _processes = new List<MockProcess>();
            _scenario = SimulationScenario.Stable60Fps;

            ParameterNameIndexMapping = new Dictionary<string, int>
            {
                { "ApplicationName", ApplicationName_INDEX },
                { "ProcessID", ProcessID_INDEX },
                { "MsBetweenPresents", MsBetweenPresents_INDEX },
                { "MsBetweenDisplayChange", MsBetweenDisplayChange_INDEX },
                { "MsPCLatency", MsPCLatency_INDEX },
                { "CPUStartQPCTimeInMs", StartTimeInMs_INDEX },
                { "MsCPUBusy", CpuBusy_INDEX },
                { "MsGPUBusy", GpuBusy_INDEX }
            };
        }

        /// <summary>
        /// Adds a mock process to simulate capturing.
        /// </summary>
        public void AddProcess(string name, int processId, string swapChainAddress = "0x00000001")
        {
            lock (_lock)
            {
                _processes.Add(new MockProcess
                {
                    Name = name,
                    ProcessId = processId,
                    SwapChainAddress = swapChainAddress
                });
            }
        }

        /// <summary>
        /// Removes a mock process from simulation.
        /// </summary>
        public void RemoveProcess(int processId)
        {
            lock (_lock)
            {
                _processes.RemoveAll(p => p.ProcessId == processId);
            }
        }

        public bool StartCaptureService(IServiceStartInfo startinfo)
        {
            if (_isRunning) return false;

            _isRunning = true;
            _currentTimeInSeconds = 0;
            _currentQpcTimeMs = 0;
            _pendingDisplayDebt = 0;
            _lastFrameWasStutter = 0;

            // Emit header first
            EmitHeader();

            if (EmissionIntervalMs > 0)
            {
                _emissionTimer = new Timer(
                    _ => EmitFrameData(),
                    null,
                    EmissionIntervalMs,
                    EmissionIntervalMs);
            }

            IsCaptureModeActiveStream.OnNext(true);
            return true;
        }

        public bool StopCaptureService()
        {
            if (!_isRunning) return false;

            _isRunning = false;
            _emissionTimer?.Dispose();
            _emissionTimer = null;

            IsCaptureModeActiveStream.OnNext(false);
            return true;
        }

        public IEnumerable<(string, int)> GetAllFilteredProcesses(HashSet<string> filter)
        {
            lock (_lock)
            {
                return _processes
                    .Where(p => !filter.Contains(p.Name))
                    .Select(p => (p.Name, p.ProcessId))
                    .ToList();
            }
        }

        /// <summary>
        /// Manually emit a single frame of data for synchronous testing.
        /// </summary>
        public void EmitSingleFrame()
        {
            EmitFrameData();
        }

        /// <summary>
        /// Emit multiple frames synchronously for testing.
        /// </summary>
        public void EmitFrames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                EmitFrameData();
            }
        }

        /// <summary>
        /// Emit a custom frame with specific values.
        /// </summary>
        /// <param name="applicationName">Application name</param>
        /// <param name="processId">Process ID</param>
        /// <param name="msBetweenPresents">Frame time in ms</param>
        /// <param name="msBetweenDisplayChange">Display change time in ms. If null, defaults to msBetweenPresents + small variance.</param>
        /// <param name="cpuBusy">CPU busy time in ms</param>
        /// <param name="gpuBusy">GPU busy time in ms</param>
        public void EmitCustomFrame(string applicationName, int processId, double msBetweenPresents,
            double? msBetweenDisplayChange = null, double cpuBusy = 5.0, double gpuBusy = 10.0)
        {
            double displayChange = msBetweenDisplayChange ?? (msBetweenPresents + _random.NextDouble() * 1.0);
            _currentTimeInSeconds += msBetweenPresents / 1000.0;
            _currentQpcTimeMs += msBetweenPresents;

            var frameData = CreateFrameData(applicationName, processId, "0x00000001",
                msBetweenPresents, displayChange, cpuBusy, gpuBusy);
            _frameDataSubject.OnNext(frameData);
        }

        private void EmitHeader()
        {
            // The real PresentMon emits a CSV header first - we skip it in mock
            // as subscribers typically skip the first line
        }

        private void EmitFrameData()
        {
            if (!_isRunning) return;

            List<MockProcess> currentProcesses;
            lock (_lock)
            {
                currentProcesses = _processes.ToList();
            }

            if (currentProcesses.Count == 0) return;

            foreach (var process in currentProcesses)
            {
                var (frameTime, displayChange, cpuBusy, gpuBusy) = GenerateRealisticMetrics();

                _currentTimeInSeconds += frameTime / 1000.0;
                _currentQpcTimeMs += frameTime;

                var frameData = CreateFrameData(
                    process.Name,
                    process.ProcessId,
                    process.SwapChainAddress,
                    frameTime,
                    displayChange,
                    cpuBusy,
                    gpuBusy);

                _frameDataSubject.OnNext(frameData);
            }
        }

        private (double frameTime, double displayChange, double cpuBusy, double gpuBusy) GenerateRealisticMetrics()
        {
            double baseFrameTime;
            double frameTimeVariance;
            double spikeChance;
            double baseCpuBusy;
            double baseGpuBusy;
            bool isStutter = false;

            switch (_scenario)
            {
                case SimulationScenario.Stable60Fps:
                    baseFrameTime = 16.67;   // ~60 FPS
                    frameTimeVariance = 0.5;
                    spikeChance = 0.02;
                    baseCpuBusy = 4.0;
                    baseGpuBusy = 12.0;
                    break;

                case SimulationScenario.Stable144Fps:
                    baseFrameTime = 6.94;    // ~144 FPS
                    frameTimeVariance = 0.3;
                    spikeChance = 0.03;
                    baseCpuBusy = 2.5;
                    baseGpuBusy = 5.5;
                    break;

                case SimulationScenario.Unstable30To60Fps:
                    baseFrameTime = 16.67 + (_random.NextDouble() * 16.67);  // 30-60 FPS
                    frameTimeVariance = 5.0;
                    spikeChance = 0.15;
                    baseCpuBusy = 8.0;
                    baseGpuBusy = 20.0;
                    break;

                case SimulationScenario.GpuBound:
                    baseFrameTime = 20.0;    // ~50 FPS, GPU limited
                    frameTimeVariance = 2.0;
                    spikeChance = 0.05;
                    baseCpuBusy = 3.0;
                    baseGpuBusy = 18.5;      // GPU near frame time = bottleneck
                    break;

                case SimulationScenario.CpuBound:
                    baseFrameTime = 25.0;    // ~40 FPS, CPU limited
                    frameTimeVariance = 3.0;
                    spikeChance = 0.08;
                    baseCpuBusy = 22.0;      // CPU near frame time = bottleneck
                    baseGpuBusy = 8.0;
                    break;

                case SimulationScenario.Stuttering:
                    // Periodic stutters every ~30 frames
                    isStutter = _random.NextDouble() < 0.033;
                    baseFrameTime = isStutter ? 50.0 + (_random.NextDouble() * 100) : 16.67;
                    frameTimeVariance = isStutter ? 20.0 : 1.0;
                    spikeChance = 0.0;
                    baseCpuBusy = isStutter ? 40.0 : 5.0;
                    baseGpuBusy = isStutter ? 45.0 : 12.0;
                    break;

                case SimulationScenario.HighFpsLowLatency:
                    baseFrameTime = 4.17;    // ~240 FPS
                    frameTimeVariance = 0.2;
                    spikeChance = 0.01;
                    baseCpuBusy = 1.5;
                    baseGpuBusy = 3.0;
                    break;

                case SimulationScenario.VSync60:
                    // V-Sync locked at 60, very consistent
                    baseFrameTime = 16.67;
                    frameTimeVariance = 0.1;
                    spikeChance = 0.005;
                    baseCpuBusy = 5.0;
                    baseGpuBusy = 10.0;
                    break;

                default:
                    baseFrameTime = 16.67;
                    frameTimeVariance = 1.0;
                    spikeChance = 0.05;
                    baseCpuBusy = 5.0;
                    baseGpuBusy = 10.0;
                    break;
            }

            // Apply Gaussian-like variance using Box-Muller transform
            double u1 = 1.0 - _random.NextDouble();
            double u2 = 1.0 - _random.NextDouble();
            double gaussian = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);

            double frameTime = baseFrameTime + (gaussian * frameTimeVariance);

            // Random spike simulation (also counts as stutter for display change)
            if (_random.NextDouble() < spikeChance)
            {
                frameTime *= 1.5 + _random.NextDouble();  // 1.5x to 2.5x spike
                isStutter = true;
            }

            // Ensure positive frame time (minimum 1ms)
            frameTime = Math.Max(1.0, frameTime);

            // CPU/GPU busy with some variance
            double cpuBusy = baseCpuBusy + (gaussian * baseCpuBusy * 0.15);
            double gpuBusy = baseGpuBusy + (gaussian * baseGpuBusy * 0.2);

            // Clamp to reasonable ranges
            cpuBusy = Math.Max(0.1, Math.Min(frameTime * 0.95, cpuBusy));
            gpuBusy = Math.Max(0.1, Math.Min(frameTime * 0.98, gpuBusy));

            // Calculate MsBetweenDisplayChange based on scenario
            double displayChange = CalculateDisplayChange(frameTime, isStutter);

            return (frameTime, displayChange, cpuBusy, gpuBusy);
        }

        /// <summary>
        /// Calculates realistic MsBetweenDisplayChange based on frame timing and display behavior.
        /// </summary>
        private double CalculateDisplayChange(double frameTime, bool isStutter)
        {
            double displayChange;

            switch (_scenario)
            {
                case SimulationScenario.VSync60:
                    // V-Sync: Display changes are quantized to refresh intervals
                    // Frames are held until the next vsync, so display change is always ~16.67ms
                    displayChange = DISPLAY_REFRESH_60HZ + (_random.NextDouble() * 0.3 - 0.15);
                    break;

                case SimulationScenario.Stable144Fps:
                    // At 144Hz, display changes quantize to ~6.94ms intervals
                    // But if frame time > refresh, we might skip a vsync
                    if (frameTime <= DISPLAY_REFRESH_144HZ)
                    {
                        displayChange = DISPLAY_REFRESH_144HZ + (_random.NextDouble() * 0.2 - 0.1);
                    }
                    else
                    {
                        // Frame took longer, quantize to next available refresh
                        int refreshPeriods = (int)Math.Ceiling(frameTime / DISPLAY_REFRESH_144HZ);
                        displayChange = refreshPeriods * DISPLAY_REFRESH_144HZ + (_random.NextDouble() * 0.2 - 0.1);
                    }
                    break;

                case SimulationScenario.HighFpsLowLatency:
                    // Low latency mode: display changes track frame times closely
                    // Minimal buffering, frames displayed ASAP
                    displayChange = frameTime + (_random.NextDouble() * 0.5);
                    break;

                case SimulationScenario.Stuttering:
                    if (isStutter)
                    {
                        // During a stutter: frame is delayed significantly
                        // Display change matches the long frame time (frame held on screen)
                        // But also accumulate "debt" - subsequent frames will catch up
                        _pendingDisplayDebt += frameTime - DISPLAY_REFRESH_60HZ;
                        displayChange = frameTime + (_random.NextDouble() * 2.0);
                        _lastFrameWasStutter = frameTime;
                    }
                    else if (_pendingDisplayDebt > 0)
                    {
                        // Recovery frames after stutter: very short display changes
                        // as buffered frames are displayed in quick succession
                        double recovery = Math.Min(_pendingDisplayDebt, DISPLAY_REFRESH_60HZ * 0.7);
                        displayChange = Math.Max(1.0, DISPLAY_REFRESH_60HZ - recovery + (_random.NextDouble() * 1.0));
                        _pendingDisplayDebt -= recovery;
                        if (_pendingDisplayDebt < 1.0) _pendingDisplayDebt = 0;
                    }
                    else
                    {
                        // Normal frame during stuttering scenario
                        displayChange = frameTime + (_random.NextDouble() * 1.5 - 0.5);
                    }
                    break;

                case SimulationScenario.Unstable30To60Fps:
                    // Unstable scenario: display changes have high variance
                    // Sometimes frames queue up, sometimes they're displayed immediately
                    if (_random.NextDouble() < 0.1)
                    {
                        // Occasional frame queue buildup
                        displayChange = frameTime * (0.5 + _random.NextDouble() * 0.3);
                    }
                    else if (_random.NextDouble() < 0.1)
                    {
                        // Occasional display delay
                        displayChange = frameTime * (1.2 + _random.NextDouble() * 0.5);
                    }
                    else
                    {
                        displayChange = frameTime + (_random.NextDouble() * 3.0 - 1.0);
                    }
                    break;

                case SimulationScenario.GpuBound:
                case SimulationScenario.CpuBound:
                    // Bound scenarios: frames often wait for vsync after completion
                    // Display change tends to be slightly higher than frame time
                    double jitter = _random.NextDouble() * 2.0;
                    displayChange = frameTime + jitter;

                    // Occasionally quantize to vsync
                    if (_random.NextDouble() < 0.3)
                    {
                        int vsyncs = (int)Math.Ceiling(frameTime / DISPLAY_REFRESH_60HZ);
                        displayChange = vsyncs * DISPLAY_REFRESH_60HZ + (_random.NextDouble() * 0.5);
                    }
                    break;

                case SimulationScenario.Stable60Fps:
                default:
                    // Stable scenario: display change closely tracks frame time
                    // Small variance from compositor/display pipeline
                    displayChange = frameTime + (_random.NextDouble() * 1.0 - 0.3);
                    break;
            }

            // Ensure minimum display change (can't be negative or zero)
            return Math.Max(0.5, displayChange);
        }

        private string[] CreateFrameData(string appName, int processId, string swapChain,
            double msBetweenPresents, double msBetweenDisplayChange, double cpuBusy, double gpuBusy)
        {
            var data = new string[VALID_LINE_LENGTH];

            // Fill all fields with realistic values
            data[ApplicationName_INDEX] = appName;
            data[ProcessID_INDEX] = processId.ToString();
            data[SwapChainAddress_INDEX] = swapChain;
            data[PresentRuntime_INDEX] = "DXGI";
            data[SyncInterval_INDEX] = "1";
            data[PresentFlags_INDEX] = "0";
            data[AllowsTearing_INDEX] = "1";
            data[PresentMode_INDEX] = "Hardware: Independent Flip";
            data[TimeInSeconds_INDEX] = _currentTimeInSeconds.ToString("F6");
            data[MsBetweenSimulationStart_INDEX] = msBetweenPresents.ToString("F4");
            data[MsBetweenPresents_INDEX] = msBetweenPresents.ToString("F4");
            data[MsBetweenDisplayChange_INDEX] = msBetweenDisplayChange.ToString("F4");
            data[MsInPresentAPI_INDEX] = (0.1 + _random.NextDouble() * 0.3).ToString("F4");
            data[MsRenderPresentLatency_INDEX] = (msBetweenPresents * 0.8).ToString("F4");
            data[MsUntilDisplayed_INDEX] = (msBetweenPresents * 1.2).ToString("F4");
            data[MsPCLatency_INDEX] = (msBetweenPresents * 2.5 + _random.NextDouble() * 5).ToString("F4");
            data[StartTimeInMs_INDEX] = _currentQpcTimeMs.ToString("F4");
            data[MsBetweenAppStart_INDEX] = msBetweenPresents.ToString("F4");
            data[CpuBusy_INDEX] = cpuBusy.ToString("F4");
            data[MsCpuWait_INDEX] = Math.Max(0, msBetweenPresents - cpuBusy).ToString("F4");
            data[MsGpuLatency_INDEX] = (gpuBusy * 0.3).ToString("F4");
            data[MsGpuTime_INDEX] = gpuBusy.ToString("F4");
            data[GpuBusy_INDEX] = gpuBusy.ToString("F4");
            data[MsGpuWait_INDEX] = Math.Max(0, msBetweenPresents - gpuBusy).ToString("F4");
            data[MsAnimationError_INDEX] = "0.0000";
            data[AnimationTime_INDEX] = "0";
            data[MsFlipDelay_INDEX] = (0.1 + _random.NextDouble() * 0.2).ToString("F4");

            // ETW tracking fields
            data[EtwBufferFillPct_INDEX] = (_random.NextDouble() * 10).ToString("F2");
            data[EtwBuffersInUse_INDEX] = "2";
            data[EtwTotalBuffers_INDEX] = "64";
            data[EtwEventsLost_INDEX] = "0";
            data[EtwBuffersLost_INDEX] = "0";

            return data;
        }

        public void Dispose()
        {
            StopCaptureService();
            _frameDataSubject?.Dispose();
            IsCaptureModeActiveStream?.Dispose();
        }

        private class MockProcess
        {
            public string Name { get; set; }
            public int ProcessId { get; set; }
            public string SwapChainAddress { get; set; }
        }
    }

    /// <summary>
    /// Predefined simulation scenarios for realistic frame timing patterns.
    /// </summary>
    public enum SimulationScenario
    {
        /// <summary>
        /// Stable 60 FPS with minimal variance (~16.67ms frame time).
        /// </summary>
        Stable60Fps,

        /// <summary>
        /// Stable 144 FPS gaming scenario (~6.94ms frame time).
        /// </summary>
        Stable144Fps,

        /// <summary>
        /// Unstable performance ranging from 30-60 FPS with high variance.
        /// </summary>
        Unstable30To60Fps,

        /// <summary>
        /// GPU-bound scenario where GPU busy time approaches frame time.
        /// </summary>
        GpuBound,

        /// <summary>
        /// CPU-bound scenario where CPU busy time approaches frame time.
        /// </summary>
        CpuBound,

        /// <summary>
        /// Periodic stuttering with occasional long frames.
        /// </summary>
        Stuttering,

        /// <summary>
        /// High FPS (240) with low latency, typical of esports games.
        /// </summary>
        HighFpsLowLatency,

        /// <summary>
        /// V-Sync locked at 60 FPS with very consistent frame delivery.
        /// </summary>
        VSync60
    }
}
