using CapFrameX.Configuration;
using CapFrameX.Statistics;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace CapFrameX.OcatInterface
{
	public class Session
	{
		IStatisticProvider _frametimeStatisticProvider;

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
		public List<double> Displaytimes { get; set; }
		public List<double> QPCTimes { get; set; }
		public bool IsVR { get; set; }
		public int AppMissesCount { get; set; }
		public int WarpMissesCount { get; set; }
		public int ValidAppFrames { get; set; }
		public int ValidReproFrames { get; set; }
		public double LastFrameTime { get; set; }
		public double LastReprojectionTime { get; set; }

		public Session()
		{		
			_frametimeStatisticProvider = new FrametimeStatisticProvider(new CapFrameXConfiguration());
		}

		public IList<double> GetFrametimeSampleWindow(int startIndex, double endIndex, ERemoveOutlierMethod removeOutlierMethod)
		{
			var frametimesSampleWindow = new List<double>();
			var frametimes = _frametimeStatisticProvider?.GetOutlierAdjustedSequence(FrameTimes, removeOutlierMethod);

			if (frametimes != null && frametimes.Any())
			{
				for (int i = startIndex; i < frametimes.Count - endIndex; i++)
				{
					frametimesSampleWindow.Add(frametimes[i]);
				}
			}

			return frametimesSampleWindow;
		}

		public IList<double> GetFrametimeTimeWindow(double startTime, double endTime, ERemoveOutlierMethod removeOutlierMethod)
		{
			IList<double> frametimesTimeWindow = new List<double>();
			var frametimes = _frametimeStatisticProvider?.GetOutlierAdjustedSequence(FrameTimes, removeOutlierMethod);

			if (frametimes != null && FrameStart != null)
			{
				for (int i = 0; i < frametimes.Count; i++)
				{
					if (FrameStart[i] >= startTime && FrameStart[i] <= endTime)
					{
						frametimesTimeWindow.Add(frametimes[i]);
					}
				}
			}

			return frametimesTimeWindow;
		}

		public IList<Point> GetFrametimePointsTimeWindow(double startTime, double endTime)
		{
			IList<Point> frametimesPointsWindow = new List<Point>();
			var frametimes = _frametimeStatisticProvider?.GetOutlierAdjustedSequence(FrameTimes, ERemoveOutlierMethod.None);

			if (frametimes != null && FrameStart != null)
			{
				for (int i = 0; i < frametimes.Count; i++)
				{
					if (FrameStart[i] >= startTime && FrameStart[i] <= endTime)
					{
						frametimesPointsWindow.Add(new Point(FrameStart[i], frametimes[i]));
					}
				}
			}

			return frametimesPointsWindow;
		}

		public IList<Point> GetFrametimePointsSampleWindow(int startIndex, double endIndex)
		{
			var frametimesPointsSampleWindow = new List<Point>();
			var frametimes = _frametimeStatisticProvider?.GetOutlierAdjustedSequence(FrameTimes, ERemoveOutlierMethod.None);

			if (frametimes != null && frametimes.Any())
			{
				for (int i = startIndex; i < frametimes.Count - endIndex; i++)
				{
					frametimesPointsSampleWindow.Add(new Point(FrameStart[i], frametimes[i]));
				}
			}

			return frametimesPointsSampleWindow;
		}
	}
}
