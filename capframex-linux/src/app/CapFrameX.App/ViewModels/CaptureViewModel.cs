using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CapFrameX.Core.Capture;
using CapFrameX.Core.Configuration;
using CapFrameX.Core.Data;
using CapFrameX.Core.Hardware;
using CapFrameX.Core.Hotkey;
using CapFrameX.Core.System;
using CapFrameX.Shared.Models;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.Defaults;
using SkiaSharp;
using Avalonia.Media;

namespace CapFrameX.App.ViewModels;

public partial class CaptureViewModel : ObservableObject, IDisposable
{
    private readonly CaptureService _captureService;
    private readonly FrametimeReceiver _frametimeReceiver;
    private readonly SessionManager _sessionManager;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly ISettingsService _settingsService;
    private readonly ISystemInfoService _systemInfoService;
    private readonly IDisposable _gameDetectedSub;
    private readonly IDisposable _gameUpdatedSub;
    private readonly IDisposable _gameExitedSub;
    private readonly IDisposable _frameDataSub;
    private readonly IDisposable _hotkeySub;
    private readonly IDisposable _settingsSub;
    private readonly System.Timers.Timer _statsTimer;
    private readonly System.Timers.Timer _processPollTimer;
    private readonly System.Timers.Timer _hardwareMetricsTimer;
    private System.Timers.Timer? _autoStopTimer;

    [ObservableProperty]
    private ObservableCollection<GameInfo> _detectedGames = new();

    [ObservableProperty]
    private GameInfo? _selectedGame;

    [ObservableProperty]
    private bool _isCapturing;

    [ObservableProperty]
    private bool _liveChartEnabled = true;

    private bool _isLiveViewActive;

    // Computed property for enabling capture button
    public bool CanCapture => DetectedGames.Count > 0;

    // Game info bar properties with placeholders
    private static readonly IBrush AccentBrush = new SolidColorBrush(Color.Parse("#FFFFFF"));
    private static readonly IBrush PlaceholderBrush = new SolidColorBrush(Color.Parse("#666666"));

    public string GameInfoName => SelectedGame?.Name ?? "No game selected";
    public IBrush GameInfoNameColor => SelectedGame != null ? AccentBrush : PlaceholderBrush;
    public string GameInfoResolution => !string.IsNullOrEmpty(SelectedGame?.Resolution) ? SelectedGame.Resolution : "-";
    public string GameInfoGpu => !string.IsNullOrEmpty(SelectedGame?.GpuName) ? SelectedGame.GpuName : "-";
    public string GameInfoLauncher => !string.IsNullOrEmpty(SelectedGame?.Launcher) ? SelectedGame.Launcher : "-";

    // Legacy properties for compatibility
    public bool HasSelectedGame => SelectedGame != null;

    partial void OnSelectedGameChanged(GameInfo? oldValue, GameInfo? newValue)
    {
        OnPropertyChanged(nameof(GameInfoName));
        OnPropertyChanged(nameof(GameInfoNameColor));
        OnPropertyChanged(nameof(GameInfoResolution));
        OnPropertyChanged(nameof(GameInfoGpu));
        OnPropertyChanged(nameof(GameInfoLauncher));
        OnPropertyChanged(nameof(HasSelectedGame));

        // Update live view subscription when game selection changes
        _ = UpdateLiveViewAsync();
    }

