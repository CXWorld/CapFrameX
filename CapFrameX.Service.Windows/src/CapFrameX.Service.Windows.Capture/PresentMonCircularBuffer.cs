namespace CapFrameX.Service.Capture;

/// <summary>
/// Sizes CapFrameX offers for PresentMon's present event circular buffer
/// (<c>--set_circular_buffer_size</c>).
/// </summary>
/// <remarks>
/// PresentMon only accepts powers of two and rejects the whole command line otherwise, which would
/// keep the capture service from starting at all. Its own default is 2048; CapFrameX has always
/// run with 4096.
/// </remarks>
public static class PresentMonCircularBuffer
{
    /// <summary>The size CapFrameX uses unless the user picks another one.</summary>
    public const int DefaultSize = 4096;

    /// <summary>The sizes CapFrameX offers.</summary>
    public static IReadOnlyList<int> Sizes { get; } = [2048, 4096, 8192];

    /// <summary>Falls back to <see cref="DefaultSize"/> for anything PresentMon would reject.</summary>
    /// <param name="size">Requested buffer size.</param>
    public static int Normalize(int size) => Sizes.Contains(size) ? size : DefaultSize;
}
