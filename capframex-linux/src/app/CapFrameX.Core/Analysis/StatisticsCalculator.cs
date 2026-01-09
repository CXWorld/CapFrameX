using CapFrameX.Shared.Models;

namespace CapFrameX.Core.Analysis;

/// <summary>
/// Frametime statistics result
/// </summary>
public record FrametimeStatistics
{
    public double Average { get; init; }
    public double Median { get; init; }
    public double Min { get; init; }
    public double Max { get; init; }
    public double StdDev { get; init; }

    // Percentile frametimes (lower is better)
    public double P99 { get; init; }      // 99th percentile
    public double P95 { get; init; }      // 95th percentile

    // Percentile FPS (1% low, 0.1% low, etc.)
    public double P1 { get; init; }       // 1% low (99th percentile frametime)
    public double P01 { get; init; }      // 0.1% low (99.9th percentile frametime)
    public double P001 { get; init; }     // 0.01% low (99.99th percentile frametime)

    public double AverageFps => Average > 0 ? 1000.0 / Average : 0;
    public double P1Fps => P1 > 0 ? 1000.0 / P1 : 0;
    public double P01Fps => P01 > 0 ? 1000.0 / P01 : 0;
    public double P001Fps => P001 > 0 ? 1000.0 / P001 : 0;
}

/// <summary>
/// Calculator for frametime statistics
/// </summary>
public static class StatisticsCalculator
{
    public static FrametimeStatistics Calculate(IReadOnlyList<FrameData> frames)
    {
        if (frames.Count == 0)
            return new FrametimeStatistics();

        var frametimes = frames.Select(f => (double)f.FrametimeMs).ToList();
        return Calculate(frametimes);
    }

    public static FrametimeStatistics Calculate(IReadOnlyList<double> frametimes)
    {
        if (frametimes.Count == 0)
            return new FrametimeStatistics();

        var sorted = frametimes.OrderBy(x => x).ToList();
        var count = sorted.Count;

        var average = sorted.Average();
        var median = GetPercentile(sorted, 50);
        var min = sorted[0];
        var max = sorted[count - 1];

        // Standard deviation
        var variance = sorted.Sum(x => Math.Pow(x - average, 2)) / count;
        var stdDev = Math.Sqrt(variance);

        // Percentile frametimes
        var p95 = GetPercentile(sorted, 95);
        var p99 = GetPercentile(sorted, 99);

        // For "1% low FPS", we need the 99th percentile frametime
        // (the slowest 1% of frames)
        var p1Low = GetPercentile(sorted, 99);
        var p01Low = GetPercentile(sorted, 99.9);
        var p001Low = GetPercentile(sorted, 99.99);

        return new FrametimeStatistics
        {
            Average = average,
            Median = median,
            Min = min,
            Max = max,
            StdDev = stdDev,
            P95 = p95,
            P99 = p99,
            P1 = p1Low,
            P01 = p01Low,
            P001 = p001Low
        };
    }

    public static double GetPercentile(IReadOnlyList<double> sortedData, double percentile)
    {
        if (sortedData.Count == 0) return 0;
        if (sortedData.Count == 1) return sortedData[0];

        var index = (percentile / 100.0) * (sortedData.Count - 1);
        var lower = (int)Math.Floor(index);
        var upper = (int)Math.Ceiling(index);

        if (lower == upper || upper >= sortedData.Count)
            return sortedData[lower];

        var fraction = index - lower;
        return sortedData[lower] + fraction * (sortedData[upper] - sortedData[lower]);
    }

    /// <summary>
    /// Calculate moving average for smoothing
    /// </summary>
    public static IReadOnlyList<double> MovingAverage(IReadOnlyList<double> data, int windowSize)
    {
        if (data.Count == 0 || windowSize <= 0)
            return Array.Empty<double>();

        var result = new List<double>(data.Count);
        var window = new Queue<double>();
        double sum = 0;

        foreach (var value in data)
        {
            window.Enqueue(value);
            sum += value;

            if (window.Count > windowSize)
            {
                sum -= window.Dequeue();
            }

            result.Add(sum / window.Count);
        }

        return result;
    }

    /// <summary>
    /// Calculate histogram bins for frametime distribution
    /// </summary>
    public static (double[] bins, int[] counts) CalculateHistogram(
        IReadOnlyList<double> frametimes, int binCount = 50)
    {
        if (frametimes.Count == 0)
            return (Array.Empty<double>(), Array.Empty<int>());

        var min = frametimes.Min();
        var max = frametimes.Max();
        var range = max - min;

        if (range <= 0)
        {
            return (new[] { min }, new[] { frametimes.Count });
        }

        var binWidth = range / binCount;
        var bins = new double[binCount];
        var counts = new int[binCount];

        for (int i = 0; i < binCount; i++)
        {
            bins[i] = min + (i + 0.5) * binWidth;
        }

        foreach (var ft in frametimes)
        {
            var binIndex = (int)((ft - min) / binWidth);
            if (binIndex >= binCount) binIndex = binCount - 1;
            if (binIndex < 0) binIndex = 0;
            counts[binIndex]++;
        }

        return (bins, counts);
    }

    /// <summary>
    /// Generate L-shape curve data (percentile vs frametime)
    /// </summary>
    public static (double[] percentiles, double[] frametimes) CalculateLShapeCurve(
        IReadOnlyList<double> frametimes, int points = 100)
    {
        if (frametimes.Count == 0)
            return (Array.Empty<double>(), Array.Empty<double>());

        var sorted = frametimes.OrderBy(x => x).ToList();
        var percentiles = new double[points];
        var values = new double[points];

        for (int i = 0; i < points; i++)
        {
            percentiles[i] = (i + 1.0) / points * 100;
            values[i] = GetPercentile(sorted, percentiles[i]);
        }

        return (percentiles, values);
    }
}
