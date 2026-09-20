namespace CapFrameX.Service.Records.Tests;

/// <summary>
/// Capture content for tests.
/// </summary>
/// <remarks>
/// Written out by hand rather than copied from a real capture: a real one carries the machine and
/// the games of whoever recorded it, and it is hundreds of kilobytes of frame data for an assertion
/// about four fields. The shape is what matters, and that it is the shape real files have is pinned
/// separately by <see cref="RealCaptureFolderTests"/>.
/// </remarks>
internal static class RecordFixtures
{
    /// <summary>A capture with one run and three frames.</summary>
    public const string MinimalCapture = """
        {
          "Hash": "0123456789abcdef",
          "Info": {
            "Id": "8d1d4b3e-6b2a-4f52-9c4e-2f0b3a1c5d7e",
            "AppVersion": "1.7.2.21",
            "GameName": "Cyberpunk 2077",
            "ProcessName": "Cyberpunk2077",
            "CreationDate": "2026-09-20T18:10:00.0000000Z",
            "Processor": "Ryzen 9 9950X",
            "GPU": "RTX 5090",
            "SystemRam": "32 GB",
            "OS": "Windows 11",
            "Comment": "ultra settings"
          },
          "Runs": [RUN]
        }
        """;

    /// <summary>One run: three frames at roughly 60 fps.</summary>
    private const string Run = """

            {
              "Hash": "fedcba9876543210",
              "PresentMonRuntime": "DXGI",
              "SampleTime": 10,
              "CaptureData": {
                "TimeInSeconds": [0.0, 0.0166, 0.0333],
                "MsBetweenPresents": [16.6, 16.7, 16.5],
                "MsBetweenDisplayChange": [16.6, 16.7, 16.5],
                "Dropped": [false, false, false]
              }
            }
        """;

    /// <summary>The capture as read by the tests.</summary>
    public static string Capture { get; } = MinimalCapture.Replace("RUN", Run, StringComparison.Ordinal);

    /// <summary>The same capture, with input-to-display latency recorded.</summary>
    public static string CaptureWithLatency { get; } = Capture.Replace(
        "\"Dropped\": [false, false, false]",
        "\"Dropped\": [false, false, false], \"PcLatency\": [12.1, 11.8, 12.4]",
        StringComparison.Ordinal);

    /// <summary>The same capture with its run present twice, as a multi-run benchmark produces.</summary>
    public static string CaptureWithTwoRuns { get; } =
        MinimalCapture.Replace("RUN", Run + "," + Run, StringComparison.Ordinal);

    /// <summary>A capture whose run carries no frame data at all.</summary>
    public static string CaptureWithoutFrameData { get; } = Capture.Replace(
        "\"CaptureData\":",
        "\"SomethingElse\":",
        StringComparison.Ordinal);
}
