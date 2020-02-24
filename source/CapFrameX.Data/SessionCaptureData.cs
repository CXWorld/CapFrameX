using CapFrameX.Contracts.Data;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.Data
{
	public class SessionCaptureData : ISessionCaptureData
	{
		public bool[] Dropped { get; set; }
		public double[] MsBetweenDisplayChange { get; set; }
		public double[] MsUntilRenderComplete { get; set; }
		public double[] TimeInSeconds { get; set; }
		public double[] MsBetweenPresents { get; set; }
		public double[] MsInPresentAPI { get; set; }
		public double[] QPCTime { get; set; }
		public double[] ReprojectionEnd { get; set; }
		public double[] ReprojectionStart { get; set; }
		public double[] ReprojectionTimes { get; set; }
		public double[] MsUntilDisplayed { get; set; }
		public double[] VSync { get; set; }
		public bool[] LsrMissed { get; set; }

		public SessionCaptureData(int numberOfCapturePoints) {
			Dropped = new bool[numberOfCapturePoints];
			MsBetweenDisplayChange = new double[numberOfCapturePoints];
			MsUntilRenderComplete = new double[numberOfCapturePoints];
			TimeInSeconds = new double[numberOfCapturePoints];
			MsBetweenPresents = new double[numberOfCapturePoints];
			MsInPresentAPI = new double[numberOfCapturePoints];
			QPCTime = new double[numberOfCapturePoints];
			ReprojectionEnd = new double[numberOfCapturePoints];
			ReprojectionStart = new double[numberOfCapturePoints];
			ReprojectionTimes = new double[numberOfCapturePoints];
			MsUntilDisplayed = new double[numberOfCapturePoints];
			VSync = new double[numberOfCapturePoints];
			LsrMissed = new bool[numberOfCapturePoints];
		}

		public IEnumerable<CaptureDataEntry> LineWise()
		{
			for(int i = 0; i < MsBetweenPresents.Count(); i++)
			{
				yield return new CaptureDataEntry()
				{
					Dropped = Dropped[i],
					MsBetweenDisplayChange = MsBetweenDisplayChange[i],
					MsUntilRenderComplete = MsUntilRenderComplete[i],
					TimeInSeconds = TimeInSeconds[i],
					MsBetweenPresents = MsBetweenPresents[i],
					MsInPresentAPI = MsInPresentAPI[i],
					QPCTime = QPCTime[i],
					ReprojectionEnd = ReprojectionEnd[i],
					ReprojectionStart = ReprojectionStart[i],
					ReprojectionTime = ReprojectionTimes[i],
					VSync = VSync[i],
					LsrMissed = LsrMissed[i],
					MsUntilDisplayed = MsUntilDisplayed[i]
				};
			}
		}
	}

	public struct CaptureDataEntry
	{
		public bool Dropped { get; set; }
		public double MsBetweenDisplayChange { get; set; }
		public double MsUntilRenderComplete { get; set; }
		public double TimeInSeconds { get; set; }
		public double MsBetweenPresents { get; set; }
		public double MsInPresentAPI { get; set; }
		public double QPCTime { get; set; }
		public double ReprojectionEnd { get; set; }
		public double ReprojectionStart { get; set; }
		public double ReprojectionTime { get; set; }
		public double MsUntilDisplayed { get; set; }
		public double VSync { get; set; }
		public bool LsrMissed { get; set; }
	}
}
