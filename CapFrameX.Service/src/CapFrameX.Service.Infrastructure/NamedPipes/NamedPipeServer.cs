using CapFrameX.Service.Core.Interfaces;
using System.IO.Pipes;
using System.Text.Json;

namespace CapFrameX.Service.Infrastructure.NamedPipes;

/// <summary>
/// Named pipe server for streaming real-time power measurement data
/// Pipe name: CapFrameXPmdData
/// </summary>
public class NamedPipeServer : INamedPipeServer
{
    private const string PipeName = "CapFrameXPmdData";
    private readonly List<NamedPipeServerStream> _activeConnections = new();
    private CancellationTokenSource? _cancellationTokenSource;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // TODO: Implement pipe server connection handling
        // Start listening for client connections in background

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _cancellationTokenSource?.Cancel();

        foreach (var connection in _activeConnections)
        {
            connection.Dispose();
        }

        _activeConnections.Clear();

        return Task.CompletedTask;
    }

    public async Task BroadcastAsync<T>(T data, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(data);
        var buffer = System.Text.Encoding.UTF8.GetBytes(json);

        foreach (var connection in _activeConnections.ToList())
        {
            try
            {
                if (connection.IsConnected)
                {
                    await connection.WriteAsync(buffer, cancellationToken);
                    await connection.FlushAsync(cancellationToken);
                }
            }
            catch
            {
                // Remove disconnected clients
                _activeConnections.Remove(connection);
                connection.Dispose();
            }
        }
    }
}
