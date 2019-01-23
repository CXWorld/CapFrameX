using System.Collections.Generic;
using System.Windows;

namespace CapFrameX.OcatInterface
{
	public class Session
	{
		public string Path { get; set; }
		public string Filename { get; set; }
		public List<double> FrameStart { get; set; }
		public List<double> FrameEnd { get; set; }
		public List<double> FrameTimes { get; set; }
		public List<double> ReprojectionStart { get; set; }
		public List<double> ReprojectionEnd { get; set; }
		public List<double> ReprojectionTimes { get; set; }
		public List<double> VSync { get; set; }
		public List<bool> AppMissed { get; set; }
		public List<bool> WarpMissed { get; set; }
		public bool IsVR { get; set; }
		public int AppMissesCount { get; set; }
		public int WarpMissesCount { get; set; }
		public int ValidAppFrames { get; set; }
		public int ValidReproFrames { get; set; }
		public double LastFrameTime { get; set; }
		public double LastReprojectionTime { get; set; }
		// System info
		public string MotherboardName { get; set; }
		public string OsVersion { get; set; }
		public string ProcessorName { get; set; }
		public string SystemRamInfo { get; set; }
		public string BaseDriverVersion { get; set; }
		public string DriverPackage { get; set; }
		public string NumberGPUs { get; set; }
		public string GraphicCardName { get; set; }
		public string GPUCoreClock { get; set; }
		public string GPUMemoryClock { get; set; }
		public string GPUMemory { get; set; }
		public string Comment { get; set; }

		public IList<double> GetFrametimeSamplesWindow(double startTime, double endTime)
		{
			IList<double> frametimesWindow = new List<double>();

			if (FrameTimes != null && FrameStart != null)
			{
				for (int i = 0; i < FrameTimes.Count; i++)
				{
					if (FrameStart[i] >= startTime && FrameStart[i] <= endTime)
					{
						frametimesWindow.Add(FrameTimes[i]);
					}
				}
			}

			return frametimesWindow;
		}

		public IList<Point> GetFrametimePointsWindow(double startTime, double endTime)
		{
			IList<Point> frametimesWindow = new List<Point>();

			if (FrameTimes != null && FrameStart != null)
			{
				for (int i = 0; i < FrameTimes.Count; i++)
				{
					if (FrameStart[i] >= startTime && FrameStart[i] <= endTime)
					{
						frametimesWindow.Add(new Point(FrameStart[i], FrameTimes[i]));
					}
				}
			}

			return frametimesWindow;
		}
	}
}
