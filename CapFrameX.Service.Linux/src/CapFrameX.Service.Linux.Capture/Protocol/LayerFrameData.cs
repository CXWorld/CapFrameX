using System.Buffers.Binary;

namespace CapFrameX.Service.Linux.Capture.Protocol;

/// <summary>
/// One frame as the Vulkan layer reports it.
/// </summary>
/// <remarks>
/// Mirrors the packed <c>FrameDataPoint</c> in the native <c>common.h</c>. The layer writes zero
/// for everything it could not measure - the present-timing fields stay zero without
/// <c>VK_EXT_present_timing</c> - which is why they are nullable once mapped.
/// </remarks>
/// <param name="FrameNumber">Running frame number within the swap chain.</param>
/// <param name="TimestampNs">Present time stamp in nanoseconds.</param>
/// <param name="FrametimeMs">CPU-side interval between presents, in milliseconds.</param>
/// <param name="Fps">Frames per second as the layer computed them.</param>
/// <param name="ProcessId">Process that presented.</param>
/// <param name="ActualPresentTimeNs">Display-side present time, or zero when unavailable.</param>
/// <param name="MsUntilRenderComplete">Time until rendering finished, or zero when unavailable.</param>
/// <param name="MsUntilDisplayed">Time until the frame was displayed, or zero when unavailable.</param>
/// <param name="ActualFrametimeMs">Display-side interval, or zero when unavailable.</param>
public readonly record struct LayerFrameData(
    ulong FrameNumber,
    ulong TimestampNs,
    float FrametimeMs,
    float Fps,
    int ProcessId,
    ulong ActualPresentTimeNs,
    float MsUntilRenderComplete,
    float MsUntilDisplayed,
    float ActualFrametimeMs)
{
    /// <summary>Size of one frame data point on the wire, in bytes.</summary>
    public const int Size = 52;

    /// <summary>
    /// Reads one frame data point. Returns <c>false</c> when the buffer is shorter than
    /// <see cref="Size"/>.
    /// </summary>
    /// <param name="source">Buffer positioned at the start of a frame data point.</param>
    /// <param name="frame">The frame when this returns <c>true</c>.</param>
    public static bool TryRead(ReadOnlySpan<byte> source, out LayerFrameData frame)
    {
        if (source.Length < Size)
        {
            frame = default;
            return false;
        }

        frame = new LayerFrameData(
            BinaryPrimitives.ReadUInt64LittleEndian(source),
            BinaryPrimitives.ReadUInt64LittleEndian(source[8..]),
            BinaryPrimitives.ReadSingleLittleEndian(source[16..]),
            BinaryPrimitives.ReadSingleLittleEndian(source[20..]),
            BinaryPrimitives.ReadInt32LittleEndian(source[24..]),
            BinaryPrimitives.ReadUInt64LittleEndian(source[28..]),
            BinaryPrimitives.ReadSingleLittleEndian(source[36..]),
            BinaryPrimitives.ReadSingleLittleEndian(source[40..]),
            BinaryPrimitives.ReadSingleLittleEndian(source[44..]));

        return true;
    }
}
