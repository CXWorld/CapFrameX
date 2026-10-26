namespace CapFrameX.Capture.Contracts
{
    /// <summary>
    /// What the capture service saw a listed process present during the last seconds. The
    /// automatic target selection weighs it when several processes are listed.
    /// </summary>
    public sealed class CaptureProcessActivity
    {
        public CaptureProcessActivity(string name, int processId, int idleSeconds,
            double presentsPerSecond, double? gpuBusyShare, string presentMode, bool hasGeneratedFrames)
        {
            Name = name;
            ProcessId = processId;
            IdleSeconds = idleSeconds;
            PresentsPerSecond = presentsPerSecond;
            GpuBusyShare = gpuBusyShare;
            PresentMode = presentMode;
            HasGeneratedFrames = hasGeneratedFrames;
        }

        public string Name { get; }

        public int ProcessId { get; }

        /// <summary>
        /// Whole seconds since the process presented last; 0 within the current second.
        /// </summary>
        public int IdleSeconds { get; }

        /// <summary>
        /// Presents per second over the measured seconds; 0 before the first second is complete.
        /// </summary>
        public double PresentsPerSecond { get; }

        /// <summary>
        /// Share (0..1) of the measured seconds the GPU was busy with the process' frames
        /// (PresentMon's MsGPUBusy). Null before the first second is complete and without GPU
        /// tracking.
        /// </summary>
        public double? GpuBusyShare { get; }

        /// <summary>
        /// PresentMon's present mode of the latest present, e.g. "Hardware: Independent Flip".
        /// </summary>
        public string PresentMode { get; }

        /// <summary>
        /// True when a generated frame (frame generation reported through FrameType) was seen
        /// during the measured seconds.
        /// </summary>
        public bool HasGeneratedFrames { get; }
    }
}
