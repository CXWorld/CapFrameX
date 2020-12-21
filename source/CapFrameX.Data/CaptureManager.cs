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
        private readonly List<string> _captureDataArchive = new List<string>();
        private readonly object _archiveLock = new object();

        private IDisposable _disposableCaptureStream;
        private IDisposable _disposableArchiveStream;
        private IDisposable _autoCompletionDisposableStream;
        private List<string> _captureData = new List<string>();
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
            IRTSSService rtssService)
        {
            _presentMonCaptureService = presentMonCaptureService;
            _sensorService = sensorService;
            _overlayService = overlayService;
            _soundManager = soundManager;
            _recordManager = recordManager;
            _logger = logger;
            _appConfiguration = appConfiguration;
            _rtssService = rtssService;
            _presentMonCaptureService.IsCaptureModeActiveStream.OnNext(false);
        }

        public IEnumerable<string> GetAllFilteredProcesses(HashSet<string> filter)
        {
            return _presentMonCaptureService.GetAllFilteredProcesses(filter);
        }

        public async Task StartCapture(CaptureOptions options)
        {
            if (IsCapturing)
                throw new Exception("Capture already running.");

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            if (!GetAllFilteredProcesses(new HashSet<string>()).Contains(options.ProcessName))
                throw new Exception($"Process {options.ProcessName} not found");
            if (options.RecordDirectory != null && !Directory.Exists(options.RecordDirectory))
                throw new Exception($"RecordDirectory {options.RecordDirectory} does not exist");

            if (_appConfiguration.IsOverlayActive && _appConfiguration.AutoDisableOverlay)
            {
                _rtssService.OnOSDOff();
                _appConfiguration.IsOverlayActive = false;
                _overlayService.IsOverlayActiveStream.OnNext(false);
                OSDAutoDisabled = true;
            }

            IsCapturing = true;
            _soundManager.PlaySound(Sound.CaptureStarted);

            _timestampStartCapture = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
            _currentCaptureOptions = options;

            _captureData = new List<string>();

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

            _disposableCaptureStream = _presentMonCaptureService.RedirectedOutputDataStream
                .Skip(5)
                .ObserveOn(new EventLoopScheduler())
                .Where(dataLine => !string.IsNullOrWhiteSpace(dataLine))
                .Subscribe(dataLine =>
                {
                    _captureData.Add(dataLine);

                    if (!intializedStartTime && _captureData.Any())
                    {
                        double captureDataFirstTime = GetStartTimeFromDataLine(_captureData.First());
                        lock (_archiveLock)
                        {
                            if (_captureDataArchive.Any())
                            {
                                captureDataArchiveLastTime = GetStartTimeFromDataLine(_captureDataArchive.Last());
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
                .RedirectedOutputDataStream.Where(x => _fillArchive == true)
                .Subscribe(dataLine =>
                {
                    AddDataLineToArchive(dataLine);
                });
        }

        public void StopFillArchive()
        {
            _disposableArchiveStream?.Dispose();
            _fillArchive = false;
            ResetArchive();
            _presentMonCaptureService.StopCaptureService();
        }

        public IObservable<string> GetRedirectedOutputDataStream()
            => _presentMonCaptureService.RedirectedOutputDataStream;

        public bool StartCaptureService(IServiceStartInfo startInfo)
        {
            return _presentMonCaptureService.StartCaptureService(startInfo);
        }

        public void ToggleSensorLogging(bool enabled)
        {
            _presentMonCaptureService.IsLoggingActiveStream.OnNext(enabled);
        }

        private void AddDataLineToArchive(string dataLine)
        {
            if (string.IsNullOrWhiteSpace(dataLine))
            {
                return;
            }

            lock (_archiveLock)
            {
                if (_captureDataArchive.Count < ARCHIVE_LENGTH)
                {
                    _captureDataArchive.Add(dataLine);
                }
                else
                {
                    _captureDataArchive.RemoveAt(0);
                    _captureDataArchive.Add(dataLine);
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
                if (string.IsNullOrWhiteSpace(_currentCaptureOptions.ProcessName))
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
                sessionRun.SensorData = _sensorService.GetSensorSessionData();

                if (_appConfiguration.UseRunHistory)
                {
                    await Task.Factory.StartNew(() => _overlayService.AddRunToHistory(sessionRun, _currentCaptureOptions.ProcessName, _currentCaptureOptions.RecordDirectory));
                }

                // if aggregation mode is active and "Save aggregated result only" is checked, don't save single history items
                if (_appConfiguration.UseAggregation && _appConfiguration.SaveAggregationOnly)
                    return;

                if (_currentCaptureOptions.CaptureFileMode == Enum.GetName(typeof(ECaptureFileMode), ECaptureFileMode.JsonCsv))
                {
                    await _recordManager.SavePresentmonRawToFile(normalizedAdjustedCaptureData, _currentCaptureOptions.ProcessName, _currentCaptureOptions.RecordDirectory);
                }

                bool checkSave = await _recordManager.SaveSessionRunsToFile(new ISessionRun[] { sessionRun }, _currentCaptureOptions.ProcessName, _currentCaptureOptions.RecordDirectory);

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

        private List<string> GetAdjustedCaptureData()
        {
            if (!_captureData.Any())
            {
                AddLoggerEntry($"No available capture Data...");
                return Enumerable.Empty<string>().ToList();
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
                return currentProcess == _currentCaptureOptions.ProcessName && uniqueProcessIdDict[currentProcess].Count() == 1;
            }).ToList();
            var filteredCaptureData = _captureData.Where(line =>
            {
                var currentProcess = GetProcessNameFromDataLine(line);
                return currentProcess == _currentCaptureOptions.ProcessName && uniqueProcessIdDict[currentProcess].Count() == 1;
            }).ToList();


            if (!filteredArchive.Any())
            {
                AddLoggerEntry($"Empty archive. No file will be written.");
                return Enumerable.Empty<string>().ToList();
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

                return Enumerable.Empty<string>().ToList();
            }

            var unionCaptureData = filteredArchive.Concat(filteredCaptureData.Skip(distinctIndex)).ToList();
            var unionCaptureDataStartTime = GetStartTimeFromDataLine(unionCaptureData.First());
            var unionCaptureDataEndTime = GetStartTimeFromDataLine(unionCaptureData.Last());

            AddLoggerEntry($"Length captured data + archive in sec: " +
                $"{ Math.Round(unionCaptureDataEndTime - unionCaptureDataStartTime, 2)}");

            var captureInterval = new List<string>();

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
                return Enumerable.Empty<string>().ToList();
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
                    return Enumerable.Empty<string>().ToList();
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

        private string GetProcessNameFromDataLine(string dataLine)
        {
            if (string.IsNullOrWhiteSpace(dataLine))
                return "EmptyProcessName";

            int index = dataLine.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);

            return index > 0 ? dataLine.Substring(0, index) : "EmptyProcessName";
        }

        private string GetProcessIdFromDataLine(string dataLine)
        {
            if (string.IsNullOrWhiteSpace(dataLine))
                return "EmptyProcessID";

            var lineSplit = dataLine.Split(',');
            return lineSplit[1];
        }

        private long GetQpcTimeFromDataLine(string dataLine)
        {
            if (string.IsNullOrWhiteSpace(dataLine))
                return 0;

            var lineSplit = dataLine.Split(',');
            var qpcTime = lineSplit[17];

            return Convert.ToInt64(qpcTime, CultureInfo.InvariantCulture);
        }

        private IEnumerable<string> NormalizeTimes(IEnumerable<string> recordLines)
        {
            string firstDataLine = recordLines.First();
            var lines = new List<string>();
            //start time
            var timeStart = GetStartTimeFromDataLine(firstDataLine);

            // normalize time
            var currentLineSplit = firstDataLine.Split(',');
            currentLineSplit[11] = "0";

            lines.Add(string.Join(",", currentLineSplit));

            foreach (var dataLine in recordLines.Skip(1))
            {
                double currentStartTime = GetStartTimeFromDataLine(dataLine);

                // normalize time
                double normalizedTime = currentStartTime - timeStart;

                currentLineSplit = dataLine.Split(',');
                currentLineSplit[11] = normalizedTime.ToString(CultureInfo.InvariantCulture);

                lines.Add(string.Join(",", currentLineSplit));
            }
            return lines;
        }

        private double GetStartTimeFromDataLine(string dataLine)
        {
            if (string.IsNullOrWhiteSpace(dataLine))
                return 0;

            var lineSplit = dataLine.Split(',');
            var startTime = lineSplit[11];

            return Convert.ToDouble(startTime, CultureInfo.InvariantCulture);
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
        public string ProcessName;
        public double CaptureTime;
        public string CaptureFileMode;
        public string RecordDirectory;
        public bool Remote;
    }
}
