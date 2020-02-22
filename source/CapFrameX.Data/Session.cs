using CapFrameX.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Statistics;
using CapFrameX.Statistics;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace CapFrameX.Data
{
	public sealed class Session : ISession
	{
		public string Hash { get; set; }
		[JsonProperty("Info")]
		public ISessionInfo Info { get; set; } = new SessionInfo();
		[JsonProperty("Runs")]
		public IList<ISessionRun> Runs { get; set; } = new List<ISessionRun>();
	}

	public static class SessionExtensions
	{

		public static IList<double> GetFrametimeTimeWindow(this ISession session, double startTime, double endTime,
			ERemoveOutlierMethod eRemoveOutlierMethod = ERemoveOutlierMethod.None)
		{
			IList<double> frametimesTimeWindow = new List<double>();
			var frametimeStatisticProvider = new FrametimeStatisticProvider(new CapFrameXConfiguration());
			var frameStarts = session.Runs.SelectMany(r => r.CaptureData.TimeInSeconds).ToArray();
			var frametimes = frametimeStatisticProvider?.GetOutlierAdjustedSequence(session.Runs.SelectMany(r => r.CaptureData.MsBetweenPresents).ToArray(), eRemoveOutlierMethod);

			if (frametimes.Any() && frameStarts.Any())
			{
				for (int i = 0; i < frametimes.Count(); i++)
				{
					if (frameStarts[i] >= startTime && frameStarts[i] <= endTime)
					{
						frametimesTimeWindow.Add(frametimes[i]);
					}
				}
			}

			return frametimesTimeWindow;
		}

		public static IList<Point> GetFrametimePointsTimeWindow(this ISession session, double startTime, double endTime,
			ERemoveOutlierMethod eRemoveOutlierMethod = ERemoveOutlierMethod.None)
		{
			IList<Point> frametimesPointsWindow = new List<Point>();
			var frametimeStatisticProvider = new FrametimeStatisticProvider(new CapFrameXConfiguration());

			var frametimes = frametimeStatisticProvider?.GetOutlierAdjustedSequence(session.Runs.SelectMany(r => r.CaptureData.MsBetweenPresents).ToArray(), eRemoveOutlierMethod);
			var frameStarts = session.Runs.SelectMany(r => r.CaptureData.TimeInSeconds).ToArray();
			if (frametimes.Any() && frameStarts.Any())
			{
				for (int i = 0; i < frametimes.Count(); i++)
				{
					if (frameStarts[i] >= startTime && frameStarts[i] <= endTime)
					{
						frametimesPointsWindow.Add(new Point(frameStarts[i], frametimes[i]));
					}
				}
			}

			return frametimesPointsWindow;
		}

		/// <summary>
		/// Source: https://github.com/GameTechDev/PresentMon
		/// Formular: LatencyMs =~ MsBetweenPresents + MsUntilDisplayed - previous(MsInPresentAPI)
		/// </summary>
		/// <returns></returns>
		public static IList<double> GetApproxInputLagTimes(this ISession session)
		{
			var frameTimes = session.Runs.SelectMany(r => r.CaptureData.MsBetweenPresents).ToArray();
			var appMissed = session.Runs.SelectMany(r => r.CaptureData.Dropped).ToArray();
			var untilDisplayedTimes = session.Runs.SelectMany(r => r.CaptureData.MsUntilDisplayed).ToArray();
			var inPresentAPITimes = session.Runs.SelectMany(r => r.CaptureData.MsInPresentAPI).ToArray();
			var inputLagTimes = new List<double>(frameTimes.Count() - 1);

			for (int i = 1; i < frameTimes.Count(); i++)
			{
				if (appMissed[i] != true)
					inputLagTimes.Add(frameTimes[i] + untilDisplayedTimes[i] - inPresentAPITimes[i - 1]);
			}

			return inputLagTimes;
		}

		public static double GetSyncRangePercentage(this ISession session, int syncRangeLower, int syncRangeUpper)
		{
			var displayTimes = session.Runs.SelectMany(r => r.CaptureData.MsBetweenDisplayChange);
			if(!displayTimes.Any())
			{
				return 0d;
			}

			bool IsInRange(double value)
			{
				int hz = (int)Math.Round(value, 0);

				if (hz >= syncRangeLower && hz <= syncRangeUpper)
					return true;
				else
					return false;
			};

			return displayTimes.Select(time => 1000d / time)
				.Count(hz => IsInRange(hz)) / (double)displayTimes.Count();
		}
	}
}
