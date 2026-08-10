namespace CapFrameX.Contracts.Latency
{
    public readonly struct AmdFlmSample
    {
        public ulong Sequence { get; }

        public long InputQpc { get; }

        public long FrameQpc { get; }

        public double LatencyMs { get; }

        public double LatencyFrames { get; }

        public double Fps { get; }

        public AmdFlmSample(
            ulong sequence,
            long inputQpc,
            long frameQpc,
            double latencyMs,
            double latencyFrames,
            double fps)
        {
            Sequence = sequence;
            InputQpc = inputQpc;
            FrameQpc = frameQpc;
            LatencyMs = latencyMs;
            LatencyFrames = latencyFrames;
            Fps = fps;
        }
    }
}
