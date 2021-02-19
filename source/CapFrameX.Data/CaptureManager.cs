using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.PresentMonInterface;
using CapFrameX.Contracts.RTSS;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Data.Session.Contracts;
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
        StartedTimer,
        StartedRemote,
        Processing,
        Stopped
    }

    public struct CaptureStatus
    {
        public ECaptureStatus? Status;
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
        private readonly List<string[]> _captureDataArchive = new List<string[]>();
        private readonly object _archiveLock = new object();
        private CancellationTokenSource cancelDelay = new CancellationTokenSource();

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
        private bool _oSDAutoDisabled = false;

        private ISubject<CaptureStatus> _captureStatusChange =
            new BehaviorSubject<CaptureStatus>(new CaptureStatus { Status = ECaptureStatus.Stopped });
        public IObservable<CaptureStatus> CaptureStatusChange
            => _captureStatusChange.AsObservable();
        public bool LockCaptureService { get; private set; }

        public bool DelayRunning { get; set; }

        public bool OSDAutoDisabled
        {
            get { return _oSDAutoDisabled; }
            set
            {
                _oSDAutoDisabled = value;
            }
        }

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
            ISensorConfig sensorConfig)
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
            _presentMonCaptureService.IsCaptureModeActiveStream.OnNext(false);
        }

        public IEnumerable<(string, int)> GetAllFilteredProcesses(HashSet<string> filter)
        {
            return _presentMonCaptureService.GetAllFilteredProcesses(filter);
        }

        public async Task StartCapture(CaptureOptions options)
        {
            if (IsCapturing)
                throw new Exception("Capture already running.");

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            if (!GetAllFilteredProcesses(new HashSet<string>()).Contains(options.ProcessInfo))
                throw new Exception($"Process {options.ProcessInfo} not found");
            if (options.RecordDirectory != null && !Directory.Exists(options.RecordDirectory))
                throw new Exception($"RecordDirectory {options.RecordDirectory} does not exist");


            if (options.CaptureDelay > 0d)
            {              
                DelayRunning = true;
                // Start overlay delay countdown timer
                _overlayService.SetDelayCountdown(options.CaptureDelay);
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(options.CaptureDelay + 1), cancelDelay.Token);
                }
                catch (OperationCanceledException) when (cancelDelay.IsCancellationRequested)
                {
                    stopwatch.Reset();
                    cancelDelay?.Dispose();
                    cancelDelay = new CancellationTokenSource();
                    return;
                }
                
            }
           
            IsCapturing = true;
            DelayRunning = false;

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

            AddLoggerEntry("Capturing started.");
            _overlayService.SetCaptureServiceStatus("Recording frametimes");

            if (options.Remote)
                _captureStatusChange.OnNext(new CaptureStatus() { Status = ECaptureStatus.StartedRemote });
            else if (options.CaptureTime == 0.0)
                _captureStatusChange.OnNext(new CaptureStatus() { Status = ECaptureStatus.Started });
            else
                _captureStatusChange.OnNext(new CaptureStatus() { Status = ECaptureStatus.StartedTimer });

            bool intializedStartTime = false;
            double captureDataArchiveLastTime = 0;

            _ = QueryPerformanceCounter(out long startCounter);
            AddLoggerEntry($"Performance counter on start capturing: {startCounter}");
            _qpcTimeStart = startCounter;

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

                            AddLoggerEntry("Stopped filling Archive");
                        }
                    }
                });

            _sensorService.StartSensorLogging();

            stopwatch.Stop();
            AddLoggerEntry($"Time between capture start entry and finished in ms: {stopwatch.ElapsedMilliseconds}");

            if (options.CaptureTime > 0d)
            {
                // Start overlay countdown timer
                _overlayService.StartCountdown(options.CaptureTime);
                _autoCompletionDisposableStream = Observable.Timer(TimeSpan.FromSeconds(options.CaptureTime))
                    .Subscribe(async _ => await StopCapture());
            }
            else
            {
                _overlayService.StartCaptureTimer();
            }

            await Task.FromResult(1);
        }

        public async Task StopCapture()
        {
            if (DelayRunning)
            {
                cancelDelay.Cancel();
                DelayRunning = false;
                _overlayService.SetDelayCountdown(0);
                return;
            }

            if (!IsCapturing)
                throw new Exception("No capture running.");

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

            await Task.Delay(TimeSpan.FromMilliseconds(PRESICE_OFFSET));
            IsCapturing = false;
            _disposableCaptureStream?.Dispose();
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
            AddLoggerEntry("Started filling archive.");
        }

        private async Task WriteExtractedCaptureDataToFileAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_currentCaptureOptions.ProcessInfo.Item1))
                    throw new InvalidDataException("Invalid process name!");

                var adjustedCaptureData = GetAdjustedCaptureData();

                PrepareForNextCapture();

                if (!adjustedCaptureData.Any())
                {
                    AddLoggerEntry("Error while extracting capture data. No file will be written.");
                    return;
                }

                // Skip first line to compensate the first frametime being one frame before original capture start point.
                var normalizedAdjustedCaptureData = NormalizeTimes(adjustedCaptureData.Skip(1));
                var sessionRun = _recordManager.ConvertPresentDataLinesToSessionRun(normalizedAdjustedCaptureData);

                //ToDo: data could be adjusted (cutting at end)
                sessionRun.SensorData2 = _sensorService.GetSensorSessionData();

                if (_appConfiguration.UseRunHistory)
                {
                    await Task.Factory.StartNew(() => _overlayService.AddRunToHistory(sessionRun, _currentCaptureOptions.ProcessInfo.Item1, _currentCaptureOptions.RecordDirectory));
                }

                // if aggregation mode is active and "Save aggregated result only" is checked, don't save single history items
                if (_appConfiguration.UseAggregation && _appConfiguration.SaveAggregationOnly)
                {
                    AddLoggerEntry("Aggregation active, adding to history...");
                    return;
                }

                if (_currentCaptureOptions.CaptureFileMode == Enum.GetName(typeof(ECaptureFileMode), ECaptureFileMode.JsonCsv))
                {
                    await _recordManager.SavePresentmonRawToFile(normalizedAdjustedCaptureData, _currentCaptureOptions.ProcessInfo.Item1, _currentCaptureOptions.RecordDirectory);
                }

                bool checkSave = await _recordManager.SaveSessionRunsToFile(new ISessionRun[] { sessionRun }, _currentCaptureOptions.ProcessInfo.Item1, _currentCaptureOptions.RecordDirectory);

                if (!checkSave)
                    AddLoggerEntry("Error while saving capture data.");
                else
                    AddLoggerEntry("Capture file is successfully written into directory.");

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
                AddLoggerEntry($"No available capture Data...");
                return Enumerable.Empty<string[]>().ToList();
            }

            var startTimeWithOffset = GetStartTimeFromDataLine(_captureData.First());
            var stopwatchTime = (_timestampStopCapture - _timestampStartCapture) / 1000d;

            if (string.IsNullOrWhiteSpace(_captureTimeString))
            {
                _captureTimeString = "0";
                AddLoggerEntry($"Wrong capture time string. Value will be set to default (0).");
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
                AddLoggerEntry($"Multi instances detected. Capture data will be filtered.");

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
                AddLoggerEntry($"Empty archive. No file will be written.");
                return Enumerable.Empty<string[]>().ToList();
            }
            else
            {
                AddLoggerEntry($"Using archive with {filteredArchive.Count} frames.");
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
                AddLoggerEntry("Comparison with archive data is invalid.");

                return Enumerable.Empty<string[]>().ToList();
            }

            var unionCaptureData = filteredArchive.Concat(filteredCaptureData.Skip(distinctIndex)).ToList();
            var unionCaptureDataStartTime = GetStartTimeFromDataLine(unionCaptureData.First());
            var unionCaptureDataEndTime = GetStartTimeFromDataLine(unionCaptureData.Last());

            AddLoggerEntry($"Length captured data + archive in sec: " +
                $"{ Math.Round(unionCaptureDataEndTime - unionCaptureDataStartTime, 2)}");

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
                AddLoggerEntry($"Start time is invalid. Error while evaluating QPCTime start.");
                return Enumerable.Empty<string[]>().ToList();
            }

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
                    AddLoggerEntry($"Empty capture interval. Error while evaluating start and end time.");
                    return Enumerable.Empty<string[]>().ToList();
                }
            }
            else
            {
                AddLoggerEntry($"Length captured data QPCTime start to end with buffer in sec: " +
                    $"{ Math.Round(unionCaptureDataEndTime - startTime, 2)}");

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
            return lineSplit[0].Replace(".exe", "");
        }

        private string GetProcessIdFromDataLine(string[] lineSplit)
        {
            return lineSplit[1];
        }

        private long GetQpcTimeFromDataLine(string[] lineSplit)
        {
            return Convert.ToInt64(lineSplit[17], CultureInfo.InvariantCulture);
        }

        private IEnumerable<string> NormalizeTimes(IEnumerable<string[]> recordLines)
        {
            string[] firstLineSplit = recordLines.First();
            var lines = new List<string>();
            //start time
            var timeStart = GetStartTimeFromDataLine(firstLineSplit);

            // normalize time
            var currentLineSplit = firstLineSplit;
            currentLineSplit[11] = "0";

            lines.Add(string.Join(",", currentLineSplit));

            foreach (var lineSplit in recordLines.Skip(1))
            {
                double currentStartTime = GetStartTimeFromDataLine(lineSplit);

                // normalize time
                double normalizedTime = currentStartTime - timeStart;

                currentLineSplit = lineSplit;
                currentLineSplit[11] = normalizedTime.ToString(CultureInfo.InvariantCulture);

                lines.Add(string.Join(",", currentLineSplit));
            }
            return lines;
        }

        private double GetStartTimeFromDataLine(string[] lineSplit)
        {
            if (lineSplit.Length < 10)
                return 0;

            return Convert.ToDouble(lineSplit[11], CultureInfo.InvariantCulture);
        }

        private void AddLoggerEntry(string entry)
        {
            _captureStatusChange.OnNext(new CaptureStatus()
            {
                Message = entry
            });
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
