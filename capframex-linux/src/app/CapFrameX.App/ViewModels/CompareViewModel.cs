using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CapFrameX.Core.Analysis;
using CapFrameX.Core.Data;
using CapFrameX.Shared.Models;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace CapFrameX.App.ViewModels;

public partial class CompareViewModel : ObservableObject, IDisposable
{
    private readonly SessionManager _sessionManager;
    private readonly List<(CaptureSession session, FrametimeAnalyzer analyzer)> _selectedSessions = new();
    private readonly SynchronizationContext? _syncContext;

    [ObservableProperty]
    private ObservableCollection<SessionMetadata> _availableSessions = new();

    private bool _isRefreshing;

    [ObservableProperty]
    private ObservableCollection<SessionMetadata> _sessionsToCompare = new();

    [ObservableProperty]
    private ObservableCollection<ComparisonRow> _comparisonTable = new();

    // Combined chart
    private readonly ObservableCollection<ISeries> _frametimeSeries = new();

    public ObservableCollection<ISeries> FrametimeSeries => _frametimeSeries;

    private static readonly SolidColorPaint WhitePaint = new(SKColors.White);
    private static readonly SolidColorPaint GrayPaint = new(new SKColor(100, 100, 100));

    public Axis[] XAxes { get; } = { new Axis { Name = "Frame", NamePaint = WhitePaint, LabelsPaint = WhitePaint, SeparatorsPaint = GrayPaint } };
    public Axis[] YAxes { get; } = { new Axis { Name = "Frametime (ms)", NamePaint = WhitePaint, LabelsPaint = WhitePaint, SeparatorsPaint = GrayPaint } };

    private static readonly SKColor[] SeriesColors =
    {
        SKColors.DodgerBlue,
        SKColors.Orange,
        SKColors.LimeGreen,
        SKColors.Red,
        SKColors.Purple
    };

    public CompareViewModel(SessionManager sessionManager)
    {
        _sessionManager = sessionManager;
        _syncContext = SynchronizationContext.Current;

        // Subscribe to session changes
        _sessionManager.SessionsChanged += OnSessionsChanged;
        _sessionManager.SessionAdded += OnSessionAdded;
        _sessionManager.SessionRemoved += OnSessionRemoved;

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
        void AddSession() => AvailableSessions.Insert(0, metadata);

        if (_syncContext != null)
            _syncContext.Post(_ => AddSession(), null);
        else
            AddSession();
    }

    private void OnSessionRemoved(object? sender, string filePath)
    {
        void RemoveSession()
        {
            var session = AvailableSessions.FirstOrDefault(s => s.FilePath == filePath);
            if (session != null)
            {
                AvailableSessions.Remove(session);

                // Also remove from comparison if present
                var compareSession = SessionsToCompare.FirstOrDefault(s => s.FilePath == filePath);
                if (compareSession != null)
                {
                    var index = SessionsToCompare.IndexOf(compareSession);
                    SessionsToCompare.RemoveAt(index);
                    _selectedSessions.RemoveAt(index);
                    UpdateComparison();
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
            AvailableSessions.Clear();

            var sessions = await _sessionManager.GetSessionsAsync();
            foreach (var session in sessions)
            {
                AvailableSessions.Add(session);
            }
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task AddSessionAsync(SessionMetadata? session)
    {
        if (session == null || SessionsToCompare.Contains(session)) return;
        if (SessionsToCompare.Count >= 5) return; // Max 5 sessions

        SessionsToCompare.Add(session);

        var fullSession = await _sessionManager.LoadSessionAsync(session.FilePath);
        var analyzer = new FrametimeAnalyzer(fullSession.Frames);
        _selectedSessions.Add((fullSession, analyzer));

        UpdateComparison();
    }

    [RelayCommand]
    private void RemoveSession(SessionMetadata? session)
    {
        if (session == null) return;

        var index = SessionsToCompare.IndexOf(session);
        if (index >= 0)
        {
            SessionsToCompare.RemoveAt(index);
            _selectedSessions.RemoveAt(index);
            UpdateComparison();
        }
    }

    [RelayCommand]
    private void ClearAll()
    {
        SessionsToCompare.Clear();
        _selectedSessions.Clear();
        ComparisonTable.Clear();
        _frametimeSeries.Clear();
    }

    private void UpdateComparison()
    {
        // Update table
        ComparisonTable.Clear();
        for (int i = 0; i < _selectedSessions.Count; i++)
        {
            var (session, analyzer) = _selectedSessions[i];
            var stats = analyzer.Statistics;

            ComparisonTable.Add(new ComparisonRow
            {
                Index = i + 1,
                GameName = session.GameName,
                AverageFps = stats.AverageFps,
                P1Fps = stats.P1Fps,
                P01Fps = stats.P01Fps,
                MinFrametime = stats.Min,
                MaxFrametime = stats.Max,
                StdDev = stats.StdDev,
                FrameCount = session.FrameCount
            });
        }

        // Update chart
        _frametimeSeries.Clear();
        for (int i = 0; i < _selectedSessions.Count; i++)
        {
            var (session, analyzer) = _selectedSessions[i];
            var frametimes = analyzer.GetFrametimes();

            // Downsample if needed
            var values = new ObservableCollection<ObservableValue>();
            var step = Math.Max(1, frametimes.Count / 500);

            for (int j = 0; j < frametimes.Count; j += step)
            {
                values.Add(new ObservableValue(frametimes[j]));
            }

            _frametimeSeries.Add(new LineSeries<ObservableValue>
            {
                Values = values,
                Fill = null,
                GeometrySize = 0,
                LineSmoothness = 0,
                Name = session.GameName,
                Stroke = new SolidColorPaint(SeriesColors[i % SeriesColors.Length], 2)
            });
        }
    }
}

public partial class ComparisonRow : ObservableObject
{
    [ObservableProperty] private int _index;
    [ObservableProperty] private string _gameName = "";
    [ObservableProperty] private double _averageFps;
    [ObservableProperty] private double _p1Fps;
    [ObservableProperty] private double _p01Fps;
    [ObservableProperty] private double _minFrametime;
    [ObservableProperty] private double _maxFrametime;
    [ObservableProperty] private double _stdDev;
    [ObservableProperty] private int _frameCount;
}
