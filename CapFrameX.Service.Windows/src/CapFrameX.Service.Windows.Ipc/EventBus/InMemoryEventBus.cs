using CapFrameX.Service.Core.Events;
using CapFrameX.Service.Core.Interfaces;

namespace CapFrameX.Service.Infrastructure.EventBus;

/// <summary>
/// Simple in-memory event bus implementation
/// </summary>
public class InMemoryEventBus : IEventBus
{
    private readonly Dictionary<Type, List<object>> _handlers = new();

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IEvent
    {
        var eventType = typeof(TEvent);

        if (_handlers.TryGetValue(eventType, out var handlers))
        {
            foreach (var handler in handlers.Cast<IEventHandler<TEvent>>())
            {
                await handler.HandleAsync(@event, cancellationToken);
            }
        }
    }

    public void Subscribe<TEvent>(IEventHandler<TEvent> handler)
        where TEvent : IEvent
    {
        var eventType = typeof(TEvent);

        if (!_handlers.ContainsKey(eventType))
        {
            _handlers[eventType] = new List<object>();
        }

        _handlers[eventType].Add(handler);
    }
}
