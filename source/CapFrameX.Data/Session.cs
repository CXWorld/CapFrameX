using CapFrameX.Configuration;
using CapFrameX.Statistics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace CapFrameX.Data
{
	public sealed class Session
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
		public List<double> UntilDisplayedTimes { get; set; }
		public List<double> InPresentAPITimes { get; set; }
		public List<double> DisplayTimes { get; set; }
		public List<double> QPCTimes { get; set; }
		public bool IsVR { get; set; }
		public int WarpMissesCount { get; set; }
		public int ValidReproFrames { get; set; }
		public double LastFrameTime { get; set; }

		public Session()
		{
			_frametimeStatisticProvider = new FrametimeStatisticProvider(new CapFrameXConfiguration());
		}

		public IList<double> GetFrametimeSampleWindow(int startIndex, double endIndex,
			ERemoveOutlierMethod eRemoveOutlierMethod = ERemoveOutlierMethod.None)
		{
			var frametimesSampleWindow = new List<double>();

			var frametimes = _frametimeStatisticProvider?.GetOutlierAdjustedSequence(FrameTimes, eRemoveOutlierMethod);

			if (frametimes != null && frametimes.Any())
			{
				for (int i = startIndex; i < frametimes.Count - endIndex; i++)
				{
					frametimesSampleWindow.Add(frametimes[i]);
				}
			}

			return frametimesSampleWindow;
		}

		public IList<double> GetFrametimeTimeWindow(double startTime, double endTime,
			ERemoveOutlierMethod eRemoveOutlierMethod = ERemoveOutlierMethod.None)
		{
			IList<double> frametimesTimeWindow = new List<double>();

			var frametimes = _frametimeStatisticProvider?.GetOutlierAdjustedSequence(FrameTimes, eRemoveOutlierMethod);

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

		public IList<Point> GetFrametimePointsTimeWindow(double startTime, double endTime,
			ERemoveOutlierMethod eRemoveOutlierMethod = ERemoveOutlierMethod.None)
		{
			IList<Point> frametimesPointsWindow = new List<Point>();

			var frametimes = _frametimeStatisticProvider?.GetOutlierAdjustedSequence(FrameTimes, eRemoveOutlierMethod);

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

		public IList<Point> GetFrametimePointsSampleWindow(int startIndex, double endIndex,
			ERemoveOutlierMethod eRemoveOutlierMethod = ERemoveOutlierMethod.None)
		{
			var frametimesPointsSampleWindow = new List<Point>();

			var frametimes = _frametimeStatisticProvider?.GetOutlierAdjustedSequence(FrameTimes, eRemoveOutlierMethod);

			if (frametimes != null && frametimes.Any())
			{
				for (int i = startIndex; i < frametimes.Count - endIndex; i++)
				{
					frametimesPointsSampleWindow.Add(new Point(FrameStart[i], frametimes[i]));
				}
			}

			return frametimesPointsSampleWindow;
		}

		/// <summary>
		/// Source: https://github.com/GameTechDev/PresentMon
		/// Formular: LatencyMs =~ MsBetweenPresents + MsUntilDisplayed - previous(MsInPresentAPI)
		/// </summary>
		/// <returns></returns>
		public IList<double> GetApproxInputLagTimes()
		{
			var inputLagTimes = new List<double>(FrameTimes.Count - 1);

			for (int i = 1; i < FrameTimes.Count; i++)
			{
				if (AppMissed[i] != true)
					inputLagTimes.Add(FrameTimes[i] + UntilDisplayedTimes[i] - InPresentAPITimes[i - 1]);
			}

			return inputLagTimes;
		}

		public double GetSyncRangePercentage(int syncRangeLower, int syncRangeUpper)
		{
			if (DisplayTimes == null)
				return 0d;

			bool IsInRange(double value)
			{
				int hz = (int)Math.Round(value, 0);

				if (hz >= syncRangeLower && hz <= syncRangeUpper)
					return true;
				else
					return false;
			};

			return DisplayTimes.Select(time => 1000d / time)
				.Count(hz => IsInRange(hz)) / (double)DisplayTimes.Count;
		}
	}
}
