using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using CapFrameX.Service.Core.Interfaces;

namespace CapFrameX.Service.Infrastructure.NamedPipes;

/// <summary>
/// Named pipe server for streaming real-time sensor data to RTSS overlay provider
/// Pipe name: CapFrameXSensorData
/// </summary>
public class SensorDataPipeServer : ISensorDataPipeServer, IDisposable
{
    private const string PipeName = "CapFrameXSensorData";
    private const int MaxConnections = 10;

    private readonly List<NamedPipeServerStream> _activeConnections = new();
    private readonly object _connectionsLock = new();
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _acceptTask;
    private bool _disposed;

    public bool IsRunning => _cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested;
    public int ConnectionCount => _activeConnections.Count;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_cancellationTokenSource != null)
            return Task.CompletedTask;

        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _acceptTask = AcceptConnectionsAsync(_cancellationTokenSource.Token);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _cancellationTokenSource?.Cancel();

        if (_acceptTask != null)
        {
            try
            {
                await _acceptTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (TimeoutException)
            {
                // Accept task didn't complete in time
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        lock (_connectionsLock)
        {
            foreach (var connection in _activeConnections)
            {
                try
                {
                    connection.Dispose();
                }
                catch
                {
                    // Ignore disposal errors
                }
            }
            _activeConnections.Clear();
        }

        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        _acceptTask = null;
    }

    private async Task AcceptConnectionsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var pipeServer = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.Out,
                    MaxConnections,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await pipeServer.WaitForConnectionAsync(cancellationToken);

                lock (_connectionsLock)
                {
                    _activeConnections.Add(pipeServer);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException)
            {
                // Pipe broken, continue accepting
            }
        }
    }

    public async Task BroadcastAsync<T>(T data, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(data);
        var message = json + "\n"; // Newline-delimited JSON
        var buffer = Encoding.UTF8.GetBytes(message);

        List<NamedPipeServerStream> connectionsToRemove = new();

        List<NamedPipeServerStream> currentConnections;
        lock (_connectionsLock)
        {
            currentConnections = _activeConnections.ToList();
        }

        foreach (var connection in currentConnections)
        {
            try
            {
                if (connection.IsConnected)
                {
                    await connection.WriteAsync(buffer, cancellationToken);
                    await connection.FlushAsync(cancellationToken);
                }
                else
                {
                    connectionsToRemove.Add(connection);
                }
            }
            catch
            {
                connectionsToRemove.Add(connection);
            }
        }

        // Remove disconnected clients
        if (connectionsToRemove.Count > 0)
        {
            lock (_connectionsLock)
            {
                foreach (var connection in connectionsToRemove)
                {
                    _activeConnections.Remove(connection);
                    try
                    {
                        connection.Dispose();
                    }
                    catch
                    {
                        // Ignore disposal errors
                    }
                }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        StopAsync().GetAwaiter().GetResult();
    }
}
