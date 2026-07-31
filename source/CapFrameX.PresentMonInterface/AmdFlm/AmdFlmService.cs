using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Latency;
using Microsoft.Extensions.Logging;
using System;
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
        private readonly ILogger<AmdFlmService> _logger;
        private readonly Subject<AmdFlmSample> _sampleSubject = new Subject<AmdFlmSample>();
        private readonly EventLoopScheduler _lifecycleScheduler;
        private readonly IDisposable _configurationSubscription;
        private readonly object _lifecycleLock = new object();

        private AmdFlmNative.SessionHandle _session;
        private CancellationTokenSource _pollCancellation;
        private Task _pollTask;
        private bool? _activeFrameGeneration;
        private volatile bool _isRunning;
        private bool _disposed;

        public IObservable<AmdFlmSample> SampleStream => _sampleSubject.AsObservable();

        public bool IsRunning => _isRunning;

        public string LastError { get; private set; } = string.Empty;

        public AmdFlmService(
            IAppConfiguration appConfiguration,
            ILogger<AmdFlmService> logger)
        {
            _appConfiguration = appConfiguration;
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
                .Where(change => change.key == nameof(IAppConfiguration.UseAmdFlmLatency)
                    || change.key == nameof(IAppConfiguration.AmdFlmFrameGeneration))
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
                return;
            }

            bool useFrameGenerationTiming = _appConfiguration.AmdFlmFrameGeneration;
            if (_session != null && _activeFrameGeneration == useFrameGenerationTiming)
                return;

            StopNativeSession();
            StartNativeSession(useFrameGenerationTiming);
        }

        private void StartNativeSession(bool useFrameGenerationTiming)
        {
            lock (_lifecycleLock)
            {
                if (_disposed || _session != null)
                    return;

                AmdFlmNative.SessionHandle pendingSession = null;
                CancellationTokenSource pendingPollCancellation = null;
                try
                {
                    var config = AmdFlmNative.Config.CreateDefault(useFrameGenerationTiming);
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
                    _activeFrameGeneration = useFrameGenerationTiming;
                    _logger.LogInformation(
                        "AMD FLM continuous latency measurement started (frameGeneration={FrameGeneration})",
                        useFrameGenerationTiming);
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
                    _logger.LogError(exception, "CapFrameX.FLM.dll could not be loaded");
                }
                catch (Exception exception)
                {
                    pendingPollCancellation?.Cancel();
                    pendingPollCancellation?.Dispose();
                    pendingSession?.Dispose();
                    _isRunning = false;
                    LastError = exception.Message;
                    _logger.LogError(exception, "AMD FLM latency measurement failed to start");
                }
            }
        }

        private async Task PollSamplesAsync(AmdFlmNative.SessionHandle session, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    int status;
                    do
                    {
                        var nativeSample = AmdFlmNative.Sample.Create();
                        status = AmdFlmNative.FlmTryGetSample(session, ref nativeSample);
                        if (status == AmdFlmNative.Ok)
                        {
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

                    if (status != AmdFlmNative.NoSample)
                    {
                        LastError = AmdFlmNative.GetLastError(session);
                        _isRunning = false;
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
                    _activeFrameGeneration = null;
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
                _activeFrameGeneration = null;
                _isRunning = false;
                _logger.LogInformation("AMD FLM continuous latency measurement stopped");
            }
        }

        private void SetStartError(int status, string error)
        {
            _isRunning = false;
            LastError = string.IsNullOrWhiteSpace(error) ? $"Native status {status}" : error;
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
        }
    }
}
