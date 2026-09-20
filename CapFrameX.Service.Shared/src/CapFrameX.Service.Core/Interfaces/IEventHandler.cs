using CapFrameX.Service.Core.Events;

namespace CapFrameX.Service.Core.Interfaces;

/// <summary>
/// Handler for domain events
/// </summary>
public interface IEventHandler<in TEvent> where TEvent : IEvent
{
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}
