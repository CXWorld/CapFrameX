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
        return await _client.ConnectAsync(cancellationToken);
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

    public void Dispose()
    {
        _gameDetected.Dispose();
        _gameExited.Dispose();
        _frameData.Dispose();
        _connectionStatus.Dispose();
        _client.Dispose();
    }
}
