using CapFrameX.Capture.Contracts;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Logging;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.RTSS;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Monitoring.Contracts;
using CapFrameX.PMD;
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
using System.Text;
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
        // Serializes the swap of the frame-stream subscription and its scheduler. Concurrent
        // StartCapture calls are possible (see the comment at the subscription site), and two
        // of them racing here would either orphan a subscription or dispose a scheduler the
        // other is about to subscribe on.
        private readonly object _captureStreamGate = new object();
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
        // PMD samples of the running capture: time in seconds on the QPC clock, power per rail.
        private readonly object _pmdSamplesLock = new object();
        private List<double> _pmdSampleTimes = new List<double>();
        private List<float> _pmdDataGpuPower = new List<float>();
        private List<float> _pmdDataCpuPower = new List<float>();
        private List<float> _pmdDataSystemPower = new List<float>();

        public IObservable<CaptureStatus> CaptureStatusChange
            => _captureStatusChange.AsObservable();

        /// <summary>
        /// True while PresentMon is up and delivering data - independent of a recording being in
        /// progress. Settings that only reach PresentMon through its command line must not be
        /// changed while this is false.
        /// </summary>
        public bool IsCaptureServiceRunning => _captureService.IsCaptureServiceRunning;

        public IObservable<bool> CaptureServiceRunningStream => _captureService.CaptureServiceRunningStream;

        public bool LockCaptureService { get; private set; }

        public bool DelayCountdownRunning { get; set; }

        public bool OSDAutoDisabled { get; set; } = false;

        /// <summary>
        /// Indicates whether a capture is currently active. For external components to check capture status.
        public static bool IsCapturingStatus { get; private set; } = false;

        public bool IsCapturing
        {
            get { return _isCapturing; }
            set
            {
                _isCapturing = value;
                IsCapturingStatus = value;
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
            IsCapturingStatus = false;
            _captureService.IsCaptureModeActiveStream.OnNext(false);
        }

        public IEnumerable<(string, int)> GetAllFilteredProcesses(HashSet<string> filter)
            => _captureService.GetAllFilteredProcesses(filter);

        public IEnumerable<(string, int)> GetAllNonBlacklistedProcesses()
        {
            var filter = new HashSet<string>(
                _processList.GetIgnoredProcessNames(),
                StringComparer.OrdinalIgnoreCase);

            return GetAllFilteredProcesses(filter);
        }

        public async Task StartCapture(CaptureOptions options)
        {
            // Logged first thing: next to the hotkey log lines this shows how long the request
            // waited between the hotkey callback and the thread that runs it.
            _logger.LogInformation("Capture start requested for {process} (PID {pid}).",
                options.ProcessInfo.Item1, options.ProcessInfo.Item2);

            if (IsCapturing)
                throw new Exception("Capture already running.");

            if (!GetAllFilteredProcesses(new HashSet<string>()).Contains(options.ProcessInfo))
                throw new Exception($"Process {options.ProcessInfo} not found");
            if (options.RecordDirectory != null && !Directory.Exists(options.RecordDirectory))
                throw new Exception($"RecordDirectory {options.RecordDirectory} does not exist");

            // Before the start time is taken, so the PMD samples cover the first frame.
            StartPmdLogging();

            // The capture starts at the request; the frames shown until the frame data subscription
            // below is up come from the archive. A capture delay moves the start (see below).
            QueryPerformanceCounter(out long startCounter);
            QueryPerformanceFrequency(out long lpFrequency);
            _qpcTimeStart = (double)startCounter / lpFrequency;

            var delayStopwatch = new Stopwatch();
            delayStopwatch.Start();

            // On its first use AtomicTime asks five NIST time servers and, when none answers, waits
            // until all of them gave up (21 s when they are unreachable). It used to run right here,
            // holding back the timers, the frame subscription and the sensor logging of the first
            // capture of a session.
            var requestStopwatch = Stopwatch.StartNew();
            var atomicTimeOnRequest = Task.Run(() =>
            {
                var atomicNow = AtomicTime.Now;
                return atomicNow == DateTime.MinValue ? (TimeSpan?)null : atomicNow.TimeOfDay - requestStopwatch.Elapsed;
            });

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
                    StopPmdLogging();
                    _overlayService.SetCaptureServiceStatus("Ready to capture...");
                    _captureStatusChange.OnNext(new CaptureStatus() { Status = ECaptureStatus.Stopped });
                    delayStopwatch.Reset();
                    _logEntryManager.AddLogEntry("Capture canceled", ELogMessageType.BasicInfo, false);
                    _cancelDelay?.Dispose();
                    _cancelDelay = new CancellationTokenSource();
                    return;
                }

                // The delay holds the capture back, so the frames shown meanwhile do not belong to
                // it. With the request time as start, GetAdjustedCaptureData took them from the
                // archive (as far as its 500 frames reach) and the capture ended that much early.
                QueryPerformanceCounter(out long delayEndCounter);
                _qpcTimeStart = (double)delayEndCounter / lpFrequency;
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

            // Replace the previous subscription instead of assigning over it, the way
            // StartFillArchive already does. The handle is the only way to dispose one, so
            // overwriting a live subscription orphans it forever — and its handler appends to
            // _captureData, the FIELD, so it goes on filling whatever list the CURRENT capture
            // uses. Each orphan adds one more copy of every frame for the rest of the session.
            //
            // This is reachable: the IsCapturing guard in StartCapture is a check-then-act
            // across the awaits below it, so several hotkey presses a few hundred ms apart can
            // all pass it and run concurrently. The lock keeps that concurrency from disposing
            // a scheduler another start is about to subscribe on; it holds no await.
            lock (_captureStreamGate)
            {
                _disposableCaptureStream?.Dispose();
                _captureStreamScheduler?.Dispose();

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
            }

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

            _logEntryManager.AddLogEntry($"Performance counter on capture start request: {startCounter}" + Environment.NewLine
                + $"Time between capture start request and execution in ms: {delayStopwatch.ElapsedMilliseconds}", ELogMessageType.AdvancedInfo, false);

            _ = atomicTimeOnRequest.ContinueWith(task => _logEntryManager.AddLogEntry(
                $"Atomic time(UTC) on capture start request: {(task.Result.HasValue ? task.Result.Value.ToString() : "unavailable")}",
                ELogMessageType.AdvancedInfo, false), TaskContinuationOptions.OnlyOnRanToCompletion);
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
                // The renderer may have been switched off during the capture; then the overlay stays off.
                if (_appConfiguration.OverlayRenderer != EOverlayRenderer.Off)
                {
                    _appConfiguration.IsOverlayActive = true;
                    _overlayService.IsOverlayActiveStream.OnNext(true);
                    _rtssService.OnOSDOn();
                }
                OSDAutoDisabled = false;
            }

            if (_appConfiguration.IsOverlayActive)
                _rtssService.Refresh();

            _logEntryManager.AddLogEntry($"Running offset of {PRESICE_OFFSET}ms to gather latest frames", ELogMessageType.AdvancedInfo, false);

            await Task.Delay(TimeSpan.FromMilliseconds(PRESICE_OFFSET));
            IsCapturing = false;

            // Stop Present logging
            lock (_captureStreamGate)
            {
                _disposableCaptureStream?.Dispose();
                _disposableCaptureStream = null;
                _captureStreamScheduler?.Dispose();
                _captureStreamScheduler = null;
            }

            StopPmdLogging();

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

        /// <summary>
        /// Start info for PresentMon, built from the current configuration. Everything that starts
        /// PresentMon goes through here: PC latency tracking, the circular buffer size and the
        /// ignore list only reach PresentMon on its command line, so a second place building these
        /// arguments would quietly start the service with stale settings.
        /// </summary>
        public IServiceStartInfo GetPresentMonStartInfo()
        {
            var serviceConfig = new PresentMonServiceConfiguration
            {
                RedirectOutputStream = true,
                ExcludeProcesses = _processList.GetIgnoredProcessNames().ToList(),
                TrackPcLatency = _appConfiguration.UsePcLatency,
                CircularBufferSize = _appConfiguration.PresentMonCircularBufferSize
            };

            return CaptureServiceConfiguration.GetServiceStartInfo(serviceConfig.ConfigParameterToArguments());
        }

        /// <summary>
        /// Brings PresentMon up with the current configuration: a running instance is stopped
        /// first and the frame data archive is dropped and refilled from the new session. Also
        /// covers the initial start - stopping a service that is not running is a no-op.
        /// </summary>
        /// <remarks>
        /// The archive is not optional here. Its subscription carries the Skip(1) that drops the
        /// CSV header, and the frame data subject outlives a PresentMon restart - a subscription
        /// kept across one would take the new instance's header for a data row. On top of that,
        /// rows from the old session can have a different column layout when PC latency tracking
        /// was toggled in between.
        /// Waits for the ETW session teardown, so keep it off the UI thread where responsiveness
        /// matters.
        /// </remarks>
        public bool RestartCaptureService()
        {
            StopFillArchive();
            var success = StartCaptureService(GetPresentMonStartInfo());
            StartFillArchive();

            _logger.LogInformation("Capture service restarted, success: {success}", success);
            return success;
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

        // Collects PMD samples from before the capture start on. The capture window is cut out by
        // the sample times when the file is written, so the subscription neither has to start at
        // the first frame nor to stop at the last one.
        private void StartPmdLogging()
        {
            StopPmdLogging();

            if (!_appConfiguration.UsePmdDataLogging)
                return;

            lock (_pmdSamplesLock)
            {
                _pmdSampleTimes = new List<double>();
                _pmdDataGpuPower = new List<float>();
                _pmdDataCpuPower = new List<float>();
                _pmdDataSystemPower = new List<float>();
            }

            // Create schedulers that we can dispose later
            _poweneticsScheduler = new EventLoopScheduler();
            _benchlabScheduler = new EventLoopScheduler();

            _disposablePoweneticsDataStream = _poweneticsService.PmdChannelStream
                .ObserveOn(_poweneticsScheduler)
                .Subscribe(channels => FillPmdDataLists(channels));

            _disposableBenchlabDataStream = _benchlabService.PmdSensorStream
                .ObserveOn(_benchlabScheduler)
                .Subscribe(sensorSample => FillPmdDataLists(sensorSample));
        }

        private void StopPmdLogging()
        {
            _disposablePoweneticsDataStream?.Dispose();
            _disposableBenchlabDataStream?.Dispose();
            _disposablePoweneticsDataStream = null;
            _disposableBenchlabDataStream = null;
            _poweneticsScheduler?.Dispose();
            _benchlabScheduler?.Dispose();
            _poweneticsScheduler = null;
            _benchlabScheduler = null;
        }

        private void FillPmdDataLists(PoweneticsChannel[] channel)
        {
            // The time stamp is the start of the downsampling bin; the sample stands for its centre.
            AddPmdSample((channel[0].TimeStamp + 0.5 * _poweneticsService.DownSamplingSize) * 1E-03,
                PoweneticsChannelExtensions.GPUPowerIndexGroup.Sum(index => channel[index].Value),
                PoweneticsChannelExtensions.EPSPowerIndexGroup.Sum(index => channel[index].Value),
                PoweneticsChannelExtensions.SystemPowerIndexGroup.Sum(index => channel[index].Value));
        }

        private void FillPmdDataLists(SensorSample sensorSample)
        {
            AddPmdSample(sensorSample.TimeStamp * 1E-03,
                (float)sensorSample.Sensors[_benchlabService.GpuPowerSensorIndex].Value,
                (float)sensorSample.Sensors[_benchlabService.CpuPowerSensorIndex].Value,
                (float)sensorSample.Sensors[_benchlabService.SytemPowerSensorIndex].Value);
        }

        private void AddPmdSample(double time, float gpuPower, float cpuPower, float systemPower)
        {
            lock (_pmdSamplesLock)
            {
                _pmdSampleTimes.Add(time);
                _pmdDataGpuPower.Add(gpuPower);
                _pmdDataCpuPower.Add(cpuPower);
                _pmdDataSystemPower.Add(systemPower);
            }
        }

        /// <summary>
        /// Puts the PMD samples on the grid of the capture file, where value i belongs to
        /// i * SampleTime after the first frame. The sample times are on the QPC clock, like the
        /// frame times, so the series lines up with the frames whatever rate the device ran at.
        /// </summary>
        private void AddPmdData(ISessionRun sessionRun, double captureStartTime, double captureLengthMs)
        {
            double[] times;
            float[] gpuPower, cpuPower, systemPower;
            lock (_pmdSamplesLock)
            {
                times = _pmdSampleTimes.ToArray();
                gpuPower = _pmdDataGpuPower.ToArray();
                cpuPower = _pmdDataCpuPower.ToArray();
                systemPower = _pmdDataSystemPower.ToArray();
            }

            if (times.Length == 0)
                return;

            int count = (int)(captureLengthMs / sessionRun.SampleTime) + 1;
            double step = sessionRun.SampleTime * 1E-03;

            sessionRun.PmdGpuPower = PmdTimeSeriesResampler.Resample(times, gpuPower, captureStartTime, step, count);
            sessionRun.PmdCpuPower = PmdTimeSeriesResampler.Resample(times, cpuPower, captureStartTime, step, count);
            sessionRun.PmdSystemPower = PmdTimeSeriesResampler.Resample(times, systemPower, captureStartTime, step, count);

            double first = times[0] - captureStartTime;
            double last = times[^1] - captureStartTime;
            double captureEnd = (count - 1) * step;
            // BENCHLAB samples are about one step apart, so its first one may come a step late.
            double slack = Math.Max(2 * step, 0.25);

            _logEntryManager.AddLogEntry(string.Format(CultureInfo.InvariantCulture,
                "PMD: {0} samples from {1:F2} s to {2:F2} s, stored as {3} values every {4} ms",
                times.Length, first, last, count, sessionRun.SampleTime), ELogMessageType.AdvancedInfo, false);

            if (first > slack || last < captureEnd - slack)
            {
                _logEntryManager.AddLogEntry(string.Format(CultureInfo.InvariantCulture,
                    "PMD data only covers {0:F2} s to {1:F2} s of the capture; the uncovered part repeats the nearest value.",
                    first, last), ELogMessageType.BasicInfo, false);
            }
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
                        sessionRun.SampleTime = _poweneticsService.DownSamplingSize;
                    else if (_benchlabService.IsServiceRunning)
                        sessionRun.SampleTime = _benchlabService.MonitoringInterval;

                    // Time 0 of the file is the first frame NormalizeTimes kept.
                    if (sessionRun.SampleTime > 0)
                        AddPmdData(sessionRun, GetCpuStartQpcFromDataLine(adjustedCaptureData[1]), finalCaptureTime);
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

        /// <summary>
        /// Rebases every frame's CPUStartQPCTime onto the first frame of the capture.
        ///
        /// Deliberately writes into fresh strings instead of back into the caller's rows: those
        /// rows are the live capture buffer, and normalizing one twice would subtract the start
        /// time twice. A row that reaches this method more than once used to come out at
        /// -k * startTime — a capture full of timestamps hours before the run, which no longer
        /// loads. Reading the input read-only makes duplicates stay duplicates instead of
        /// turning into corruption.
        /// </summary>
        internal IEnumerable<string> NormalizeTimes(IEnumerable<string[]> recordLines)
        {
            int timeIndex = _captureService.CPUStartQPCTimeInMs_Index;
            string[] firstLineSplit = recordLines.First();
            var lines = new List<string>();
            //start time
            var timeStart = GetCpuStartQpcFromDataLine(firstLineSplit);

            // normalize time
            double previousNormalizedTime = 0;
            double delta = 0;

            lines.Add(JoinWithReplacedField(firstLineSplit, timeIndex, "0"));

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

                lines.Add(JoinWithReplacedField(lineSplit, timeIndex,
                    (1E03 * (normalizedTime + delta)).ToString(CultureInfo.InvariantCulture)));
            }

            return lines;
        }

        // Joins a data line, substituting one field, without touching the source row.
        private static string JoinWithReplacedField(string[] lineSplit, int index, string value)
        {
            var line = new StringBuilder();
            for (int i = 0; i < lineSplit.Length; i++)
            {
                if (i > 0) line.Append(',');
                line.Append(i == index ? value : lineSplit[i]);
            }
            return line.ToString();
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
