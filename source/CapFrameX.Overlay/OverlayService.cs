using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
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
using System.Threading;
using System.Threading.Tasks;

namespace CapFrameX.Overlay
{
	public class OverlayService : RTSSCSharpWrapper, IOverlayService
	{
		private readonly IStatisticProvider _statisticProvider;
		private readonly IRecordDataProvider _recordDataProvider;
		private readonly IOverlayEntryProvider _overlayEntryProvider;
		private readonly IAppConfiguration _appConfiguration;

		private IDisposable _disposableHeartBeat;
		private IDisposable _disposableCaptureTimer;
		private IDisposable _disposableCountdown;

		private IList<string> _runHistory = new List<string>();
		private IList<IList<string>> _captureDataHistory = new List<IList<string>>();
		private IList<IList<double>> _frametimeHistory = new List<IList<double>>();
		private bool[] _runHistoryOutlierFlags;
		private int _refreshPeriod;
		private int _numberOfRuns;

		public Subject<bool> IsOverlayActiveStream { get; }

		public string SecondMetric { get; set; }

		public string ThirdMetric { get; set; }

		public int RunHistoryCount => _runHistory.Count(run => run != "N/A");

		public OverlayService(IStatisticProvider statisticProvider, IRecordDataProvider recordDataProvider,
			IOverlayEntryProvider overlayEntryProvider, IAppConfiguration appConfiguration)
			: base()
		{
			_statisticProvider = statisticProvider;
			_recordDataProvider = recordDataProvider;
			_overlayEntryProvider = overlayEntryProvider;
			_appConfiguration = appConfiguration;

			_refreshPeriod = _appConfiguration.OSDRefreshPeriod;
			_numberOfRuns = _appConfiguration.SelectedHistoryRuns;
			SecondMetric = _appConfiguration.SecondMetricOverlay;
			ThirdMetric = _appConfiguration.ThirdMetricOverlay;
			IsOverlayActiveStream = new Subject<bool>();
			_runHistoryOutlierFlags = Enumerable.Repeat(false, _numberOfRuns).ToArray();

			SetOverlayEntries(overlayEntryProvider?.GetOverlayEntries());
			overlayEntryProvider.EntryUpdateStream.Subscribe(x =>
				SetOverlayEntries(overlayEntryProvider?.GetOverlayEntries()));

			_runHistory = Enumerable.Repeat("N/A", _numberOfRuns).ToList();
			SetRunHistory(_runHistory.ToArray());
			SetRunHistoryAggregation(string.Empty);
			SetRunHistoryOutlierFlags(_runHistoryOutlierFlags);
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

			SetCaptureTimerValue(0);
			_disposableCountdown?.Dispose();
			_disposableCountdown = obs.Subscribe(t =>
			{
				SetCaptureTimerValue((int)t);

				if (t == 0)
					SetShowCaptureTimer(false);
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

		public void SetCaptureTimerValue(int t)
		{
			var captureTimer = _overlayEntryProvider.GetOverlayEntry("CaptureTimer");
			captureTimer.Value = $"{t.ToString()} s";
			SetOverlayEntries(_overlayEntryProvider?.GetOverlayEntries());
			if (_appConfiguration.IsOverlayActive)
			{
				CheckRTSSRunningAndRefresh();
			};
		}

		public void SetCaptureServiceStatus(string status)
		{
			var captureStatus = _overlayEntryProvider.GetOverlayEntry("CaptureServiceStatus");
			captureStatus.Value = status;
			SetOverlayEntries(_overlayEntryProvider?.GetOverlayEntries());
		}

		public void SetShowRunHistory(bool showHistory)
		{
			var history = _overlayEntryProvider.GetOverlayEntry("RunHistory");
			history.ShowOnOverlay = showHistory;
			SetOverlayEntries(_overlayEntryProvider?.GetOverlayEntries());
		}

		public void ResetHistory()
		{
			_runHistory = Enumerable.Repeat("N/A", _numberOfRuns).ToList();
			_runHistoryOutlierFlags = Enumerable.Repeat(false, _numberOfRuns).ToArray();
			_captureDataHistory.Clear();
			_frametimeHistory.Clear();
			SetRunHistory(_runHistory.ToArray());
			SetRunHistoryAggregation(string.Empty);
			SetRunHistoryOutlierFlags(_runHistoryOutlierFlags);
		}

		public void AddRunToHistory(List<string> captureData)
		{
			var frametimes = captureData.Select(line =>
				RecordDataProvider.GetFrameTimeFromDataLine(line)).ToList();

			if (RunHistoryCount == _numberOfRuns)
				ResetHistory();

			if (RunHistoryCount < _numberOfRuns)
			{
				// metric history
				_runHistory[RunHistoryCount] = GetMetrics(frametimes);
				SetRunHistory(_runHistory.ToArray());

				// capture data history
				_captureDataHistory.Add(captureData);

				// frametime history
				_frametimeHistory.Add(frametimes);

				if (_appConfiguration.UseAggregation
					&& RunHistoryCount == _numberOfRuns
					&& _runHistoryOutlierFlags.All(x => x == false))
				{
					// analysis
					// Todo...

					SetRunHistoryAggregation(GetAggregation());

					// write aggregated file
					Task.Run(async () =>
					{
						await SetTaskDelayOffset().ContinueWith(_ =>
						{
							_recordDataProvider
							.SaveAggregatedPresentData(_captureDataHistory);
						}, CancellationToken.None, TaskContinuationOptions.RunContinuationsAsynchronously, TaskScheduler.Default);
					});
				}
			}
		}

		public void UpdateOverlayEntries()
		{
			SetOverlayEntries(_overlayEntryProvider?.GetOverlayEntries());
		}

		public void UpdateNumberOfRuns(int numberOfRuns)
		{
			_numberOfRuns = numberOfRuns;
			_runHistory = Enumerable.Repeat("N/A", _numberOfRuns).ToList();
			_runHistoryOutlierFlags = Enumerable.Repeat(false, _numberOfRuns).ToArray();
			SetRunHistory(_runHistory.ToArray());
			SetRunHistoryAggregation(string.Empty);
			SetRunHistoryOutlierFlags(_runHistoryOutlierFlags);
		}

		public string GetRTSSFullPath()
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

		private async Task SetTaskDelayOffset()
		{
			await Task.Delay(TimeSpan.FromMilliseconds(1000));
		}

		private void SetShowCaptureTimer(bool show)
		{
			var captureTimer = _overlayEntryProvider.GetOverlayEntry("CaptureTimer");
			captureTimer.ShowOnOverlay = show;
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

		private IDisposable GetOverlayRefreshHeartBeat()
		{
			CheckRTSSRunningAndRefresh();

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

		private string GetAggregation()
		{
			var concatedFrametimes = new List<double>(_frametimeHistory.Sum(set => set.Count));

			foreach (var frametimeSet in _frametimeHistory)
			{
				concatedFrametimes.AddRange(frametimeSet);
			}

			return GetMetrics(concatedFrametimes);
		}

		private string GetMetrics(List<double> frametimes)
		{
			var average = _statisticProvider.GetFpsMetricValue(frametimes, EMetric.Average);
			var secondMetricValue = _statisticProvider.GetFpsMetricValue(frametimes, SecondMetric.ConvertToEnum<EMetric>());
			var thrirdMetricValue = _statisticProvider.GetFpsMetricValue(frametimes, ThirdMetric.ConvertToEnum<EMetric>());
			string numberFormat = string.Format("F{0}", _appConfiguration.FpsValuesRoundingDigits);
			var cultureInfo = CultureInfo.InvariantCulture;

			string secondMetricString =
				SecondMetric.ConvertToEnum<EMetric>() != EMetric.None ?
				$"{SecondMetric.ConvertToEnum<EMetric>().GetShortDescription()}=" +
				$"{secondMetricValue.ToString(numberFormat, cultureInfo)} " +
				$"FPS | " : string.Empty;

			string thirdMetricString =
				ThirdMetric.ConvertToEnum<EMetric>() != EMetric.None ?
				$"{ThirdMetric.ConvertToEnum<EMetric>().GetShortDescription()}=" +
				$"{thrirdMetricValue.ToString(numberFormat, cultureInfo)} " +
				$"FPS" : string.Empty;

			return $"Avg={average.ToString(numberFormat, cultureInfo)} " +
				$"FPS | " + secondMetricString + thirdMetricString;
		}
	}
}