    partial void OnLiveChartEnabledChanged(bool value)
    {
        // Clear chart data when disabling
        if (!value)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => _frametimeValues.Clear());
        }
        // Update live view subscription when toggle changes
        _ = UpdateLiveViewAsync();
    }

    private async Task UpdateLiveViewAsync()
    {
        // Don't manage live view while capturing - capture handles its own subscription
        if (IsCapturing) return;

        var shouldBeActive = LiveChartEnabled && SelectedGame != null;

        if (shouldBeActive && !_isLiveViewActive)
        {
            await StartLiveViewAsync();
        }
        else if (!shouldBeActive && _isLiveViewActive)
        {
            await StopLiveViewAsync();
        }
    }

    private async Task StartLiveViewAsync()
    {
        if (_isLiveViewActive || SelectedGame == null) return;

        Console.WriteLine($"[CaptureVM] Starting live view for {SelectedGame.Name} (PID {SelectedGame.Pid})");
        _frametimeValues.Clear();
        await _captureService.StartCaptureAsync(SelectedGame.Pid);
        _isLiveViewActive = true;
    }

    private async Task StopLiveViewAsync()
    {
        if (!_isLiveViewActive) return;

        Console.WriteLine("[CaptureVM] Stopping live view");
        await _captureService.StopCaptureAsync();
        _isLiveViewActive = false;
    }

    [ObservableProperty]
    private string _captureButtonText = "Start Capture";

    // Live statistics
    [ObservableProperty]
    private float _currentFps;

    [ObservableProperty]
    private float _p1LowFps;

    [ObservableProperty]
    private float _p01LowFps;

    [ObservableProperty]
    private int _frameCount;

    [ObservableProperty]
    private string _captureDuration = "00:00:00";

    [ObservableProperty]
    private float _averageFrametime;

    // GPU Metrics
    [ObservableProperty]
    private string _gpuTemperature = "--";

    [ObservableProperty]
    private string _gpuPower = "--";

    [ObservableProperty]
    private string _gpuUsage = "--";

    [ObservableProperty]
    private string _gpuCoreClock = "--";

    [ObservableProperty]
    private string _gpuMemClock = "--";

    [ObservableProperty]
    private string _gpuVram = "--";

    // CPU Metrics
    [ObservableProperty]
    private string _cpuTemperature = "--";

    [ObservableProperty]
    private string _cpuFrequency = "--";

    [ObservableProperty]
    private string _cpuUsage = "--";

    // Live chart
    private readonly ObservableCollection<ObservableValue> _frametimeValues = new();

    public ISeries[] FrametimeSeries { get; }
    public Axis[] XAxes { get; }
    public Axis[] YAxes { get; }

    // System info
    public SystemInfo SystemInfo { get; private set; } = new();

    public CaptureViewModel(
        CaptureService captureService,
        FrametimeReceiver frametimeReceiver,
        SessionManager sessionManager,
        IGlobalHotkeyService hotkeyService,
        ISettingsService settingsService,
        ISystemInfoService systemInfoService)
    {
        _captureService = captureService;
        _frametimeReceiver = frametimeReceiver;
        _sessionManager = sessionManager;
        _hotkeyService = hotkeyService;
        _settingsService = settingsService;
        _systemInfoService = systemInfoService;

        // Load system info
        SystemInfo = _systemInfoService.GetSystemInfo();

        // Notify CanCapture when games list changes
        DetectedGames.CollectionChanged += (_, _) => OnPropertyChanged(nameof(CanCapture));

        // Initialize chart with proper styling
        var accentColor = new SKColor(74, 163, 223); // #4AA3DF
        FrametimeSeries = new ISeries[]
        {
            new LineSeries<ObservableValue>
            {
                Values = _frametimeValues,
                Fill = null,
                GeometrySize = 0,
                LineSmoothness = 0,
                Stroke = new SolidColorPaint(accentColor) { StrokeThickness = 2 },
                Name = "Frametime"
            }
        };

        var whitePaint = new SolidColorPaint(SKColors.White);
        var grayPaint = new SolidColorPaint(new SKColor(100, 100, 100));

        XAxes = new Axis[]
        {
            new Axis
            {
                Name = "Frames",
                MinLimit = 0,
                NamePaint = whitePaint,
                LabelsPaint = whitePaint,
                SeparatorsPaint = grayPaint
            }
        };

        YAxes = new Axis[]
        {
            new Axis
            {
                Name = "Frametime (ms)",
                MinLimit = 0,
                NamePaint = whitePaint,
                LabelsPaint = whitePaint,
                SeparatorsPaint = grayPaint
            }
        };

        // Subscribe to events
        _gameDetectedSub = _captureService.GameDetected.Subscribe(game =>
        {
            Console.WriteLine($"[CaptureVM] Game detected: {game.Name} (PID {game.Pid}), Launcher: '{game.Launcher}', GPU: '{game.GpuName}', Resolution: {game.Resolution}");
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                DetectedGames.Add(game);
                // Auto-select first detected game
                if (SelectedGame == null)
                    SelectedGame = game;
            });
        });

        _gameUpdatedSub = _captureService.GameUpdated.Subscribe(update =>
        {
            Console.WriteLine($"[CaptureVM] Game updated: {update.Name} (PID {update.Pid}) - {update.Resolution} on {update.GpuName}");
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var game = DetectedGames.FirstOrDefault(g => g.Pid == update.Pid);
                if (game != null)
                {
                    // GameInfo implements INotifyPropertyChanged, so UI auto-refreshes
                    game.GpuName = update.GpuName;
                    game.ResolutionWidth = update.ResolutionWidth;
                    game.ResolutionHeight = update.ResolutionHeight;
                    if (!string.IsNullOrEmpty(update.Launcher))
                        game.Launcher = update.Launcher;

                    // If this is the selected game, notify computed properties
                    if (SelectedGame?.Pid == update.Pid)
                    {
                        OnPropertyChanged(nameof(GameInfoResolution));
                        OnPropertyChanged(nameof(GameInfoGpu));
                        OnPropertyChanged(nameof(GameInfoLauncher));
                    }
                }
            });
        });

        _gameExitedSub = _captureService.GameExited.Subscribe(pid =>
        {
            Console.WriteLine($"[CaptureVM] Game exited: PID {pid}");
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var game = DetectedGames.FirstOrDefault(g => g.Pid == pid);
                if (game != null) DetectedGames.Remove(game);
            });
        });

        _frameDataSub = _captureService.FrameData.Subscribe(frame =>
        {
            _frametimeReceiver.AddFrame(frame);
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _frametimeValues.Add(new ObservableValue(frame.FrametimeMs));
                if (_frametimeValues.Count > 500)
                {
                    _frametimeValues.RemoveAt(0);
                }
            });
        });

        // Stats update timer
        _statsTimer = new System.Timers.Timer(500);
        _statsTimer.Elapsed += (_, _) => UpdateLiveStats();
        _statsTimer.Start();

        // Process polling timer - check every 2 seconds if processes are still running
        _processPollTimer = new System.Timers.Timer(2000);
        _processPollTimer.Elapsed += (_, _) => PollProcesses();
        _processPollTimer.Start();

        // Hardware metrics timer - update every second
        _hardwareMetricsTimer = new System.Timers.Timer(1000);
        _hardwareMetricsTimer.Elapsed += (_, _) => UpdateHardwareMetrics();
        _hardwareMetricsTimer.Start();

        // Populate with already detected games
        foreach (var game in _captureService.DetectedGames)
        {
            DetectedGames.Add(game);
        }
        // Auto-select first game if any were already detected
        if (SelectedGame == null && DetectedGames.Count > 0)
        {
            SelectedGame = DetectedGames[0];
            Console.WriteLine($"[CaptureVM] Auto-selected existing game: {SelectedGame.Name} (PID {SelectedGame.Pid})");
        }

        // Setup hotkey service
        _hotkeyService.SetCaptureHotkey(_settingsService.Settings.CaptureHotkey);
        _hotkeySub = _hotkeyService.CaptureHotkeyPressed.Subscribe(_ =>
        {
            Console.WriteLine("[CaptureVM] Hotkey pressed - toggling capture");
            Avalonia.Threading.Dispatcher.UIThread.Post(async () => await ToggleCaptureAsync());
        });

        // Subscribe to settings changes
        _settingsSub = _settingsService.SettingsChanged.Subscribe(settings =>
        {
            Console.WriteLine($"[CaptureVM] Settings changed - updating hotkey to {settings.CaptureHotkey}");
            _hotkeyService.SetCaptureHotkey(settings.CaptureHotkey);
        });
    }

    private void UpdateLiveStats()
    {
        // Update stats when capturing OR live view is active
        if (!IsCapturing && !_isLiveViewActive) return;

        var stats = _frametimeReceiver.GetLiveStats();

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            CurrentFps = stats.CurrentFps;
            P1LowFps = stats.P1Low;
            P01LowFps = stats.P01Low;
            FrameCount = stats.FrameCount;
            CaptureDuration = stats.Duration.ToString(@"hh\:mm\:ss");
            AverageFrametime = stats.AverageFrametime;
        });
    }

    private void UpdateHardwareMetrics()
    {
        try
        {
            var hwMonitor = _systemInfoService.HardwareMonitor;

            // GPU metrics
            var gpuMetrics = hwMonitor.GetGpuMetrics();
            if (gpuMetrics != null)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    // Fixed width formatting: Temp 3, Power 3, Usage 3, Core 4, Mem 4, VRAM ##.#
                    GpuTemperature = gpuMetrics.Temperature.HasValue
                        ? $"{gpuMetrics.Temperature.Value.ToString("F0").PadLeft(3)}°C" : "  --";
                    GpuPower = gpuMetrics.PowerWatts.HasValue
                        ? $"{gpuMetrics.PowerWatts.Value.ToString("F0").PadLeft(3)}W" : " --";
                    GpuUsage = gpuMetrics.UsagePercent.HasValue
                        ? $"{gpuMetrics.UsagePercent.Value.ToString().PadLeft(3)}%" : " --";
                    GpuCoreClock = gpuMetrics.CoreClockMhz.HasValue
                        ? $"{gpuMetrics.CoreClockMhz.Value.ToString().PadLeft(4)}MHz" : "  --";
                    GpuMemClock = gpuMetrics.MemoryClockMhz.HasValue
                        ? $"{gpuMetrics.MemoryClockMhz.Value.ToString().PadLeft(4)}MHz" : "  --";

                    if (gpuMetrics.VramUsed.HasValue && gpuMetrics.VramTotal.HasValue)
                    {
                        var usedGb = gpuMetrics.VramUsed.Value / (1024.0 * 1024 * 1024);
                        var totalGb = gpuMetrics.VramTotal.Value / (1024.0 * 1024 * 1024);
                        GpuVram = $"{usedGb.ToString("F1").PadLeft(4)}/{totalGb:F1}GB";
                    }
                    else
                    {
                        GpuVram = "  --";
                    }
                });
            }

            // CPU metrics
            var cpuMetrics = hwMonitor.GetCpuMetrics();
            if (cpuMetrics != null)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    // Fixed width formatting: Load 3, Temp 3, Freq 4
                    CpuUsage = cpuMetrics.UsagePercent.HasValue
                        ? $"{cpuMetrics.UsagePercent.Value.ToString("F0").PadLeft(3)}%" : " --";
                    CpuTemperature = cpuMetrics.Temperature.HasValue
                        ? $"{cpuMetrics.Temperature.Value.ToString("F0").PadLeft(3)}°C" : "  --";
                    CpuFrequency = cpuMetrics.FrequencyMhz.HasValue
                        ? $"{cpuMetrics.FrequencyMhz.Value.ToString().PadLeft(4)}MHz" : "  --";
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CaptureVM] Error updating hardware metrics: {ex.Message}");
        }
    }

    private void PollProcesses()
    {
        var deadProcesses = new List<GameInfo>();

        foreach (var game in DetectedGames.ToList())
        {
            try
            {
                Process.GetProcessById(game.Pid);
            }
            catch (ArgumentException)
            {
                // Process no longer exists
                deadProcesses.Add(game);
            }
        }

        if (deadProcesses.Count > 0)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                foreach (var game in deadProcesses)
                {
                    Console.WriteLine($"[CaptureVM] Process exited (poll): {game.Name} (PID {game.Pid})");
                    DetectedGames.Remove(game);

                    // Stop capture if the captured process exited
                    if (IsCapturing && SelectedGame?.Pid == game.Pid)
                    {
                        _ = StopCaptureAsync();
                    }
                }
            });
        }
    }

    [RelayCommand]
    private async Task ToggleCaptureAsync()
    {
        if (IsCapturing)
        {
            await StopCaptureAsync();
        }
        else
        {
            await StartCaptureAsync();
        }
    }

    [RelayCommand]
    private async Task AddToIgnoreListAsync(GameInfo? game)
    {
        if (game == null) return;

        Console.WriteLine($"[CaptureVM] Adding to ignore list: {game.Name}");

        await _captureService.AddToIgnoreListAsync(game.Name);

        // Remove from detected games list immediately (daemon will filter future detections)
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            DetectedGames.Remove(game);
            if (SelectedGame?.Pid == game.Pid)
            {
                SelectedGame = null;
            }
        });
    }

    private async Task StartCaptureAsync()
    {
        // Use selected game, or first detected game if none selected
        var targetGame = SelectedGame ?? DetectedGames.FirstOrDefault();
        if (targetGame == null) return;

        // Auto-select the game we're capturing
        if (SelectedGame == null)
            SelectedGame = targetGame;

        Console.WriteLine($"[CaptureVM] Starting capture for {targetGame.Name} (PID {targetGame.Pid})");

        _frametimeValues.Clear();
        _frametimeReceiver.StartCapture();

        // If live view is already active for the same game, we're already subscribed
        // Otherwise, subscribe now
        if (!_isLiveViewActive || _captureService.CapturingPid != targetGame.Pid)
        {
            await _captureService.StartCaptureAsync(targetGame.Pid);
        }
        _isLiveViewActive = false; // Capture takes over from live view

        IsCapturing = true;
        CaptureButtonText = "Stop Capture";
        Console.WriteLine($"[CaptureVM] Capture started - subscribed to PID {targetGame.Pid}");

        // Setup auto-stop timer if enabled
        var settings = _settingsService.Settings;
        if (settings.AutoStopEnabled && settings.CaptureDurationSeconds > 0)
        {
            Console.WriteLine($"[CaptureVM] Auto-stop enabled: {settings.CaptureDurationSeconds} seconds");
            _autoStopTimer?.Dispose();
            // Add 500ms buffer to account for timer/dispatch latency and ensure full duration
            _autoStopTimer = new System.Timers.Timer(settings.CaptureDurationSeconds * 1000 + 500);
            _autoStopTimer.AutoReset = false;
            _autoStopTimer.Elapsed += async (_, _) =>
            {
                Console.WriteLine("[CaptureVM] Auto-stop timer elapsed");
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () => await StopCaptureAsync());
            };
            _autoStopTimer.Start();
        }
    }

    private async Task StopCaptureAsync()
    {
        // Cancel auto-stop timer if running
        _autoStopTimer?.Stop();
        _autoStopTimer?.Dispose();
        _autoStopTimer = null;

        await _captureService.StopCaptureAsync();
        _frametimeReceiver.StopCapture();

        IsCapturing = false;
        CaptureButtonText = "Start Capture";

        // Save the session
        var frames = _frametimeReceiver.GetCapturedFrames();
        if (frames.Count > 0)
        {
            var session = new CaptureSession
            {
                GameName = SelectedGame?.Name ?? "Unknown",
                StartTime = DateTime.Now.AddMilliseconds(-frames.Sum(f => f.FrametimeMs)),
                EndTime = DateTime.Now,
                Frames = frames.ToList()
            };

            await _sessionManager.SaveSessionAsync(session);
        }

        // Resume live view if enabled
        await UpdateLiveViewAsync();
    }

    public void Dispose()
    {
        _gameDetectedSub.Dispose();
        _gameUpdatedSub.Dispose();
        _gameExitedSub.Dispose();
        _frameDataSub.Dispose();
        _hotkeySub.Dispose();
        _settingsSub.Dispose();
        _statsTimer.Dispose();
        _processPollTimer.Dispose();
        _hardwareMetricsTimer.Dispose();
        _autoStopTimer?.Dispose();
    }
}
