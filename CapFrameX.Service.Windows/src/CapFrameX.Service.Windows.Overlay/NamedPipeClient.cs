using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace CapFrameX.Service.Overlay;

/// <summary>
/// Named pipe client for receiving sensor data from the CapFrameX service
/// Pipe name: CapFrameXSensorData
/// </summary>
public sealed class NamedPipeClient : IDisposable
{
    private const string PipeName = "CapFrameXSensorData";
    private const int ConnectionTimeoutMs = 5000;
    private const int ReadBufferSize = 65536;

    private NamedPipeClientStream? _pipeClient;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _readTask;
    private bool _disposed;

    private readonly byte[] _readBuffer = new byte[ReadBufferSize];
    private readonly StringBuilder _messageBuffer = new();

    public event EventHandler<SensorData>? DataReceived;
    public event EventHandler<Exception>? ErrorOccurred;
    public event EventHandler? Disconnected;

    public bool IsConnected => _pipeClient?.IsConnected == true;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_pipeClient != null)
            return;

        _pipeClient = new NamedPipeClientStream(
            ".",
            PipeName,
            PipeDirection.In,
            PipeOptions.Asynchronous);

        try
        {
            await _pipeClient.ConnectAsync(ConnectionTimeoutMs, cancellationToken);

            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _readTask = ReadLoopAsync(_cancellationTokenSource.Token);
        }
        catch
        {
            _pipeClient.Dispose();
            _pipeClient = null;
            throw;
        }
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _pipeClient?.IsConnected == true)
            {
                var bytesRead = await _pipeClient.ReadAsync(_readBuffer.AsMemory(), cancellationToken);

                if (bytesRead == 0)
                {
                    // Pipe closed
                    break;
                }

                var text = Encoding.UTF8.GetString(_readBuffer, 0, bytesRead);
                _messageBuffer.Append(text);

                // Process complete JSON messages (newline-delimited)
                ProcessMessages();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex);
        }
        finally
        {
            Disconnected?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ProcessMessages()
    {
        var content = _messageBuffer.ToString();
        var lines = content.Split('\n');

        // Keep incomplete last line in buffer
        _messageBuffer.Clear();
        if (!content.EndsWith('\n') && lines.Length > 0)
        {
            _messageBuffer.Append(lines[^1]);
            lines = lines[..^1];
        }

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            try
            {
                var data = JsonSerializer.Deserialize<SensorData>(trimmed, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (data != null)
                {
                    DataReceived?.Invoke(this, data);
                }
            }
            catch (JsonException)
            {
                // Skip malformed messages
            }
        }
    }

    public void Disconnect()
    {
        _cancellationTokenSource?.Cancel();

        try
        {
            _readTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Ignore errors during disconnect
        }

        _pipeClient?.Dispose();
        _pipeClient = null;
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        _readTask = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Disconnect();
    }
}
