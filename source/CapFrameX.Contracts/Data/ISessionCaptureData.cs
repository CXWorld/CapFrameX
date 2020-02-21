namespace CapFrameX.Contracts.Data
{
	public interface ISessionCaptureData
	{
		bool[] AppMissed { get; set; }
		double[] DisplayTimes { get; set; }
		double[] FrameEnd { get; set; }
		double[] FrameStart { get; set; }
		double[] FrameTimes { get; set; }
		double[] InPresentAPITimes { get; set; }
		double[] QPCTimes { get; set; }
		double[] ReprojectionEnd { get; set; }
		double[] ReprojectionStart { get; set; }
		double[] ReprojectionTimes { get; set; }
		double[] UntilDisplayedTimes { get; set; }
		double[] VSync { get; set; }
		bool[] WarpMissed { get; set; }
	}
}