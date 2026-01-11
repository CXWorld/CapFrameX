using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CapFrameX.Core.Analysis;
using CapFrameX.Core.Data;
using CapFrameX.Shared.Models;
using Avalonia.Media;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.Defaults;
using SkiaSharp;

namespace CapFrameX.App.ViewModels;

public partial class AnalysisViewModel : ObservableObject, IDisposable
{
    private readonly SessionManager _sessionManager;
    private CaptureSession? _currentSession;
    private FrametimeAnalyzer? _analyzer;
    private readonly SynchronizationContext? _syncContext;

    [ObservableProperty]
    private ObservableCollection<SessionMetadata> _sessions = new();

    [ObservableProperty]
    private SessionMetadata? _selectedSession;

    [ObservableProperty]
    private bool _isLoading;

    private bool _isRefreshing;

    // Statistics
    [ObservableProperty]
    private double _averageFps;

    [ObservableProperty]
    private double _p1Fps;

    [ObservableProperty]
    private double _p01Fps;

    [ObservableProperty]
    private double _averageFrametime;

    [ObservableProperty]
    private double _minFrametime;

    [ObservableProperty]
    private double _maxFrametime;

    [ObservableProperty]
    private double _stdDev;

    [ObservableProperty]
    private int _frameCount;

    [ObservableProperty]
    private string _duration = "";

    // Session info
    [ObservableProperty]
    private string _gameName = "";

    [ObservableProperty]
    private string _gpuName = "";

    [ObservableProperty]
    private string _resolution = "";

    [ObservableProperty]
    private string _timingMode = "";

    public bool HasPresentTiming => TimingMode == "Present Timing";

    public IBrush TimingModeColor => HasPresentTiming
        ? new SolidColorBrush(Color.Parse("#27AE60"))  // Green for Present Timing
        : new SolidColorBrush(Colors.White);           // White for Layer Timing

    // Charts
    private readonly ObservableCollection<ObservableValue> _frametimeValues = new();
    private readonly ObservableCollection<ObservableValue> _fpsValues = new();
    private readonly ObservableCollection<ObservablePoint> _histogramValues = new();

    public ISeries[] FrametimeSeries { get; }
    public ISeries[] FpsSeries { get; }
    public ISeries[] HistogramSeries { get; }

    private static readonly SolidColorPaint WhitePaint = new(SKColors.White);
    private static readonly SolidColorPaint GrayPaint = new(new SKColor(100, 100, 100));

    public Axis[] FrametimeXAxes { get; } = { new Axis { Name = "Time (s)", NamePaint = WhitePaint, LabelsPaint = WhitePaint, SeparatorsPaint = GrayPaint } };
    public Axis[] FrametimeYAxes { get; } = { new Axis { Name = "Frametime (ms)", NamePaint = WhitePaint, LabelsPaint = WhitePaint, SeparatorsPaint = GrayPaint } };
    public Axis[] FpsXAxes { get; } = { new Axis { Name = "Time (s)", NamePaint = WhitePaint, LabelsPaint = WhitePaint, SeparatorsPaint = GrayPaint } };
    public Axis[] FpsYAxes { get; } = { new Axis { Name = "FPS", NamePaint = WhitePaint, LabelsPaint = WhitePaint, SeparatorsPaint = GrayPaint } };

    public AnalysisViewModel(SessionManager sessionManager)
    {
        _sessionManager = sessionManager;
        _syncContext = SynchronizationContext.Current;

        // Subscribe to session changes
        _sessionManager.SessionsChanged += OnSessionsChanged;
        _sessionManager.SessionAdded += OnSessionAdded;
        _sessionManager.SessionRemoved += OnSessionRemoved;

        FrametimeSeries = new ISeries[]
        {
            new LineSeries<ObservableValue>
            {
                Values = _frametimeValues,
                Fill = null,
                GeometrySize = 0,
                LineSmoothness = 0,
                Name = "Frametime"
            }
        };

        FpsSeries = new ISeries[]
        {
            new LineSeries<ObservableValue>
            {
                Values = _fpsValues,
                Fill = null,
                GeometrySize = 0,
                LineSmoothness = 0,
                Name = "FPS"
            }
        };

        HistogramSeries = new ISeries[]
        {
            new ColumnSeries<ObservablePoint>
            {
                Values = _histogramValues,
                Name = "Distribution"
            }
        };

        _ = LoadSessionsAsync();
    }

