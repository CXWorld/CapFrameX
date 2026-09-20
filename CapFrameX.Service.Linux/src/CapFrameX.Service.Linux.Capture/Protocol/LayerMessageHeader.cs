using System.Buffers.Binary;

namespace CapFrameX.Service.Linux.Capture.Protocol;

/// <summary>
/// Header of every layer message: type, payload length and the layer's time stamp.
/// </summary>
/// <remarks>
/// Mirrors <c>MessageHeader</c> in the native <c>common.h</c>: two 32-bit fields followed by a
/// 64-bit time stamp, little endian on every platform CapFrameX supports.
/// </remarks>
/// <param name="Type">Message type.</param>
/// <param name="PayloadSize">Length of the payload that follows, in bytes.</param>
/// <param name="TimestampNs">Layer time stamp in nanoseconds.</param>
public readonly record struct LayerMessageHeader(LayerMessageType Type, uint PayloadSize, ulong TimestampNs)
{
    /// <summary>Size of the header on the wire, in bytes.</summary>
    public const int Size = 16;

    /// <summary>
    /// Reads a header. Returns <c>false</c> when the buffer is shorter than <see cref="Size"/>.
    /// </summary>
    /// <param name="source">Buffer positioned at the start of a message.</param>
    /// <param name="header">The header when this returns <c>true</c>.</param>
    public static bool TryRead(ReadOnlySpan<byte> source, out LayerMessageHeader header)
    {
        if (source.Length < Size)
        {
            header = default;
            return false;
        }

        header = new LayerMessageHeader(
            (LayerMessageType)BinaryPrimitives.ReadUInt32LittleEndian(source),
            BinaryPrimitives.ReadUInt32LittleEndian(source[4..]),
            BinaryPrimitives.ReadUInt64LittleEndian(source[8..]));

        return true;
    }
}
