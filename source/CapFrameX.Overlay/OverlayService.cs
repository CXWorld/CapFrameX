using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Latency;
using CapFrameX.Contracts.Logging;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.RTSS;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Extensions.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;
using LibreHardwareMonitor.Hardware;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

namespace CapFrameX.Overlay
{
    public class OverlayService : IOverlayService
    {
        private readonly object _dictLock = new object();

        private readonly IStatisticProvider _statisticProvider;
        private readonly IOverlayEntryProvider _overlayEntryProvider;
        private readonly IAppConfiguration _appConfiguration;
        private static ILogger<OverlayService> _logger;
        private readonly IRecordManager _recordManager;
        private readonly ISensorService _sensorService;
        private readonly IRTSSService _rTSSService;
        private readonly IOverlayEntryCore _overlayEntryCore;
        private readonly ILogEntryManager _logEntryManager;
        private readonly EventLoopScheduler _overlayRefreshScheduler;

        private IDisposable _disposableCaptureTimer;
        private IDisposable _disposableDelayCountdown;
        private IDisposable _disposableCountdown;
        private IDisposable _overlayActiveStreamDisposable;
        private IDisposable _sensorRefreshDisposable;

        private IList<string> _runHistory = new List<string>();
        private volatile string[] _runHistorySnapshot = Array.Empty<string>();
        private IList<ISessionRun> _captureDataHistory = new List<ISessionRun>();
        private IList<IList<double>> _frametimeHistory = new List<IList<double>>();
        private IList<IList<double>> _displaytimeHistory = new List<IList<double>>();
        private bool[] _runHistoryOutlierFlags;
        private volatile bool[] _runHistoryOutlierFlagsSnapshot = Array.Empty<bool>();
        private volatile string _runHistoryAggregation = string.Empty;
        private int _numberOfRuns;
        private IList<IMetricAnalysis> _metricAnalysis = new List<IMetricAnalysis>();
        private ISubject<IOverlayEntry[]> _onDictionaryUpdated = new Subject<IOverlayEntry[]>();
        private readonly ISubject<Unit> _refreshRequested = Subject.Synchronize(new Subject<Unit>());
        private volatile bool _isServiceAlive = true;

        public bool IsOverlayActive => _appConfiguration.IsOverlayActive;

        public ISubject<bool> IsOverlayActiveStream { get; }

        public string SecondMetric { get; set; }

        public string ThirdMetric { get; set; }

        public int RunHistoryCount => _runHistory.Count(run => run != "N/A");

        public IReadOnlyList<string> RunHistory => _runHistorySnapshot;

        public IReadOnlyList<bool> RunHistoryOutlierFlags => _runHistoryOutlierFlagsSnapshot;

        public string RunHistoryAggregation => _runHistoryAggregation;

        public IObservable<IOverlayEntry[]> OnDictionaryUpdated => _onDictionaryUpdated;

        public IOverlayEntry[] CurrentOverlayEntries { get; private set; } = Array.Empty<IOverlayEntry>();

        public Action<IOverlayEntry[]> OSDUpdateNotifier { get; set; } = (_) => { };


