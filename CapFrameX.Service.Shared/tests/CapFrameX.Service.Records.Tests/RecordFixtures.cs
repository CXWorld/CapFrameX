namespace CapFrameX.Service.Records.Tests;

/// <summary>
/// Capture content for tests.
/// </summary>
/// <remarks>
/// Written out by hand rather than copied from a real capture: a real one carries the machine and
/// the games of whoever recorded it, and it is 600 kB of frame data for an assertion about four
/// fields. The shape is what matters, and that is pinned against reality separately by
/// <see cref="RealCaptureFolderTests"/>.
/// </remarks>
internal static class RecordFixtures
{
    /// <summary>A capture with one run and three frames.</summary>
    public const string MinimalCapture = """
        {
          "Hash": "0123456789abcdef",
          "Info": {
            "Id": "8d1d4b3e-6b2a-4f52-9c4e-2f0b3a1c5d7e",
            "GameName": "Cyberpunk 2077",
            "ProcessName": "Cyberpunk2077",
            "AppVersion": "1.7.2.21",
            "CreationDate": "2026-09-20T18:10:00.0000000Z",
            "Processor": "Ryzen 9 9950X",
            "GPU": "RTX 5090",
            "SystemRam": "32 GB",
            "OS": "Windows 11",
            "Comment": "ultra settings"
          },
          "Runs": [
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
          ]
        }
        """;
}
