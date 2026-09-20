using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CapFrameX.Service.Capture.Tests;

/// <summary>
/// Helper methods for tests.
/// </summary>
internal static class TestHelpers
{
    /// <summary>
    /// Creates a PresentMonCaptureService with a null logger for testing.
    /// </summary>
    public static PresentMonCaptureService CreateCaptureService()
    {
        return new PresentMonCaptureService(NullLogger<PresentMonCaptureService>.Instance);
    }
}
