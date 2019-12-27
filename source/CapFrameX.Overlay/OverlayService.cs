using CapFrameX.Contracts.Configuration;
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
		private readonly IAppConfiguration _appConfiguration;

		private IDisposable _disposableHeartBeat;
		private IDisposable _disposableCaptureTimer;

		private List<string> _runHistory = new List<string>();
		/// <summary>
		/// Refresh period in milliseconds
		/// </summary>
		private int _refreshPeriod;
		private int _numberOfRuns;

		public Subject<bool> IsOverlayActiveStream { get; }

		public string SecondMetric { get; set; }

		public string ThirdMetric { get; set; }

		public OverlayService(IStatisticProvider statisticProvider, IAppConfiguration appConfiguration) 
			: base()
		{
			_statisticProvider = statisticProvider;
			_appConfiguration = appConfiguration;

			// ToDo: get from config
			// default 500 milliseconds
			_refreshPeriod = 500;
			_numberOfRuns = _appConfiguration.SelectedHistoryRuns;
			SecondMetric = _appConfiguration.SecondMetricOverlay;
			ThirdMetric = _appConfiguration.ThirdMetricOverlay;
			IsOverlayActiveStream = new Subject<bool>();

			_runHistory = Enumerable.Repeat("N/A", _numberOfRuns).ToList();
			SetRunHistory(_runHistory.ToArray());
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
			_runHistory = Enumerable.Repeat("N/A", _numberOfRuns).ToList();
			SetRunHistory(_runHistory.ToArray());
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
				$"{secondMetricValue.ToString(string.Format("F{0}", _appConfiguration.FpsValuesRoundingDigits), CultureInfo.InvariantCulture)} " +
				$"FPS | " : string.Empty;

			string thirdMetricString =
				ThirdMetric.ConvertToEnum<EMetric>() != EMetric.None ?
				$"{ThirdMetric.ConvertToEnum<EMetric>().GetShortDescription()}=" +
				$"{thrirdMetricValue.ToString(string.Format("F{0}", _appConfiguration.FpsValuesRoundingDigits), CultureInfo.InvariantCulture)} " +
				$"FPS | " : string.Empty;

			var currentList = new List<string>() { $"Avg={average.ToString(string.Format("F{0}", _appConfiguration.FpsValuesRoundingDigits), CultureInfo.InvariantCulture)} " +
				$"FPS | " + secondMetricString + thirdMetricString };
			_runHistory = currentList.Concat(_runHistory).ToList();

			if (_runHistory.Count > _numberOfRuns)
				_runHistory.RemoveAt(_runHistory.Count - 1);

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
			_numberOfRuns = numberOfRuns;
			_runHistory = Enumerable.Repeat("N/A", _numberOfRuns).ToList();
			SetRunHistory(_runHistory.ToArray());
		}
	}
}
