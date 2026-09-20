namespace CapFrameX.Service.Core.Interfaces;

/// <summary>
/// Named pipe server for real-time power measurement data streaming
/// </summary>
public interface INamedPipeServer
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task BroadcastAsync<T>(T data, CancellationToken cancellationToken = default);
}
