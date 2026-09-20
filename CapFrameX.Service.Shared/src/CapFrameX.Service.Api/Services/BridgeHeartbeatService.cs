using CapFrameX.Service.Contracts.App;
using CapFrameX.Service.Contracts.Bridge;

namespace CapFrameX.Service.Api.Services;

public sealed class BridgeHeartbeatService : BackgroundService
{
    private readonly BridgeEventStream _eventStream;

    public BridgeHeartbeatService(BridgeEventStream eventStream)
    {
        _eventStream = eventStream;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _eventStream.Publish(
                    BridgeEventTypes.AppHeartbeat,
                    new AppHeartbeatDto(DateTimeOffset.UtcNow));

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
