using CapFrameX.Data.Session.Classes;
using CapFrameX.Data.Session.Contracts;

namespace CapFrameX.Service.Analysis.Tests;

/// <summary>
/// Captures built frame by frame.
/// </summary>
/// <remarks>
/// A real capture file would not do for most of this: the tests here are about which frames end up
/// in a window and which frame counts as a spike, and that needs frame times chosen on purpose.
/// The parity tests use the running user's own captures instead, where the shape is real.
/// </remarks>
internal static class Captures
{
    /// <summary>A run of evenly spaced frames.</summary>
    /// <param name="frametimes">Milliseconds per frame.</param>
    /// <param name="startSeconds">When the run starts.</param>
    /// <param name="pcLatency">Latency per frame, if the capture recorded it.</param>
    /// <param name="gpuActive">GPU-busy milliseconds per frame, if the capture recorded it.</param>
    /// <param name="displayChange">Milliseconds between display changes, if the capture recorded it.</param>
    /// <param name="sensorData">Whether the run carries sensor data.</param>
    public static ISessionRun Run(
        IReadOnlyList<double> frametimes,
        double startSeconds = 0,
        IReadOnlyList<double>? pcLatency = null,
        IReadOnlyList<double>? gpuActive = null,
        IReadOnlyList<double>? displayChange = null,
        bool sensorData = false)
    {
        var times = new double[frametimes.Count];
        var elapsed = startSeconds;

        for (var i = 0; i < frametimes.Count; i++)
        {
            times[i] = elapsed;
            elapsed += frametimes[i] / 1000d;
        }

        return new SessionRun
        {
            PresentMonRuntime = "DXGI",
            SampleTime = (int)Math.Ceiling(elapsed),
            SensorData2 = sensorData ? new SessionSensorData2() : null,
            CaptureData = new SessionCaptureData(frametimes.Count)
            {
                TimeInSeconds = times,
                MsBetweenPresents = [.. frametimes],
                MsBetweenDisplayChange = displayChange is null ? [] : [.. displayChange],
                PcLatency = pcLatency is null ? [] : [.. pcLatency],
                GpuActive = gpuActive is null ? [] : [.. gpuActive],
            },
        };
    }

    /// <summary>A capture made of the given runs.</summary>
    /// <param name="runs">Its runs.</param>
    public static ISession Of(params ISessionRun[] runs) =>
        new Session
        {
            Hash = "0123456789abcdef",
            Info = new SessionInfo
            {
                Id = Guid.NewGuid(),
                GameName = "Cyberpunk 2077",
                ProcessName = "Cyberpunk2077",
                Processor = "Ryzen 9 9950X",
                GPU = "RTX 5090",
                OS = "Windows 11",
                CreationDate = new DateTime(2026, 9, 20, 18, 10, 0, DateTimeKind.Utc),
            },
            Runs = [.. runs],
        };

    /// <summary>Sixty frames at roughly 60 fps.</summary>
    /// <param name="count">How many frames.</param>
    /// <param name="milliseconds">How long each takes.</param>
    public static double[] Steady(int count = 60, double milliseconds = 16.6) =>
        [.. Enumerable.Repeat(milliseconds, count)];
}
