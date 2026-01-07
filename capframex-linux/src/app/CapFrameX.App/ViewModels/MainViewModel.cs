using System.Diagnostics;
using System.Timers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CapFrameX.Core.Capture;
using Microsoft.Extensions.DependencyInjection;

namespace CapFrameX.App.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly CaptureService _captureService;
    private readonly System.Timers.Timer _statusTimer;
    private readonly Process _currentProcess;
    private int? _daemonPid;

    // For CPU usage calculation
    private DateTime _lastCpuCheck = DateTime.UtcNow;
    private TimeSpan _lastAppCpuTime = TimeSpan.Zero;
    private TimeSpan _lastDaemonCpuTime = TimeSpan.Zero;

    [ObservableProperty]
    private object? _currentView;

    [ObservableProperty]
    private string _connectionStatus = "Initializing...";

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private bool _isVulkanLayerActive;

    [ObservableProperty]
    private string _vulkanLayerStatus = "Checking...";

    [ObservableProperty]
    private string _appMemoryUsage = "-- MB";

    [ObservableProperty]
    private string _appCpuUsage = "-- %";

    [ObservableProperty]
    private string _daemonMemoryUsage = "-- MB";

    [ObservableProperty]
    private string _daemonCpuUsage = "-- %";

    public CaptureViewModel CaptureViewModel { get; }
    public AnalysisViewModel AnalysisViewModel { get; }
    public CompareViewModel CompareViewModel { get; }

    public MainViewModel(CaptureService captureService)
    {
        _captureService = captureService;
        _currentProcess = Process.GetCurrentProcess();

        CaptureViewModel = App.Services.GetRequiredService<CaptureViewModel>();
        AnalysisViewModel = App.Services.GetRequiredService<AnalysisViewModel>();
        CompareViewModel = App.Services.GetRequiredService<CompareViewModel>();

        CurrentView = CaptureViewModel;

        _captureService.ConnectionStatus.Subscribe(connected =>
        {
            IsConnected = connected;
            ConnectionStatus = connected ? "Ready to capture" : "Daemon offline";
            if (connected)
            {
                FindDaemonProcess();
            }
        });

        // Check Vulkan layer status
        CheckVulkanLayerStatus();

        // Start status monitoring timer
        _statusTimer = new System.Timers.Timer(1000);
        _statusTimer.Elapsed += OnStatusTimerElapsed;
        _statusTimer.Start();

        // Auto-connect on startup
        _ = ConnectAsync();
    }

    private void CheckVulkanLayerStatus()
    {
        var layerManifestPath = "/usr/share/vulkan/implicit_layer.d/capframex_layer.json";
        IsVulkanLayerActive = File.Exists(layerManifestPath);
        VulkanLayerStatus = IsVulkanLayerActive ? "Layer active" : "Layer not installed";
    }

    private void FindDaemonProcess()
    {
        try
        {
            var processes = Process.GetProcessesByName("capframex-daemon");
            if (processes.Length > 0)
            {
                _daemonPid = processes[0].Id;
            }
        }
        catch
        {
            _daemonPid = null;
        }
    }

    private void OnStatusTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        try
        {
            var now = DateTime.UtcNow;
            var elapsed = (now - _lastCpuCheck).TotalMilliseconds;
            if (elapsed < 100) return; // Avoid division by zero

            // App stats
            _currentProcess.Refresh();
            var appMemMb = _currentProcess.WorkingSet64 / (1024.0 * 1024.0);
            AppMemoryUsage = $"{appMemMb:F0} MB";

            var currentAppCpuTime = _currentProcess.TotalProcessorTime;
            var appCpuDelta = (currentAppCpuTime - _lastAppCpuTime).TotalMilliseconds;
            var appCpuPercent = (appCpuDelta / elapsed) * 100.0 / Environment.ProcessorCount;
            AppCpuUsage = $"{appCpuPercent:F1} %";
            _lastAppCpuTime = currentAppCpuTime;

            // Daemon stats
            if (_daemonPid.HasValue)
            {
                try
                {
                    var daemonProcess = Process.GetProcessById(_daemonPid.Value);
                    daemonProcess.Refresh();
                    var daemonMemMb = daemonProcess.WorkingSet64 / (1024.0 * 1024.0);
                    DaemonMemoryUsage = $"{daemonMemMb:F0} MB";

                    var currentDaemonCpuTime = daemonProcess.TotalProcessorTime;
                    var daemonCpuDelta = (currentDaemonCpuTime - _lastDaemonCpuTime).TotalMilliseconds;
                    var daemonCpuPercent = (daemonCpuDelta / elapsed) * 100.0 / Environment.ProcessorCount;
                    DaemonCpuUsage = $"{daemonCpuPercent:F1} %";
                    _lastDaemonCpuTime = currentDaemonCpuTime;

                    daemonProcess.Dispose();
                }
                catch
                {
                    DaemonMemoryUsage = "-- MB";
                    DaemonCpuUsage = "-- %";
                    _daemonPid = null;
                    _lastDaemonCpuTime = TimeSpan.Zero;
                }
            }
            else if (IsConnected)
            {
                FindDaemonProcess();
            }

            _lastCpuCheck = now;
        }
        catch
        {
            // Ignore errors in status update
        }
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        ConnectionStatus = "Starting daemon...";
        var success = await _captureService.ConnectAsync();
        if (!success)
        {
            ConnectionStatus = "Daemon unavailable";
        }
    }

    public void Dispose()
    {
        _statusTimer.Stop();
        _statusTimer.Dispose();
        _currentProcess.Dispose();
    }

    [RelayCommand]
    private void SelectTab(string indexStr)
    {
        if (int.TryParse(indexStr, out var index))
            SelectedTabIndex = index;
    }

    partial void OnSelectedTabIndexChanged(int value)
    {
        CurrentView = value switch
        {
            0 => CaptureViewModel,
            1 => AnalysisViewModel,
            2 => CompareViewModel,
            _ => CaptureViewModel
        };
    }
}
