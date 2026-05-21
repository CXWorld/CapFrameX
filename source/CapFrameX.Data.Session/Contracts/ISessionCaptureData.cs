namespace CapFrameX.Data.Session.Contracts
{
	public interface ISessionCaptureData
	{
		double[] TimeInSeconds { get; set; }
		double[] MsBetweenPresents { get; set; }
		double[] MsInPresentAPI { get; set; }
		double[] MsBetweenDisplayChange { get; set; }
		double[] MsUntilRenderComplete { get; set; }
		double[] MsUntilDisplayed { get; set; }
		double[] QPCTime { get; set; }
		int[] PresentMode { get; set; }
		int[] AllowsTearing { get; set; }
		int[] SyncInterval { get; set; }
		bool[] Dropped { get; set; }
        double[] PcLatency { get; set; }
		double[] AnimationError { get; set; }
        double[] GpuActive { get; set; }
		double[] CpuActive { get; set; }
    }
}