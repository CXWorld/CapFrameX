using CapFrameX.Service.Contracts.Frames;

namespace CapFrameX.Service.Capture.Frames;

/// <summary>
/// Maps PresentMon CSV column names to their position in the current output.
/// </summary>
/// <remarks>
/// The capture service used to address columns by fixed index, which silently produced wrong
/// numbers whenever PresentMon changed its column set - and it changes it between versions and
/// between command-line options, for example when PC latency tracking is off. The layout is read
/// from the header line instead, and what is absent from the header becomes an absent metric
/// rather than a wrong value.
/// </remarks>
public sealed class PresentMonColumnLayout
{
    private readonly Dictionary<string, int> _indices;

    private PresentMonColumnLayout(Dictionary<string, int> indices, int columnCount)
    {
        _indices = indices;
        ColumnCount = columnCount;
        AvailableMetrics = DetermineMetrics(indices);
    }

    /// <summary>Number of columns the header declared.</summary>
    public int ColumnCount { get; }

    /// <summary>Optional frame metrics this column set can deliver.</summary>
    public FrameMetrics AvailableMetrics { get; }

    /// <summary>Reads a layout from a PresentMon CSV header line.</summary>
    /// <param name="headerLine">The header line, without trailing line break.</param>
    /// <exception cref="ArgumentException">
    /// The header does not contain the columns every present has: process id, time stamp and
    /// present interval. Without them the output cannot be interpreted at all.
    /// </exception>
    public static PresentMonColumnLayout FromHeader(string headerLine)
    {
        ArgumentNullException.ThrowIfNull(headerLine);

        var columns = headerLine.Split(',');
        var indices = new Dictionary<string, int>(columns.Length, StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < columns.Length; i++)
        {
            var name = columns[i].Trim();

            // A duplicate name would make the column ambiguous; the first one wins, as PresentMon
            // writes the value we know under the position it has always had.
            if (name.Length > 0 && !indices.ContainsKey(name))
            {
                indices.Add(name, i);
            }
        }

        foreach (var mandatory in new[]
                 {
                     PresentMonColumns.ProcessId,
                     PresentMonColumns.TimeInSeconds,
                     PresentMonColumns.MsBetweenPresents,
                 })
        {
            if (!indices.ContainsKey(mandatory))
            {
                throw new ArgumentException(
                    $"PresentMon header is missing the mandatory column '{mandatory}'.",
                    nameof(headerLine));
            }
        }

        return new PresentMonColumnLayout(indices, columns.Length);
    }

    /// <summary>Index of a column, or -1 when the output does not contain it.</summary>
    /// <param name="columnName">Column name, see <see cref="PresentMonColumns"/>.</param>
    public int IndexOf(string columnName) =>
        columnName is not null && _indices.TryGetValue(columnName, out var index) ? index : -1;

    private static FrameMetrics DetermineMetrics(Dictionary<string, int> indices)
    {
        var metrics = FrameMetrics.None;

        Add(PresentMonColumns.MsBetweenDisplayChange, FrameMetrics.DisplayChange);
        Add(PresentMonColumns.MsUntilDisplayed, FrameMetrics.UntilDisplayed);
        Add(PresentMonColumns.MsGpuBusy, FrameMetrics.GpuBusy);
        Add(PresentMonColumns.MsCpuBusy, FrameMetrics.CpuBusy);
        Add(PresentMonColumns.MsPcLatency, FrameMetrics.PcLatency);
        Add(PresentMonColumns.MsAnimationError, FrameMetrics.AnimationError);
        Add(PresentMonColumns.PresentMode, FrameMetrics.PresentMode);
        Add(PresentMonColumns.FrameType, FrameMetrics.FrameType);
        Add(PresentMonColumns.SwapChainAddress, FrameMetrics.SwapChain);

        // PresentMon 2.x renamed the render-complete column; either spelling delivers the metric.
        if (indices.ContainsKey(PresentMonColumns.MsRenderPresentLatency) ||
            indices.ContainsKey(PresentMonColumns.MsUntilRenderComplete))
        {
            metrics |= FrameMetrics.RenderComplete;
        }

        return metrics;

        void Add(string column, FrameMetrics metric)
        {
            if (indices.ContainsKey(column))
            {
                metrics |= metric;
            }
        }
    }
}
