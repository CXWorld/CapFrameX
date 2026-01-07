using System.Diagnostics;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using CapFrameX.Shared.IPC;
using CapFrameX.Shared.Models;

namespace CapFrameX.Core.Capture;

/// <summary>
/// Wrapper around the IPC daemon client with Reactive extensions
/// </summary>
public class CaptureService : IDisposable
{
    private readonly DaemonClient _client;
    private readonly Subject<GameInfo> _gameDetected = new();
    private readonly Subject<int> _gameExited = new();
    private readonly Subject<FrameDataPoint> _frameData = new();
    private readonly Subject<bool> _connectionStatus = new();

    private readonly List<GameInfo> _detectedGames = new();
    private bool _isCapturing;
    private int _capturingPid;

    public CaptureService()
    {
        _client = new DaemonClient();

        _client.GameDetected += (_, game) =>
        {
            _detectedGames.Add(game);
            _gameDetected.OnNext(game);
        };

        _client.GameExited += (_, pid) =>
        {
            _detectedGames.RemoveAll(g => g.Pid == pid);
            _gameExited.OnNext(pid);

            if (_capturingPid == pid)
            {
                _isCapturing = false;
                _capturingPid = 0;
            }
        };

        _client.FrameDataReceived += (_, frame) =>
        {
            _frameData.OnNext(frame);
        };

        _client.Connected += (_, _) => _connectionStatus.OnNext(true);
        _client.Disconnected += (_, _) => _connectionStatus.OnNext(false);
    }

    public IObservable<GameInfo> GameDetected => _gameDetected.AsObservable();
    public IObservable<int> GameExited => _gameExited.AsObservable();
    public IObservable<FrameDataPoint> FrameData => _frameData.AsObservable();
    public IObservable<bool> ConnectionStatus => _connectionStatus.AsObservable();

    public bool IsConnected => _client.IsConnected;
    public bool IsCapturing => _isCapturing;
    public IReadOnlyList<GameInfo> DetectedGames => _detectedGames;

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        // First attempt to connect
        var result = await _client.ConnectAsync(cancellationToken);
        if (result)
        {
            await _client.RequestStatusAsync();
            return true;
        }

        // Connection failed - try to start daemon
        Console.WriteLine("[CaptureService] Daemon not running, attempting to start...");

        if (!await TryStartDaemonAsync(cancellationToken))
        {
            Console.WriteLine("[CaptureService] Failed to start daemon");
            return false;
        }

        // Retry connection after starting daemon
        for (int attempt = 0; attempt < 5; attempt++)
        {
            await Task.Delay(500, cancellationToken);
            result = await _client.ConnectAsync(cancellationToken);
            if (result)
            {
                Console.WriteLine("[CaptureService] Connected to daemon");
                await _client.RequestStatusAsync();
                return true;
            }
        }

        Console.WriteLine("[CaptureService] Failed to connect after starting daemon");
        return false;
    }

    private async Task<bool> TryStartDaemonAsync(CancellationToken cancellationToken)
    {
        var daemonPath = FindDaemonPath();
        if (daemonPath == null)
        {
            Console.WriteLine("[CaptureService] Could not find daemon executable");
            return false;
        }

        Console.WriteLine($"[CaptureService] Starting daemon: {daemonPath}");

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = daemonPath,
                Arguments = "-d",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var process = Process.Start(startInfo);
            if (process == null)
            {
                return false;
            }

            // Wait a moment for daemon to initialize
            await Task.Delay(500, cancellationToken);

            // Check if process is still running (it should be, since -d daemonizes)
            // Note: The parent process exits quickly after forking, so we can't check it
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CaptureService] Failed to start daemon: {ex.Message}");
            return false;
        }
    }

    private static string? FindDaemonPath()
    {
        var searchPaths = new List<string>();

        // 1. Check relative to app location (development/local build)
        // From: src/app/CapFrameX.App/bin/Debug/net8.0/
        // To:   build/bin/capframex-daemon (6 levels up)
        var appDir = AppContext.BaseDirectory;
        var buildBinPath = Path.GetFullPath(Path.Combine(appDir, "..", "..", "..", "..", "..", "..", "build", "bin", "capframex-daemon"));
        searchPaths.Add(buildBinPath);

        // 2. Check in same directory as app
        searchPaths.Add(Path.Combine(appDir, "capframex-daemon"));

        // 3. Check standard install locations
        searchPaths.Add("/usr/bin/capframex-daemon");
        searchPaths.Add("/usr/local/bin/capframex-daemon");
        searchPaths.Add("/usr/lib/capframex/capframex-daemon");

        // 4. Check in PATH
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (pathEnv != null)
        {
            foreach (var dir in pathEnv.Split(':'))
            {
                searchPaths.Add(Path.Combine(dir, "capframex-daemon"));
            }
        }

        foreach (var path in searchPaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    public async Task DisconnectAsync()
    {
        await _client.DisconnectAsync();
    }

    public async Task StartCaptureAsync(int pid)
    {
        if (_isCapturing)
            throw new InvalidOperationException("Already capturing");

        await _client.SendStartCaptureAsync(pid);
        _isCapturing = true;
        _capturingPid = pid;

        // Update game status
        var game = _detectedGames.FirstOrDefault(g => g.Pid == pid);
        if (game != null)
        {
            game.IsCapturing = true;
        }
    }

    public async Task StopCaptureAsync()
    {
        if (!_isCapturing)
            return;

        await _client.SendStopCaptureAsync();
        _isCapturing = false;

        var game = _detectedGames.FirstOrDefault(g => g.Pid == _capturingPid);
        if (game != null)
        {
            game.IsCapturing = false;
        }

        _capturingPid = 0;
    }

    public void StopDaemon()
    {
        try
        {
            var processes = Process.GetProcessesByName("capframex-daemon");
            foreach (var process in processes)
            {
                Console.WriteLine($"[CaptureService] Stopping daemon (PID {process.Id})");
                process.Kill();
                process.WaitForExit(2000);
                process.Dispose();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CaptureService] Error stopping daemon: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _gameDetected.Dispose();
        _gameExited.Dispose();
        _frameData.Dispose();
        _connectionStatus.Dispose();
        _client.Dispose();
        StopDaemon();
    }
}
