using CapFrameX.Service.Core.Events;

namespace CapFrameX.Service.Core.Interfaces;

/// <summary>
/// Event bus for publishing and subscribing to domain events
/// </summary>
public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IEvent;

    void Subscribe<TEvent>(IEventHandler<TEvent> handler)
        where TEvent : IEvent;
}
