using CapFrameX.Contracts.Overlay;
using CapFrameX.Data;
using CapFrameX.Extensions;
using CapFrameX.Statistics;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace CapFrameX.Overlay
{
	public class OverlayService : RTSSCSharpWrapper, IOverlayService
	{
		private readonly IStatisticProvider _statisticProvider;

		private IDisposable _disposableHeartBeat;
		private IDisposable _disposableCaptureTimer;

		private List<string> _runHistory = new List<string>();
		/// <summary>
		/// Refresh period in milliseconds
		/// </summary>
		private int _refreshPeriod;
		private int _numberOfRuns;
		private int _numberOfRunsToAggregate;

		public Subject<bool> IsOverlayActiveStream { get; }

		public string SecondMetric { get; set; }

		public string ThirdMetric { get; set; }

		public OverlayService(IStatisticProvider statisticProvider) : base()
		{
			_statisticProvider = statisticProvider;

			// default 500 milliseconds
			_refreshPeriod = 500;
			_numberOfRuns = 3;
			_numberOfRunsToAggregate = 3;
			SecondMetric = "P1";
			ThirdMetric = "P0dot2";
			IsOverlayActiveStream = new Subject<bool>();
		}

		public void ShowOverlay()
		{
			_disposableHeartBeat = GetOverlayRefreshHeartBeat();
		}

		public void HideOverlay()
		{
			_disposableHeartBeat?.Dispose();
			ReleaseOSD();
		}

		public void UpdateRefreshRate(int milliSeconds)
		{
			_refreshPeriod = milliSeconds;
			_disposableHeartBeat?.Dispose();
			_disposableHeartBeat = GetOverlayRefreshHeartBeat();
		}

		public void StartCountdown(int seconds)
		{
			IObservable<long> obs = Extensions.ObservableExtensions.CountDown(seconds);
			SetShowCaptureTimer(true);
			obs.Subscribe(t =>
			{
				SetCaptureTimerValue((int)t);

				if (t == 0)
					OnCountdownFinished();
			});
		}

		public void StartCaptureTimer()
		{
			SetShowCaptureTimer(true);
			_disposableCaptureTimer = GetCaptureTimer();
		}

		public void StopCaptureTimer()
		{
			SetShowCaptureTimer(false);
			SetCaptureTimerValue(0);
			_disposableCaptureTimer?.Dispose();
		}

		public void ResetHistory()
		{
			_runHistory.Clear();
			SetRunHistory(null);
		}

		public void AddRunToHistory(List<string> captureData)
		{
			var frametimes = captureData.Select(line => RecordDataProvider.GetFrameTimeFromDataLine(line)).ToList();
			var average = _statisticProvider.GetFpsMetricValue(frametimes, EMetric.Average);
			var secondMetricValue = _statisticProvider.GetFpsMetricValue(frametimes, SecondMetric.ConvertToEnum<EMetric>());
			var thrirdMetricValue = _statisticProvider.GetFpsMetricValue(frametimes, ThirdMetric.ConvertToEnum<EMetric>());

			string secondMetricString = 
				SecondMetric.ConvertToEnum<EMetric>() != EMetric.None ? 
				$"{SecondMetric.ConvertToEnum<EMetric>().GetShortDescription()}=" +
				$"{secondMetricValue.ToString(CultureInfo.InvariantCulture)} FPS | " : string.Empty;

			string thirdMetricString =
				ThirdMetric.ConvertToEnum<EMetric>() != EMetric.None ?
				$"{ThirdMetric.ConvertToEnum<EMetric>().GetShortDescription()}=" +
				$"{thrirdMetricValue.ToString(CultureInfo.InvariantCulture)} FPS | " : string.Empty;

			_runHistory.Add($"Avg={average.ToString(CultureInfo.InvariantCulture)} FPS | " + secondMetricString + thirdMetricString);

			if (_runHistory.Count > _numberOfRuns)
				_runHistory.RemoveAt(0);

			SetRunHistory(_runHistory.ToArray());
		}

		private void OnCountdownFinished()
		{
			SetShowCaptureTimer(false);
		}

		private void CheckRTSSRunningAndRefresh()
		{
			var processes = Process.GetProcessesByName("RTSS");

			if (!processes.Any())
			{
				try
				{
					Process proc = new Process();
					proc.StartInfo.FileName = Path.Combine(GetRTSSFullPath());
					proc.StartInfo.UseShellExecute = false;
					proc.StartInfo.Verb = "runas";
					proc.Start();
				}
				catch { }
			}

			Refresh();
		}

		private string GetRTSSFullPath()
		{
			string installPath = string.Empty;

			try
			{
				// SOFTWARE\WOW6432Node\Unwinder\RTSS
				using (RegistryKey key = Registry.LocalMachine.OpenSubKey("Software\\WOW6432Node\\Unwinder\\RTSS"))
				{
					if (key != null)
					{
						object o = key.GetValue("InstallPath");
						if (o != null)
						{
							installPath = o as string;  //"as" because it's REG_SZ...otherwise ToString() might be safe(r)
						}
					}
				}

				// SOFTWARE\Unwinder\RTSS
				if (string.IsNullOrWhiteSpace(installPath))
				{
					using (RegistryKey key = Registry.LocalMachine.OpenSubKey("Software\\Unwinder\\RTSS"))
					{
						if (key != null)
						{
							object o = key.GetValue("InstallPath");
							if (o != null)
							{
								installPath = o as string;  //"as" because it's REG_SZ...otherwise ToString() might be safe(r)
							}
						}
					}
				}
			}
			catch (Exception)
			{
				throw;
			}

			return installPath;
		}

		private IDisposable GetOverlayRefreshHeartBeat()
		{
			return Observable
				.Timer(DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(_refreshPeriod))
				.Subscribe(x => CheckRTSSRunningAndRefresh());
		}

		private IDisposable GetCaptureTimer()
		{
			return Observable
				.Timer(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1))
				.Subscribe(x => SetCaptureTimerValue((int)x));
		}

		public void UpdateNumberOfRuns(int numberOfRuns)
		{
			throw new NotImplementedException();
		}

		public void UpdateNumberOfRunsToAggregate(int numberOfRunsToAggregate)
		{
			throw new NotImplementedException();
		}
	}
}
