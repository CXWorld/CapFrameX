using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.Contracts.Configuration
{
    /// <summary>
    /// Sizes CapFrameX offers for PresentMon's present event circular buffer
    /// (<c>--set_circular_buffer_size</c>). PresentMon only accepts powers of two and rejects the
    /// whole command line otherwise, which would keep the capture service from starting at all.
    /// Its own default is 2048; CapFrameX has always run with 4096.
    /// </summary>
    public static class PresentMonCircularBuffer
    {
        public const int DefaultSize = 4096;

        public static readonly IReadOnlyList<int> Sizes = new[] { 2048, 4096, 8192 };

        public static int Normalize(int size)
        {
            return Sizes.Contains(size) ? size : DefaultSize;
        }
    }
}
