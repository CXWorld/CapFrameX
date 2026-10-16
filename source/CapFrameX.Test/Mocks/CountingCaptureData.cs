using CapFrameX.Data.Session.Contracts;

namespace CapFrameX.Test.Mocks
{
    /// <summary>
    /// Capture data that counts how often the frame time and display change arrays are read.
    /// A session time window reads its array once per run, so the counts tell how many windows
    /// a chart or a view model built.
    /// </summary>
    public sealed class CountingCaptureData : ISessionCaptureData
    {
        private readonly ISessionCaptureData _inner;

        public CountingCaptureData(ISessionCaptureData inner) => _inner = inner;

        public int FrametimeReads { get; private set; }
        public int DisplayChangeReads { get; private set; }

        public double[] MsBetweenPresents
        {
            get { FrametimeReads++; return _inner.MsBetweenPresents; }
            set => _inner.MsBetweenPresents = value;
        }

        public double[] MsBetweenDisplayChange
        {
            get { DisplayChangeReads++; return _inner.MsBetweenDisplayChange; }
            set => _inner.MsBetweenDisplayChange = value;
        }

        public double[] TimeInSeconds { get => _inner.TimeInSeconds; set => _inner.TimeInSeconds = value; }
        public double[] MsInPresentAPI { get => _inner.MsInPresentAPI; set => _inner.MsInPresentAPI = value; }
        public double[] MsUntilRenderComplete { get => _inner.MsUntilRenderComplete; set => _inner.MsUntilRenderComplete = value; }
        public double[] MsUntilDisplayed { get => _inner.MsUntilDisplayed; set => _inner.MsUntilDisplayed = value; }
        public double[] QPCTime { get => _inner.QPCTime; set => _inner.QPCTime = value; }
        public int[] PresentMode { get => _inner.PresentMode; set => _inner.PresentMode = value; }
        public int[] AllowsTearing { get => _inner.AllowsTearing; set => _inner.AllowsTearing = value; }
        public int[] SyncInterval { get => _inner.SyncInterval; set => _inner.SyncInterval = value; }
        public bool[] Dropped { get => _inner.Dropped; set => _inner.Dropped = value; }
        public string[] FrameType { get => _inner.FrameType; set => _inner.FrameType = value; }
        public double[] PcLatency { get => _inner.PcLatency; set => _inner.PcLatency = value; }
        public double[] AnimationError { get => _inner.AnimationError; set => _inner.AnimationError = value; }
        public double[] GpuActive { get => _inner.GpuActive; set => _inner.GpuActive = value; }
        public double[] CpuActive { get => _inner.CpuActive; set => _inner.CpuActive = value; }
        public int[] LayerIndex { get => _inner.LayerIndex; set => _inner.LayerIndex = value; }
    }
}
