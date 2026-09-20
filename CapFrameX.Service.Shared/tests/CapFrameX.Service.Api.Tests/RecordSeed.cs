using CapFrameX.Data.Session.Classes;
using CapFrameX.Service.Data;
using CapFrameX.Service.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using LegacyRun = CapFrameX.Data.Session.Classes.SessionRun;
using LegacySession = CapFrameX.Data.Session.Classes.Session;
using RecordRow = CapFrameX.Service.Data.Models.Session;

namespace CapFrameX.Service.Api.Tests;

/// <summary>A record as the tests put it in front of the API.</summary>
/// <param name="Id">Identity of the indexed row.</param>
/// <param name="Path">Where its capture file was written.</param>
internal readonly record struct SeededRecord(Guid Id, string Path);

/// <summary>
/// Writes a capture file and indexes it, which is the state every record endpoint assumes.
/// </summary>
/// <remarks>
/// The file is written rather than faked: the endpoints read it back through the same parser the
/// indexer uses, so a capture that only exists as a database row would test half the path.
/// </remarks>
internal static class RecordSeed
{
    /// <summary>Frames in a seeded capture.</summary>
    public const int FrameCount = 600;

    /// <summary>Writes a capture and the row that points at it.</summary>
    /// <param name="factory">The hosted API, for its database.</param>
    /// <param name="folder">Where to write the capture file.</param>
    /// <param name="game">Game name to record.</param>
    /// <param name="comment">Comment to record.</param>
    /// <param name="runs">How many runs the capture has.</param>
    /// <param name="deleteFile">Whether to delete the file again, leaving the row behind.</param>
    public static async Task<SeededRecord> WriteAsync(
        GuardedApiFactory factory,
        string folder,
        string game = "Cyberpunk 2077",
        string comment = "ultra settings",
        int runs = 1,
        bool deleteFile = false)
    {
        var path = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".json");
        var session = new LegacySession
        {
            Hash = "0123456789abcdef",
            Info = new SessionInfo
            {
                Id = Guid.NewGuid(),
                GameName = game,
                ProcessName = "Cyberpunk2077.exe",
                Comment = comment,
                Processor = "Ryzen 9 9950X",
                GPU = "RTX 5090",
                SystemRam = "32GB (2x16GB) 6000MT/s",
                Motherboard = "X870E",
                OS = "Windows 11",
                ApiInfo = "DX12",
                GPUDriverVersion = "566.36",
                ResizableBar = "Enabled",
                HAGS = "Enabled",
                WinGameMode = "Disabled",
                CreationDate = new DateTime(2026, 9, 20, 18, 10, 0, DateTimeKind.Utc),
            },
            Runs = [.. Enumerable.Range(0, runs).Select(_ => Run())],
        };

        await File.WriteAllTextAsync(path, JsonConvert.SerializeObject(session));

        var info = new FileInfo(path);
        var id = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CapFrameXDbContext>();
            var suite = new Suite
            {
                Id = Guid.NewGuid(),
                Name = "Captures",
                Type = SuiteType.Miscellaneous,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            context.Suites.Add(suite);
            context.Sessions.Add(new RecordRow
            {
                Id = id,
                SuiteId = suite.Id,
                GameName = game,
                ProcessName = "Cyberpunk2077.exe",
                Processor = "Ryzen 9 9950X",
                Gpu = "RTX 5090",
                Os = "Windows 11",
                CreatedAt = new DateTime(2026, 9, 20, 18, 10, 0, DateTimeKind.Utc),
                SourceFilePath = path,
                SourceFileSize = info.Length,
                SourceModifiedUtc = info.LastWriteTimeUtc,
                IndexVersion = 2,
                DurationSeconds = 10,
                RunCount = runs,
                FrameCount = runs * FrameCount,
                SparklineJson = "[16.6,16.7]",
                HasPcLatency = true,
                HasDisplayChange = false,
                AverageFps = 83.4,
                P1Fps = 50.1,
                P99Fps = 116.2,
            });

            await context.SaveChangesAsync();
        }

        if (deleteFile)
        {
            File.Delete(path);
        }

        return new SeededRecord(id, path);
    }

    /// <summary>Empties the index between tests.</summary>
    /// <param name="factory">The hosted API, for its database.</param>
    public static async Task ClearAsync(GuardedApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CapFrameXDbContext>();

        await context.Sessions.ExecuteDeleteAsync();
        await context.Suites.ExecuteDeleteAsync();
    }

    private static LegacyRun Run()
    {
        var random = new Random(20260920);
        var frametimes = new double[FrameCount];
        var times = new double[FrameCount];
        var latency = new double[FrameCount];
        var elapsed = 0d;

        for (var i = 0; i < FrameCount; i++)
        {
            frametimes[i] = 10d + random.NextDouble() * 15d;
            times[i] = elapsed;
            latency[i] = 25d + random.NextDouble() * 10d;
            elapsed += frametimes[i] / 1000d;
        }

        return new LegacyRun
        {
            PresentMonRuntime = "DXGI",
            SampleTime = (int)Math.Ceiling(elapsed),
            CaptureData = new SessionCaptureData(FrameCount)
            {
                TimeInSeconds = times,
                MsBetweenPresents = frametimes,
                PcLatency = latency,
            },
        };
    }
}
