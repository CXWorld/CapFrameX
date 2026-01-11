using System.Globalization;
using System.Text.Json;
using CapFrameX.Shared.Models;

namespace CapFrameX.Core.Data;

/// <summary>
/// Handles session file I/O
/// </summary>
public static class SessionIO
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Load a session from CSV and JSON metadata files
    /// </summary>
    public static async Task<CaptureSession> LoadAsync(string csvPath)
    {
        var session = new CaptureSession
        {
            FilePath = csvPath
        };

        // Try to load JSON metadata
        var jsonPath = Path.ChangeExtension(csvPath, ".json");
        if (File.Exists(jsonPath))
        {
            try
            {
                var jsonContent = await File.ReadAllTextAsync(jsonPath);
                var metadata = JsonSerializer.Deserialize<SessionMetadataJson>(jsonContent, JsonOptions);
                if (metadata != null)
                {
                    session.GameName = metadata.Game ?? string.Empty;
                    session.GpuName = metadata.Gpu ?? string.Empty;
                    session.Resolution = metadata.Resolution ?? string.Empty;
                    session.TimingMode = metadata.TimingMode ?? string.Empty;
                    session.StartTime = DateTimeOffset.FromUnixTimeSeconds(metadata.StartTime).LocalDateTime;
                    session.EndTime = DateTimeOffset.FromUnixTimeSeconds(metadata.EndTime).LocalDateTime;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load metadata: {ex.Message}");
            }
        }

        // Load CSV frame data
        // Format: MsBetweenPresents,MsUntilRenderComplete,MsUntilDisplayed,MsActualPresent
        var lines = await File.ReadAllLinesAsync(csvPath);
        ulong frameNumber = 0;
        ulong timestamp = 0;

        foreach (var line in lines.Skip(1)) // Skip header
        {
            var parts = line.Split(',');
            if (parts.Length >= 4 &&
                float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var frametime) &&
                float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var msUntilRenderComplete) &&
                float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var msUntilDisplayed) &&
                float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var actualFrametime))
            {
                timestamp += (ulong)(frametime * 1_000_000); // Convert ms to ns
                session.Frames.Add(new FrameData
                {
                    FrameNumber = frameNumber++,
                    TimestampNs = timestamp,
                    FrametimeMs = frametime,
                    MsUntilRenderComplete = msUntilRenderComplete,
                    MsUntilDisplayed = msUntilDisplayed,
                    ActualFrametimeMs = actualFrametime
                });
            }
        }

        return session;
    }

    /// <summary>
    /// Save a session to CSV and JSON files
    /// </summary>
    public static async Task SaveAsync(CaptureSession session, string basePath)
    {
        var csvPath = Path.ChangeExtension(basePath, ".csv");
        var jsonPath = Path.ChangeExtension(basePath, ".json");

        // Save CSV with both CPU sampled and actual present timing
        using (var writer = new StreamWriter(csvPath))
        {
            // Header: CPU sampled frametime, render complete, displayed, actual present timing
            await writer.WriteLineAsync("MsBetweenPresents,MsUntilRenderComplete,MsUntilDisplayed,MsActualPresent");
            foreach (var frame in session.Frames)
            {
                await writer.WriteLineAsync(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:F2},{1:F2},{2:F2},{3:F2}",
                    frame.FrametimeMs,
                    frame.MsUntilRenderComplete,
                    frame.MsUntilDisplayed,
                    frame.ActualFrametimeMs));
            }
        }

        // Save JSON metadata
        var metadata = new SessionMetadataJson
        {
            Game = session.GameName,
            Gpu = session.GpuName,
            Resolution = session.Resolution,
            TimingMode = session.TimingMode,
            StartTime = new DateTimeOffset(session.StartTime).ToUnixTimeSeconds(),
            EndTime = new DateTimeOffset(session.EndTime).ToUnixTimeSeconds(),
            DurationSeconds = (long)session.Duration.TotalSeconds,
            FrameCount = session.FrameCount
        };

        var jsonContent = JsonSerializer.Serialize(metadata, JsonOptions);
        await File.WriteAllTextAsync(jsonPath, jsonContent);

        session.FilePath = csvPath;
    }

    private class SessionMetadataJson
    {
        public string? Game { get; set; }
        public string? Gpu { get; set; }
        public string? Resolution { get; set; }
        public string? TimingMode { get; set; }
        public long StartTime { get; set; }
        public long EndTime { get; set; }
        public long DurationSeconds { get; set; }
        public int FrameCount { get; set; }
    }
}
