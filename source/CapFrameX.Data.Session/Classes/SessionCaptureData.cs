using CapFrameX.Data.Session.Contracts;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.Data.Session.Classes
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
		public double[] MsUntilDisplayed { get; set; }
		public int[] PresentMode { get; set; }
		public int[] PresentFlags { get; set; }
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
			MsUntilDisplayed = new double[numberOfCapturePoints];
			PresentMode = new int[numberOfCapturePoints];
			PresentFlags = new int[numberOfCapturePoints];
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
					VSync = VSync[i],
					LsrMissed = LsrMissed[i],
					MsUntilDisplayed = MsUntilDisplayed[i],
					PresentMode = PresentMode[i],
					PresentFlags = PresentFlags[i]
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
		public double MsUntilDisplayed { get; set; }
		public int PresentMode { get; set; }
		public int PresentFlags { get; set; }
		public double VSync { get; set; }
		public bool LsrMissed { get; set; }
	}
}