    private void OnSessionsChanged(object? sender, EventArgs e)
    {
        // Full refresh for external changes (files added/removed outside the app)
        if (_syncContext != null)
        {
            _syncContext.Post(_ => _ = LoadSessionsAsync(), null);
        }
        else
        {
            _ = LoadSessionsAsync();
        }
    }

    private void OnSessionAdded(object? sender, SessionMetadata metadata)
    {
        // Insert at beginning (newest first)
        void AddSession() => Sessions.Insert(0, metadata);

        if (_syncContext != null)
            _syncContext.Post(_ => AddSession(), null);
        else
            AddSession();
    }

    private void OnSessionRemoved(object? sender, string filePath)
    {
        void RemoveSession()
        {
            var session = Sessions.FirstOrDefault(s => s.FilePath == filePath);
            if (session != null)
            {
                Sessions.Remove(session);

                // Clear analysis if the removed session was selected
                if (SelectedSession == session)
                {
                    SelectedSession = null;
                    _frametimeValues.Clear();
                    _fpsValues.Clear();
                    _histogramValues.Clear();
                    AverageFps = 0;
                    P1Fps = 0;
                    P01Fps = 0;
                }
            }
        }

        if (_syncContext != null)
            _syncContext.Post(_ => RemoveSession(), null);
        else
            RemoveSession();
    }

    public void Dispose()
    {
        _sessionManager.SessionsChanged -= OnSessionsChanged;
        _sessionManager.SessionAdded -= OnSessionAdded;
        _sessionManager.SessionRemoved -= OnSessionRemoved;
    }

    [RelayCommand]
    private async Task LoadSessionsAsync()
    {
        // Prevent concurrent refreshes
        if (_isRefreshing) return;
        _isRefreshing = true;

        try
        {
            IsLoading = true;
            Sessions.Clear();

            var sessions = await _sessionManager.GetSessionsAsync();
            foreach (var session in sessions)
            {
                Sessions.Add(session);
            }
        }
        finally
        {
            IsLoading = false;
            _isRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadSessionsAsync();
    }

    partial void OnSelectedSessionChanged(SessionMetadata? value)
    {
        if (value != null)
        {
            _ = LoadSessionDataAsync(value);
        }
    }

    private async Task LoadSessionDataAsync(SessionMetadata metadata)
    {
        IsLoading = true;

        _currentSession = await _sessionManager.LoadSessionAsync(metadata.FilePath);
        _analyzer = new FrametimeAnalyzer(_currentSession.Frames);

        var stats = _analyzer.Statistics;

        // Update session info
        GameName = _currentSession.GameName;
        GpuName = _currentSession.GpuName;
        Resolution = _currentSession.Resolution;
        TimingMode = !string.IsNullOrEmpty(_currentSession.TimingMode) ? _currentSession.TimingMode : "Layer Timing";
        OnPropertyChanged(nameof(HasPresentTiming));
        OnPropertyChanged(nameof(TimingModeColor));

        // Update statistics
        AverageFps = stats.AverageFps;
        P1Fps = stats.P1Fps;
        P01Fps = stats.P01Fps;
        AverageFrametime = stats.Average;
        MinFrametime = stats.Min;
        MaxFrametime = stats.Max;
        StdDev = stats.StdDev;
        FrameCount = _currentSession.FrameCount;
        Duration = _analyzer.Duration.ToString(@"hh\:mm\:ss");

        // Update charts
        UpdateCharts();

        IsLoading = false;
    }

    private void UpdateCharts()
    {
        if (_analyzer == null) return;

        _frametimeValues.Clear();
        _fpsValues.Clear();
        _histogramValues.Clear();

        // Frametime and FPS time series
        var frametimes = _analyzer.GetFrametimes();
        var fps = _analyzer.GetFpsValues();

        // Downsample if too many points
        var step = Math.Max(1, frametimes.Count / 1000);

        for (int i = 0; i < frametimes.Count; i += step)
        {
            _frametimeValues.Add(new ObservableValue(frametimes[i]));
            _fpsValues.Add(new ObservableValue(fps[i]));
        }

        // Histogram
        var (bins, counts) = _analyzer.GetHistogramData(30);
        for (int i = 0; i < bins.Length; i++)
        {
            _histogramValues.Add(new ObservablePoint(bins[i], counts[i]));
        }
    }

    [RelayCommand]
    private void DeleteSession()
    {
        if (SelectedSession == null) return;

        // SessionRemoved event will handle collection update and clearing analysis
        _sessionManager.DeleteSession(SelectedSession.FilePath);
    }
}
