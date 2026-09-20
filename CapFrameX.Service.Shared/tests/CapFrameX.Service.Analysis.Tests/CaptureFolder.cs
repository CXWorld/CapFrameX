using CapFrameX.Service.Records;

namespace CapFrameX.Service.Analysis.Tests;

/// <summary>
/// The capture folder of whoever runs the tests.
/// </summary>
internal static class CaptureFolder
{
    /// <summary>Where CapFrameX 1.x writes its captures.</summary>
    public static string Path { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "CapFrameX",
        "Captures");

    /// <summary>Every capture file below it, or nothing when the folder does not exist.</summary>
    public static string[] Records() =>
        Directory.Exists(Path)
            ? Directory.GetFiles(Path, "*" + RecordFileReader.Extension, SearchOption.AllDirectories)
            : [];
}

/// <summary>
/// A test that needs the running user's own captures.
/// </summary>
/// <remarks>
/// It reports itself skipped where there are none, so it never fails on a build agent - which also
/// means a green run is no evidence that it ran. Its job is to catch the gap between the shape a
/// hand-built capture has and the shape a recorded one has.
/// </remarks>
public sealed class RequiresRealCapturesFactAttribute : FactAttribute
{
    private static readonly int Available = CaptureFolder.Records().Length;

    /// <summary>Creates the attribute and skips the test when no captures are present.</summary>
    public RequiresRealCapturesFactAttribute()
    {
        if (Available == 0)
        {
            Skip = $"No capture files under '{CaptureFolder.Path}'.";
        }
    }
}
