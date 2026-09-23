using CapFrameX.Shared.Models;

namespace CapFrameX.Core.Analysis;

/// <summary>
/// Analyzer for frametime data providing various metrics and visualizations
/// </summary>
public class FrametimeAnalyzer
{
    private readonly List<FrameData> _frames;
    private FrametimeStatistics? _cachedStats;

    public FrametimeAnalyzer(IEnumerable<FrameData> frames)
    {
        _frames = frames.ToList();
    }

    public FrametimeStatistics Statistics => _cachedStats ??= StatisticsCalculator.Calculate(_frames);

    public int FrameCount => _frames.Count;

    public TimeSpan Duration
    {
        get
        {
            if (_frames.Count < 2) return TimeSpan.Zero;
            var totalMs = _frames.Sum(f => f.FrametimeMs);
            return TimeSpan.FromMilliseconds(totalMs);
        }
    }

    /// <summary>
    /// Get frametimes as a list
    /// </summary>
    public IReadOnlyList<double> GetFrametimes()
    {
        return _frames.Select(f => (double)f.FrametimeMs).ToList();
    }

    /// <summary>
    /// Get FPS values as a list
    /// </summary>
    public IReadOnlyList<double> GetFpsValues()
    {
        return _frames.Select(f => (double)f.Fps).ToList();
    }

    /// <summary>
    /// Get time series data (time in seconds, frametime in ms)
    /// </summary>
    public (double[] time, double[] frametime) GetTimeSeriesData()
    {
        var time = new double[_frames.Count];
        var frametime = new double[_frames.Count];
        double currentTime = 0;

        for (int i = 0; i < _frames.Count; i++)
        {
            time[i] = currentTime / 1000.0; // Convert to seconds
            frametime[i] = _frames[i].FrametimeMs;
            currentTime += _frames[i].FrametimeMs;
        }

        return (time, frametime);
    }

    /// <summary>
    /// Get time series FPS data
    /// </summary>
    public (double[] time, double[] fps) GetFpsTimeSeriesData()
    {
        var (time, frametime) = GetTimeSeriesData();
        var fps = frametime.Select(ft => ft > 0 ? 1000.0 / ft : 0).ToArray();
        return (time, fps);
    }

    /// <summary>
    /// Detect stutters (frames significantly longer than average)
    /// </summary>
    public IReadOnlyList<(int index, double frametime, double severity)> DetectStutters(
        double thresholdMultiplier = 2.0)
    {
        if (_frames.Count == 0) return Array.Empty<(int, double, double)>();

        var avgFrametime = Statistics.Average;
        var threshold = avgFrametime * thresholdMultiplier;

        return _frames
            .Select((f, i) => (index: i, frametime: (double)f.FrametimeMs))
            .Where(x => x.frametime > threshold)
            .Select(x => (x.index, x.frametime, severity: x.frametime / avgFrametime))
            .ToList();
    }

    /// <summary>
    /// Calculate frame pacing consistency (lower is better)
    /// </summary>
    public double CalculateFramePacing()
    {
        if (_frames.Count < 2) return 0;

        var frametimes = GetFrametimes();
        var diffs = new double[frametimes.Count - 1];

        for (int i = 1; i < frametimes.Count; i++)
        {
            diffs[i - 1] = Math.Abs(frametimes[i] - frametimes[i - 1]);
        }

        return diffs.Average();
    }

    /// <summary>
    /// Get histogram data for frametime distribution
    /// </summary>
    public (double[] bins, int[] counts) GetHistogramData(int binCount = 50)
    {
        return StatisticsCalculator.CalculateHistogram(GetFrametimes(), binCount);
    }

    /// <summary>
    /// Get L-shape curve data
    /// </summary>
    public (double[] percentiles, double[] frametimes) GetLShapeCurve(int points = 100)
    {
        return StatisticsCalculator.CalculateLShapeCurve(GetFrametimes(), points);
    }

    /// <summary>
    /// Compare this session with another
    /// </summary>
    public SessionComparison CompareTo(FrametimeAnalyzer other)
    {
        return new SessionComparison
        {
            Session1Stats = Statistics,
            Session2Stats = other.Statistics,
            AvgFpsDifference = Statistics.AverageFps - other.Statistics.AverageFps,
            P1FpsDifference = Statistics.P1Fps - other.Statistics.P1Fps,
            P01FpsDifference = Statistics.P01Fps - other.Statistics.P01Fps,
            FramePacingDifference = CalculateFramePacing() - other.CalculateFramePacing()
        };
    }
}

/// <summary>
/// Result of comparing two sessions
/// </summary>
public record SessionComparison
{
    public FrametimeStatistics Session1Stats { get; init; } = new();
    public FrametimeStatistics Session2Stats { get; init; } = new();
    public double AvgFpsDifference { get; init; }
    public double P1FpsDifference { get; init; }
    public double P01FpsDifference { get; init; }
    public double FramePacingDifference { get; init; }

    public double AvgFpsPercentDifference =>
        Session2Stats.AverageFps != 0
            ? (AvgFpsDifference / Session2Stats.AverageFps) * 100
            : 0;
}
