using CapFrameX.Statistics.NetStandard.Contracts;

namespace CapFrameX.Service.Analysis;

/// <summary>
/// The ways of removing outliers this service offers.
/// </summary>
/// <remarks>
/// <see cref="ERemoveOutlierMethod"/> declares five. The 1.x provider implements
/// <see cref="ERemoveOutlierMethod.DeciPercentile"/> and returns the sequence unchanged for
/// interquartile range, three sigma and two and a half sigma. Offering those would put a setting in
/// the UI that does nothing, so they are not in this list and the API refuses them by name.
/// </remarks>
public static class OutlierMethods
{
    /// <summary>The methods that actually remove something, plus doing nothing.</summary>
    public static IReadOnlyList<ERemoveOutlierMethod> Supported { get; } =
        [ERemoveOutlierMethod.None, ERemoveOutlierMethod.DeciPercentile];

    /// <summary>What a caller calls each of them.</summary>
    public static IReadOnlyList<string> Names { get; } = [.. Supported.Select(method => method.ToString())];

    /// <summary>Resolves what a caller asked for.</summary>
    /// <param name="name">Its name, in any casing.</param>
    /// <param name="method">The method it names.</param>
    public static bool TryParse(string? name, out ERemoveOutlierMethod method)
    {
        method = ERemoveOutlierMethod.None;

        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var trimmed = name.Trim();

        foreach (var supported in Supported)
        {
            if (string.Equals(supported.ToString(), trimmed, StringComparison.OrdinalIgnoreCase))
            {
                method = supported;

                return true;
            }
        }

        return false;
    }
}
