namespace CapFrameX.Service.Core.Bridge;

/// <summary>
/// Pushes an event to whoever is listening on the bridge.
/// </summary>
/// <remarks>
/// The stream itself lives in the API, which the layers below it do not reference. Background work
/// announces what it did through this instead, so a service that indexes records or reads sensors
/// stays usable - and testable - without an HTTP host around it.
/// </remarks>
public interface IBridgeEventPublisher
{
    /// <summary>Publishes one event.</summary>
    /// <param name="type">Event type, from <c>BridgeEventTypes</c>.</param>
    /// <param name="payload">What the event carries.</param>
    /// <param name="version">Payload version, bumped when its shape changes.</param>
    void Publish(string type, object payload, int version = 1);
}
