using System.Net.Sockets;
using System.Runtime.InteropServices;
using CapFrameX.Shared.Models;

namespace CapFrameX.Shared.IPC;

/// <summary>
/// Unix socket client for communicating with the CapFrameX daemon
/// </summary>
public class DaemonClient : IDisposable
{
    private Socket? _socket;
    private readonly string _socketPath;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;
    private bool _disposed;

    public event EventHandler<GameInfo>? GameDetected;
    public event EventHandler<int>? GameExited;
    public event EventHandler<FrameDataPoint>? FrameDataReceived;
    public event EventHandler? Connected;
    public event EventHandler? Disconnected;

    public bool IsConnected => _socket?.Connected ?? false;

    public DaemonClient()
    {
        var runtimeDir = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        _socketPath = runtimeDir != null
            ? Path.Combine(runtimeDir, "capframex.sock")
            : $"/tmp/capframex.sock-{Environment.ProcessId}";
    }

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected)
            return true;

        try
        {
            _socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            var endpoint = new UnixDomainSocketEndPoint(_socketPath);
            await _socket.ConnectAsync(endpoint, cancellationToken);

            _receiveCts = new CancellationTokenSource();
            _receiveTask = ReceiveLoopAsync(_receiveCts.Token);

            Connected?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to connect to daemon: {ex.Message}");
            _socket?.Dispose();
            _socket = null;
            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        if (_receiveCts != null)
        {
            await _receiveCts.CancelAsync();
            if (_receiveTask != null)
            {
                try { await _receiveTask; } catch { }
            }
            _receiveCts.Dispose();
            _receiveCts = null;
        }

        _socket?.Shutdown(SocketShutdown.Both);
        _socket?.Dispose();
        _socket = null;

        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    public async Task SendStartCaptureAsync(int pid)
    {
        await SendMessageAsync(MessageType.StartCapture, BitConverter.GetBytes(pid));
    }

    public async Task SendStopCaptureAsync()
    {
        await SendMessageAsync(MessageType.StopCapture, Array.Empty<byte>());
    }

    public async Task SendPingAsync()
    {
        await SendMessageAsync(MessageType.Ping, Array.Empty<byte>());
    }

    public async Task RequestStatusAsync()
    {
        await SendMessageAsync(MessageType.StatusRequest, Array.Empty<byte>());
    }

    private async Task SendMessageAsync(MessageType type, byte[] payload)
    {
        if (_socket == null || !_socket.Connected)
            throw new InvalidOperationException("Not connected to daemon");

        var header = new MessageHeader
        {
            Type = (uint)type,
            PayloadSize = (uint)payload.Length,
            Timestamp = (ulong)(DateTime.UtcNow - DateTime.UnixEpoch).Ticks * 100
        };

        var headerBytes = new byte[Marshal.SizeOf<MessageHeader>()];
        var handle = GCHandle.Alloc(headerBytes, GCHandleType.Pinned);
        try
        {
            Marshal.StructureToPtr(header, handle.AddrOfPinnedObject(), false);
        }
        finally
        {
            handle.Free();
        }

        var message = new byte[headerBytes.Length + payload.Length];
        Buffer.BlockCopy(headerBytes, 0, message, 0, headerBytes.Length);
        Buffer.BlockCopy(payload, 0, message, headerBytes.Length, payload.Length);

        await _socket.SendAsync(message, SocketFlags.None);
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        var headerSize = Marshal.SizeOf<MessageHeader>();

        while (!cancellationToken.IsCancellationRequested && _socket != null)
        {
            try
            {
                var received = await _socket.ReceiveAsync(buffer, SocketFlags.None, cancellationToken);
                if (received == 0)
                {
                    Disconnected?.Invoke(this, EventArgs.Empty);
                    break;
                }

                if (received >= headerSize)
                {
                    var header = BytesToStruct<MessageHeader>(buffer);
                    var payload = new byte[header.PayloadSize];
                    if (header.PayloadSize > 0 && received > headerSize)
                    {
                        Buffer.BlockCopy(buffer, headerSize, payload, 0,
                            Math.Min((int)header.PayloadSize, received - headerSize));
                    }

                    ProcessMessage((MessageType)header.Type, payload);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error receiving from daemon: {ex.Message}");
                break;
            }
        }
    }

    private void ProcessMessage(MessageType type, byte[] payload)
    {
        switch (type)
        {
            case MessageType.GameStarted:
                if (payload.Length >= Marshal.SizeOf<GameDetectedPayload>())
                {
                    var gamePayload = BytesToStruct<GameDetectedPayload>(payload);
                    var gameInfo = new GameInfo
                    {
                        Pid = gamePayload.Pid,
                        Name = GetStringFromFixedBuffer(gamePayload, 0),
                        ExePath = GetStringFromFixedBuffer(gamePayload, 1),
                        Launcher = GetStringFromFixedBuffer(gamePayload, 2),
                        DetectedTime = DateTime.Now
                    };
                    GameDetected?.Invoke(this, gameInfo);
                }
                break;

            case MessageType.GameStopped:
                if (payload.Length >= sizeof(int))
                {
                    var pid = BitConverter.ToInt32(payload);
                    GameExited?.Invoke(this, pid);
                }
                break;

            case MessageType.FrametimeData:
                if (payload.Length >= Marshal.SizeOf<FrameDataPointIpc>())
                {
                    var frameData = BytesToStruct<FrameDataPointIpc>(payload);
                    var point = new FrameDataPoint
                    {
                        FrameNumber = frameData.FrameNumber,
                        TimestampNs = frameData.TimestampNs,
                        FrametimeMs = frameData.FrametimeMs,
                        Fps = frameData.Fps
                    };
                    FrameDataReceived?.Invoke(this, point);
                }
                break;

            case MessageType.Pong:
                // Keepalive response - could update connection status
                break;
        }
    }

    private static T BytesToStruct<T>(byte[] bytes) where T : struct
    {
        var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            return Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
        }
        finally
        {
            handle.Free();
        }
    }

    private static unsafe string GetStringFromFixedBuffer(GameDetectedPayload payload, int bufferIndex)
    {
        fixed (byte* ptr = bufferIndex switch
        {
            0 => payload.GameName,
            1 => payload.ExePath,
            2 => payload.Launcher,
            _ => payload.GameName
        })
        {
            return Marshal.PtrToStringAnsi((IntPtr)ptr) ?? string.Empty;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _receiveCts?.Cancel();
        _socket?.Dispose();
        _receiveCts?.Dispose();
    }
}
