using CapFrameX.Contracts.Logging;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.PresentMonInterface;
using CapFrameX.Contracts.RTSS;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.PresentMonInterface;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace CapFrameX.Data
{
    public enum ECaptureStatus
    {
        Started,
        StartedDelay,
        StartedTimer,
        StartedRemote,
        Processing,
        Stopped
    }

    public struct CaptureStatus
    {
        public ECaptureStatus? Status;
        public ELogMessageType MessageType;
        public string Message;
    }

    public class CaptureManager
    {
        private const int PRESICE_OFFSET = 2500;
        private const int ARCHIVE_LENGTH = 500;

        private readonly ICaptureService _presentMonCaptureService;
        private readonly ISensorService _sensorService;
        private readonly IOverlayService _overlayService;
        private readonly SoundManager _soundManager;
        private readonly IRecordManager _recordManager;
        private readonly ILogger<CaptureManager> _logger;
        private readonly IAppConfiguration _appConfiguration;
        private readonly IRTSSService _rtssService;
        private readonly ISensorConfig _sensorConfig;
        private readonly ILogEntryManager _logEntryManager;
        private readonly List<string[]> _captureDataArchive = new List<string[]>();
        private readonly object _archiveLock = new object();
        private readonly ProcessList _processList;
        private CancellationTokenSource _cancelDelay = new CancellationTokenSource();

        private IDisposable _disposableCaptureStream;
        private IDisposable _disposableArchiveStream;
        private IDisposable _autoCompletionDisposableStream;
        private List<string[]> _captureData = new List<string[]>();
        private bool _fillArchive;
        private long _qpcTimeStart;
        private string _captureTimeString = "0";
        private long _timestampStartCapture;
        private CaptureOptions _currentCaptureOptions;
        private long _timestampStopCapture;
        private bool _isCapturing;
        private ISubject<CaptureStatus> _captureStatusChange =
            new BehaviorSubject<CaptureStatus>(new CaptureStatus { Status = ECaptureStatus.Stopped });
        public IObservable<CaptureStatus> CaptureStatusChange
            => _captureStatusChange.AsObservable();
        public bool LockCaptureService { get; private set; }

        public bool DelayCountdownRunning { get; set; }

        public bool OSDAutoDisabled { get; set; } = false;

        public bool IsCapturing
        {
            get { return _isCapturing; }
            set
            {
                _isCapturing = value;
                _presentMonCaptureService.IsCaptureModeActiveStream.OnNext(value);
                _sensorConfig.IsCapturing = value;
                if (!value)
                    _captureStatusChange.OnNext(new CaptureStatus { Status = ECaptureStatus.Stopped });
            }
        }

        [DllImport("Kernel32.dll")]
        private static extern bool QueryPerformanceCounter(out long lpPerformanceCount);

        public CaptureManager(ICaptureService presentMonCaptureService,
            ISensorService sensorService,
            IOverlayService overlayService,
            SoundManager soundManager,
            IRecordManager recordManager,
            ILogger<CaptureManager> logger,
            IAppConfiguration appConfiguration,
            IRTSSService rtssService,
            ISensorConfig sensorConfig,
            ProcessList processList,
            ILogEntryManager logEntryManager)
        {
            _presentMonCaptureService = presentMonCaptureService;
            _sensorService = sensorService;
            _overlayService = overlayService;
            _soundManager = soundManager;
            _recordManager = recordManager;
            _logger = logger;
            _appConfiguration = appConfiguration;
            _rtssService = rtssService;
            _sensorConfig = sensorConfig;
            _processList = processList;
            _logEntryManager = logEntryManager;
            _presentMonCaptureService.IsCaptureModeActiveStream.OnNext(false);
        }

        public IEnumerable<(string, int)> GetAllFilteredProcesses(HashSet<string> filter)
            => _presentMonCaptureService.GetAllFilteredProcesses(filter);

        public async Task StartCapture(CaptureOptions options)
        {
            if (IsCapturing)
                throw new Exception("Capture already running.");

            if (!GetAllFilteredProcesses(new HashSet<string>()).Contains(options.ProcessInfo))
                throw new Exception($"Process {options.ProcessInfo} not found");
            if (options.RecordDirectory != null && !Directory.Exists(options.RecordDirectory))
                throw new Exception($"RecordDirectory {options.RecordDirectory} does not exist");

            _ = QueryPerformanceCounter(out long startCounter);
            _qpcTimeStart = startCounter;

            var atomicTime = AtomicTime.Now.TimeOfDay;

            var delayStopwatch = new Stopwatch();
            delayStopwatch.Start();

            if (options.CaptureDelay > 0d)
            {
                DelayCountdownRunning = true;
                var delay = options.CaptureDelay;
                // Start overlay delay countdown timer
                _captureStatusChange.OnNext(new CaptureStatus() { Status = ECaptureStatus.StartedDelay });

                _logEntryManager.AddLogEntry($"Capture delay timer of {delay.ToString(CultureInfo.InvariantCulture)} seconds started", ELogMessageType.BasicInfo, true);

                _overlayService.SetDelayCountdown(delay);

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(delay), _cancelDelay.Token);
                }
                catch (OperationCanceledException) when (_cancelDelay.IsCancellationRequested)
                {
                    _overlayService.SetCaptureServiceStatus("Ready to capture...");
                    _captureStatusChange.OnNext(new CaptureStatus() { Status = ECaptureStatus.Stopped });
                    delayStopwatch.Reset();
                    _logEntryManager.AddLogEntry("Capture canceled", ELogMessageType.BasicInfo, false);
                    _cancelDelay?.Dispose();
                    _cancelDelay = new CancellationTokenSource();
                    return;
                }
            }

            IsCapturing = true;
            DelayCountdownRunning = false;

            if (_appConfiguration.IsOverlayActive && _appConfiguration.AutoDisableOverlay)
            {
                _rtssService.OnOSDOff();
                _appConfiguration.IsOverlayActive = false;
                _overlayService.IsOverlayActiveStream.OnNext(false);
                OSDAutoDisabled = true;
            }

            _soundManager.PlaySound(Sound.CaptureStarted);
            _timestampStartCapture = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
            _currentCaptureOptions = options;

            _captureData = new List<string[]>();

            _overlayService.SetCaptureServiceStatus("Recording frametimes");

            if (options.Remote)
                _captureStatusChange.OnNext(new CaptureStatus() { Status = ECaptureStatus.StartedRemote });
            else if (options.CaptureTime == 0.0)
                _captureStatusChange.OnNext(new CaptureStatus() { Status = ECaptureStatus.Started });
            else
                _captureStatusChange.OnNext(new CaptureStatus() { Status = ECaptureStatus.StartedTimer });

            bool intializedStartTime = false;
            double captureDataArchiveLastTime = 0;

            _disposableCaptureStream = _presentMonCaptureService
                .RedirectedOutputDataStream
                .Skip(1)
                .ObserveOn(new EventLoopScheduler())
                .Subscribe(lineSplit =>
                {
                    _captureData.Add(lineSplit);

                    if (!intializedStartTime && _captureData.Any())
                    {
                        double captureDataFirstTime = 0;
                        try
                        {
                            captureDataFirstTime = GetStartTimeFromDataLine(_captureData.First());
                        }
                        catch { return; }

                        lock (_archiveLock)
                        {
                            if (_captureDataArchive.Any())
                            {
                                try
                                {
                                    captureDataArchiveLastTime = GetStartTimeFromDataLine(_captureDataArchive.Last());
                                }
                                catch { return; }
                            }
                        }

                        if (captureDataFirstTime < captureDataArchiveLastTime)
                        {
                            intializedStartTime = true;

                            // stop archive
                            _fillArchive = false;
                            _disposableArchiveStream?.Dispose();

                            _logEntryManager.AddLogEntry("Stopped filling Archive", ELogMessageType.AdvancedInfo, false);
                        }
                    }
                });

            _sensorService.StartSensorLogging();

            delayStopwatch.Stop();

            if (options.CaptureTime > 0d)
            {
                //Start overlay countdown timer
                _overlayService.StartCountdown(options.CaptureTime);
                _logEntryManager.AddLogEntry($"Capturing of process \"{options.ProcessInfo.Item1}\" started." + Environment.NewLine +
                    $"Set time in sec: {options.CaptureTime.ToString(CultureInfo.InvariantCulture)}", ELogMessageType.BasicInfo, options.CaptureDelay > 0d ? false : true);

                _autoCompletionDisposableStream = Observable.Timer(TimeSpan.FromSeconds(options.CaptureTime))
                    .Subscribe(async _ =>
                    {
                        try
                        {
                            await StopCapture();
                        }
                        catch (Exception e)
                        {

                        }
                    });
            }
            else
            {
                _overlayService.StartCaptureTimer();
                _logEntryManager.AddLogEntry($"Capturing of process \"{options.ProcessInfo.Item1}\" started", ELogMessageType.BasicInfo, options.CaptureDelay > 0d ? false : true);
            }

            _logEntryManager.AddLogEntry($"Atomic time(UTC) on capture start request: {atomicTime}" + Environment.NewLine
                + $"Performance counter on capture start request: {startCounter}" + Environment.NewLine
                + $"Time between capture start request and execution in ms: {delayStopwatch.ElapsedMilliseconds}", ELogMessageType.AdvancedInfo, false);

            await Task.FromResult(0);
        }

        public async Task StopCapture()
        {
            if (DelayCountdownRunning)
            {
                _cancelDelay.Cancel();
                DelayCountdownRunning = false;
                _overlayService.CancelDelayCountdown();
                return;
            }

            if (!IsCapturing)
                throw new Exception("No capture running.");


            _logEntryManager.AddLogEntry("Capture finished", ELogMessageType.BasicInfo, false);

            LockCaptureService = true;

            _timestampStopCapture = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
            _soundManager.PlaySound(Sound.CaptureStopped);
            _overlayService.StopCaptureTimer();
            _autoCompletionDisposableStream?.Dispose();
            _sensorService.StopSensorLogging();
            _captureStatusChange.OnNext(new CaptureStatus() { Status = ECaptureStatus.Processing });


            if (_appConfiguration.AutoDisableOverlay && OSDAutoDisabled)
            {
                _appConfiguration.IsOverlayActive = true;
                _overlayService.IsOverlayActiveStream.OnNext(true);
                _rtssService.OnOSDOn();
                OSDAutoDisabled = false;
            }

            if (_appConfiguration.IsOverlayActive)
                _rtssService.Refresh();

            _logEntryManager.AddLogEntry($"Running offset of {PRESICE_OFFSET}ms to gather latest frames", ELogMessageType.AdvancedInfo, false);

            await Task.Delay(TimeSpan.FromMilliseconds(PRESICE_OFFSET));
            IsCapturing = false;
            _disposableCaptureStream?.Dispose();

            _logEntryManager.AddLogEntry("Processing captured data", ELogMessageType.BasicInfo, false);

            if (_appConfiguration.IsOverlayActive)
                _rtssService.Refresh();

            await WriteExtractedCaptureDataToFileAsync();
            LockCaptureService = false;
        }

        public void StartFillArchive()
        {
            _disposableArchiveStream?.Dispose();
            _fillArchive = true;
            ResetArchive();

            _disposableArchiveStream = _presentMonCaptureService
                .RedirectedOutputDataStream
                .Skip(1)
                .Where(x => _fillArchive == true)
                .Subscribe(lineSplit =>
                {
                    AddDataLineToArchive(lineSplit);
                });
        }

        public void StopFillArchive()
        {
            _disposableArchiveStream?.Dispose();
            _fillArchive = false;
            ResetArchive();
            _presentMonCaptureService.StopCaptureService();
        }

        public bool StartCaptureService(IServiceStartInfo startInfo)
        {
            return _presentMonCaptureService.StartCaptureService(startInfo);
        }

        public void ToggleSensorLogging(bool enabled)
        {
            _presentMonCaptureService.IsLoggingActiveStream.OnNext(enabled);
        }

        private void AddDataLineToArchive(string[] lineSplit)
        {
            lock (_archiveLock)
            {
                if (_captureDataArchive.Count < ARCHIVE_LENGTH)
                {
                    _captureDataArchive.Add(lineSplit);
                }
                else
                {
                    _captureDataArchive.RemoveAt(0);
                    _captureDataArchive.Add(lineSplit);
                }
            }
        }

        private void ResetArchive() => _captureDataArchive.Clear();

        private void PrepareForNextCapture()
        {
            StartFillArchive();
            _logEntryManager.AddLogEntry("Started filling archive.", ELogMessageType.AdvancedInfo, false);
        }

        private async Task WriteExtractedCaptureDataToFileAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_currentCaptureOptions.ProcessInfo.Item1))
                    throw new InvalidDataException("Invalid process name!");

                var adjustedCaptureData = GetAdjustedCaptureData();

                if (!adjustedCaptureData.Any())
                {
                    _logEntryManager.AddLogEntry("Error while extracting capture data. No file will be written.", ELogMessageType.Error, false);
                    PrepareForNextCapture();
                    return;
                }

                PrepareForNextCapture();

                // Skip first line to compensate the first frametime being one frame before original capture start point.
                var normalizedAdjustedCaptureData = NormalizeTimes(adjustedCaptureData.Skip(1));
                var sessionRun = _recordManager.ConvertPresentDataLinesToSessionRun(normalizedAdjustedCaptureData);
                var finalCaptureTime = GetTimeFromDataLine(normalizedAdjustedCaptureData?.Last());

                //ToDo: data could be adjusted (cutting at end)
                sessionRun.SensorData2 = _sensorService.GetSensorSessionData();

                if (_appConfiguration.UseRunHistory)
                {
                    await Task.Factory.StartNew(() => _overlayService.AddRunToHistory(sessionRun, _currentCaptureOptions.ProcessInfo.Item1, _currentCaptureOptions.RecordDirectory));
                }

                // if aggregation mode is active and "Save aggregated result only" is checked, don't save single history items
                if (_appConfiguration.UseAggregation && _appConfiguration.SaveAggregationOnly)
                    return;

                if (_currentCaptureOptions.CaptureFileMode == Enum.GetName(typeof(ECaptureFileMode), ECaptureFileMode.JsonCsv))
                {
                    await _recordManager.SavePresentmonRawToFile(normalizedAdjustedCaptureData, _currentCaptureOptions.ProcessInfo.Item1, _currentCaptureOptions.RecordDirectory);
                }

                bool checkSave = await _recordManager.SaveSessionRunsToFile(new ISessionRun[] { sessionRun }, _currentCaptureOptions.ProcessInfo.Item1, _currentCaptureOptions.RecordDirectory, null);

                if (!checkSave)
                    _logEntryManager.AddLogEntry("Error while saving capture data.", ELogMessageType.Error, false);
                else
                    _logEntryManager.AddLogEntry("Capture file successfully written into directory." +
                        Environment.NewLine + $"Length in sec: {finalCaptureTime.ToString(CultureInfo.InvariantCulture)}", ELogMessageType.BasicInfo, false);

                LockCaptureService = false;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error writing capture data");
                PrepareForNextCapture();
            }
        }

        private List<string[]> GetAdjustedCaptureData()
        {
            if (!_captureData.Any())
            {
                _logEntryManager.AddLogEntry($"No available capture Data...", ELogMessageType.Error, false);
                return Enumerable.Empty<string[]>().ToList();
            }

            var startTimeWithOffset = GetStartTimeFromDataLine(_captureData.First());
            var stopwatchTime = (_timestampStopCapture - _timestampStartCapture) / 1000d;

            if (string.IsNullOrWhiteSpace(_captureTimeString))
            {
                _captureTimeString = "0";
                _logEntryManager.AddLogEntry($"Wrong capture time string. Value will be set to default (0).", ELogMessageType.BasicInfo, false);
            }

            var definedTime = _currentCaptureOptions.CaptureTime;
            bool autoTermination = _currentCaptureOptions.CaptureTime > 0;

            if (autoTermination)
            {
                if (stopwatchTime < definedTime - 0.2 && stopwatchTime > 0)
                    autoTermination = false;
            }

            var uniqueProcessIdDict = new Dictionary<string, HashSet<string>>();

            foreach (var filteredCaptureDataLine in _captureData)
            {
                var currentProcess = GetProcessNameFromDataLine(filteredCaptureDataLine);
                var currentProcessId = GetProcessIdFromDataLine(filteredCaptureDataLine);

                if (!uniqueProcessIdDict.ContainsKey(currentProcess))
                {
                    var idHashSet = new HashSet<string>
                    {
                        currentProcessId
                    };
                    uniqueProcessIdDict.Add(currentProcess, idHashSet);
                }
                else
                    uniqueProcessIdDict[currentProcess].Add(currentProcessId);
            }

            if (uniqueProcessIdDict.Any(dict => dict.Value.Count() > 1))
                _logEntryManager.AddLogEntry($"Multi instances detected. Capture data will be filtered.", ELogMessageType.BasicInfo, false);

            var filteredArchive = _captureDataArchive.Where(line =>
            {
                var currentProcess = GetProcessNameFromDataLine(line);
                return currentProcess == _currentCaptureOptions.ProcessInfo.Item1 && uniqueProcessIdDict[currentProcess].Count() == 1;
            }).ToList();
            var filteredCaptureData = _captureData.Where(line =>
            {
                var currentProcess = GetProcessNameFromDataLine(line);
                return currentProcess == _currentCaptureOptions.ProcessInfo.Item1 && uniqueProcessIdDict[currentProcess].Count() == 1;
            }).ToList();


            if (!filteredArchive.Any())
            {
                _logEntryManager.AddLogEntry($"Empty archive. Unable to process capture data", ELogMessageType.Error, false);
                return Enumerable.Empty<string[]>().ToList();
            }

            // Distinct archive and live stream
            var lastArchiveTime = GetStartTimeFromDataLine(filteredArchive.Last());
            int distinctIndex = 0;
            for (int i = 0; i < filteredCaptureData.Count; i++)
            {
                if (GetStartTimeFromDataLine(filteredCaptureData[i]) <= lastArchiveTime)
                    distinctIndex++;
                else
                    break;
            }

            if (distinctIndex == 0)
            {
                _logger.LogWarning("Something went wrong getting union capture data. We cant use the data from archive(First CaptureDataTime was {firstCaptureTime}, last ArchiveTime was {lastArchiveTime})", GetStartTimeFromDataLine(filteredCaptureData.First()), lastArchiveTime);
                _logEntryManager.AddLogEntry("Comparison with archive data is invalid.", ELogMessageType.Error, false);

                return Enumerable.Empty<string[]>().ToList();
            }

            var unionCaptureData = filteredArchive.Concat(filteredCaptureData.Skip(distinctIndex)).ToList();
            var unionCaptureDataStartTime = GetStartTimeFromDataLine(unionCaptureData.First());
            var unionCaptureDataEndTime = GetStartTimeFromDataLine(unionCaptureData.Last());

            var captureInterval = new List<string[]>();

            double startTime = 0;

            // find first dataline that fits start of valid interval
            for (int i = 0; i < unionCaptureData.Count - 1; i++)
            {
                var currentQpcTime = GetQpcTimeFromDataLine(unionCaptureData[i + 1]);

                if (currentQpcTime >= _qpcTimeStart)
                {
                    startTime = GetStartTimeFromDataLine(unionCaptureData[i]);
                    break;
                }
            }

            if (startTime == 0)
            {
                _logEntryManager.AddLogEntry($"Start time is invalid. Error while evaluating QPCTime start.", ELogMessageType.Error, false);
                return Enumerable.Empty<string[]>().ToList();
            }

            _logEntryManager.AddLogEntry($"Length captured data + archive ({filteredArchive.Count} frames) in sec: " + $"{ Math.Round(unionCaptureDataEndTime - unionCaptureDataStartTime, 2).ToString(CultureInfo.InvariantCulture)}" + Environment.NewLine
                + $"Length captured data QPCTime start to end with buffer in sec: " + $"{ Math.Round(unionCaptureDataEndTime - startTime, 2).ToString(CultureInfo.InvariantCulture)}", ELogMessageType.AdvancedInfo, false);


            if (!autoTermination)
            {
                for (int i = 0; i < unionCaptureData.Count; i++)
                {
                    var currentqpcTime = GetQpcTimeFromDataLine(unionCaptureData[i]);
                    var currentTime = GetStartTimeFromDataLine(unionCaptureData[i]);

                    if (currentqpcTime >= _qpcTimeStart && currentTime - startTime <= stopwatchTime)
                        captureInterval.Add(unionCaptureData[i]);
                }

                if (!captureInterval.Any())
                {
                    _logEntryManager.AddLogEntry($"Empty capture interval. Error while evaluating start and end time.", ELogMessageType.Error, false);
                    return Enumerable.Empty<string[]>().ToList();
                }
            }
            else
            {
                double normalizeTimeOffset = 0;

                for (int i = 0; i < unionCaptureData.Count; i++)
                {
                    var currentStartTime = GetStartTimeFromDataLine(unionCaptureData[i]);

                    var currentRecordTime = Math.Round(currentStartTime - startTime, 3);
                    var maxRecordTime = Math.Round(definedTime + normalizeTimeOffset, 3);

                    if (currentStartTime >= startTime && currentRecordTime <= maxRecordTime)
                    {
                        captureInterval.Add(unionCaptureData[i]);

                        if (captureInterval.Count == 2)
                            normalizeTimeOffset = GetStartTimeFromDataLine(captureInterval[1]) - startTime;
                    }
                }
            }

            return captureInterval;
        }

        private string GetProcessNameFromDataLine(string[] lineSplit)
        {
            return lineSplit[PresentMonCaptureService.ApplicationName_INDEX].Replace(".exe", "");
        }

        private string GetProcessIdFromDataLine(string[] lineSplit)
        {
            return lineSplit[PresentMonCaptureService.ProcessID_INDEX];
        }

        private long GetQpcTimeFromDataLine(string[] lineSplit)
        {
            return Convert.ToInt64(lineSplit[PresentMonCaptureService.QPCTime_INDEX], CultureInfo.InvariantCulture);
        }

        private double GetTimeFromDataLine(string line)
        {
            var lineSplit = line.Split(',');
            var length = Convert.ToDouble(lineSplit[PresentMonCaptureService.TimeInSeconds_INDEX], CultureInfo.InvariantCulture);
            return Math.Round(length, 2);
        }

        private IEnumerable<string> NormalizeTimes(IEnumerable<string[]> recordLines)
        {
            string[] firstLineSplit = recordLines.First();
            var lines = new List<string>();
            //start time
            var timeStart = GetStartTimeFromDataLine(firstLineSplit);

            // normalize time
            var currentLineSplit = firstLineSplit;
            currentLineSplit[PresentMonCaptureService.TimeInSeconds_INDEX] = "0";

            lines.Add(string.Join(",", currentLineSplit));

            foreach (var lineSplit in recordLines.Skip(1))
            {
                double currentStartTime = GetStartTimeFromDataLine(lineSplit);

                // normalize time
                double normalizedTime = currentStartTime - timeStart;

                currentLineSplit = lineSplit;
                currentLineSplit[PresentMonCaptureService.TimeInSeconds_INDEX] = normalizedTime.ToString(CultureInfo.InvariantCulture);

                lines.Add(string.Join(",", currentLineSplit));
            }
            return lines;
        }

        private double GetStartTimeFromDataLine(string[] lineSplit)
        {
            return Convert.ToDouble(lineSplit[PresentMonCaptureService.TimeInSeconds_INDEX], CultureInfo.InvariantCulture);
        }

    }

    public class CaptureOptions
    {
        public (string, int) ProcessInfo;
        public double CaptureTime;
        public double CaptureDelay;
        public string CaptureFileMode;
        public string RecordDirectory;
        public bool Remote;
    }
}
