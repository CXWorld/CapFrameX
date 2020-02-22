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
	public class SessionInfo : ISessionInfo
	{
		public Version AppVersion { get; set; }
		public Guid Id { get; set; }
		public string Processor { get; set; }
		public string GameName { get; set; }
		public string ProcessName { get; set; }
		public DateTime CreationDate { get; set; }
		public string Motherboard { get; set; }
		public string OS { get; set; }
		public string SystemRam { get; set; }
		public string BaseDriverVersion { get; set; }
		public string DriverPackage { get; set; }
		public string GPU { get; set; }
		public string GPUCount { get; set; }
		public string GpuCoreClock { get; set; }
		public string GpuMemoryClock { get; set; }
		public string Comment { get; set; }
		public bool IsAggregated { get; set; }
	}

	public class SessionRun : ISessionRun
	{
		public string Path { get; set; }
		public bool IsVR { get; set; }
		public int WarpMissesCount => CaptureData.LsrMissed.Count(x => x == true);
		public double LastFrameTime => CaptureData.MsBetweenPresents.Last();
		public int ValidReproFrames { get; set; }
		public string Filename { get; set; }
		[JsonProperty("CaptureData")]
		public ISessionCaptureData CaptureData { get; set; }
		[JsonProperty("SensorData")]
		public ISessionSensorData SensorData { get; set; }
	}

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

	public struct CaptureDataEntry {
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

	public class SessionSensorData : ISessionSensorData
	{
		public double[] MeasureTime { get; set; }
		public int[] GpuUsage { get; set; }
		public double[] RamUsage { get; set; }
		public bool[] IsInGpuLimit { get; set; }
		public int[] GpuPower { get; set; }
		public int[] GpuTemp { get; set; }
	}

	public sealed class Session : ISession
	{
		[JsonProperty("Info")]
		public ISessionInfo Info { get; set; } = new SessionInfo();
		[JsonProperty("Runs")]
		public IList<ISessionRun> Runs { get; set; } = new List<ISessionRun>();
	}

	public static class SessionExtensions
	{
		public static IList<double> GetFrametimeSampleWindow(this ISession session, int startIndex, double endIndex,
			ERemoveOutlierMethod eRemoveOutlierMethod = ERemoveOutlierMethod.None)
		{
			var frametimeStatisticProvider = new FrametimeStatisticProvider(new CapFrameXConfiguration());
			var frametimesSampleWindow = new List<double>();

			var frametimes = frametimeStatisticProvider?.GetOutlierAdjustedSequence(session.Runs.SelectMany(r => r.CaptureData.MsBetweenPresents).ToArray(), eRemoveOutlierMethod);

			if (frametimes.Any())
			{
				for (int i = startIndex; i < frametimes.Count() - endIndex; i++)
				{
					frametimesSampleWindow.Add(frametimes[i]);
				}
			}

			return frametimesSampleWindow;
		}

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

		public static IList<Point> GetFrametimePointsSampleWindow(this ISession session, int startIndex, double endIndex,
			ERemoveOutlierMethod eRemoveOutlierMethod = ERemoveOutlierMethod.None)
		{
			var frametimesPointsSampleWindow = new List<Point>();
			var frametimeStatisticProvider = new FrametimeStatisticProvider(new CapFrameXConfiguration());

			var frametimes = frametimeStatisticProvider?.GetOutlierAdjustedSequence(session.Runs.SelectMany(r => r.CaptureData.MsBetweenPresents).ToArray(), eRemoveOutlierMethod);
			var frameStarts = session.Runs.SelectMany(r => r.CaptureData.TimeInSeconds).ToArray();
			if (frametimes.Any())
			{
				for (int i = startIndex; i < frametimes.Count() - endIndex; i++)
				{
					frametimesPointsSampleWindow.Add(new Point(frameStarts[i], frametimes[i]));
				}
			}

			return frametimesPointsSampleWindow;
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
