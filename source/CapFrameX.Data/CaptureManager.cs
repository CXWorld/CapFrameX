using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.PresentMonInterface;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Data.Session.Contracts;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace CapFrameX.Data
{
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
        private readonly List<string> _captureDataArchive = new List<string>();
        private readonly List<string> _captureData = new List<string>();
        private readonly object _archiveLock = new object();

        private IDisposable _disposableCaptureStream;
        private IDisposable _disposableArchiveStream;
        private IDisposable _autoCompletionDisposableStream;
        private bool _fillArchive;
        private long _qpcTimeStart;
        private string _captureTimeString = "0";
        private long _timestampStartCapture;
        private CaptureOptions _currentCaptureOptions;
        private long _timestampStopCapture;

        public bool DataOffsetRunning { get; private set; }

        [DllImport("Kernel32.dll")]
        private static extern bool QueryPerformanceCounter(out long lpPerformanceCount);

        public CaptureManager(ICaptureService presentMonCaptureService, ISensorService sensorService, IOverlayService overlayService, SoundManager soundManager, IRecordManager recordManager, ILogger<CaptureManager> logger)
        {
            _presentMonCaptureService = presentMonCaptureService;
            _sensorService = sensorService;
            _overlayService = overlayService;
            _soundManager = soundManager;
            _recordManager = recordManager;
            _logger = logger;
        }

        public async Task StartCapture(CaptureOptions options)
        {
            _timestampStartCapture = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
            _currentCaptureOptions = options;
            _captureData.Clear();
            _sensorService.StartSensorLogging();

            bool intializedStartTime = false;
            double captureDataArchiveLastTime = 0;

            _ = QueryPerformanceCounter(out long startCounter);
            AddLoggerEntry($"Performance counter on start capturing: {startCounter}");
            _qpcTimeStart = startCounter;

            _disposableCaptureStream = _presentMonCaptureService.RedirectedOutputDataStream
                .Skip(5)
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
                        }
                    }
                });

            Application.Current.Dispatcher.Invoke(() => _soundManager.PlaySound(Sound.CaptureStarted));

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

        }

        public async Task StopCapture()
        {
            _timestampStopCapture = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
            _disposableCaptureStream?.Dispose();
            _autoCompletionDisposableStream?.Dispose();
            _sensorService.StopSensorLogging();
            _overlayService.StopCaptureTimer();
            _overlayService.SetCaptureServiceStatus("Processing data");
            Application.Current.Dispatcher.Invoke(() => _soundManager.PlaySound(Sound.CaptureStopped));
            DataOffsetRunning = true;
            await Task.Delay(TimeSpan.FromMilliseconds(PRESICE_OFFSET));
            await WriteCaptureDataToFile();
        }

        private async Task WriteCaptureDataToFile()
        {
            await WriteExtractedCaptureDataToFileAsync();
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
            DataOffsetRunning = false;
            StartFillArchive();
            AddLoggerEntry("Started filling archive.");
        }

        private async Task WriteExtractedCaptureDataToFileAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_currentCaptureOptions.ProcessName))
                {
                    PrepareForNextCapture();
                    return;
                }

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
                sessionRun.SensorData = _sensorService.GetSessionSensorData();


                if (_currentCaptureOptions.UseRunHistory)
                {
                    await Task.Factory.StartNew(() => _overlayService.AddRunToHistory(sessionRun, _currentCaptureOptions.ProcessName));
                }


                // if aggregation mode is active and "Save aggregated result only" is checked, don't save single history items
                if (_currentCaptureOptions.UseAggregation && _currentCaptureOptions.SaveAggregationOnly)
                    return;

                if (_currentCaptureOptions.CaptureFileMode == Enum.GetName(typeof(ECaptureFileMode), ECaptureFileMode.JsonCsv))
                {
                    await _recordManager.SavePresentmonRawToFile(normalizedAdjustedCaptureData, _currentCaptureOptions.ProcessName);
                }

                bool checkSave = await _recordManager.SaveSessionRunsToFile(new ISessionRun[] { sessionRun }, _currentCaptureOptions.ProcessName);


                if (!checkSave)
                    AddLoggerEntry("Error while saving capture data.");
                else
                    AddLoggerEntry("Capture file is successfully written into directory.");
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
                return Enumerable.Empty<string>().ToList();

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

        }
    }

    public class CaptureOptions
    {
        public string ProcessName;
        public double CaptureTime;
        public bool UseRunHistory;
        public bool UseAggregation;
        public bool SaveAggregationOnly;
        public string CaptureFileMode;
    }
}
