using System;

namespace CapFrameX.PresentMonInterface
{
    /// <summary>Non-consuming current frame scalars over the most recent 500 ms of delivered frames.</summary>
    public sealed class OnlineFrameTelemetrySnapshot
    {
        public DateTime TimestampUtc { get; init; }
        public string ProcessName { get; init; }
        public string Runtime { get; init; }
        public double Framerate { get; init; }
        public double Frametime { get; init; }
        public double DisplayTime { get; init; }
    }
}
