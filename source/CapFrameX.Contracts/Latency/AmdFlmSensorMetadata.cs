using CapFrameX.Contracts.Overlay;

namespace CapFrameX.Contracts.Latency
{
    public static class AmdFlmSensorMetadata
    {
        public const string Identifier = "/capframex/amd-flm/0/latency/0";

        public const string Name = "AMD FLM Latency";

        public const string HardwareName = "AMD Frame Latency Meter";

        public static bool IsUnavailable(IOverlayEntry entry)
        {
            if (entry.Identifier != "OnlineAmdFlmLatency" && entry.Identifier != Identifier && entry.StableIdentifier != Identifier)
                return false;
            return entry.Value switch
            {
                double value => !double.IsFinite(value) || value <= 0,
                float value => !float.IsFinite(value) || value <= 0,
                _ => true
            };
        }
    }
}
