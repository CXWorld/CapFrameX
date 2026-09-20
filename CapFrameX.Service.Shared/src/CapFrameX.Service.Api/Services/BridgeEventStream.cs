using System.Collections.Concurrent;
using System.Threading.Channels;
using CapFrameX.Service.Contracts.Bridge;
using CapFrameX.Service.Core.Bridge;

namespace CapFrameX.Service.Api.Services;

public sealed class BridgeEventStream : IBridgeEventPublisher
{
    private readonly ConcurrentDictionary<Guid, Channel<BridgeEventEnvelope>> _subscribers = new();
    private long _sequence;

    public BridgeEventSubscription Subscribe()
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateUnbounded<BridgeEventEnvelope>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        _subscribers[id] = channel;

        return new BridgeEventSubscription(channel.Reader, () => Unsubscribe(id));
    }

    public BridgeEventEnvelope Publish(string type, object payload, int version = 1)
    {
        var envelope = new BridgeEventEnvelope(
            type,
            version,
            Interlocked.Increment(ref _sequence),
            DateTimeOffset.UtcNow,
            payload);

        foreach (var subscriber in _subscribers.Values)
        {
            subscriber.Writer.TryWrite(envelope);
        }

        return envelope;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Explicit because background work only needs to announce something, while a caller
    /// inside the API also wants the envelope back - the sequence number it carries is what
    /// a reconnecting client resumes from.
    /// </remarks>
    void IBridgeEventPublisher.Publish(string type, object payload, int version) =>
        Publish(type, payload, version);

    private void Unsubscribe(Guid id)
    {
        if (_subscribers.TryRemove(id, out var channel))
        {
            channel.Writer.TryComplete();
        }
    }
}

public sealed class BridgeEventSubscription : IAsyncDisposable
{
    private readonly Action _dispose;

    public BridgeEventSubscription(ChannelReader<BridgeEventEnvelope> reader, Action dispose)
    {
        Reader = reader;
        _dispose = dispose;
    }

    public ChannelReader<BridgeEventEnvelope> Reader { get; }

    public ValueTask DisposeAsync()
    {
        _dispose();
        return ValueTask.CompletedTask;
    }
}
