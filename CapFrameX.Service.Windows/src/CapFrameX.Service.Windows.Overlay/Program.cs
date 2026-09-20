using CapFrameX.Service.Overlay;
using CapFrameX.Service.Overlay.Interop;

namespace CapFrameX.Service.Overlay;

internal static class Program
{
    private static SharedMemoryWriter? _sharedMemoryWriter;
    private static NamedPipeClient? _pipeClient;
    private static double _startTime;
    private static bool _running = true;

    [STAThread]
    static void Main(string[] args)
    {
        // Parse command line arguments
        uint processId = 0;
        bool unload = false;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-u" || args[i] == "--unload")
            {
                unload = true;
            }
            else if ((args[i] == "-i" || args[i] == "--pid") && i + 1 < args.Length)
            {
                uint.TryParse(args[i + 1], out processId);
                i++;
            }
        }

        // Handle unload request - find existing window and close it
        if (unload)
        {
            var existingWindow = NativeMethods.FindWindow(null, PmdpConstants.ConnectWindowName);
            if (existingWindow != IntPtr.Zero)
            {
                NativeMethods.PostMessage(existingWindow, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            }
            return;
        }

        // Check if another instance is already running
        var existingProvider = NativeMethods.FindWindow(null, PmdpConstants.ConnectWindowName);
        if (existingProvider != IntPtr.Zero)
        {
            // Already running - exit
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        try
        {
            // Initialize shared memory
            _sharedMemoryWriter = new SharedMemoryWriter();
            _sharedMemoryWriter.Initialize();

            // Initialize named pipe client
            _pipeClient = new NamedPipeClient();
            _pipeClient.DataReceived += OnSensorDataReceived;
            _pipeClient.Disconnected += OnPipeDisconnected;
            _pipeClient.ErrorOccurred += OnPipeError;

            // Connect to the CapFrameX service
            _ = ConnectToPipeAsync();

            // Record start time for relative timestamps
            _startTime = GetCurrentTimeSeconds();

            // Create hidden form with detection window name
            using var connectForm = new ConnectForm(PmdpConstants.ConnectWindowName);
            connectForm.FormClosing += (s, e) => _running = false;

            Application.Run(connectForm);
        }
        finally
        {
            _pipeClient?.Dispose();
            _sharedMemoryWriter?.Dispose();
        }
    }

    private static async Task ConnectToPipeAsync()
    {
        while (_running)
        {
            try
            {
                if (_pipeClient != null && !_pipeClient.IsConnected)
                {
                    await _pipeClient.ConnectAsync();
                    _sharedMemoryWriter?.SetStatus(PmdpStatus.Ok);
                }
            }
            catch
            {
                _sharedMemoryWriter?.SetStatus(PmdpStatus.InitFailed);
            }

            // Retry connection every 2 seconds
            await Task.Delay(2000);
        }
    }

    private static void OnSensorDataReceived(object? sender, SensorData data)
    {
        if (_sharedMemoryWriter == null)
            return;

        var frameData = new PmFrameData
        {
            Application = data.Application ?? string.Empty,
            ProcessId = data.ProcessId,
            SwapChainAddress = 0,
            Runtime = "D3D11",
            SyncInterval = 0,
            PresentFlags = 0,
            Dropped = data.Dropped ? 1u : 0u,
            TimeInSeconds = data.TimeInSeconds > 0 ? data.TimeInSeconds : GetCurrentTimeSeconds() - _startTime,
            MsInPresentApi = data.MsInPresentApi,
            MsBetweenPresents = data.MsBetweenPresents,
            AllowsTearing = 0,
            PresentMode = PmPresentMode.HardwareIndependentFlip,
            MsUntilRenderComplete = data.MsUntilRenderComplete,
            MsUntilDisplayed = data.MsUntilDisplayed,
            MsBetweenDisplayChange = data.MsBetweenDisplayChange,
            MsUntilRenderStart = data.MsUntilRenderStart,
            QpcTime = data.QpcTime > 0 ? data.QpcTime : GetQpcTime(),
            MsSinceInput = data.MsSinceInput,
            MsGpuActive = data.MsGpuActive,
            MsGpuVideoActive = 0,

            // GPU telemetry
            GpuPowerW = PmFrameDataOptDouble.Create(data.GpuPowerW),
            GpuSustainedPowerLimitW = PmFrameDataOptDouble.Create(data.GpuPowerLimitW),
            GpuVoltageV = PmFrameDataOptDouble.Create(data.GpuVoltageV),
            GpuFrequencyMhz = PmFrameDataOptDouble.Create(data.GpuFrequencyMhz),
            GpuTemperatureC = PmFrameDataOptDouble.Create(data.GpuTemperatureC),
            GpuUtilization = PmFrameDataOptDouble.Create(data.GpuUsage),
            GpuRenderComputeUtilization = PmFrameDataOptDouble.Create(null),
            GpuMediaUtilization = PmFrameDataOptDouble.Create(null),

            // VRAM telemetry
            VramPowerW = PmFrameDataOptDouble.Create(data.VramPowerW),
            VramVoltageV = PmFrameDataOptDouble.Create(data.VramVoltageV),
            VramFrequencyMhz = PmFrameDataOptDouble.Create(data.VramFrequencyMhz),
            VramEffectiveFrequencyGbs = PmFrameDataOptDouble.Create(null),
            VramTemperatureC = PmFrameDataOptDouble.Create(data.VramTemperatureC),

            // GPU memory telemetry
            GpuMemTotalSizeB = PmFrameDataOptUInt64.Create(data.VramTotalB),
            GpuMemUsedB = PmFrameDataOptUInt64.Create(data.VramUsedB),
            GpuMemMaxBandwidthBps = PmFrameDataOptUInt64.Create(null),
            GpuMemReadBandwidthBps = PmFrameDataOptDouble.Create(null),
            GpuMemWriteBandwidthBps = PmFrameDataOptDouble.Create(null),

            // Throttling flags (not implemented)
            GpuPowerLimited = PmFrameDataOptInt.Create(null),
            GpuTemperatureLimited = PmFrameDataOptInt.Create(null),
            GpuCurrentLimited = PmFrameDataOptInt.Create(null),
            GpuVoltageLimited = PmFrameDataOptInt.Create(null),
            GpuUtilizationLimited = PmFrameDataOptInt.Create(null),
            VramPowerLimited = PmFrameDataOptInt.Create(null),
            VramTemperatureLimited = PmFrameDataOptInt.Create(null),
            VramCurrentLimited = PmFrameDataOptInt.Create(null),
            VramVoltageLimited = PmFrameDataOptInt.Create(null),
            VramUtilizationLimited = PmFrameDataOptInt.Create(null),

            // CPU telemetry
            CpuUtilization = PmFrameDataOptDouble.Create(data.CpuUsage),
            CpuPowerW = PmFrameDataOptDouble.Create(data.CpuPowerW),
            CpuPowerLimitW = PmFrameDataOptDouble.Create(data.CpuPowerLimitW),
            CpuTemperatureC = PmFrameDataOptDouble.Create(data.CpuTemperatureC),
            CpuFrequency = PmFrameDataOptDouble.Create(data.CpuFrequencyMhz)
        };

        // Set fan speeds
        frameData.SetFanSpeedRpm(0, data.GpuFanRpm);
        frameData.SetFanSpeedRpm(1, data.GpuFan2Rpm);

        _sharedMemoryWriter.WriteFrame(ref frameData);
    }

    private static void OnPipeDisconnected(object? sender, EventArgs e)
    {
        _sharedMemoryWriter?.SetStatus(PmdpStatus.InitFailed);
    }

    private static void OnPipeError(object? sender, Exception e)
    {
        _sharedMemoryWriter?.SetStatus(PmdpStatus.GetFrameDataFailed);
    }

    private static double GetCurrentTimeSeconds()
    {
        return (double)Environment.TickCount64 / 1000.0;
    }

    private static ulong GetQpcTime()
    {
        NativeMethods.QueryPerformanceCounter(out long qpc);
        return (ulong)qpc;
    }
}
