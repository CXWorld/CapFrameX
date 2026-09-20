using System.Buffers.Binary;
using CapFrameX.Service.Linux.Capture.Protocol;

namespace CapFrameX.Service.Linux.Tests.Protocol;

/// <summary>
/// The wire format is fixed by the native <c>common.h</c>, and decoding it is pure byte work, so
/// it is pinned down here on any machine rather than only on the Linux box that has a layer.
/// </summary>
public sealed class LayerProtocolTests
{
    private static byte[] Message(LayerMessageType type, ReadOnlySpan<byte> payload, ulong timestampNs = 42)
    {
        var buffer = new byte[LayerMessageHeader.Size + payload.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, (uint)type);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(4), (uint)payload.Length);
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(8), timestampNs);
        payload.CopyTo(buffer.AsSpan(LayerMessageHeader.Size));
        return buffer;
    }

    private static byte[] Frame(
        ulong frameNumber = 1,
        ulong timestampNs = 1_000_000_000,
        float frametimeMs = 16.6f,
        float fps = 60f,
        int processId = 4711,
        ulong actualPresentTimeNs = 0,
        float msUntilRenderComplete = 0,
        float msUntilDisplayed = 0,
        float actualFrametimeMs = 0)
    {
        var buffer = new byte[LayerFrameData.Size];
        BinaryPrimitives.WriteUInt64LittleEndian(buffer, frameNumber);
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(8), timestampNs);
        BinaryPrimitives.WriteSingleLittleEndian(buffer.AsSpan(16), frametimeMs);
        BinaryPrimitives.WriteSingleLittleEndian(buffer.AsSpan(20), fps);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(24), processId);
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(28), actualPresentTimeNs);
        BinaryPrimitives.WriteSingleLittleEndian(buffer.AsSpan(36), msUntilRenderComplete);
        BinaryPrimitives.WriteSingleLittleEndian(buffer.AsSpan(40), msUntilDisplayed);
        BinaryPrimitives.WriteSingleLittleEndian(buffer.AsSpan(44), actualFrametimeMs);
        return buffer;
    }

    [Fact]
    public void Wire_sizes_match_the_native_header()
    {
        Assert.Equal(16, LayerMessageHeader.Size);
        Assert.Equal(52, LayerFrameData.Size);
    }

    [Fact]
    public void Reads_one_message()
    {
        var reader = new LayerProtocolReader();
        reader.Append(Message(LayerMessageType.FrametimeData, Frame()));

        Assert.True(reader.TryReadMessage(out var header, out var payload));

        Assert.Equal(LayerMessageType.FrametimeData, header.Type);
        Assert.Equal((uint)LayerFrameData.Size, header.PayloadSize);
        Assert.Equal(42UL, header.TimestampNs);
        Assert.Equal(LayerFrameData.Size, payload.Length);
        Assert.Equal(0, reader.PendingBytes);
    }

    [Fact]
    public void Message_split_across_reads_is_reassembled()
    {
        var message = Message(LayerMessageType.FrametimeData, Frame(processId: 99));
        var reader = new LayerProtocolReader();

        reader.Append(message.AsSpan(0, 7));
        Assert.False(reader.TryReadMessage(out _, out _));

        reader.Append(message.AsSpan(7, 20));
        Assert.False(reader.TryReadMessage(out _, out _));

        reader.Append(message.AsSpan(27));
        Assert.True(reader.TryReadMessage(out var header, out var payload));

        Assert.Equal(LayerMessageType.FrametimeData, header.Type);
        Assert.True(LayerFrameData.TryRead(payload.Span, out var frame));
        Assert.Equal(99, frame.ProcessId);
    }

    [Fact]
    public void Several_messages_in_one_read_are_taken_in_order()
    {
        var reader = new LayerProtocolReader();
        reader.Append([.. Message(LayerMessageType.LayerHello, []),
                       .. Message(LayerMessageType.FrametimeData, Frame()),
                       .. Message(LayerMessageType.Ping, [])]);

        Assert.True(reader.TryReadMessage(out var first, out _));
        Assert.True(reader.TryReadMessage(out var second, out _));
        Assert.True(reader.TryReadMessage(out var third, out _));
        Assert.False(reader.TryReadMessage(out _, out _));

        Assert.Equal(LayerMessageType.LayerHello, first.Type);
        Assert.Equal(LayerMessageType.FrametimeData, second.Type);
        Assert.Equal(LayerMessageType.Ping, third.Type);
    }

    [Fact]
    public void Empty_payload_is_a_complete_message()
    {
        var reader = new LayerProtocolReader();
        reader.Append(Message(LayerMessageType.Ping, []));

        Assert.True(reader.TryReadMessage(out var header, out var payload));

        Assert.Equal(LayerMessageType.Ping, header.Type);
        Assert.Equal(0, payload.Length);
    }

    [Fact]
    public void Implausible_payload_length_is_rejected_instead_of_allocated()
    {
        var reader = new LayerProtocolReader();
        var header = new byte[LayerMessageHeader.Size];
        BinaryPrimitives.WriteUInt32LittleEndian(header, (uint)LayerMessageType.FrametimeData);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), uint.MaxValue);
        reader.Append(header);

        Assert.Throws<InvalidDataException>(() => reader.TryReadMessage(out _, out _));
    }

    [Fact]
    public void Frame_batch_is_read_completely()
    {
        var reader = new LayerProtocolReader();
        byte[] batch = [.. Frame(frameNumber: 1), .. Frame(frameNumber: 2), .. Frame(frameNumber: 3)];
        reader.Append(Message(LayerMessageType.FrametimeData, batch));
        reader.TryReadMessage(out _, out var payload);

        var frames = LayerProtocolReader.ReadFrames(payload).ToArray();

        Assert.Equal([1UL, 2UL, 3UL], frames.Select(f => f.FrameNumber));
    }

    [Fact]
    public void Trailing_partial_frame_is_ignored()
    {
        byte[] batch = [.. Frame(frameNumber: 1), .. Frame().AsSpan(0, 10).ToArray()];

        var frames = LayerProtocolReader.ReadFrames(batch).ToArray();

        Assert.Equal(1UL, Assert.Single(frames).FrameNumber);
    }

    [Fact]
    public void Frame_fields_are_read_from_their_native_offsets()
    {
        var bytes = Frame(
            frameNumber: 7,
            timestampNs: 123_456_789_000,
            frametimeMs: 8.25f,
            fps: 121.2f,
            processId: 31337,
            actualPresentTimeNs: 123_456_790_000,
            msUntilRenderComplete: 3.5f,
            msUntilDisplayed: 4.25f,
            actualFrametimeMs: 8.5f);

        Assert.True(LayerFrameData.TryRead(bytes, out var frame));

        Assert.Equal(7UL, frame.FrameNumber);
        Assert.Equal(123_456_789_000UL, frame.TimestampNs);
        Assert.Equal(8.25f, frame.FrametimeMs);
        Assert.Equal(121.2f, frame.Fps, 3);
        Assert.Equal(31337, frame.ProcessId);
        Assert.Equal(123_456_790_000UL, frame.ActualPresentTimeNs);
        Assert.Equal(3.5f, frame.MsUntilRenderComplete);
        Assert.Equal(4.25f, frame.MsUntilDisplayed);
        Assert.Equal(8.5f, frame.ActualFrametimeMs);
    }

    [Fact]
    public void Truncated_buffers_are_not_decoded()
    {
        Assert.False(LayerFrameData.TryRead(new byte[LayerFrameData.Size - 1], out _));
        Assert.False(LayerMessageHeader.TryRead(new byte[LayerMessageHeader.Size - 1], out _));
    }
}
