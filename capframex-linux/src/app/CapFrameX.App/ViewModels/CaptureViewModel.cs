using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CapFrameX.Core.Capture;
using CapFrameX.Core.Data;
using CapFrameX.Shared.Models;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.Defaults;
using SkiaSharp;

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
    private readonly System.Timers.Timer _processPollTimer;

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

    [ObservableProperty]
    private float _averageFrametime;

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
            Console.WriteLine($"[CaptureVM] Game detected: {game.Name} (PID {game.Pid})");
            Avalonia.Threading.Dispatcher.UIThread.Post(() => DetectedGames.Add(game));
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

        // Populate with already detected games
        foreach (var game in _captureService.DetectedGames)
        {
            DetectedGames.Add(game);
        }
    }

    private int _lastLoggedFrameCount = 0;

    private void UpdateLiveStats()
    {
        if (!IsCapturing) return;

        var stats = _frametimeReceiver.GetLiveStats();

        // Log frame count every 100 frames for debugging
        if (stats.FrameCount > _lastLoggedFrameCount + 100)
        {
            Console.WriteLine($"[CaptureVM] Frames: {stats.FrameCount}, FPS: {stats.CurrentFps:F1}, Avg: {stats.AverageFrametime:F2}ms");
            _lastLoggedFrameCount = stats.FrameCount;
        }

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

    private async Task StartCaptureAsync()
    {
        if (SelectedGame == null) return;

        Console.WriteLine($"[CaptureVM] Starting capture for {SelectedGame.Name} (PID {SelectedGame.Pid})");

        _frametimeValues.Clear();
        _frametimeReceiver.StartCapture();

        await _captureService.StartCaptureAsync(SelectedGame.Pid);

        IsCapturing = true;
        CaptureButtonText = "Stop Capture";
        Console.WriteLine($"[CaptureVM] Capture started - subscribed to PID {SelectedGame.Pid}");
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
        _processPollTimer.Dispose();
    }
}
