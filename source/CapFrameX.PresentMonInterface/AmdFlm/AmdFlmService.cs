using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Latency;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.Sensor;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

namespace CapFrameX.PresentMonInterface.AmdFlm
{
    public sealed class AmdFlmService : IAmdFlmService
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(4);

        private readonly IAppConfiguration _appConfiguration;
        // Lazy: SensorService itself depends on IAmdFlmService, a direct
        // ISensorService constructor dependency would be circular.
        private readonly Lazy<ISensorService> _sensorService;
        private readonly ILogger<AmdFlmService> _logger;
        private readonly Subject<AmdFlmSample> _sampleSubject = new Subject<AmdFlmSample>();
        private readonly EventLoopScheduler _lifecycleScheduler;
        private readonly IDisposable _configurationSubscription;
        private readonly object _lifecycleLock = new object();

        private AmdFlmNative.SessionHandle _session;
        private CancellationTokenSource _pollCancellation;
        private Task _pollTask;
        private AmdFlmNative.Config? _activeConfig;
        private readonly BehaviorSubject<AmdFlmStatus> _statusSubject = new BehaviorSubject<AmdFlmStatus>(
            new AmdFlmStatus(AmdFlmState.Disabled, "AMD FLM is disabled."));
        private bool? _isAmdGpuSystem;
        private volatile bool _isRunning;
        private bool _disposed;

        public IObservable<AmdFlmSample> SampleStream => _sampleSubject.AsObservable();

        public bool IsRunning => _isRunning;

        public IObservable<AmdFlmStatus> StatusStream => _statusSubject.AsObservable();

        public AmdFlmStatus Status => _statusSubject.Value;

        private void PublishStatus(AmdFlmStatus status) => _statusSubject.OnNext(status);

        public string LastError { get; private set; } = string.Empty;

        public AmdFlmService(
            IAppConfiguration appConfiguration,
            Lazy<ISensorService> sensorService,
            ILogger<AmdFlmService> logger)
        {
            _appConfiguration = appConfiguration;
            _sensorService = sensorService;
            _logger = logger;
            _lifecycleScheduler = new EventLoopScheduler(start =>
            {
                var thread = new Thread(start)
                {
                    IsBackground = true,
                    Name = "CapFrameX FLM lifecycle"
                };
                return thread;
            });

            var relevantConfigurationChanges = _appConfiguration.OnValueChanged
                .Where(change => AmdFlmSettings.IsConfigurationKey(change.key))
                .Throttle(TimeSpan.FromMilliseconds(300))
                .Select(_ => Unit.Default);

            _configurationSubscription = Observable.Return(Unit.Default)
                .Concat(relevantConfigurationChanges)
                .ObserveOn(_lifecycleScheduler)
                .Subscribe(_ => ApplyConfiguration(), exception =>
                    _logger.LogError(exception, "FLM configuration subscription failed"));
        }

        private void ApplyConfiguration()
        {
            if (!_appConfiguration.UseAmdFlmLatency)
            {
                StopNativeSession();
                LastError = string.Empty;
                PublishStatus(new AmdFlmStatus(AmdFlmState.Disabled, "AMD FLM is disabled."));
                return;
            }

            if (!IsAmdGpuSystem())
            {
                // A copied AppSettings.json can carry the option onto a non-AMD system.
                // Reset it so the overlay entry and sensor stay disabled consistently.
                _logger.LogInformation("AMD FLM latency measurement is only supported on AMD GPUs, disabling the option");
                _appConfiguration.UseAmdFlmLatency = false;
                StopNativeSession();
                return;
            }

            var config = AmdFlmNative.Config.Create(AmdFlmSettings.FromConfiguration(_appConfiguration));
            if (_session != null && _isRunning && _activeConfig.HasValue && _activeConfig.Value.Equals(config))
                return;

            StopNativeSession();
            StartNativeSession(config);
        }

