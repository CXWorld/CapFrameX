using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Logging;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.RTSS;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Extensions.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;
using Microsoft.Extensions.Logging;
using OpenHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
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

        private IDisposable _disposableCaptureTimer;
        private IDisposable _disposableDelayCountdown;
        private IDisposable _disposableCountdown;
        private IDisposable _overlayActiveStreamDisposable;

        private IList<string> _runHistory = new List<string>();
        private IList<ISessionRun> _captureDataHistory = new List<ISessionRun>();
        private IList<IList<double>> _frametimeHistory = new List<IList<double>>();
        private bool[] _runHistoryOutlierFlags;
        private int _numberOfRuns;
        private IList<IMetricAnalysis> _metricAnalysis = new List<IMetricAnalysis>();
        private ISubject<IOverlayEntry[]> _onDictionaryUpdated = new Subject<IOverlayEntry[]>();
		private bool _isServiceAlive = true;

		public bool IsOverlayActive => _appConfiguration.IsOverlayActive;

        public ISubject<bool> IsOverlayActiveStream { get; }

        public string SecondMetric { get; set; }

        public string ThirdMetric { get; set; }

        public int RunHistoryCount => _runHistory.Count(run => run != "N/A");

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
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            _statisticProvider = statisticProvider;
            _overlayEntryProvider = overlayEntryProvider;
            _appConfiguration = appConfiguration;
            _logger = logger;
            _recordManager = recordManager;
            _sensorService = sensorService;
            _logEntryManager = logEntryManager;
            _rTSSService = rTSSService;
            _overlayEntryCore = overlayEntryCore;

            _numberOfRuns = _appConfiguration.SelectedHistoryRuns;
            SecondMetric = _appConfiguration.RunHistorySecondMetric;
            ThirdMetric = _appConfiguration.RunHistoryThirdMetric;
            IsOverlayActiveStream = new BehaviorSubject<bool>(_appConfiguration.IsOverlayActive);
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

            Task.Run(async () => await InitializeOverlayEntryDict())
                .ContinueWith(t =>
               {
                   _overlayActiveStreamDisposable = IsOverlayActiveStream
                       .Where(_ => _isServiceAlive)
                       .Select(isActive =>
                       {
                           if (isActive)
                           {
                               _rTSSService.CheckRTSSRunning().Wait();
                               _rTSSService.OnOSDOn();
                               _rTSSService.ClearOSD();
                               return _onDictionaryUpdated.
                                   SelectMany(_ => _overlayEntryProvider.GetOverlayEntries());
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

                           if(!overlayOnAPIOnly)
                           { 
                                _rTSSService.SetOverlayEntries(entries);
                                await _rTSSService.CheckRTSSRunningAndRefresh();
                           }
                       });
               });

            Task.Run(async () => await _overlayEntryCore.OverlayEntryCoreCompletionSource.Task)
                .ContinueWith(t =>
                {
                    _sensorService.SensorSnapshotStream
                       .Sample(_sensorService.OsdUpdateStream.Select(timespan => Observable.Concat(Observable.Return(-1L), Observable.Interval(timespan))).Switch())
                       .Where(_ => _isServiceAlive)
                       .Where((_, idx) => idx == 0 || IsOverlayActive)
                       .Subscribe(sensorData =>
                       {
                           if (sensorData.Item2.Any())
                               UpdateOverlayEntries(sensorData.Item2);

                           if (_overlayEntryCore.OverlayEntryDict.Values.Any())
                               _onDictionaryUpdated.OnNext(_overlayEntryCore.OverlayEntryDict.Values.ToArray());
                       });
                });

            _runHistory = Enumerable.Repeat("N/A", _numberOfRuns).ToList();
            _rTSSService.SetRunHistory(_runHistory.ToArray());
            _rTSSService.SetRunHistoryAggregation(string.Empty);
            _rTSSService.SetRunHistoryOutlierFlags(_runHistoryOutlierFlags);
            _rTSSService.SetIsCaptureTimerActive(false);

            stopwatch.Stop();
            _logger.LogInformation(GetType().Name + " {initializationTime}s initialization time", Math.Round(stopwatch.ElapsedMilliseconds * 1E-03, 1));
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
            _metricAnalysis.Clear();
            _rTSSService.SetRunHistory(_runHistory.ToArray());
            _rTSSService.SetRunHistoryAggregation(string.Empty);
            _rTSSService.SetRunHistoryOutlierFlags(_runHistoryOutlierFlags);
        }

        public void AddRunToHistory(ISessionRun sessionRun, string process, string recordDirectory)
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
                    _rTSSService.SetRunHistory(_runHistory.ToArray());
                    _rTSSService.SetRunHistoryAggregation(string.Empty);
                    _rTSSService.SetRunHistoryOutlierFlags(_runHistoryOutlierFlags);
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
                _rTSSService.SetRunHistory(_runHistory.ToArray());

                // capture data history
                _captureDataHistory.Add(sessionRun);

                // frametime history
                _frametimeHistory.Add(frametimes);

                if (_appConfiguration.UseAggregation)
                {
                    _logEntryManager.AddLogEntry($"Aggregation active. Adding captured data to history ({RunHistoryCount} of {_numberOfRuns})", ELogMessageType.BasicInfo, false);

                    if (RunHistoryCount == _numberOfRuns)
                    {
                        _runHistoryOutlierFlags = _statisticProvider
                            .GetOutlierAnalysis(_metricAnalysis,
                                                _appConfiguration.RelatedMetricOverlay,
                                                _appConfiguration.OutlierPercentageOverlay);
                        _rTSSService.SetRunHistoryOutlierFlags(_runHistoryOutlierFlags);

                        if ((_runHistoryOutlierFlags.All(x => x == false)
                            && _appConfiguration.OutlierHandling == EOutlierHandling.Replace.ConvertToString())
                            || _appConfiguration.OutlierHandling == EOutlierHandling.Ignore.ConvertToString())
                        {
                            _rTSSService.SetRunHistoryAggregation(GetAggregation());


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
                            _logEntryManager.AddLogEntry($"Aggregation outliers detected ({_runHistoryOutlierFlags.Where(x => x == true).Count()}). Additional runs required.", ELogMessageType.BasicInfo, false);
                    }
                }
            }
        }

        public void UpdateNumberOfRuns(int numberOfRuns)
        {
            _numberOfRuns = numberOfRuns;
            ResetHistory();
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
            _overlayActiveStreamDisposable?.Dispose();
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
                        var dictEntry = CreateOverlayEntry(sensor);
                        var id = sensor.Identifier.ToString();
                        if (!_overlayEntryCore.OverlayEntryDict.ContainsKey(id))
                            _overlayEntryCore.OverlayEntryDict.Add(id, dictEntry);
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
                Description = GetDescription(sensor),
                OverlayEntryType = MapType(sensor.HardwareType),
                GroupName = GetGroupName(sensor),
                ShowGraph = false,
                ShowGraphIsEnabled = false,
                ShowOnOverlayIsEnabled = true,
                ShowOnOverlay = GetIsDefaultOverlayItem(sensor),
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
            }

            return formatString;
        }

        private string GetValueUnitString(string sensorTypeString)
        {
            string formatString = "{0}";
            Enum.TryParse(sensorTypeString, out SensorType sensorType);
            switch (sensorType)
            {
                case SensorType.Voltage:
                    formatString = "V  ";
                    break;
                case SensorType.Clock:
                    formatString = "MHz";
                    break;
                case SensorType.Temperature:
                    formatString = $"{GetDegreeCelciusUnitByCulture()} ";
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
            }

            return formatString;
        }

        private string GetDegreeCelciusUnitByCulture()
        {
            try
            {
                if (CultureInfo.CurrentCulture.Name == new CultureInfo("en-DE").Name)
                    return "బC";
                else
                    return "°C";
            }
            catch
            {
                return "°C";
            }
        }

        private bool GetIsDefaultOverlayItem(ISensorEntry sensor)
        {
            Enum.TryParse(sensor.SensorType, out SensorType sensorType);
            Enum.TryParse(sensor.HardwareType, out HardwareType hardwareType);

            if (sensor.Name.Contains("Core"))
            {
                if ((sensorType == SensorType.Power &&
                    sensor.Name.Contains("CPU")) ||
                    (sensorType == SensorType.Temperature &&
                    sensor.Name.Contains("CPU")) ||
                    sensor.Name.Contains("VRM") ||
                    sensorType == SensorType.Voltage)
                    return false;

                return true;
            }
            else if (sensor.Name.Contains("Memory")
                && hardwareType == HardwareType.RAM
                && sensorType == SensorType.Load)
            {
                return true;
            }
            else
                return false;
        }

        private string GetGroupName(ISensorEntry sensor)
        {
            var name = sensor.Name;
            if (name.Contains("CPU Core #"))
            {
                name = name.Replace("Core #", "");
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

            if (name.Contains(" - Thread #1"))
            {
                name = name.Replace(" - Thread #1", "");
            }

            if (name.Contains(" - Thread #2"))
            {
                name = name.Replace(" - Thread #2", "");
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
            }

            return description;
        }

        private EOverlayEntryType MapType(string hardwareTypeString)
        {
            EOverlayEntryType type = EOverlayEntryType.Undefined;
            Enum.TryParse(hardwareTypeString, out HardwareType hardwareType);
            switch (hardwareType)
            {
                case HardwareType.Mainboard:
                    type = EOverlayEntryType.Mainboard;
                    break;
                case HardwareType.SuperIO:
                    type = EOverlayEntryType.Undefined;
                    break;
                case HardwareType.CPU:
                    type = EOverlayEntryType.CPU;
                    break;
                case HardwareType.RAM:
                    type = EOverlayEntryType.RAM;
                    break;
                case HardwareType.GpuNvidia:
                    type = EOverlayEntryType.GPU;
                    break;
                case HardwareType.GpuAti:
                    type = EOverlayEntryType.GPU;
                    break;
                case HardwareType.GpuIntel:
                    type = EOverlayEntryType.GPU;
                    break;
                case HardwareType.TBalancer:
                    type = EOverlayEntryType.Undefined;
                    break;
                case HardwareType.Heatmaster:
                    type = EOverlayEntryType.Undefined;
                    break;
                case HardwareType.HDD:
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

            foreach (var frametimeSet in _frametimeHistory)
            {
                concatedFrametimes.AddRange(frametimeSet);
            }

            return _statisticProvider
                .GetMetricAnalysis(concatedFrametimes, SecondMetric, ThirdMetric)
                .ResultString;
        }
    }
}
