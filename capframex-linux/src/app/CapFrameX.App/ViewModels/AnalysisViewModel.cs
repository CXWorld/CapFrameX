using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CapFrameX.Core.Analysis;
using CapFrameX.Core.Data;
using CapFrameX.Shared.Models;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.Defaults;

namespace CapFrameX.App.ViewModels;

public partial class AnalysisViewModel : ObservableObject
{
    private readonly SessionManager _sessionManager;
    private CaptureSession? _currentSession;
    private FrametimeAnalyzer? _analyzer;

    [ObservableProperty]
    private ObservableCollection<SessionMetadata> _sessions = new();

    [ObservableProperty]
    private SessionMetadata? _selectedSession;

    [ObservableProperty]
    private bool _isLoading;

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

    // Charts
    private readonly ObservableCollection<ObservableValue> _frametimeValues = new();
    private readonly ObservableCollection<ObservableValue> _fpsValues = new();
    private readonly ObservableCollection<ObservablePoint> _histogramValues = new();

    public ISeries[] FrametimeSeries { get; }
    public ISeries[] FpsSeries { get; }
    public ISeries[] HistogramSeries { get; }

    public Axis[] FrametimeXAxes { get; } = { new Axis { Name = "Time (s)" } };
    public Axis[] FrametimeYAxes { get; } = { new Axis { Name = "Frametime (ms)" } };
    public Axis[] FpsXAxes { get; } = { new Axis { Name = "Time (s)" } };
    public Axis[] FpsYAxes { get; } = { new Axis { Name = "FPS" } };

    public AnalysisViewModel(SessionManager sessionManager)
    {
        _sessionManager = sessionManager;

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

    [RelayCommand]
    private async Task LoadSessionsAsync()
    {
        IsLoading = true;
        Sessions.Clear();

        var sessions = await _sessionManager.GetSessionsAsync();
        foreach (var session in sessions)
        {
            Sessions.Add(session);
        }

        IsLoading = false;
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

        _sessionManager.DeleteSession(SelectedSession.FilePath);
        Sessions.Remove(SelectedSession);
        SelectedSession = null;

        // Clear analysis
        _frametimeValues.Clear();
        _fpsValues.Clear();
        _histogramValues.Clear();
        AverageFps = 0;
        P1Fps = 0;
        P01Fps = 0;
    }
}