        private bool IsAmdGpuSystem()
        {
            if (!_isAmdGpuSystem.HasValue)
            {
                // Runs on the dedicated lifecycle thread, so blocking until the hardware
                // monitor has finished initializing is safe; before that point the vendor
                // reads as Unknown and an AMD system would wrongly be rejected.
                var sensorService = _sensorService.Value;
                sensorService.SensorServiceCompletionSource.Task.GetAwaiter().GetResult();
                _isAmdGpuSystem = sensorService.GetGpuVendor() == EGpuVendor.Amd;
            }

            return _isAmdGpuSystem.Value;
        }

        private void StartNativeSession(AmdFlmNative.Config config)
        {
            lock (_lifecycleLock)
            {
                if (_disposed || _session != null)
                    return;

                AmdFlmNative.SessionHandle pendingSession = null;
                CancellationTokenSource pendingPollCancellation = null;
                try
                {
                    PublishStatus(new AmdFlmStatus(AmdFlmState.Starting, "Starting screen capture..."));
                    int status = AmdFlmNative.FlmCreate(ref config, out IntPtr nativeHandle);
                    if (status != AmdFlmNative.Ok || nativeHandle == IntPtr.Zero)
                    {
                        string error = AmdFlmNative.GetLastError();
                        if (nativeHandle != IntPtr.Zero)
                            new AmdFlmNative.SessionHandle(nativeHandle).Dispose();
                        SetStartError(status, error);
                        return;
                    }

                    pendingSession = new AmdFlmNative.SessionHandle(nativeHandle);
                    status = AmdFlmNative.FlmStart(pendingSession);
                    if (status != AmdFlmNative.Ok)
                    {
                        string error = AmdFlmNative.GetLastError(pendingSession);
                        pendingSession.Dispose();
                        SetStartError(status, error);
                        return;
                    }

                    pendingPollCancellation = new CancellationTokenSource();
                    AmdFlmNative.SessionHandle activeSession = pendingSession;
                    CancellationTokenSource activePollCancellation = pendingPollCancellation;
                    LastError = string.Empty;
                    _isRunning = true;
                    Task pollTask = Task.Run(() =>
                        PollSamplesAsync(activeSession, activePollCancellation.Token));

                    _session = activeSession;
                    _pollCancellation = activePollCancellation;
                    _pollTask = pollTask;
                    pendingSession = null;
                    pendingPollCancellation = null;
                    _activeConfig = config;
                    _logger.LogInformation(
                        "AMD FLM started (codec={Codec}, dx12={Dx12}, output={Output}, region={X},{Y},{Width},{Height}, threshold={Threshold})",
                        config.Codec, config.InitAmfUsingDx12, config.CaptureOutputIndex,
                        config.CaptureStartX, config.CaptureStartY, config.CaptureWidth, config.CaptureHeight, config.ThresholdCoefficient);
                }
                catch (Exception exception) when (exception is DllNotFoundException ||
                                                  exception is EntryPointNotFoundException ||
                                                  exception is BadImageFormatException)
                {
                    pendingPollCancellation?.Cancel();
                    pendingPollCancellation?.Dispose();
                    pendingSession?.Dispose();
                    _isRunning = false;
                    LastError = exception.Message;
                    PublishStatus(new AmdFlmStatus(AmdFlmState.Error,
                        "FLM runtime is missing or incompatible. Reinstall CapFrameX. " + LastError));
                    _logger.LogError(exception, "CapFrameX.FLM.dll could not be loaded");
                }
                catch (Exception exception)
                {
                    pendingPollCancellation?.Cancel();
                    pendingPollCancellation?.Dispose();
                    pendingSession?.Dispose();
                    _isRunning = false;
                    LastError = exception.Message;
                    PublishStatus(new AmdFlmStatus(AmdFlmState.Error, LastError));
                    _logger.LogError(exception, "AMD FLM latency measurement failed to start");
                }
            }
        }