        public OverlayService(IStatisticProvider statisticProvider,
            ISensorService sensorService,
            IOverlayEntryProvider overlayEntryProvider,
            IAppConfiguration appConfiguration,
            ILogger<OverlayService> logger,
            IRecordManager recordManager,
            IRTSSService rTSSService,
            IOverlayEntryCore overlayEntryCore,
            ILogEntryManager logEntryManager)
        {
            _statisticProvider = statisticProvider;
            _overlayEntryProvider = overlayEntryProvider;
            _appConfiguration = appConfiguration;
            _logger = logger;
            _recordManager = recordManager;
            _sensorService = sensorService;
            _logEntryManager = logEntryManager;
            _rTSSService = rTSSService;
            _overlayEntryCore = overlayEntryCore;
            _overlayRefreshScheduler = new EventLoopScheduler(start =>
            {
                var thread = new Thread(start)
                {
                    IsBackground = true,
                    Name = "CapFrameX overlay refresh",
                    Priority = ThreadPriority.BelowNormal
                };
                return thread;
            });

            _numberOfRuns = _appConfiguration.SelectedHistoryRuns;
            SecondMetric = _appConfiguration.RunHistorySecondMetric;
            ThirdMetric = _appConfiguration.RunHistoryThirdMetric;

            bool isRTSSInstalled = _rTSSService.IsRTSSInstalled();
            if (ShouldDefaultToHookFreeOverlay(isRTSSInstalled,
                _appConfiguration.EnableHookFreeOverlay,
                _appConfiguration.EnableHookOverlay))
            {
                // With neither CapFrameX renderer selected, the two false flags mean "RTSS".
                // Do not leave a fresh or migrated configuration on a renderer that cannot exist:
                // persist hook-free as the selected mode so the UI and every downstream consumer
                // observe the same usable default. Explicit in-game/hook-free choices are preserved.
                _appConfiguration.EnableHookFreeOverlay = true;
                _logger.LogInformation(
                    "RTSS is not installed. Selecting the CapFrameX hook-free overlay as the default renderer.");
            }

            bool configuredOverlayActive = _appConfiguration.IsOverlayActive;
            bool initialOverlayActive = GetInitialOverlayActiveState(
                configuredOverlayActive,
                isRTSSInstalled,
                _appConfiguration.EnableHookFreeOverlay,
                _appConfiguration.EnableHookOverlay);

            if (configuredOverlayActive && !initialOverlayActive)
            {
                // A fresh configuration defaults the overlay to on and the renderer to RTSS. Do
                // not retain an impossible active state when RTSS is absent: all consumers of the
                // BehaviorSubject (including StateViewModel) must observe the same persisted state.
                _appConfiguration.IsOverlayActive = false;
                _logger.LogWarning(
                    "Overlay was configured active, but the selected RTSS renderer is unavailable. Disabling the overlay.");
            }

            IsOverlayActiveStream = new BehaviorSubject<bool>(initialOverlayActive);
            _runHistoryOutlierFlags = Enumerable.Repeat(false, _numberOfRuns).ToArray();

            _logger.LogDebug("{componentName} Ready", this.GetType().Name);

            var overlayOnAPIOnly = _appConfiguration.HideOverlay;
            _appConfiguration.OnValueChanged
                .Where(x => x.key == nameof(IAppConfiguration.HideOverlay))
                .Select(x => (bool)x.value)
                .Subscribe(hideOSD =>
                {
                    overlayOnAPIOnly = hideOSD;
                    if (hideOSD)
                        _rTSSService.ReleaseOSD();
                });

            // Overlay-renderer switch (RTSS / hook-free / in-game hook). React to BOTH flags and
            // re-evaluate the COMBINED state — using the single changed value was wrong (turning one
            // off while the other was on, or switching back to RTSS, was mishandled).
            _appConfiguration.OnValueChanged
                .Where(x => x.key == nameof(IAppConfiguration.EnableHookFreeOverlay)
                         || x.key == nameof(IAppConfiguration.EnableHookOverlay))
                .Select(_ => _appConfiguration.EnableHookFreeOverlay || _appConfiguration.EnableHookOverlay)
                .DistinctUntilChanged()
                .Subscribe(useHook =>
                {
                    _logger.LogInformation("Overlay renderer switch: hookFree={hf}, hook={h}, overlayActive={a} -> {mode}",
                        _appConfiguration.EnableHookFreeOverlay, _appConfiguration.EnableHookOverlay,
                        _appConfiguration.IsOverlayActive, useHook ? "hook (clear RTSS)" : "RTSS");
                    if (useHook)
                    {
                        // Keep the RTSS slot owned while its renderer may still be reading it. Clearing
                        // uses RTSS' dwBusy synchronization; ReleaseOSD zeroes the whole shared-memory
                        // entry and is reserved for a real overlay shutdown.
                        _rTSSService.ClearOSD();
                    }
                    else if (_appConfiguration.IsOverlayActive && _isServiceAlive)
                    {
                        // Switched back to the RTSS renderer while the overlay is active. Re-drive the
                        // overlay-active pipeline (as an Alt+O toggle would) so it re-runs the RTSS init
                        // (launch + OnOSDOn) AND rebuilds the entry feed via .Switch(). Nothing else
                        // re-initializes RTSS after a hook path released it — that's why it looked
                        // "stuck off"; a one-shot OnOSDOn wasn't enough because the feed wasn't re-driven.
                        IsOverlayActiveStream.OnNext(true);
                    }
                });

            Task.Run(async () => await InitializeOverlayEntryDict())
                .ContinueWith(t =>
               {
                   int rtssFeedLogState = -1; // logs only when the RTSS-feed decision flips (avoids per-tick spam)
                   _overlayActiveStreamDisposable = IsOverlayActiveStream
                       .Where(_ => _isServiceAlive)
                       .Select(isActive =>
                       {
                           if (isActive)
                           {
                               // Serialize profile changes and regular ticks on the refresh thread.
                               // FromAsync defers each read until the previous one has completed.
                               var entryUpdates = _refreshRequested
                                   .StartWith(Unit.Default)
                                   .ObserveOn(_overlayRefreshScheduler)
                                   .Select(_ => Observable.FromAsync(() => _overlayEntryProvider.GetOverlayEntries()))
                                   .Concat();

                               if (!_appConfiguration.EnableHookFreeOverlay && !_appConfiguration.EnableHookOverlay)
                               {
                                   // Deferred instead of awaited inline: this selector runs on the
                                   // thread that pushed the value — the WPF dispatcher for the
                                   // overlay hotkey and for the checkbox — while the RTSS check
                                   // enumerates processes and may start RTSS. FromAsync preserves
                                   // the ordering (entries only flow once RTSS is up) without
                                   // blocking the caller, which .Wait() did.
                                   return Observable
                                       .FromAsync(cancellationToken => InitializeRTSSAsync(cancellationToken))
                                       .SelectMany(_ => entryUpdates);
                               }

                               return entryUpdates;
                           }
                           else
                           {
                               _rTSSService.ReleaseOSD();
                               return Observable.Empty<IOverlayEntry[]>();
                           }
                       })
                       .Switch()
                       .Subscribe(async entries =>
                       {
                           CurrentOverlayEntries = entries;
                           OSDUpdateNotifier(entries);
                           // Both CapFrameX renderers read CurrentOverlayEntries from this event.
                           // Publishing the raw tick first could make them render the old profile.
                           _onDictionaryUpdated.OnNext(entries);

                           bool feedRtss = !overlayOnAPIOnly && !_appConfiguration.EnableHookFreeOverlay && !_appConfiguration.EnableHookOverlay;
                           int feedState = feedRtss ? 1 : 0;
                           if (feedState != rtssFeedLogState)
                           {
                               rtssFeedLogState = feedState;
                               _logger.LogInformation("RTSS feed {state} (apiOnly={api}, hookFree={hf}, hook={h})",
                                   feedRtss ? "ON" : "OFF", overlayOnAPIOnly,
                                   _appConfiguration.EnableHookFreeOverlay, _appConfiguration.EnableHookOverlay);
                           }
                           if (feedRtss)
                           {
                               _rTSSService.SetOverlayEntries(entries.Where(entry => entry.IsEntryEnabled).ToArray());
                               await _rTSSService.CheckRTSSRunningAndRefresh();
                           }
                       });
               });

            Task.Run(async () => await _overlayEntryCore.OverlayEntryCoreCompletionSource.Task)
                .ContinueWith(t =>
                {
                    var refreshTicks = _sensorService.OsdUpdateStream
                       .Select(timespan => Observable.Concat(
                            Observable.Return(-1L, _overlayRefreshScheduler),
                            Observable.Interval(timespan, _overlayRefreshScheduler)))
                       .Switch();

                    _sensorRefreshDisposable = RefreshFromLatest(
                        _sensorService.SensorSnapshotStream,
                        refreshTicks,
                        (DateTime.UtcNow, new Dictionary<ISensorEntry, float>()))
                       .Where(_ => _isServiceAlive)
                       .Where((_, idx) => idx == 0 || IsOverlayActive)
                       .Subscribe(sensorData =>
                       {
                           if (sensorData.Item2.Any())
                               UpdateOverlayEntries(sensorData.Item2);

                           RequestRefresh();
                       });
                });

            _runHistory = Enumerable.Repeat("N/A", _numberOfRuns).ToList();
            PublishRunHistory();
            PublishRunHistoryAggregation(string.Empty);
            PublishRunHistoryOutlierFlags();
            _rTSSService.SetIsCaptureTimerActive(false);
        }

