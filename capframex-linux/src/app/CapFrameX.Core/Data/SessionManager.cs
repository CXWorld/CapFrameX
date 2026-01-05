using CapFrameX.Core.Analysis;
using CapFrameX.Shared.Models;

namespace CapFrameX.Core.Data;

/// <summary>
/// Manages capture sessions (loading, listing, organizing)
/// </summary>
public class SessionManager
{
    private readonly string _sessionsDirectory;
    private readonly List<SessionMetadata> _sessionCache = new();

    public SessionManager()
    {
        var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");

        _sessionsDirectory = Path.Combine(dataHome, "capframex", "sessions");
        Directory.CreateDirectory(_sessionsDirectory);
    }

    public string SessionsDirectory => _sessionsDirectory;

    /// <summary>
    /// Get list of all sessions (metadata only)
    /// </summary>
    public async Task<IReadOnlyList<SessionMetadata>> GetSessionsAsync()
    {
        _sessionCache.Clear();

        var csvFiles = Directory.GetFiles(_sessionsDirectory, "*.csv")
            .OrderByDescending(f => File.GetLastWriteTime(f));

        foreach (var csvFile in csvFiles)
        {
            try
            {
                var session = await SessionIO.LoadAsync(csvFile);
                var stats = StatisticsCalculator.Calculate(session.Frames);

                _sessionCache.Add(new SessionMetadata
                {
                    Id = Path.GetFileNameWithoutExtension(csvFile),
                    GameName = session.GameName,
                    GpuName = session.GpuName,
                    StartTime = session.StartTime,
                    Duration = session.Duration,
                    FrameCount = session.FrameCount,
                    FilePath = csvFile,
                    AverageFps = (float)stats.AverageFps,
                    P1Fps = (float)stats.P1Fps,
                    P01Fps = (float)stats.P01Fps
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load session {csvFile}: {ex.Message}");
            }
        }

        return _sessionCache;
    }

    /// <summary>
    /// Load full session data
    /// </summary>
    public async Task<CaptureSession> LoadSessionAsync(string filePath)
    {
        return await SessionIO.LoadAsync(filePath);
    }

    /// <summary>
    /// Save a new session
    /// </summary>
    public async Task<string> SaveSessionAsync(CaptureSession session)
    {
        var fileName = GenerateFileName(session.GameName);
        var basePath = Path.Combine(_sessionsDirectory, fileName);

        await SessionIO.SaveAsync(session, basePath);

        return session.FilePath;
    }

    /// <summary>
    /// Delete a session
    /// </summary>
    public void DeleteSession(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        var jsonPath = Path.ChangeExtension(filePath, ".json");
        if (File.Exists(jsonPath))
        {
            File.Delete(jsonPath);
        }
    }

    /// <summary>
    /// Export session to a different location
    /// </summary>
    public async Task ExportSessionAsync(CaptureSession session, string exportPath)
    {
        await SessionIO.SaveAsync(session, exportPath);
    }

    private static string GenerateFileName(string gameName)
    {
        // Sanitize game name
        var safeName = string.Join("_", gameName.Split(Path.GetInvalidFileNameChars()));
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "Unknown";
        }

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return $"{safeName}_{timestamp}";
    }
}
