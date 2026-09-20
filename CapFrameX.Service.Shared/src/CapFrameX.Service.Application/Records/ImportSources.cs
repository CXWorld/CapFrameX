using System.Runtime.Versioning;
using System.Text.Json;
using CapFrameX.Service.Records;

namespace CapFrameX.Service.Application.Records;

/// <summary>A folder worth offering to import from.</summary>
/// <param name="Path">The folder.</param>
/// <param name="Origin">Where the suggestion came from, so the user can tell them apart.</param>
/// <param name="CaptureCount">How many capture files are in it, including its subfolders.</param>
public sealed record ImportSource(string Path, string Origin, int CaptureCount);

/// <summary>
/// Finds the folders a first import should offer.
/// </summary>
/// <remarks>
/// CapFrameX 1.x's settings are read here and nowhere else, and only to make a suggestion. That is
/// the whole of the legacy relationship: the service does not follow 1.x's observed directory, it
/// offers to import from it once. A user who says no is never asked again, and nothing about the
/// two applications stays coupled afterwards.
/// </remarks>
public static class ImportSources
{
    /// <summary>Where CapFrameX 1.x keeps its settings, relative to the roaming profile.</summary>
    public const string LegacySettingsFile = @"CapFrameX\Configuration\AppSettings.json";

    /// <summary>Suggestion for the folder 1.x writes its captures to.</summary>
    public const string LegacyRootOrigin = "CapFrameX 1.x captures";

    /// <summary>Suggestion for the folder 1.x is currently showing.</summary>
    public const string LegacyObservedOrigin = "CapFrameX 1.x observed folder";

    /// <summary>The token 1.x writes instead of the documents folder.</summary>
    private const string DocumentsToken = @"MyDocuments\";

    /// <summary>Folders worth offering, each holding at least one capture.</summary>
    /// <param name="exclude">A folder the service already watches, which needs no import.</param>
    /// <param name="settingsPath">Where to look for 1.x's settings; defaults to the roaming profile.</param>
    /// <param name="documents">The documents folder; defaults to the current user's.</param>
    [SupportedOSPlatform("windows")]
    public static IReadOnlyList<ImportSource> Suggest(
        string? exclude = null,
        string? settingsPath = null,
        string? documents = null)
    {
        var legacy = LegacySettings(settingsPath ?? DefaultSettingsPath());
        var documentsFolder = documents ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var sources = new List<ImportSource>(2);

        Add(sources, Resolve(legacy.Root, documentsFolder), LegacyRootOrigin, exclude);
        Add(sources, Resolve(legacy.Observed, documentsFolder), LegacyObservedOrigin, exclude);

        return sources;
    }

    /// <summary>How many capture files a folder holds, including its subfolders.</summary>
    /// <param name="directory">The folder to count.</param>
    public static int Count(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return 0;
        }

        try
        {
            return Directory
                .EnumerateFiles(directory, "*" + RecordFileReader.Extension, SearchOption.AllDirectories)
                .Count();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }

    private static void Add(List<ImportSource> sources, string? directory, string origin, string? exclude)
    {
        if (directory is null || !Directory.Exists(directory))
        {
            return;
        }

        // The folder the service watches is already in the index; offering it would import nothing
        // and only raise the question of why.
        if (exclude is not null && Same(directory, exclude))
        {
            return;
        }

        // A folder already suggested under another name - 1.x often has the observed folder inside
        // the capture root, and once is enough.
        if (sources.Any(source => Same(source.Path, directory)))
        {
            return;
        }

        var count = Count(directory);

        if (count > 0)
        {
            sources.Add(new ImportSource(directory, origin, count));
        }
    }

    private static bool Same(string left, string right) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Turns what 1.x stored into a path.
    /// </summary>
    /// <remarks>
    /// It writes either an absolute path or one starting with a <c>MyDocuments\</c> token, which is
    /// its own way of staying portable between profiles.
    /// </remarks>
    private static string? Resolve(string? stored, string documents)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return null;
        }

        if (stored.StartsWith(DocumentsToken, StringComparison.OrdinalIgnoreCase))
        {
            return Path.Combine(documents, stored[DocumentsToken.Length..]);
        }

        return Path.IsPathRooted(stored) ? stored : null;
    }

    [SupportedOSPlatform("windows")]
    private static string DefaultSettingsPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), LegacySettingsFile);

    private static (string? Root, string? Observed) LegacySettings(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return (null, null);
            }

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;

            return (Text(root, "CaptureRootDirectory"), Text(root, "ObservedDirectory"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // No suggestion is a fine outcome; the user can still name a folder themselves.
            return (null, null);
        }
    }

    private static string? Text(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
