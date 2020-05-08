namespace CapFrameX.Data.Session.Contracts
{
	public interface ISessionCaptureData
	{
		bool[] Dropped { get; set; }
		double[] MsBetweenDisplayChange { get; set; }
		double[] MsUntilRenderComplete { get; set; }
		double[] TimeInSeconds { get; set; }
		double[] MsBetweenPresents { get; set; }
		double[] MsInPresentAPI { get; set; }
		double[] QPCTime { get; set; }
		double[] MsUntilDisplayed { get; set; }
		int[] PresentMode { get; set; }
		int[] PresentFlags { get; set; }
		double[] VSync { get; set; }
		bool[] LsrMissed { get; set; }
	}
}