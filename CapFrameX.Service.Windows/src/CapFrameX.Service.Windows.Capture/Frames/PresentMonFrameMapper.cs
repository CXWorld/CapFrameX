using System.Globalization;
using CapFrameX.Service.Contracts.Frames;

namespace CapFrameX.Service.Capture.Frames;

/// <summary>
/// Turns one split PresentMon CSV row into a <see cref="FrameSample"/>.
/// </summary>
/// <remarks>
/// Values PresentMon marks as unavailable stay <c>null</c>; they are never substituted with zero,
/// because zero is a legitimate measurement and a chart cannot tell the two apart afterwards.
/// </remarks>
public sealed class PresentMonFrameMapper
{
    private readonly int _columnCount;
    private readonly int _application;
    private readonly int _processId;
    private readonly int _swapChainAddress;
    private readonly int _presentMode;
    private readonly int _frameType;
    private readonly int _timeInSeconds;
    private readonly int _msBetweenPresents;
    private readonly int _msBetweenDisplayChange;
    private readonly int _msUntilRenderComplete;
    private readonly int _msUntilDisplayed;
    private readonly int _msGpuBusy;
    private readonly int _msCpuBusy;
    private readonly int _msPcLatency;
    private readonly int _msAnimationError;

    /// <summary>Creates a mapper for one column layout.</summary>
    /// <param name="layout">Layout read from the PresentMon header.</param>
    public PresentMonFrameMapper(PresentMonColumnLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        _columnCount = layout.ColumnCount;
        AvailableMetrics = layout.AvailableMetrics;

        _application = layout.IndexOf(PresentMonColumns.Application);
        _processId = layout.IndexOf(PresentMonColumns.ProcessId);
        _swapChainAddress = layout.IndexOf(PresentMonColumns.SwapChainAddress);
        _presentMode = layout.IndexOf(PresentMonColumns.PresentMode);
        _frameType = layout.IndexOf(PresentMonColumns.FrameType);
        _timeInSeconds = layout.IndexOf(PresentMonColumns.TimeInSeconds);
        _msBetweenPresents = layout.IndexOf(PresentMonColumns.MsBetweenPresents);
        _msBetweenDisplayChange = layout.IndexOf(PresentMonColumns.MsBetweenDisplayChange);
        _msUntilDisplayed = layout.IndexOf(PresentMonColumns.MsUntilDisplayed);
        _msGpuBusy = layout.IndexOf(PresentMonColumns.MsGpuBusy);
        _msCpuBusy = layout.IndexOf(PresentMonColumns.MsCpuBusy);
        _msPcLatency = layout.IndexOf(PresentMonColumns.MsPcLatency);
        _msAnimationError = layout.IndexOf(PresentMonColumns.MsAnimationError);

        var renderPresentLatency = layout.IndexOf(PresentMonColumns.MsRenderPresentLatency);
        _msUntilRenderComplete = renderPresentLatency >= 0
            ? renderPresentLatency
            : layout.IndexOf(PresentMonColumns.MsUntilRenderComplete);
    }

    /// <summary>Optional metrics the mapper fills, taken from the layout.</summary>
    public FrameMetrics AvailableMetrics { get; }

    /// <summary>
    /// Maps a row. Returns <c>false</c> for rows that are not frame data: PresentMon error lines,
    /// truncated rows and rows whose process id or time stamp cannot be read.
    /// </summary>
    /// <param name="row">The row, already split on commas.</param>
    /// <param name="sample">The mapped frame when this returns <c>true</c>.</param>
    public bool TryMap(IReadOnlyList<string> row, out FrameSample sample)
    {
        sample = default;

        if (row is null || row.Count < _columnCount)
        {
            return false;
        }

        if (_application >= 0 &&
            string.Equals(row[_application], PresentMonColumns.ErrorMarker, StringComparison.Ordinal))
        {
            return false;
        }

        if (!int.TryParse(Text(row, _processId), NumberStyles.Integer, CultureInfo.InvariantCulture, out var processId) ||
            Number(row, _timeInSeconds) is not { } timeInSeconds ||
            Number(row, _msBetweenPresents) is not { } msBetweenPresents)
        {
            return false;
        }

        sample = new FrameSample
        {
            ProcessId = processId,
            TimeInSeconds = timeInSeconds,
            MsBetweenPresents = msBetweenPresents,
            MsBetweenDisplayChange = Number(row, _msBetweenDisplayChange),
            MsUntilRenderComplete = Number(row, _msUntilRenderComplete),
            MsUntilDisplayed = Number(row, _msUntilDisplayed),
            MsGpuBusy = Number(row, _msGpuBusy),
            MsCpuBusy = Number(row, _msCpuBusy),
            MsPcLatency = Number(row, _msPcLatency),
            MsAnimationError = Number(row, _msAnimationError),
            PresentMode = Text(row, _presentMode),
            FrameType = Text(row, _frameType),
            SwapChainId = SwapChainId(row),
        };

        return true;
    }

    /// <summary>
    /// Reads a cell, or <c>null</c> when the column is absent, empty or marked unavailable by
    /// PresentMon.
    /// </summary>
    private static string? Text(IReadOnlyList<string> row, int index)
    {
        if (index < 0)
        {
            return null;
        }

        var value = row[index];

        return string.IsNullOrWhiteSpace(value) ||
               string.Equals(value, PresentMonColumns.NotAvailable, StringComparison.OrdinalIgnoreCase)
            ? null
            : value;
    }

    private static double? Number(IReadOnlyList<string> row, int index) =>
        Text(row, index) is { } text &&
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private ulong? SwapChainId(IReadOnlyList<string> row)
    {
        if (Text(row, _swapChainAddress) is not { } text)
        {
            return null;
        }

        var digits = text.AsSpan().Trim();

        if (digits.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            digits = digits[2..];
        }

        return ulong.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var address)
            ? address
            : null;
    }
}
