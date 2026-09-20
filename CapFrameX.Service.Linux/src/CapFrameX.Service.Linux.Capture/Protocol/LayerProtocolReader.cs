namespace CapFrameX.Service.Linux.Capture.Protocol;

/// <summary>
/// Reassembles layer messages from a stream socket.
/// </summary>
/// <remarks>
/// A stream socket delivers bytes, not messages: a read can end mid-header, carry several messages
/// at once, or split a frame batch. Feeding every read through this reader keeps that concern out
/// of the frame source. The reader enforces a payload limit, because the peer is a library loaded
/// into an arbitrary game process and a corrupted length must not turn into an allocation.
/// </remarks>
public sealed class LayerProtocolReader
{
    /// <summary>Largest payload accepted, in bytes.</summary>
    public const int MaxPayloadSize = 1 << 20;

    private byte[] _buffer;
    private int _length;

    /// <summary>Creates a reader.</summary>
    /// <param name="initialCapacity">Initial buffer size in bytes.</param>
    public LayerProtocolReader(int initialCapacity = 64 * 1024) =>
        _buffer = new byte[Math.Max(initialCapacity, LayerMessageHeader.Size)];

    /// <summary>Bytes buffered but not yet forming a complete message.</summary>
    public int PendingBytes => _length;

    /// <summary>Appends received bytes.</summary>
    /// <param name="data">Bytes as they came off the socket.</param>
    public void Append(ReadOnlySpan<byte> data)
    {
        EnsureCapacity(_length + data.Length);
        data.CopyTo(_buffer.AsSpan(_length));
        _length += data.Length;
    }

    /// <summary>
    /// Takes the next complete message.
    /// </summary>
    /// <param name="header">Header of the message when this returns <c>true</c>.</param>
    /// <param name="payload">Payload of the message when this returns <c>true</c>.</param>
    /// <exception cref="InvalidDataException">
    /// The peer announced a payload beyond <see cref="MaxPayloadSize"/>. The connection cannot be
    /// resynchronised after that and has to be dropped.
    /// </exception>
    public bool TryReadMessage(out LayerMessageHeader header, out ReadOnlyMemory<byte> payload)
    {
        payload = default;

        if (!LayerMessageHeader.TryRead(_buffer.AsSpan(0, _length), out header))
        {
            return false;
        }

        if (header.PayloadSize > MaxPayloadSize)
        {
            throw new InvalidDataException(
                $"Layer announced a payload of {header.PayloadSize} bytes, more than the {MaxPayloadSize} byte limit.");
        }

        var total = LayerMessageHeader.Size + (int)header.PayloadSize;

        if (_length < total)
        {
            return false;
        }

        payload = _buffer.AsMemory(LayerMessageHeader.Size, (int)header.PayloadSize);
        Consume(total);
        return true;
    }

    /// <summary>
    /// Reads the frames of a <see cref="LayerMessageType.FrametimeData"/> payload. A trailing
    /// partial record is ignored rather than guessed at.
    /// </summary>
    /// <param name="payload">Payload of a frame data message.</param>
    public static IEnumerable<LayerFrameData> ReadFrames(ReadOnlyMemory<byte> payload)
    {
        var frames = new List<LayerFrameData>(payload.Length / LayerFrameData.Size);

        for (var offset = 0; offset + LayerFrameData.Size <= payload.Length; offset += LayerFrameData.Size)
        {
            if (LayerFrameData.TryRead(payload.Span.Slice(offset, LayerFrameData.Size), out var frame))
            {
                frames.Add(frame);
            }
        }

        return frames;
    }

    /// <summary>
    /// Drops a message that was read but must not be copied out, and compacts the buffer.
    /// </summary>
    /// <param name="count">Number of bytes to drop from the front.</param>
    private void Consume(int count)
    {
        _length -= count;

        if (_length > 0)
        {
            _buffer.AsSpan(count, _length).CopyTo(_buffer);
        }
    }

    private void EnsureCapacity(int required)
    {
        if (_buffer.Length >= required)
        {
            return;
        }

        var capacity = _buffer.Length;

        while (capacity < required)
        {
            capacity *= 2;
        }

        Array.Resize(ref _buffer, capacity);
    }
}
