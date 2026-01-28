using CapFrameX.Capture.Contracts;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Logging;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.RTSS;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Monitoring.Contracts;
using CapFrameX.PMD.Benchlab;
using CapFrameX.PMD.Powenetics;
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

        private readonly ICaptureService _captureService;
        private readonly ISensorService _sensorService;
        private readonly IOverlayService _overlayService;
        private readonly SoundManager _soundManager;
        private readonly IRecordManager _recordManager;
        private readonly ILogger<CaptureManager> _logger;
        private readonly IAppConfiguration _appConfiguration;
        private readonly IRTSSService _rtssService;
        private readonly ISensorConfig _sensorConfig;
        private readonly IPoweneticsService _poweneticsService;
        private readonly IBenchlabService _benchlabService;
        private readonly ILogEntryManager _logEntryManager;
        private readonly List<string[]> _captureDataArchive = new List<string[]>();
        private readonly object _archiveLock = new object();
        private readonly ProcessList _processList;
        private CancellationTokenSource _cancelDelay = new CancellationTokenSource();

        private IDisposable _disposableCaptureStream;
        private IDisposable _disposableArchiveStream;
        private IDisposable _autoCompletionDisposableStream;
        private IDisposable _disposablePoweneticsDataStream;
        private IDisposable _disposableBenchlabDataStream;
        private IDisposable _rTSSFrameTimesIntervalStream;
        private EventLoopScheduler _captureStreamScheduler;
        private EventLoopScheduler _poweneticsScheduler;
        private EventLoopScheduler _benchlabScheduler;
        private List<string[]> _captureData = new List<string[]>();
        private bool _fillArchive;
        private double _qpcTimeStart;
        private string _captureTimeString = "0";
        private long _timestampStartCapture;
        private CaptureOptions _currentCaptureOptions;
        private long _timestampStopCapture;
        private bool _isCapturing;
        private ISubject<CaptureStatus> _captureStatusChange =
            new BehaviorSubject<CaptureStatus>(new CaptureStatus { Status = ECaptureStatus.Stopped });
        private List<float> _aggregatedRTSSFrameTimes;
        private LinkedList<float> _pmdDataGpuPower;
        private LinkedList<float> _pmdDataCpuPower;
        private LinkedList<float> _pmdDataSystemPower;

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
                _captureService.IsCaptureModeActiveStream.OnNext(value);
                _sensorConfig.IsCapturing = value;
                if (!value)
                    _captureStatusChange.OnNext(new CaptureStatus { Status = ECaptureStatus.Stopped });
            }
        }

        [DllImport("Kernel32.dll")]
        private static extern bool QueryPerformanceCounter(out long lpPerformanceCount);


        [DllImport("Kernel32.dll")]
        private static extern bool QueryPerformanceFrequency(out long lpFrequency);

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
            IPoweneticsService poweneticsService,
            IBenchlabService benchlabService,
            ILogEntryManager logEntryManager)
        {
            _captureService = presentMonCaptureService;
            _sensorService = sensorService;
            _overlayService = overlayService;
            _soundManager = soundManager;
            _recordManager = recordManager;
            _logger = logger;
            _appConfiguration = appConfiguration;
            _rtssService = rtssService;
            _sensorConfig = sensorConfig;
            _processList = processList;
            _poweneticsService = poweneticsService;
            _benchlabService = benchlabService;
            _logEntryManager = logEntryManager;
            _captureService.IsCaptureModeActiveStream.OnNext(false);
        }

        public IEnumerable<(string, int)> GetAllFilteredProcesses(HashSet<string> filter)
            => _captureService.GetAllFilteredProcesses(filter);

        public async Task StartCapture(CaptureOptions options)
        {
            if (IsCapturing)
                throw new Exception("Capture already running.");

            if (!GetAllFilteredProcesses(new HashSet<string>()).Contains(options.ProcessInfo))
                throw new Exception($"Process {options.ProcessInfo} not found");
            if (options.RecordDirectory != null && !Directory.Exists(options.RecordDirectory))
                throw new Exception($"RecordDirectory {options.RecordDirectory} does not exist");

            QueryPerformanceCounter(out long startCounter);
            QueryPerformanceFrequency(out long lpFrequency);
            _qpcTimeStart = (double)startCounter / lpFrequency;

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

            if (_appConfiguration.UsePmdDataLogging)
            {
                _pmdDataGpuPower = new LinkedList<float>();
                _pmdDataCpuPower = new LinkedList<float>();
                _pmdDataSystemPower = new LinkedList<float> { };

                // Create schedulers that we can dispose later
                _poweneticsScheduler = new EventLoopScheduler();
                _benchlabScheduler = new EventLoopScheduler();

                _disposablePoweneticsDataStream = _poweneticsService.PmdChannelStream
                    .Where(x => IsCapturing)
                    .ObserveOn(_poweneticsScheduler)
                    .Subscribe(channels => FillPmdDataLists(channels));

                _disposableBenchlabDataStream = _benchlabService.PmdSensorStream
                    .Where(x => IsCapturing)
                    .ObserveOn(_benchlabScheduler)
                    .Subscribe(sensorSample => FillPmdDataLists(sensorSample));
            }

            // Create scheduler for capture stream
            _captureStreamScheduler = new EventLoopScheduler();

            _disposableCaptureStream = _captureService
                .FrameDataStream
                .Skip(1)
                .ObserveOn(_captureStreamScheduler)
                .Subscribe(lineSplit =>
                {
                    _captureData.Add(lineSplit);

                    if (!intializedStartTime && _captureData.Any())
                    {
                        double captureDataFirstTime = 0;
                        try
                        {
                            captureDataFirstTime = GetCpuStartQpcFromDataLine(_captureData.First());
                        }
                        catch { return; }

                        lock (_archiveLock)
                        {
                            if (_captureDataArchive.Any())
                            {
                                try
                                {
                                    captureDataArchiveLastTime = GetCpuStartQpcFromDataLine(_captureDataArchive.Last());
                                }
                                catch { return; }
                            }
                        }

                        if (captureDataFirstTime < captureDataArchiveLastTime)
                        {
                            intializedStartTime = true;

                            // stop filling archive
                            _fillArchive = false;
                            _disposableArchiveStream?.Dispose();

                            _logEntryManager.AddLogEntry("Stopped filling Archive", ELogMessageType.AdvancedInfo, false);
                        }
                    }
                });

            // Start capturing RTSS frame times
            _rTSSFrameTimesIntervalStream = GetRTSSFrameTimesIntervalHeartBeat(options.ProcessInfo.Item2);
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
                            _logger.LogError(e, "Error on capture stop.");
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
            await _sensorService.StopSensorLogging();
            _soundManager.PlaySound(Sound.CaptureStopped);
            _overlayService.StopCaptureTimer();
            _autoCompletionDisposableStream?.Dispose();
            _captureStatusChange.OnNext(new CaptureStatus() { Status = ECaptureStatus.Processing });

            // Stop capturing RTSS frame times
            _rTSSFrameTimesIntervalStream?.Dispose();

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

            // Stop Present logging
            _disposableCaptureStream?.Dispose();
            _captureStreamScheduler?.Dispose();
            _captureStreamScheduler = null;

            // Stop PMD logging
            _disposablePoweneticsDataStream?.Dispose();
            _disposableBenchlabDataStream?.Dispose();
            _poweneticsScheduler?.Dispose();
            _benchlabScheduler?.Dispose();
            _poweneticsScheduler = null;
            _benchlabScheduler = null;

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

            _disposableArchiveStream = _captureService
                .FrameDataStream
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
            _captureService.StopCaptureService();
        }

        public bool StartCaptureService(IServiceStartInfo startInfo)
        {
            return _captureService.StartCaptureService(startInfo);
        }

        public void ToggleSensorLogging(bool enabled)
        {
            _sensorService.IsLoggingActiveStream.OnNext(enabled);
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

        private void FillPmdDataLists(PoweneticsChannel[] channel)
        {
            _pmdDataGpuPower.AddLast(PoweneticsChannelExtensions.GPUPowerIndexGroup.Sum(index => channel[index].Value));
            _pmdDataCpuPower.AddLast(PoweneticsChannelExtensions.EPSPowerIndexGroup.Sum(index => channel[index].Value));
            _pmdDataSystemPower.AddLast(PoweneticsChannelExtensions.SystemPowerIndexGroup.Sum(index => channel[index].Value));
        }

        private void FillPmdDataLists(SensorSample sensorSample)
        {
            _pmdDataGpuPower.AddLast((float)sensorSample.Sensors[_benchlabService.GpuPowerSensorIndex].Value);
            _pmdDataCpuPower.AddLast((float)sensorSample.Sensors[_benchlabService.CpuPowerSensorIndex].Value);
            _pmdDataSystemPower.AddLast((float)sensorSample.Sensors[_benchlabService.SytemPowerSensorIndex].Value);
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
                sessionRun.RTSSFrameTimes = _aggregatedRTSSFrameTimes?.ToArray();

                if (_appConfiguration.UsePmdDataLogging)
                {
                    if (_poweneticsService.IsServiceRunning)
                    {
                        sessionRun.SampleTime = _poweneticsService.DownSamplingSize;
                        int count = (int)(finalCaptureTime / sessionRun.SampleTime) + 1;

                        if (_pmdDataGpuPower.Any())
                            sessionRun.PmdGpuPower = _pmdDataGpuPower.Take(count).ToArray();
                        if (_pmdDataCpuPower.Any())
                            sessionRun.PmdCpuPower = _pmdDataCpuPower.Take(count).ToArray();
                        if (_pmdDataSystemPower.Any())
                            sessionRun.PmdSystemPower = _pmdDataSystemPower.Take(count).ToArray();
                    }
                    else if (_benchlabService.IsServiceRunning)
                    {
                        sessionRun.SampleTime = _benchlabService.MonitoringInterval;
                        int count = (int)(finalCaptureTime / sessionRun.SampleTime) + 1;

                        if (_pmdDataGpuPower.Any())
                            sessionRun.PmdGpuPower = _pmdDataGpuPower.Take(count).ToArray();
                        if (_pmdDataCpuPower.Any())
                            sessionRun.PmdCpuPower = _pmdDataCpuPower.Take(count).ToArray();
                        if (_pmdDataSystemPower.Any())
                            sessionRun.PmdSystemPower = _pmdDataSystemPower.Take(count).ToArray();
                    }
                }

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

                bool checkSave = await _recordManager.SaveSessionRunsToFile(new ISessionRun[] { sessionRun }, _currentCaptureOptions.ProcessInfo.Item1, _currentCaptureOptions.Comment, _currentCaptureOptions.RecordDirectory, null);

                var roundedCaptureTimeInSec = Math.Round(finalCaptureTime * 1E-3, 2, MidpointRounding.AwayFromZero);

                if (!checkSave)
                    _logEntryManager.AddLogEntry("Error while saving capture data.", ELogMessageType.Error, false);
                else
                    _logEntryManager.AddLogEntry("Capture file successfully written into directory." +
                        Environment.NewLine + $"Length in sec: {roundedCaptureTimeInSec.ToString(CultureInfo.InvariantCulture)}", ELogMessageType.BasicInfo, false);

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

            _logEntryManager.AddLogEntry($"Raw data counts - Archive: {_captureDataArchive.Count} frames, Capture: {_captureData.Count} frames",
                ELogMessageType.AdvancedInfo, false);

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

            _logEntryManager.AddLogEntry($"After process filter - Archive: {filteredArchive.Count} frames, Capture: {filteredCaptureData.Count} frames " +
                $"(target process: '{_currentCaptureOptions.ProcessInfo.Item1}')",
                ELogMessageType.AdvancedInfo, false);

            // Filter by dominant SwapChainAddress to handle mixed swap chain scenarios (e.g., CS2 with DXGI + Vulkan)
            var allProcessFilteredData = filteredArchive.Concat(filteredCaptureData).ToList();
            if (allProcessFilteredData.Any())
            {
                var swapChainCounts = allProcessFilteredData
                    .GroupBy(line => GetSwapChainAddressFromDataLine(line))
                    .ToDictionary(g => g.Key, g => g.Count());

                if (swapChainCounts.Count > 1)
                {
                    var dominantSwapChain = swapChainCounts.OrderByDescending(kvp => kvp.Value).First();
                    var filteredOutCount = allProcessFilteredData.Count - dominantSwapChain.Value;

                    // Log swap chain distribution in archive vs capture data for debugging
                    var archiveSwapChainCounts = filteredArchive
                        .GroupBy(line => GetSwapChainAddressFromDataLine(line))
                        .ToDictionary(g => g.Key, g => g.Count());
                    var captureSwapChainCounts = filteredCaptureData
                        .GroupBy(line => GetSwapChainAddressFromDataLine(line))
                        .ToDictionary(g => g.Key, g => g.Count());

                    var archiveSwapChainInfo = string.Join(", ", archiveSwapChainCounts.Select(kvp => $"'{kvp.Key}': {kvp.Value}"));
                    var captureSwapChainInfo = string.Join(", ", captureSwapChainCounts.Select(kvp => $"'{kvp.Key}': {kvp.Value}"));

                    _logEntryManager.AddLogEntry($"Multiple SwapChains detected. Using dominant SwapChain '{dominantSwapChain.Key}' " +
                        $"({dominantSwapChain.Value} frames). Filtered out {filteredOutCount} frames from {swapChainCounts.Count - 1} other SwapChain(s)." +
                        Environment.NewLine + $"Archive SwapChains: [{archiveSwapChainInfo}]" +
                        Environment.NewLine + $"Capture SwapChains: [{captureSwapChainInfo}]",
                        ELogMessageType.BasicInfo, false);

                    var archiveCountBeforeFilter = filteredArchive.Count;
                    filteredArchive = filteredArchive
                        .Where(line => GetSwapChainAddressFromDataLine(line) == dominantSwapChain.Key)
                        .ToList();

                    if (!filteredArchive.Any() && archiveCountBeforeFilter > 0)
                    {
                        _logEntryManager.AddLogEntry($"Archive emptied by SwapChain filter: archive had {archiveCountBeforeFilter} frames but none from dominant SwapChain '{dominantSwapChain.Key}'. " +
                            $"Archive contained only: [{archiveSwapChainInfo}]",
                            ELogMessageType.Error, false);
                    }

                    filteredCaptureData = filteredCaptureData
                        .Where(line => GetSwapChainAddressFromDataLine(line) == dominantSwapChain.Key)
                        .ToList();
                }
            }

            if (!filteredArchive.Any())
            {
                _logEntryManager.AddLogEntry($"Empty archive. Unable to process capture data", ELogMessageType.Error, false);
                return Enumerable.Empty<string[]>().ToList();
            }

            // Distinct archive and live stream
            var lastArchiveTime = GetCpuStartQpcFromDataLine(filteredArchive.Last());
            int distinctIndex = 0;
            for (int i = 0; i < filteredCaptureData.Count; i++)
            {
                if (GetCpuStartQpcFromDataLine(filteredCaptureData[i]) <= lastArchiveTime)
                    distinctIndex++;
                else
                    break;
            }

            if (distinctIndex == 0)
            {
                _logger.LogWarning("Something went wrong getting union capture data. We cant use the data from archive(First CaptureDataTime was {firstCaptureTime}, last ArchiveTime was {lastArchiveTime})", GetCpuStartQpcFromDataLine(filteredCaptureData.First()), lastArchiveTime);
                _logEntryManager.AddLogEntry("Comparison with archive data is invalid.", ELogMessageType.Error, false);

                return Enumerable.Empty<string[]>().ToList();
            }

            var unionCaptureData = filteredArchive.Concat(filteredCaptureData.Skip(distinctIndex)).ToList();
            var unionCaptureDataStartTime = GetCpuStartQpcFromDataLine(unionCaptureData.First());
            var unionCaptureDataEndTime = GetCpuStartQpcFromDataLine(unionCaptureData.Last());

            var captureInterval = new List<string[]>();

            double startTime = 0;

            // find first dataline that fits start of valid interval
            for (int i = 0; i < unionCaptureData.Count - 1; i++)
            {
                var currentQpcTime = GetCpuStartQpcFromDataLine(unionCaptureData[i + 1]);

                if (currentQpcTime >= _qpcTimeStart)
                {
                    startTime = GetCpuStartQpcFromDataLine(unionCaptureData[i]);
                    break;
                }
            }

            if (startTime == 0)
            {
                _logEntryManager.AddLogEntry($"Start time is invalid. Error while evaluating QPCTime start.", ELogMessageType.Error, false);
                return Enumerable.Empty<string[]>().ToList();
            }

            _logEntryManager.AddLogEntry($"Length captured data + archive ({filteredArchive.Count} frames) in sec: " + $"{Math.Round(unionCaptureDataEndTime - unionCaptureDataStartTime, 2, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture)}" + Environment.NewLine
                + $"Length captured data QPCTime start to end with buffer in sec: " + $"{Math.Round(unionCaptureDataEndTime - startTime, 2, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture)}", ELogMessageType.AdvancedInfo, false);

            if (!autoTermination)
            {
                for (int i = 0; i < unionCaptureData.Count; i++)
                {
                    var currentQpcTime = GetCpuStartQpcFromDataLine(unionCaptureData[i]);

                    if (currentQpcTime >= _qpcTimeStart && currentQpcTime - startTime <= stopwatchTime)
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
                    var currentStartTime = GetCpuStartQpcFromDataLine(unionCaptureData[i]);

                    var currentRecordTime = Math.Round(currentStartTime - startTime, 3, MidpointRounding.AwayFromZero);
                    var maxRecordTime = Math.Round(definedTime + normalizeTimeOffset, 3, MidpointRounding.AwayFromZero);

                    if (currentStartTime >= startTime && currentRecordTime <= maxRecordTime)
                    {
                        captureInterval.Add(unionCaptureData[i]);

                        if (captureInterval.Count == 2)
                            normalizeTimeOffset = GetCpuStartQpcFromDataLine(captureInterval[1]) - startTime;
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

        private string GetSwapChainAddressFromDataLine(string[] lineSplit)
        {
            return lineSplit[PresentMonCaptureService.SwapChainAddress_INDEX];
        }

        /// <summary>
        ///  Return the start time of the frame in seconds
        /// </summary>
        /// <param name="lineSplit"></param>
        /// <returns></returns>
        private double GetCpuStartQpcFromDataLine(string[] lineSplit)
        {
            return 1E-03 * Convert.ToDouble(lineSplit[_captureService.CPUStartQPCTimeInMs_Index], CultureInfo.InvariantCulture);
        }

        private double GetTimeFromDataLine(string line)
        {
            var lineSplit = line.Split(',');
            var length = Convert.ToDouble(lineSplit[_captureService.CPUStartQPCTimeInMs_Index], CultureInfo.InvariantCulture);
            return Math.Round(length, 2, MidpointRounding.AwayFromZero);
        }

        private IEnumerable<string> NormalizeTimes(IEnumerable<string[]> recordLines)
        {
            string[] firstLineSplit = recordLines.First();
            var lines = new List<string>();
            //start time
            var timeStart = GetCpuStartQpcFromDataLine(firstLineSplit);

            // normalize time
            var currentLineSplit = firstLineSplit;
            currentLineSplit[_captureService.CPUStartQPCTimeInMs_Index] = "0";
            double previousNormalizedTime = 0;
            double delta = 0;

            lines.Add(string.Join(",", currentLineSplit));

            foreach (var lineSplit in recordLines.Skip(1))
            {
                double currentStartTime = GetCpuStartQpcFromDataLine(lineSplit);

                // normalize time
                double normalizedTime = currentStartTime - timeStart;

                // Workaround for CPUStartQPCTime being equal to previous frame
                if (previousNormalizedTime == normalizedTime)
                {
                    delta = 1E-04;
                }
                else
                {
                    delta = 0;
                }

                previousNormalizedTime = normalizedTime;

                currentLineSplit = lineSplit;
                currentLineSplit[_captureService.CPUStartQPCTimeInMs_Index] = (1E03 * (normalizedTime + delta)).ToString(CultureInfo.InvariantCulture);

                lines.Add(string.Join(",", currentLineSplit));
            }

            return lines;
        }

        private IDisposable GetRTSSFrameTimesIntervalHeartBeat(int processId)
        {
            if (!_appConfiguration.CaptureRTSSFrameTimes)
            {
                return null;
            }

            const int SAMPLE_INTERVAL_MS = 1000;
            _aggregatedRTSSFrameTimes = new List<float>();
            return Observable
                .Timer(DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(SAMPLE_INTERVAL_MS))
                .Where(x => IsCapturing)
                .Subscribe(x => _aggregatedRTSSFrameTimes
                    .AddRange(_rtssService.GetFrameTimesInterval(processId, SAMPLE_INTERVAL_MS)));
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
        public string Comment;
    }
}
