using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CapFrameX.Core.Capture;
using CapFrameX.Core.Data;
using CapFrameX.Shared.Models;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.Defaults;

namespace CapFrameX.App.ViewModels;

public partial class CaptureViewModel : ObservableObject, IDisposable
{
    private readonly CaptureService _captureService;
    private readonly FrametimeReceiver _frametimeReceiver;
    private readonly SessionManager _sessionManager;
    private readonly IDisposable _gameDetectedSub;
    private readonly IDisposable _gameExitedSub;
    private readonly IDisposable _frameDataSub;
    private readonly System.Timers.Timer _statsTimer;

    [ObservableProperty]
    private ObservableCollection<GameInfo> _detectedGames = new();

    [ObservableProperty]
    private GameInfo? _selectedGame;

    [ObservableProperty]
    private bool _isCapturing;

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

    // Live chart
    private readonly ObservableCollection<ObservableValue> _frametimeValues = new();

    public ISeries[] FrametimeSeries { get; }
    public Axis[] XAxes { get; }
    public Axis[] YAxes { get; }

    public CaptureViewModel(CaptureService captureService, FrametimeReceiver frametimeReceiver, SessionManager sessionManager)
    {
        _captureService = captureService;
        _frametimeReceiver = frametimeReceiver;
        _sessionManager = sessionManager;

        // Initialize chart
        FrametimeSeries = new ISeries[]
        {
            new LineSeries<ObservableValue>
            {
                Values = _frametimeValues,
                Fill = null,
                GeometrySize = 0,
                LineSmoothness = 0
            }
        };

        XAxes = new Axis[]
        {
            new Axis { Name = "Frames", MinLimit = 0 }
        };

        YAxes = new Axis[]
        {
            new Axis { Name = "Frametime (ms)", MinLimit = 0 }
        };

        // Subscribe to events
        _gameDetectedSub = _captureService.GameDetected.Subscribe(game =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => DetectedGames.Add(game));
        });

        _gameExitedSub = _captureService.GameExited.Subscribe(pid =>
        {
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

        // Populate with already detected games
        foreach (var game in _captureService.DetectedGames)
        {
            DetectedGames.Add(game);
        }
    }

    private void UpdateLiveStats()
    {
        if (!IsCapturing) return;

        var stats = _frametimeReceiver.GetLiveStats();
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            CurrentFps = stats.CurrentFps;
            P1LowFps = stats.P1Low;
            P01LowFps = stats.P01Low;
            FrameCount = stats.FrameCount;
            CaptureDuration = stats.Duration.ToString(@"hh\:mm\:ss");
        });
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

    private async Task StartCaptureAsync()
    {
        if (SelectedGame == null) return;

        _frametimeValues.Clear();
        _frametimeReceiver.StartCapture();

        await _captureService.StartCaptureAsync(SelectedGame.Pid);

        IsCapturing = true;
        CaptureButtonText = "Stop Capture";
    }

    private async Task StopCaptureAsync()
    {
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
    }

    public void Dispose()
    {
        _gameDetectedSub.Dispose();
        _gameExitedSub.Dispose();
        _frameDataSub.Dispose();
        _statsTimer.Dispose();
    }
}
