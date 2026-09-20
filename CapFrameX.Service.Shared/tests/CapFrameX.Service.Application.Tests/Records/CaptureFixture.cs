namespace CapFrameX.Service.Application.Tests.Records;

/// <summary>
/// Capture content for the index tests.
/// </summary>
/// <remarks>
/// Generated rather than copied from a real capture: the index tests need several records that
/// differ in the few fields they assert on, and a real capture is hundreds of kilobytes of frame
/// data carrying the machine and the games of whoever recorded it. That this is the shape real
/// files have is pinned by the reader's own tests against the running user's capture folder.
/// </remarks>
internal static class CaptureFixture
{
    /// <summary>Frames per run in a generated capture.</summary>
    public const int FramesPerRun = 3;

    /// <summary>Builds a capture file's content.</summary>
    /// <param name="game">Game name the capture records.</param>
    /// <param name="runs">How often the run is repeated.</param>
    /// <param name="pcLatency">Whether the run carries input-to-display latency.</param>
    public static string Capture(string game = "Cyberpunk 2077", int runs = 1, bool pcLatency = false)
    {
        var latency = pcLatency ? ", \"PcLatency\": [12.1, 11.8, 12.4]" : string.Empty;

        var run = $$"""

                {
                  "Hash": "fedcba9876543210",
                  "PresentMonRuntime": "DXGI",
                  "SampleTime": 10,
                  "CaptureData": {
                    "TimeInSeconds": [0.0, 0.0166, 0.0333],
                    "MsBetweenPresents": [16.6, 16.7, 16.5],
                    "MsBetweenDisplayChange": [16.6, 16.7, 16.5],
                    "Dropped": [false, false, false]{{latency}}
                  }
                }
            """;

        // A capture's identity is its own, the way CapFrameX computes it over the runs: two
        // generated captures are two captures, and only a copy of one shares its hash.
        var hash = Guid.NewGuid().ToString("N");

        return $$"""
            {
              "Hash": "{{hash}}",
              "Info": {
                "Id": "{{Guid.NewGuid()}}",
                "AppVersion": "1.7.2.21",
                "GameName": "{{game}}",
                "ProcessName": "Cyberpunk2077",
                "CreationDate": "2026-09-20T18:10:00.0000000Z",
                "Processor": "Ryzen 9 9950X",
                "GPU": "RTX 5090",
                "SystemRam": "32 GB",
                "OS": "Windows 11"
              },
              "Runs": [{{string.Join(",", Enumerable.Repeat(run, runs))}}]
            }
            """;
    }
}
