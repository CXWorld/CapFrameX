using CapFrameX.Data.Session.Classes;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Service.Contracts.Analysis;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;

namespace CapFrameX.Service.Analysis;

/// <summary>
/// Turns a capture into the numbers and curves the analysis view shows.
/// </summary>
/// <remarks>
/// Nothing here computes a statistic. Every number comes out of
/// <see cref="FrametimeStatisticProvider"/> and every sequence out of <see cref="SessionExtensions"/>,
/// the same code the desktop app runs, so the two cannot disagree about what a percentile is. What
/// this class does own is the mapping - which window, which run, which sequence a metric needs -
/// and that is what the tests are about.
/// </remarks>
public sealed class AnalysisService
{
    private readonly AnalysisSettings _settings;
    private readonly FrametimeStatisticProvider _provider;
    private readonly FrametimeAnalyzer _analyzer = new();

    /// <summary>Creates the service.</summary>
    /// <param name="settings">The analysis options the user configured.</param>
    public AnalysisService(AnalysisSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _provider = new FrametimeStatisticProvider(settings);
    }

    /// <summary>Analyses one capture.</summary>
    /// <param name="session">The parsed capture.</param>
    /// <param name="request">Which part of it, and which metrics.</param>
    public AnalysisDto Analyze(ISession session, AnalysisRequest request)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);

        var scoped = Scope(session, request.Run);
        var (start, end) = Window(scoped, request.StartSeconds, request.EndSeconds);

        var points = scoped.GetFrametimePointsTimeWindow(start, end, _settings, request.OutlierMethod);
        var frametimes = Values(points);

        return new AnalysisDto(
            Metrics: Metrics(scoped, request, start, end, frametimes),
            FramePacing: Pacing(points, frametimes),
            PcLatency: Latency(scoped, start, end),
            LShape: LShape(frametimes, request.LShapeMetric),
            Distribution: Points(scoped.GetFrametimeDistributionPoints(start, end, _settings, request.OutlierMethod)),
            Window: new AnalysisWindowDto(start, end, frametimes.Length, request.Run),
            Thresholds: new AnalysisThresholdsDto(
                _settings.StutteringFactor,
                _settings.StutteringThreshold,
                request.OutlierMethod.ToString()));
    }

    /// <summary>Returns the curves behind the chart.</summary>
    /// <param name="session">The parsed capture.</param>
    /// <param name="request">Which part of it, and which curves.</param>
    public SeriesResponse Series(ISession session, SeriesRequest request)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);

        var scoped = Scope(session, request.Run);
        var (start, end) = Window(scoped, request.StartSeconds, request.EndSeconds);
        var kinds = new HashSet<string>(
            request.Kinds ?? SeriesKinds.Default,
            StringComparer.OrdinalIgnoreCase);

        // Outliers are not removed here. Every column is indexed by the frame the time column
        // names, and dropping frames from one curve would misplace every point after the first.
        var points = scoped.GetFrametimePointsTimeWindow(start, end, _settings);
        var time = new double[points.Count];
        var frametimes = new double[points.Count];

        for (var i = 0; i < points.Count; i++)
        {
            time[i] = points[i].X;
            frametimes[i] = points[i].Y;
        }

        return new SeriesResponse(
            Time: time,
            Frametimes: kinds.Contains(SeriesKinds.Frametimes) ? frametimes : null,
            Fps: kinds.Contains(SeriesKinds.Fps) ? Fps(frametimes) : null,
            DisplayChange: kinds.Contains(SeriesKinds.DisplayChange)
                ? Aligned(scoped.GetDisplayChangeTimePointsTimeWindow(start, end, _settings), time)
                : null,
            PcLatency: kinds.Contains(SeriesKinds.PcLatency) ? Aligned(LatencyPoints(scoped, start, end), time) : null,
            Window: new AnalysisWindowDto(start, end, time.Length, request.Run));
    }

    /// <summary>
    /// Restricts the capture to one run.
    /// </summary>
    /// <remarks>
    /// A view onto the same run rather than a copy of its data, so the extensions below see exactly
    /// what they see for a whole capture and there is no second selection path to keep in step.
    /// </remarks>
    private static ISession Scope(ISession session, int? run)
    {
        var runs = session.Runs ?? new List<ISessionRun>();

        if (run is null)
        {
            return session;
        }

        if (run.Value < 0 || run.Value >= runs.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(run),
                run,
                $"The capture has {runs.Count} run(s).");
        }

        return new Session
        {
            Hash = session.Hash,
            Info = session.Info,
            Runs = new List<ISessionRun> { runs[run.Value] },
        };
    }

    private static (double Start, double End) Window(ISession session, double? start, double? end)
    {
        var last = 0d;

        foreach (var run in session.Runs ?? new List<ISessionRun>())
        {
            if (run?.CaptureData?.TimeInSeconds is { Length: > 0 } times)
            {
                last = Math.Max(last, times[^1]);
            }
        }

        var first = Math.Max(start ?? 0d, 0d);

        // An end before the start is a window with nothing in it, not a reversed one: a caller that
        // typed the numbers means what it typed.
        return (first, Math.Max(end ?? last, first));
    }

    private IReadOnlyList<MetricDto> Metrics(
        ISession session,
        AnalysisRequest request,
        double start,
        double end,
        IList<double> frametimes)
    {
        var wanted = request.Metrics ?? MetricCatalog.Default;
        var metrics = new List<MetricDto>(wanted.Count);
        IList<double>? gpuActive = null;

        foreach (var metric in wanted)
        {
            IList<double> sequence;

            if (MetricCatalog.Source(metric) == MetricSource.GpuActive)
            {
                // Read once, and only when something asks for it: another pass over every frame.
                gpuActive ??= GpuActive(session, start, end, request.OutlierMethod);
                sequence = gpuActive;
            }
            else
            {
                sequence = frametimes;
            }

            metrics.Add(MetricCatalog.Describe(
                metric,
                sequence.Count == 0 ? null : _provider.GetFpsMetricValue(sequence, metric)));
        }

        return metrics;
    }

    private IList<double> GpuActive(ISession session, double start, double end, ERemoveOutlierMethod outliers)
    {
        // A capture without the column is not an error; the metric simply has no value.
        if (session.Runs is null || session.Runs.Any(run => run?.CaptureData?.GpuActive is not { Length: > 0 }))
        {
            return new List<double>();
        }

        return session.GetGpuActiveTimeTimeWindow(start, end, _settings, outliers);
    }

    private FramePacingDto Pacing(IList<Point> points, IList<double> frametimes)
    {
        if (frametimes.Count == 0)
        {
            return new FramePacingDto(0, 0, 0, 0, null);
        }

        var stutter = _provider.GetStutteringTimePercentage(frametimes, _settings.StutteringFactor);
        var lowFps = _provider.GetLowFPSTimePercentage(
            frametimes,
            _settings.StutteringFactor,
            _settings.StutteringThreshold);

        // The spikes are the very frames the stutter percentage is made of - same rule, same moving
        // average - so the count and the share can never tell different stories.
        var average = _provider.GetMovingAverage(frametimes);
        var count = 0;
        var worst = -1;

        for (var i = 0; i < average.Count && i < points.Count; i++)
        {
            if (frametimes[i] <= _settings.StutteringFactor * average[i])
            {
                continue;
            }

            count++;

            if (worst < 0 || points[i].Y > points[worst].Y)
            {
                worst = i;
            }
        }

        return new FramePacingDto(
            SmoothPercent: 100d - (stutter + lowFps),
            StutterPercent: stutter,
            LowFpsPercent: lowFps,
            SpikeCount: count,
            WorstSpike: worst < 0 ? null : new SpikeDto(points[worst].X, points[worst].Y));
    }

    private PcLatencyDto? Latency(ISession session, double start, double end)
    {
        var points = LatencyPoints(session, start, end);

        if (points.Count == 0)
        {
            return null;
        }

        var values = Values(points);

        return new PcLatencyDto(
            AverageMs: values.Average(),
            P99Ms: _provider.GetPQuantileSequence(values, 0.99));
    }

    /// <summary>
    /// Latency for the frames inside the window.
    /// </summary>
    /// <remarks>
    /// Read from the capture data rather than through
    /// <c>SessionExtensions.GetPcLatencyPointTimeWindow</c>, which returns nothing unless every run
    /// also carries sensor data. Latency comes from PresentMon and sensor readings do not, so that
    /// guard would hide the latency of every capture recorded without hardware monitoring - and the
    /// record list, which reads the column directly, would promise a value this never delivers.
    /// </remarks>
    private static IList<Point> LatencyPoints(ISession session, double start, double end)
    {
        var points = new List<Point>();

        foreach (var run in session.Runs ?? new List<ISessionRun>())
        {
            var data = run?.CaptureData;

            if (data?.PcLatency is not { Length: > 0 } latency || data.TimeInSeconds is not { } times)
            {
                continue;
            }

            for (var i = 0; i < latency.Length && i < times.Length; i++)
            {
                if (times[i] >= start && times[i] <= end && latency[i] > 0 && !double.IsNaN(latency[i]))
                {
                    points.Add(new Point(times[i], latency[i]));
                }
            }
        }

        return points;
    }

    private IReadOnlyList<PointDto> LShape(IList<double> frametimes, ELShapeMetrics metric)
    {
        if (frametimes.Count == 0)
        {
            return [];
        }

        var sequence = metric == ELShapeMetrics.Frametimes ? frametimes : Fps(frametimes);
        var quantiles = _analyzer.GetLShapeQuantiles(metric);
        var curve = new List<PointDto>(quantiles.Length);

        foreach (var quantile in quantiles)
        {
            curve.Add(new PointDto(quantile, _provider.GetPQuantileSequence(sequence, quantile / 100)));
        }

        return curve;
    }

    private static double[] Fps(IList<double> frametimes)
    {
        var fps = new double[frametimes.Count];

        for (var i = 0; i < frametimes.Count; i++)
        {
            fps[i] = 1000d / frametimes[i];
        }

        return fps;
    }

    private static double[] Values(IList<Point> points)
    {
        var values = new double[points.Count];

        for (var i = 0; i < points.Count; i++)
        {
            values[i] = points[i].Y;
        }

        return values;
    }

    private static IReadOnlyList<PointDto> Points(IList<Point> points)
    {
        var converted = new List<PointDto>(points.Count);

        foreach (var point in points)
        {
            converted.Add(new PointDto(point.X, point.Y));
        }

        return converted;
    }

    /// <summary>
    /// Puts a second curve on the frame index the time column already defines.
    /// </summary>
    /// <remarks>
    /// A column can be shorter than the frame times - a frame with no display change, a capture
    /// that started recording latency late - and it is the chart's x axis that decides where a
    /// value belongs. Missing entries are null rather than zero, which a chart draws as a gap
    /// instead of a drop to the floor.
    /// </remarks>
    private static double?[] Aligned(IList<Point> points, double[] time)
    {
        var byTime = new Dictionary<double, double>(points.Count);

        foreach (var point in points)
        {
            byTime[point.X] = point.Y;
        }

        var aligned = new double?[time.Length];

        for (var i = 0; i < time.Length; i++)
        {
            aligned[i] = byTime.TryGetValue(time[i], out var value) ? value : null;
        }

        return aligned;
    }
}