        private async Task PollSamplesAsync(AmdFlmNative.SessionHandle session, CancellationToken cancellationToken)
        {
            try
            {
                bool firstSampleLogged = false;
                long nextDiagnostics = 0;
                AmdFlmState? previousState = null;
                while (!cancellationToken.IsCancellationRequested)
                {
                    int status;
                    do
                    {
                        var nativeSample = AmdFlmNative.Sample.Create();
                        status = AmdFlmNative.FlmTryGetSample(session, ref nativeSample);
                        if (status == AmdFlmNative.Ok)
                        {
                            if (!firstSampleLogged)
                            {
                                firstSampleLogged = true;
                                _logger.LogInformation(
                                    "AMD FLM first latency sample received: {LatencyMs} ms",
                                    nativeSample.LatencyMs);
                            }

                            _sampleSubject.OnNext(new AmdFlmSample(
                                nativeSample.Sequence,
                                nativeSample.InputQpc,
                                nativeSample.FrameQpc,
                                nativeSample.LatencyMs,
                                nativeSample.LatencyFrames,
                                nativeSample.Fps));
                        }
                    }
                    while (status == AmdFlmNative.Ok && !cancellationToken.IsCancellationRequested);

                    if (cancellationToken.IsCancellationRequested)
                        break;

                    if (status == AmdFlmNative.NoSample && Stopwatch.GetTimestamp() >= nextDiagnostics)
                    {
                        var diagnostics = AmdFlmNative.Diagnostics.Create();
                        int diagnosticStatus = AmdFlmNative.FlmGetDiagnostics(session, ref diagnostics);
                        if (diagnosticStatus == AmdFlmNative.Ok)
                        {
                            var currentStatus = diagnostics.ToStatus();
                            PublishStatus(currentStatus);
                            if (currentStatus.State != previousState)
                            {
                                _logger.LogDebug("AMD FLM {State}: frames={Frames}, clicks={Clicks}, rejected={Rejected}, timeouts={Timeouts}",
                                    currentStatus.State, currentStatus.Frames, currentStatus.Clicks,
                                    currentStatus.RejectedClicks, currentStatus.Timeouts);
                                previousState = currentStatus.State;
                            }
                        }
                        else
                            status = diagnosticStatus;
                        nextDiagnostics = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 4;
                    }

                    if (status != AmdFlmNative.NoSample)
                    {
                        LastError = AmdFlmNative.GetLastError(session);
                        _isRunning = false;
                        PublishStatus(new AmdFlmStatus(AmdFlmState.Error,
                            string.IsNullOrWhiteSpace(LastError) ? $"FLM stopped (status {status}). Toggle FLM to retry." : LastError));
                        _logger.LogError("AMD FLM sample polling stopped with status {Status}: {Error}", status, LastError);
                        return;
                    }

                    await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                _isRunning = false;
                LastError = exception.Message;
                PublishStatus(new AmdFlmStatus(AmdFlmState.Error, LastError));
                _logger.LogError(exception, "AMD FLM sample polling failed");
            }
        }

        private void StopNativeSession()
        {
            lock (_lifecycleLock)
            {
                if (_session == null)
                {
                    _isRunning = false;
                    _activeConfig = null;
                    return;
                }

                _pollCancellation?.Cancel();
                try
                {
                    _pollTask?.GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                }

                int status = AmdFlmNative.FlmStop(_session);
                if (status != AmdFlmNative.Ok)
                    _logger.LogWarning("AMD FLM stop returned status {Status}", status);

                _pollCancellation?.Dispose();
                _pollCancellation = null;
                _pollTask = null;
                _session.Dispose();
                _session = null;
                _activeConfig = null;
                _isRunning = false;
                _logger.LogInformation("AMD FLM continuous latency measurement stopped");
            }
        }

        private void SetStartError(int status, string error)
        {
            _isRunning = false;
            LastError = string.IsNullOrWhiteSpace(error) ? $"Native status {status}" : error;
            PublishStatus(new AmdFlmStatus(AmdFlmState.Error, LastError));
            _logger.LogError("AMD FLM latency measurement failed to start with status {Status}: {Error}", status, LastError);
        }

        public void Dispose()
        {
            lock (_lifecycleLock)
            {
                if (_disposed)
                    return;
                _disposed = true;
            }

            _configurationSubscription.Dispose();
            StopNativeSession();
            _lifecycleScheduler.Dispose();
            _sampleSubject.OnCompleted();
            _sampleSubject.Dispose();
            _statusSubject.OnCompleted();
            _statusSubject.Dispose();
        }
    }
}
