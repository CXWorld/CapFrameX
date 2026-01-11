using CapFrameX.Core.Analysis;
using CapFrameX.Shared.Models;

namespace CapFrameX.Core.Data;

/// <summary>
/// Manages capture sessions (loading, listing, organizing)
/// </summary>
public class SessionManager : IDisposable
{
    private readonly string _sessionsDirectory;
    private readonly List<SessionMetadata> _sessionCache = new();
    private readonly FileSystemWatcher _watcher;
    private Timer? _debounceTimer;
    private readonly object _timerLock = new();
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private bool _suppressWatcher;

    /// <summary>
    /// Event fired when sessions directory changes externally (file added/removed outside the app)
    /// </summary>
    public event EventHandler? SessionsChanged;

    /// <summary>
    /// Event fired when a session is added
    /// </summary>
    public event EventHandler<SessionMetadata>? SessionAdded;

    /// <summary>
    /// Event fired when a session is removed
    /// </summary>
    public event EventHandler<string>? SessionRemoved;

    public SessionManager()
    {
        var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");

        _sessionsDirectory = Path.Combine(dataHome, "capframex", "sessions");
        Directory.CreateDirectory(_sessionsDirectory);

        // Set up file system watcher for auto-refresh
        // Only watch for file name changes (create/delete/rename), not content changes
        _watcher = new FileSystemWatcher(_sessionsDirectory, "*.csv")
        {
            NotifyFilter = NotifyFilters.FileName,
            EnableRaisingEvents = true
        };
        _watcher.Created += OnSessionFileCreated;
        _watcher.Deleted += OnSessionFileDeleted;
        _watcher.Renamed += OnSessionFileRenamed;
    }

    private void OnSessionFileCreated(object sender, FileSystemEventArgs e)
    {
        if (_suppressWatcher) return;

        // Debounce to ensure file is fully written
        lock (_timerLock)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = new Timer(_ => LoadSingleSessionAsync(e.FullPath), null, 500, Timeout.Infinite);
        }
    }

    private void OnSessionFileDeleted(object sender, FileSystemEventArgs e)
    {
        if (_suppressWatcher) return;

        lock (_sessionCache)
        {
            var session = _sessionCache.FirstOrDefault(s => s.FilePath == e.FullPath);
            if (session != null)
            {
                _sessionCache.Remove(session);
                SessionRemoved?.Invoke(this, e.FullPath);
            }
        }
    }

    private void OnSessionFileRenamed(object sender, RenamedEventArgs e)
    {
        if (_suppressWatcher) return;

        lock (_sessionCache)
        {
            var session = _sessionCache.FirstOrDefault(s => s.FilePath == e.OldFullPath);
            if (session != null)
            {
                session.FilePath = e.FullPath;
                session.Id = Path.GetFileNameWithoutExtension(e.FullPath);
            }
        }
    }

    private async void LoadSingleSessionAsync(string filePath)
    {
        lock (_timerLock)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = null;
        }

        try
        {
            if (!File.Exists(filePath)) return;

            var session = await SessionIO.LoadAsync(filePath);
            var stats = StatisticsCalculator.Calculate(session.Frames);

            var metadata = new SessionMetadata
            {
                Id = Path.GetFileNameWithoutExtension(filePath),
                GameName = session.GameName,
                GpuName = session.GpuName,
                TimingMode = session.TimingMode,
                StartTime = session.StartTime,
                Duration = session.Duration,
                FrameCount = session.FrameCount,
                FilePath = filePath,
                AverageFps = (float)stats.AverageFps,
                P1Fps = (float)stats.P1Fps,
                P01Fps = (float)stats.P01Fps
            };

            lock (_sessionCache)
            {
                // Avoid duplicates
                if (!_sessionCache.Any(s => s.FilePath == filePath))
                {
                    _sessionCache.Insert(0, metadata);
                    SessionAdded?.Invoke(this, metadata);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load new session {filePath}: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
        lock (_timerLock)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = null;
        }
        _loadLock.Dispose();
    }

    public string SessionsDirectory => _sessionsDirectory;

    /// <summary>
    /// Get list of all sessions (metadata only)
    /// </summary>
    public async Task<IReadOnlyList<SessionMetadata>> GetSessionsAsync()
    {
        await _loadLock.WaitAsync();
        try
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
                        TimingMode = session.TimingMode,
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

            // Return a copy to prevent external modification
            return _sessionCache.ToList();
        }
        finally
        {
            _loadLock.Release();
        }
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
    public async Task<SessionMetadata> SaveSessionAsync(CaptureSession session)
    {
        var fileName = GenerateFileName(session.GameName);
        var basePath = Path.Combine(_sessionsDirectory, fileName);

        // Suppress file watcher during save to avoid duplicate refresh
        _suppressWatcher = true;
        try
        {
            await SessionIO.SaveAsync(session, basePath);
        }
        finally
        {
            _suppressWatcher = false;
        }

        // Calculate stats and create metadata
        var stats = StatisticsCalculator.Calculate(session.Frames);
        var metadata = new SessionMetadata
        {
            Id = Path.GetFileNameWithoutExtension(session.FilePath),
            GameName = session.GameName,
            GpuName = session.GpuName,
            TimingMode = session.TimingMode,
            StartTime = session.StartTime,
            Duration = session.Duration,
            FrameCount = session.FrameCount,
            FilePath = session.FilePath,
            AverageFps = (float)stats.AverageFps,
            P1Fps = (float)stats.P1Fps,
            P01Fps = (float)stats.P01Fps
        };

        // Fire specific event for the new session
        SessionAdded?.Invoke(this, metadata);

        return metadata;
    }

    /// <summary>
    /// Delete a session
    /// </summary>
    public void DeleteSession(string filePath)
    {
        // Suppress file watcher during delete to avoid full refresh
        _suppressWatcher = true;
        try
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
        finally
        {
            _suppressWatcher = false;
        }

        // Fire specific event for the removed session
        SessionRemoved?.Invoke(this, filePath);
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
