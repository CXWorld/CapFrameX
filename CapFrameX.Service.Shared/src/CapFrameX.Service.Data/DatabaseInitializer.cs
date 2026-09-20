using CapFrameX.Service.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CapFrameX.Service.Data;

/// <summary>
/// Helper class to initialize database and seed sample data.
/// </summary>
public static class DatabaseInitializer
{
    /// <summary>
    /// Creates the database and applies all migrations.
    /// </summary>
    public static async Task InitializeDatabaseAsync(string? databasePath = null)
    {
        using var context = CapFrameXDbContextFactory.Create(databasePath);
        await context.Database.MigrateAsync();
        Console.WriteLine($"Database created/updated at: {GetDatabasePath(databasePath)}");
    }

    /// <summary>
    /// Seeds the database with sample data for testing.
    /// </summary>
    public static async Task SeedSampleDataAsync(string? databasePath = null)
    {
        using var context = CapFrameXDbContextFactory.Create(databasePath);

        // Ensure database exists
        await context.Database.MigrateAsync();

        // Check if already seeded
        if (await context.Suites.AnyAsync())
        {
            Console.WriteLine("Database already contains data. Skipping seed.");
            return;
        }

        // Create sample suite
        var suite = new Suite
        {
            Id = Guid.NewGuid(),
            Name = "RTX 4090 Gaming Benchmark",
            Description = "Performance testing with NVIDIA RTX 4090",
            Type = SuiteType.GameReview,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Suites.Add(suite);

        // Create sample sessions
        var games = new[]
        {
            ("Cyberpunk 2077", "Cyberpunk2077.exe"),
            ("Red Dead Redemption 2", "RDR2.exe"),
            ("Starfield", "Starfield.exe")
        };

        foreach (var (gameName, processName) in games)
        {
            var session = new Session
            {
                Id = Guid.NewGuid(),
                SuiteId = suite.Id,
                GameName = gameName,
                ProcessName = processName,
                Processor = "AMD Ryzen 9 7950X",
                Motherboard = "ASUS ROG Crosshair X670E Hero",
                SystemRam = "32GB DDR5-6000",
                Gpu = "NVIDIA GeForce RTX 4090",
                GpuCount = 1,
                GpuCoreClock = 2520,
                GpuMemoryClock = 10501,
                BaseDriverVersion = "537.42",
                DriverPackage = "NVIDIA Game Ready Driver",
                GpuDriverVersion = "537.42",
                Os = "Windows 11 Pro 23H2",
                ApiInfo = "DirectX 12",
                ResizableBar = true,
                WinGameMode = true,
                Hags = true,
                PresentationMode = "Fullscreen",
                ResolutionInfo = "3840x2160",
                CreatedAt = DateTime.UtcNow
            };
            context.Sessions.Add(session);

            // Create sample runs for each session
            var random = new Random();
            for (int i = 0; i < 3; i++)
            {
                var baseAvgFps = gameName switch
                {
                    "Cyberpunk 2077" => 85.0,
                    "Red Dead Redemption 2" => 110.0,
                    "Starfield" => 95.0,
                    _ => 100.0
                };

                var run = new SessionRun
                {
                    Id = Guid.NewGuid(),
                    SessionId = session.Id,
                    Hash = Guid.NewGuid().ToString("N")[..16],
                    PresentMonRuntime = "1.9.1",
                    SampleTime = 120.0,
                    CaptureDataJson = "{\"TimeInSeconds\":[],\"MsBetweenPresents\":[]}",
                    SensorDataJson = "{\"CpuTemp\":[],\"GpuTemp\":[]}",
                    AverageFps = baseAvgFps + random.Next(-10, 10),
                    P99Fps = baseAvgFps + random.Next(10, 20),
                    P95Fps = baseAvgFps + random.Next(5, 15),
                    MedianFps = baseAvgFps + random.Next(-5, 5),
                    P5Fps = baseAvgFps - random.Next(5, 15),
                    P1Fps = baseAvgFps - random.Next(10, 20),
                    P0_1Fps = baseAvgFps - random.Next(15, 25),
                    MaxFps = baseAvgFps + random.Next(30, 50),
                    AvgCpuTemp = 65.0 + random.Next(-5, 5),
                    AvgGpuTemp = 70.0 + random.Next(-5, 5),
                    AvgCpuPower = 140.0 + random.Next(-20, 20),
                    AvgGpuPower = 380.0 + random.Next(-30, 30),
                    AvgCpuUsage = 45.0 + random.Next(-10, 10),
                    AvgGpuUsage = 98.0 + random.Next(-3, 2),
                    CreatedAt = DateTime.UtcNow.AddMinutes(-i * 5)
                };
                context.SessionRuns.Add(run);
            }
        }

        await context.SaveChangesAsync();

        var totalSessions = await context.Sessions.CountAsync();
        var totalRuns = await context.SessionRuns.CountAsync();

        Console.WriteLine($"Sample data seeded successfully!");
        Console.WriteLine($"  - 1 Suite");
        Console.WriteLine($"  - {totalSessions} Sessions");
        Console.WriteLine($"  - {totalRuns} Runs");
        Console.WriteLine($"Database location: {GetDatabasePath(databasePath)}");
    }

    private static string GetDatabasePath(string? customPath)
    {
        return customPath ?? CapFrameXDbContextFactory.GetDefaultDatabasePath();
    }
}
