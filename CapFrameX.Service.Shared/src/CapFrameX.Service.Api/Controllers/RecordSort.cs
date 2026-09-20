using CapFrameX.Service.Application.Records;

namespace CapFrameX.Service.Api.Controllers;

/// <summary>
/// Reads the <c>sort</c> parameter of the record list.
/// </summary>
/// <remarks>
/// One parameter rather than a field and a direction, because a sort is one decision: a leading
/// minus means the other way round, which is the convention the rest of the web uses and the one a
/// person typing a URL by hand will guess.
/// </remarks>
internal static class RecordSort
{
    /// <summary>The order the list opens with: newest capture first.</summary>
    public const string Default = "-created";

    private static readonly IReadOnlyDictionary<string, RecordSortField> ByName =
        new Dictionary<string, RecordSortField>(StringComparer.OrdinalIgnoreCase)
        {
            ["created"] = RecordSortField.Created,
            ["game"] = RecordSortField.Game,
            ["process"] = RecordSortField.Process,
            ["duration"] = RecordSortField.Duration,
            ["frames"] = RecordSortField.Frames,
            ["averagefps"] = RecordSortField.AverageFps,
            ["p1fps"] = RecordSortField.P1Fps,
            ["p99fps"] = RecordSortField.P99Fps,
        };

    /// <summary>Every field the list can be ordered by.</summary>
    public static IReadOnlyList<string> Fields { get; } = [.. ByName.Keys];

    /// <summary>Reads one sort parameter.</summary>
    /// <param name="sort">Its value, optionally prefixed with a minus; nothing takes the default.</param>
    /// <param name="field">What to order by.</param>
    /// <param name="descending">Whether to order the other way round.</param>
    /// <param name="error">Why it could not be read.</param>
    public static bool TryParse(string? sort, out RecordSortField field, out bool descending, out string? error)
    {
        field = RecordSortField.Created;
        descending = true;
        error = null;

        if (string.IsNullOrWhiteSpace(sort))
        {
            return true;
        }

        var text = sort.Trim();
        descending = text.StartsWith('-');

        if (descending || text.StartsWith('+'))
        {
            text = text[1..];
        }

        if (ByName.TryGetValue(text, out field))
        {
            return true;
        }

        field = RecordSortField.Created;
        descending = true;
        error = $"'{sort}' is not a sort order. Known fields: {string.Join(", ", Fields)}, each optionally prefixed with '-' to reverse it.";

        return false;
    }
}