        /// <summary>
        /// Brings RTSS up for an overlay that has just been activated.
        ///
        /// Failures are logged rather than propagated: an OnError here would travel through
        /// Switch() to the subscriber and tear the overlay feed down for the rest of the session.
        /// </summary>
        private async Task InitializeRTSSAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _rTSSService.CheckRTSSRunning();

                // The overlay can be switched off again while RTSS is starting. Switch() cancels
                // this token when it drops the subscription, and turning the OSD on afterwards
                // would leave it on against the user's last input.
                if (cancellationToken.IsCancellationRequested)
                    return;

                _rTSSService.OnOSDOn();
                _rTSSService.ClearOSD();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "RTSS initialization for the activated overlay failed.");
            }
        }

        public void StartCountdown(double seconds)
        {
            IObservable<long> obs = Extensions.ObservableExtensions.CountDown(seconds);
            _rTSSService.SetIsCaptureTimerActive(true);

            SetCaptureTimerValue(0);
            _disposableCountdown?.Dispose();
            _disposableCountdown = obs.Subscribe(t =>
            {
                SetCaptureTimerValue((int)t);

                if (IsOverlayActive)
                    _rTSSService.Refresh();

                if (t == 0)
                    _rTSSService.SetIsCaptureTimerActive(false);

            });
        }

        internal static bool GetInitialOverlayActiveState(bool configuredOverlayActive,
            bool isRTSSInstalled, bool enableHookFreeOverlay, bool enableHookOverlay)
        {
            return configuredOverlayActive &&
                (isRTSSInstalled || enableHookFreeOverlay || enableHookOverlay);
        }

        internal static bool ShouldDefaultToHookFreeOverlay(bool isRTSSInstalled,
            bool enableHookFreeOverlay, bool enableHookOverlay)
        {
            return !isRTSSInstalled && !enableHookFreeOverlay && !enableHookOverlay;
        }

        /// <summary>
        /// Drives consumers from their own refresh clock and reads only the latest completed
        /// producer value. A slow producer therefore causes a repeated stale value, never a
        /// delayed refresh or a queue of catch-up work.
        /// </summary>
        internal static IObservable<T> RefreshFromLatest<T>(
            IObservable<T> values,
            IObservable<long> refreshTicks,
            T initialValue)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            if (refreshTicks == null)
                throw new ArgumentNullException(nameof(refreshTicks));

            return refreshTicks.WithLatestFrom(
                values.StartWith(initialValue),
                (_, latestValue) => latestValue);
        }

        public void SetDelayCountdown(double seconds)
        {
            IObservable<long> obs = Extensions.ObservableExtensions.CountDown(seconds);
            _rTSSService.SetIsCaptureTimerActive(true);

            SetCaptureTimerValue(-(int)seconds);
            _disposableDelayCountdown?.Dispose();
            _disposableDelayCountdown = obs.Subscribe(t =>
            {
                if (t > 0)
                {
                    SetCaptureTimerValue((int)-t);

                    if (IsOverlayActive)
                        _rTSSService.Refresh();
                }
            });
        }

        public void CancelDelayCountdown()
        {
            _disposableDelayCountdown?.Dispose();
            _rTSSService.SetIsCaptureTimerActive(false);

            if (IsOverlayActive)
                _rTSSService.Refresh();
        }

        public void StartCaptureTimer()
        {
            _disposableCaptureTimer = GetCaptureTimer();
            _rTSSService.SetIsCaptureTimerActive(true);
        }

        public void StopCaptureTimer()
        {
            _disposableCaptureTimer?.Dispose();
            _disposableCountdown?.Dispose();
            _rTSSService.SetIsCaptureTimerActive(false);
            SetCaptureTimerValue(0);
        }

        public void SetCaptureTimerValue(int t)
        {
            var captureTimer = _overlayEntryProvider.GetOverlayEntry("CaptureTimer");
            if (captureTimer != null)
            {
                captureTimer.Value = $"{t} s";
                _rTSSService.SetOverlayEntry(captureTimer);
            }
        }

        public void SetCaptureServiceStatus(string status)
        {
            if (IsOverlayActive)
            {
                var captureStatus = _overlayEntryProvider.GetOverlayEntry("CaptureServiceStatus");
                if (captureStatus != null)
                {
                    captureStatus.Value = status;
                    _rTSSService.SetOverlayEntry(captureStatus);
                }
            }
        }

        public void ResetHistory()
        {
            _runHistory = Enumerable.Repeat("N/A", _numberOfRuns).ToList();
            _runHistoryOutlierFlags = Enumerable.Repeat(false, _numberOfRuns).ToArray();
            _captureDataHistory.Clear();
            _frametimeHistory.Clear();
            _displaytimeHistory.Clear();
            _metricAnalysis.Clear();
            PublishRunHistory();
            PublishRunHistoryAggregation(string.Empty);
            PublishRunHistoryOutlierFlags();
        }

        public void AddRunToHistory(ISessionRun sessionRun, string process, string recordDirectory)
        {
            var frametimes = sessionRun.CaptureData.MsBetweenPresents;
            var displaytimes = sessionRun.CaptureData.MsBetweenDisplayChange;

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
                    var validDisplaytimes = _displaytimeHistory.Where((run, i) => _runHistoryOutlierFlags[i] == false);
                    var validMetricAnalysis = _metricAnalysis.Where((run, i) => _runHistoryOutlierFlags[i] == false);

                    _runHistory = historyDefault.ToList();
                    _captureDataHistory = validCaptureData.ToList();
                    _frametimeHistory = validFrametimes.ToList();
                    _displaytimeHistory = validDisplaytimes.ToList();
                    _metricAnalysis = validMetricAnalysis.ToList();

                    // local reset
                    _runHistoryOutlierFlags = Enumerable.Repeat(false, _numberOfRuns).ToArray();
                    PublishRunHistory();
                    PublishRunHistoryAggregation(string.Empty);
                    PublishRunHistoryOutlierFlags();
                }
                else
                {
                    ResetHistory();
                }
            }

            if (RunHistoryCount < _numberOfRuns)
            {
                // metric history
                var currentAnalysis = _statisticProvider.GetMetricAnalysis(frametimes, displaytimes,
                    _appConfiguration.UseDisplayChangeMetrics, SecondMetric, ThirdMetric);

                _metricAnalysis.Add(currentAnalysis);
                _runHistory[RunHistoryCount] = currentAnalysis.ResultString;
                PublishRunHistory();

                // capture data history
                _captureDataHistory.Add(sessionRun);

                // frametime history
                _frametimeHistory.Add(frametimes);

                // displaytime history
                _displaytimeHistory.Add(displaytimes);

                if (_appConfiguration.UseAggregation)
                {
                    _logEntryManager.AddLogEntry($"Aggregation active. Adding captured data to history ({RunHistoryCount} of {_numberOfRuns})", ELogMessageType.BasicInfo, false);

                    if (RunHistoryCount == _numberOfRuns)
                    {
                        _runHistoryOutlierFlags = _statisticProvider
                            .GetOutlierAnalysis(_metricAnalysis,
                                _appConfiguration.RelatedMetricOverlay,
                                _appConfiguration.OutlierPercentageOverlay);
                        PublishRunHistoryOutlierFlags();

                        if ((_runHistoryOutlierFlags.All(x => x == false)
                            && _appConfiguration.OutlierHandling == EOutlierHandling.Replace.ConvertToString())
                            || _appConfiguration.OutlierHandling == EOutlierHandling.Ignore.ConvertToString())
                        {
                            PublishRunHistoryAggregation(GetAggregation());

                            // write aggregated file
                            Task.Run(async () =>
                            {
                                await Task.Delay(1000);
                                bool checkSave = await _recordManager.SaveSessionRunsToFile(_captureDataHistory, process, string.Empty, recordDirectory, null);

                                if (!checkSave)
                                    _logEntryManager.AddLogEntry("Error while saving aggregated file.", ELogMessageType.Error, false);
                                else
                                    _logEntryManager.AddLogEntry("Aggregated file successfully written into directory.", ELogMessageType.BasicInfo, false);
                            });
                        }
                        else
                        {
                            _logEntryManager.AddLogEntry($"Aggregation outliers detected ({_runHistoryOutlierFlags.Where(x => x == true).Count()}). Additional runs required.", ELogMessageType.BasicInfo, false);
                        }
                    }
                }
            }
        }

        public void UpdateNumberOfRuns(int numberOfRuns)
        {
            _numberOfRuns = numberOfRuns;
            ResetHistory();
        }

        private void PublishRunHistory()
        {
            var snapshot = _runHistory.ToArray();
            _runHistorySnapshot = snapshot;
            _rTSSService.SetRunHistory(snapshot);
        }

        private void PublishRunHistoryOutlierFlags()
        {
            var snapshot = _runHistoryOutlierFlags?.ToArray() ?? Array.Empty<bool>();
            _runHistoryOutlierFlagsSnapshot = snapshot;
            _rTSSService.SetRunHistoryOutlierFlags(snapshot);
        }

        private void PublishRunHistoryAggregation(string aggregation)
        {
            _runHistoryAggregation = aggregation ?? string.Empty;
            _rTSSService.SetRunHistoryAggregation(_runHistoryAggregation);
        }

        public IOverlayEntry GetSensorOverlayEntry(string identifier)
        {
            lock (_dictLock)
            {
                _overlayEntryCore.OverlayEntryDict.TryGetValue(identifier, out IOverlayEntry entry);
                return entry;
            }
        }

        private void UpdateOverlayEntries(Dictionary<ISensorEntry, float> sensorData)
        {
            foreach (var sensorPair in sensorData)
            {
                var sensorIdentifier = sensorPair.Key.Identifier.ToString();
                var sensorValue = sensorPair.Value;

                lock (_dictLock)
                {
                    if (_overlayEntryCore.OverlayEntryDict.TryGetValue(sensorIdentifier, out IOverlayEntry entry))
                    {
                        entry.Value = sensorValue;
                    }
                }
            }
        }

        public void ShutdownOverlayService()
        {
            _isServiceAlive = false;
            _sensorRefreshDisposable?.Dispose();
            _overlayActiveStreamDisposable?.Dispose();
            _overlayRefreshScheduler?.Dispose();
        }

        public void RequestRefresh()
        {
            if (_isServiceAlive)
                _refreshRequested.OnNext(Unit.Default);
        }

        private async Task InitializeOverlayEntryDict()
        {
            _overlayEntryCore.OverlayEntryDict.Clear();

            try
            {
                var sensors = await _sensorService.GetSensorEntries();
                if (sensors != null)
                {
                    foreach (var sensor in sensors)
                    {
                        // FLM already has a purpose-built live overlay metric. Keep its virtual
                        // sensor for logging without presenting a duplicate overlay entry.
                        if (sensor.Identifier == AmdFlmSensorMetadata.Identifier)
                            continue;

                        var dictEntry = CreateOverlayEntry(sensor);
                        var id = sensor.Identifier.ToString();
                        if (!_overlayEntryCore.OverlayEntryDict.ContainsKey(id))
                            _overlayEntryCore.OverlayEntryDict.TryAdd(id, dictEntry);
                    }
                }

                _overlayEntryCore.OverlayEntryCoreCompletionSource.SetResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting sensors.");
            }
        }

        private IOverlayEntry CreateOverlayEntry(ISensorEntry sensor)
        {
            return new OverlayEntryWrapper(sensor.Identifier.ToString())
            {
                StableIdentifier = SensorIdentifierHelper.BuildStableIdentifier(sensor),
                SortKey = sensor.SortKey,
                Description = GetDescription(sensor),
                OverlayEntryType = MapType(sensor.HardwareType),
                GroupName = GetGroupName(sensor),
                ShowGraph = false,
                ShowGraphIsEnabled = false,
                ShowOnOverlayIsEnabled = true,
                ShowOnOverlay = sensor.IsPresentationDefault,
                Value = 0,
                ValueUnitFormat = GetValueUnitString(sensor.SensorType),
                ValueAlignmentAndDigits = GetValueAlignmentAndDigitsString(sensor.SensorType)
            };
        }

        private string GetValueAlignmentAndDigitsString(string sensorTypeString)
        {
            string formatString = "{0}";
            Enum.TryParse(sensorTypeString, out SensorType sensorType);
            switch (sensorType)
            {
                case SensorType.Current:
                    formatString = "{0,5:F1}";
                    break;
                case SensorType.Voltage:
                    formatString = "{0,5:F2}";
                    break;
                case SensorType.Clock:
                    formatString = "{0,5:F0}";
                    break;
                case SensorType.Temperature:
                    formatString = "{0,5:F0}";
                    break;
                case SensorType.Load:
                    formatString = "{0,5:F0}";
                    break;
                case SensorType.Fan:
                    formatString = "{0,5:F0}";
                    break;
                case SensorType.Flow:
                    formatString = "{0,5:F0}";
                    break;
                case SensorType.Control:
                    formatString = "{0,5:F0}";
                    break;
                case SensorType.Level:
                    formatString = "{0,5:F0}";
                    break;
                case SensorType.Factor:
                    formatString = "{0,5:F0}";
                    break;
                case SensorType.Power:
                    formatString = "{0,5:F1}";
                    break;
                case SensorType.Data:
                    formatString = "{0,5:F2}";
                    break;
                case SensorType.SmallData:
                    formatString = "{0,5:F0}";
                    break;
                case SensorType.Throughput:
                    formatString = "{0,5:F1}";
                    break;
                case SensorType.Frequency:
                    formatString = "{0,5:F0}";
                    break;
                case SensorType.DataRate:
                    formatString = "{0,5:F0}";
                    break;
                case SensorType.Timing:
                    formatString = "{0,5:F1}";
                    break;
                case SensorType.Latency:
                    formatString = "{0,5:F1}";
                    break;
            }

            return formatString;
        }

        private string GetValueUnitString(string sensorTypeString)
        {
            string formatString = "{0}";
            Enum.TryParse(sensorTypeString, out SensorType sensorType);
            switch (sensorType)
            {
                case SensorType.Current:
                    formatString = "A  ";
                    break;  
                case SensorType.Voltage:
                    formatString = "V  ";
                    break;
                case SensorType.Clock:
                    formatString = "MHz";
                    break;
                case SensorType.Temperature:
                    formatString = "°C ";
                    break;
                case SensorType.Load:
                    formatString = "%  ";
                    break;
                case SensorType.Fan:
                    formatString = "RPM";
                    break;
                case SensorType.Flow:
                    formatString = "L/h";
                    break;
                case SensorType.Control:
                    formatString = "%  ";
                    break;
                case SensorType.Level:
                    formatString = "%  ";
                    break;
                case SensorType.Factor:
                    formatString = "   ";
                    break;
                case SensorType.Power:
                    formatString = "W  ";
                    break;
                case SensorType.Data:
                    formatString = "GB ";
                    break;
                case SensorType.SmallData:
                    formatString = "MB ";
                    break;
                case SensorType.Throughput:
                    formatString = "GB/s";
                    break;
                case SensorType.Frequency:
                    formatString = "Hz ";
                    break;
                case SensorType.DataRate:
                    formatString = "MT/s";
                    break;
                case SensorType.Timing:
                    formatString = "ns ";
                    break;
                case SensorType.Latency:
                    formatString = "ms ";
                    break;
            }

            return formatString;
        }

        private string GetGroupName(ISensorEntry sensor)
        {
            var name = sensor.Name;
            if (name.Contains("CPU Core #"))
            {
                name = name.Replace("Core #", "").Trim();
            }
            else if (name.Contains("CPU Max Clock"))
            {
                name = name.Replace("CPU Max Clock", "CPU Max");
            }
            else if (name.Contains("CPU Max Core Temp"))
            {
                name = name.Replace("Max Core Temp", "Max");
            }
            else if (name.Contains("GPU Core"))
            {
                name = name.Replace(" Core", "");
            }
            else if (name.Contains("Memory Controller"))
            {
                name = name.Replace("Memory Controller", "MemCtrl");
            }
            else if (name.Contains("Memory"))
            {
                name = name.Replace("Memory", "Mem");

                if (name.Contains("Dedicated"))
                    name = name.Replace("GPU Mem Dedicated", "GPU Mem");

                else if (name.Contains("Shared"))
                    name = name.Replace("GPU Mem Shared", "GPU Mem");
            }
            else if (name.Contains("Power Limit"))
            {
                name = name.Replace("Power Limit", "PL");
            }
            else if (name.Contains("Thermal Limit"))
            {
                name = name.Replace("Thermal Limit", "TL");
            }
            else if (name.Contains("Voltage Limit"))
            {
                name = name.Replace("Voltage Limit", "VL");
            }

            if (name.Contains("D3D"))
            {
                if (name.Contains("D3D Dedicated"))
                    name = name.Replace("D3D Dedicated", "Dedicated");

                if (name.Contains("D3D Shared"))
                    name = name.Replace("D3D Shared", "Shared");
            }

            if (name.Contains(" - Thread #1"))
            {
                name = name.Replace(" - Thread #1", "").Trim();
            }

            if (name.Contains(" - Thread #2"))
            {
                name = name.Replace(" - Thread #2", "").Trim();
            }

            if (name.Contains("Thread #1"))
            {
                name = name.Replace("Thread #1", "").Trim();
            }

            if (name.Contains("Thread #2"))
            {
                name = name.Replace("Thread #2", "").Trim();
            }

            if (name.Contains("Monitor Refresh Rate"))
            {
                name = "MRR";
            }

            if (name.Contains("GPU Mem Junction"))
            {
                name = "VRAM Hot Spot";
            }

            return name;
        }

        private string GetDescription(ISensorEntry sensor)
        {
            string description = string.Empty;
            Enum.TryParse(sensor.SensorType, out SensorType sensorType);
            switch (sensorType)
            {
                case SensorType.Current:
                    description = $"{sensor.Name} (A)";
                    break;
                case SensorType.Voltage:
                    description = $"{sensor.Name} (V)";
                    break;
                case SensorType.Clock:
                    description = $"{sensor.Name} (MHz)";
                    break;
                case SensorType.Temperature:
                    description = $"{sensor.Name} (°C)";
                    break;
                case SensorType.Load:
                    description = $"{sensor.Name} (%)";
                    break;
                case SensorType.Fan:
                    description = $"{sensor.Name} (RPM)";
                    break;
                case SensorType.Flow:
                    description = $"{sensor.Name} (L/h)";
                    break;
                case SensorType.Control:
                    description = $"{sensor.Name} (%)";
                    break;
                case SensorType.Level:
                    description = $"{sensor.Name} (%)";
                    break;
                case SensorType.Factor:
                    description = sensor.Name;
                    break;
                case SensorType.Power:
                    description = $"{sensor.Name} (W)";
                    break;
                case SensorType.Data:
                    description = $"{sensor.Name} (GB)";
                    break;
                case SensorType.SmallData:
                    description = $"{sensor.Name} (MB)";
                    break;
                case SensorType.Throughput:
                    description = $"{sensor.Name} (GB/s)";
                    break;
                case SensorType.Frequency:
                    description = $"{sensor.Name} (Hz)";
                    break;
                case SensorType.DataRate:
                    description = $"{sensor.Name} (MT/s)";
                    break;
                case SensorType.Timing:
                    description = $"{sensor.Name} (ns)";
                    break;
                case SensorType.Latency:
                    description = $"{sensor.Name} (ms)";
                    break;
            }

            return description;
        }

        private EOverlayEntryType MapType(string hardwareTypeString)
        {
            EOverlayEntryType type = EOverlayEntryType.Undefined;
            Enum.TryParse(hardwareTypeString, out HardwareType hardwareType);
            switch (hardwareType)
            {
                case HardwareType.Motherboard:
                    type = EOverlayEntryType.Mainboard;
                    break;
                case HardwareType.SuperIO:
                    type = EOverlayEntryType.Undefined;
                    break;
                case HardwareType.Cpu:
                    type = EOverlayEntryType.CPU;
                    break;
                case HardwareType.Memory:
                    type = EOverlayEntryType.RAM;
                    break;
                case HardwareType.GpuNvidia:
                    type = EOverlayEntryType.GPU;
                    break;
                case HardwareType.GpuAmd:
                    type = EOverlayEntryType.GPU;
                    break;
                case HardwareType.GpuIntel:
                    type = EOverlayEntryType.GPU;
                    break;
                case HardwareType.Storage:
                    type = EOverlayEntryType.HDD;
                    break;
            }

            return type;
        }

        private IDisposable GetCaptureTimer()
        {
            return Observable
                .Timer(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1))
                .Subscribe(t =>
                {
                    SetCaptureTimerValue((int)t);

                    if (IsOverlayActive)
                        _rTSSService.Refresh();
                });
        }

        private string GetAggregation()
        {
            var concatedFrametimes = new List<double>(_frametimeHistory.Sum(set => set.Count));
            var concatedDisplaytimes = new List<double>(_displaytimeHistory.Sum(set => set.Count));

            foreach (var frametimeSet in _frametimeHistory)
            {
                concatedFrametimes.AddRange(frametimeSet);
            }

            foreach (var displaytimeSet in _displaytimeHistory)
            {
                concatedDisplaytimes.AddRange(displaytimeSet);
            }

            return _statisticProvider.GetMetricAnalysis(concatedFrametimes, concatedDisplaytimes,
                _appConfiguration.UseDisplayChangeMetrics, SecondMetric, ThirdMetric).ResultString;
        }
    }
}
