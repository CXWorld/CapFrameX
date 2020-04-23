﻿using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Extensions.NetStandard;
using CapFrameX.Statistics;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace CapFrameX.Overlay
{
	public class OverlayService : RTSSCSharpWrapper, IOverlayService
	{
		private readonly IStatisticProvider _statisticProvider;
		private readonly IOverlayEntryProvider _overlayEntryProvider;
		private readonly ISensorService _sensorService;
		private readonly IAppConfiguration _appConfiguration;
		private static ILogger<OverlayService> _logger;
		private readonly IRecordManager _recordManager;
		private IDisposable _disposableCaptureTimer;
		private IDisposable _disposableCountdown;

		private IList<string> _runHistory = new List<string>();
		private IList<ISessionRun> _captureDataHistory = new List<ISessionRun>();
		private IList<IList<double>> _frametimeHistory = new List<IList<double>>();
		private bool[] _runHistoryOutlierFlags;
		private int _numberOfRuns;
		private IList<IMetricAnalysis> _metricAnalysis = new List<IMetricAnalysis>();

		public ISubject<bool> IsOverlayActiveStream { get; }

		public string SecondMetric { get; set; }

		public string ThirdMetric { get; set; }

		public int RunHistoryCount => _runHistory.Count(run => run != "N/A");

		public OverlayService(IStatisticProvider statisticProvider,
							  ISensorService sensorService,
							  IOverlayEntryProvider overlayEntryProvider,
							  IAppConfiguration appConfiguration,
							  ILogger<OverlayService> logger, IRecordManager recordManager)
			: base(ExceptionAction)
		{
			_statisticProvider = statisticProvider;
			_sensorService = sensorService;
			_overlayEntryProvider = overlayEntryProvider;
			_appConfiguration = appConfiguration;
			_logger = logger;
			_recordManager = recordManager;
			_numberOfRuns = _appConfiguration.SelectedHistoryRuns;
			SecondMetric = _appConfiguration.SecondMetricOverlay;
			ThirdMetric = _appConfiguration.ThirdMetricOverlay;
			IsOverlayActiveStream = new BehaviorSubject<bool>(_appConfiguration.IsOverlayActive);
			_runHistoryOutlierFlags = Enumerable.Repeat(false, _numberOfRuns).ToArray();

			_logger.LogDebug("{componentName} Ready", this.GetType().Name);

			IsOverlayActiveStream.AsObservable()
				.Select(isActive =>
				{
					if (isActive)
					{
						ResetOSD();
						return sensorService.OnDictionaryUpdated
							.SelectMany(_ => _overlayEntryProvider.GetOverlayEntries());
					}
					else
					{
						ReleaseOSD();
						return Observable.Empty<IOverlayEntry[]>();
					}
				}).Switch()
				.Subscribe(entries =>
				{					
					SetOverlayEntries(entries); 
					CheckRTSSRunningAndRefresh();
				});

			_runHistory = Enumerable.Repeat("N/A", _numberOfRuns).ToList();
			SetRunHistory(_runHistory.ToArray());
			SetRunHistoryAggregation(string.Empty);
			SetRunHistoryOutlierFlags(_runHistoryOutlierFlags);
			SetIsCaptureTimerActive(false);
		}

		public void StartCountdown(int seconds)
		{
			IObservable<long> obs = Extensions.ObservableExtensions.CountDown(seconds);
			SetIsCaptureTimerActive(true);

			SetCaptureTimerValue(0);
			_disposableCountdown?.Dispose();
			_disposableCountdown = obs.Subscribe(t =>
			{
				SetCaptureTimerValue((int)t);

				if (t == 0)
					SetIsCaptureTimerActive(false);
			});
		}

		public void StartCaptureTimer()
		{
			_disposableCaptureTimer = GetCaptureTimer();
			SetIsCaptureTimerActive(true);
		}

		public void StopCaptureTimer()
		{
			SetIsCaptureTimerActive(false);
			SetCaptureTimerValue(0);
			_disposableCaptureTimer?.Dispose();
		}

		public void SetCaptureTimerValue(int t)
		{
			var captureTimer = _overlayEntryProvider.GetOverlayEntry("CaptureTimer");
			if (captureTimer != null)
			{
				captureTimer.Value = $"{t.ToString()} s";
				SetOverlayEntry(captureTimer);
			}
		}

		public void SetCaptureServiceStatus(string status)
		{
			var captureStatus = _overlayEntryProvider.GetOverlayEntry("CaptureServiceStatus");
			if (captureStatus != null)
			{
				captureStatus.Value = status;
				SetOverlayEntry(captureStatus);
			}
		}

		public void SetShowRunHistory(bool showHistory)
		{
			var history = _overlayEntryProvider.GetOverlayEntry("RunHistory");
			if (history != null)
			{
				history.ShowOnOverlay = showHistory;
				SetOverlayEntry(history);
			}
		}

		public void ResetHistory()
		{
			_runHistory = Enumerable.Repeat("N/A", _numberOfRuns).ToList();
			_runHistoryOutlierFlags = Enumerable.Repeat(false, _numberOfRuns).ToArray();
			_captureDataHistory.Clear();
			_frametimeHistory.Clear();
			_metricAnalysis.Clear();
			SetRunHistory(_runHistory.ToArray());
			SetRunHistoryAggregation(string.Empty);
			SetRunHistoryOutlierFlags(_runHistoryOutlierFlags);
		}

		public void AddRunToHistory(ISessionRun sessionRun, string process)
		{
			var frametimes = sessionRun.CaptureData.MsBetweenPresents;

			if (RunHistoryCount == _numberOfRuns)
			{
				if (!_runHistoryOutlierFlags.All(x => x == false)
					&& _appConfiguration.OutlierHandling == EOutlierHandling.Replace.ConvertToString())
				{
					var historyDefault = Enumerable.Repeat("N/A", _numberOfRuns).ToList();
					var validRuns = _runHistory.Where((run, i) => _runHistoryOutlierFlags[i] == false).ToList();

					for (int i = 0; i < validRuns.Count; i++)
					{
						historyDefault[i] = validRuns[i];
					}

					var validCaptureData = _captureDataHistory.Where((run, i) => _runHistoryOutlierFlags[i] == false);
					var validFrametimes = _frametimeHistory.Where((run, i) => _runHistoryOutlierFlags[i] == false);
					var validMetricAnalysis = _metricAnalysis.Where((run, i) => _runHistoryOutlierFlags[i] == false);

					_runHistory = historyDefault.ToList();
					_captureDataHistory = validCaptureData.ToList();
					_frametimeHistory = validFrametimes.ToList();
					_metricAnalysis = validMetricAnalysis.ToList();

					// local reset
					_runHistoryOutlierFlags = Enumerable.Repeat(false, _numberOfRuns).ToArray();
					SetRunHistory(_runHistory.ToArray());
					SetRunHistoryAggregation(string.Empty);
					SetRunHistoryOutlierFlags(_runHistoryOutlierFlags);
				}
				else
				{
					ResetHistory();
				}
			}

			if (RunHistoryCount < _numberOfRuns)
			{
				// metric history
				var currentAnalysis = _statisticProvider.GetMetricAnalysis(frametimes, SecondMetric, ThirdMetric);
				_metricAnalysis.Add(currentAnalysis);
				_runHistory[RunHistoryCount] = currentAnalysis.ResultString;
				SetRunHistory(_runHistory.ToArray());

				// capture data history
				_captureDataHistory.Add(sessionRun);

				// frametime history
				_frametimeHistory.Add(frametimes);

				if (_appConfiguration.UseAggregation
					&& RunHistoryCount == _numberOfRuns)
				{
					_runHistoryOutlierFlags = _statisticProvider
						.GetOutlierAnalysis(_metricAnalysis,
											_appConfiguration.RelatedMetricOverlay,
											_appConfiguration.OutlierPercentageOverlay);
					SetRunHistoryOutlierFlags(_runHistoryOutlierFlags);

					if ((_runHistoryOutlierFlags.All(x => x == false)
						&& _appConfiguration.OutlierHandling == EOutlierHandling.Replace.ConvertToString())
						|| _appConfiguration.OutlierHandling == EOutlierHandling.Ignore.ConvertToString())
					{
						SetRunHistoryAggregation(GetAggregation());

						// write aggregated file
						Task.Run(async () =>
						{
							await Task.Delay(1000);
							await _recordManager.SaveSessionRunsToFile(_captureDataHistory, process);
						});
					}
				}
			}
		}

		public void UpdateNumberOfRuns(int numberOfRuns)
		{
			_numberOfRuns = numberOfRuns;
			ResetHistory();
		}

		private void CheckRTSSRunningAndRefresh()
		{
			var processes = Process.GetProcessesByName("RTSS");
			if (!processes.Any())
			{
				try
				{
					Process proc = new Process();
					proc.StartInfo.FileName = Path.Combine(RTSSUtils.GetRTSSFullPath());
					proc.StartInfo.UseShellExecute = false;
					proc.StartInfo.Verb = "runas";
					proc.Start();
				}
				catch (Exception ex) { _logger.LogError(ex, "Error while starting RTSS process"); }
			}
			Refresh();
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

			return _statisticProvider
				.GetMetricAnalysis(concatedFrametimes, SecondMetric, ThirdMetric)
				.ResultString;
		}

		private static void ExceptionAction(Exception ex)
		{
			_logger.LogError(ex, "Exception thrown in RTSSCSharpWrapper");
		}
	}
}
