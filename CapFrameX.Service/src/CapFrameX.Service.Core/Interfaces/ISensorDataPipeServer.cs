namespace CapFrameX.Service.Core.Interfaces;

/// <summary>
/// Interface for the sensor data pipe server that broadcasts to RTSS overlay provider
/// </summary>
public interface ISensorDataPipeServer
{
    bool IsRunning { get; }
    int ConnectionCount { get; }

    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task BroadcastAsync<T>(T data, CancellationToken cancellationToken = default);
}
