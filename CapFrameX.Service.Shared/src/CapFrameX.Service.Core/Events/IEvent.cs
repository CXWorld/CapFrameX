namespace CapFrameX.Service.Core.Events;

/// <summary>
/// Base interface for all domain events
/// </summary>
public interface IEvent
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
}
