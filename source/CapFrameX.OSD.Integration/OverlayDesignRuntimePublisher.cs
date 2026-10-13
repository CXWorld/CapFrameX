using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using CapFrameX.Contracts.Localization;
using CapFrameX.Contracts.Configuration;
using Serilog;

namespace CapFrameX.OSD.Integration
{
    public sealed class OverlayDesignRuntimeSnapshot
    {
        internal OverlayDesignRuntimeSnapshot(OverlayRuntimeDesign design, string metrics, ulong designRevision, ulong metricsRevision)
        {
            Design = design;
            MetricsJson = metrics;
            DesignRevision = designRevision;
            MetricsRevision = metricsRevision;
        }
        public OverlayRuntimeDesign Design { get; }
        public string MetricsJson { get; }
        public ulong DesignRevision { get; }
        public ulong MetricsRevision { get; }
    }

    /// <summary>One demand-scoped telemetry feed for both the hook-free and in-game saved design.</summary>
    public sealed class OverlayDesignRuntimePublisher : IDisposable
    {
        private readonly OverlayDesignService _designs;
        private readonly IOverlayTelemetryService _telemetry;
        private readonly IAppConfiguration _configuration;
        private readonly Func<int, bool> _targetAllowed;
        private readonly Func<IHookDesignChannel> _createChannel;
        private readonly object _gate = new object();
        private readonly CompositeDisposable _subscriptions = new CompositeDisposable();
        private readonly BehaviorSubject<OverlayDesignRuntimeSnapshot> _updates;
        private volatile OverlayDesignRuntimeSnapshot _current;
        private volatile OverlayRuntimeDesign _observedDesign;
        private IHookDesignChannel _channel;
        private IDisposable _demand, _snapshotSubscription;
        private IReadOnlyDictionary<string, string> _aliases = new Dictionary<string, string>();
        private HashSet<string> _hardwareSourceIds = new HashSet<string>(StringComparer.Ordinal);
        private HashSet<string> _processIndependentSourceIds = new HashSet<string>(StringComparer.Ordinal);
        private IReadOnlyList<OverlayTelemetrySource> _catalogSources, _boundCatalog;
        private OverlayRuntimeDesign _boundSourceDesign, _boundDesign;
        private OverlayTelemetrySnapshot _latestTelemetry;
        private int _pid, _catalogGeneration;
        private bool _active, _hookEnabled, _freeEnabled, _hasProcesses, _disposed;
        private bool _channelFailureReported;
        private bool _telemetryFailureReported;
        private ulong _designRevision, _metricsRevision;
        private DateTime _targetChangedUtc;

        public OverlayDesignRuntimePublisher(OverlayDesignService designs, IOverlayTelemetryService telemetry,
            IAppConfiguration configuration, IObservable<int> processIdStream,
            IObservable<int> processCountStream = null)
            : this(designs, telemetry, configuration, processIdStream, processCountStream,
                  pid => HookTargetPolicy.IsAllowed(pid, out _), () => HookDesignChannel.Create(),
                  Observable.Interval(TimeSpan.FromMilliseconds(500)))
        {
        }

        internal OverlayDesignRuntimePublisher(OverlayDesignService designs, IOverlayTelemetryService telemetry,
            IAppConfiguration configuration, IObservable<int> processIdStream, IObservable<int> processCountStream,
            Func<int, bool> targetAllowed,
            Func<IHookDesignChannel> createChannel, IObservable<long> heartbeat)
        {
            _designs = designs ?? throw new ArgumentNullException(nameof(designs));
            _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _targetAllowed = targetAllowed ?? throw new ArgumentNullException(nameof(targetAllowed));
            _createChannel = createChannel ?? throw new ArgumentNullException(nameof(createChannel));
            if (processIdStream == null) throw new ArgumentNullException(nameof(processIdStream));
            _active = configuration.IsOverlayActive;
            _hookEnabled = configuration.EnableHookOverlay;
            _freeEnabled = configuration.EnableHookFreeOverlay;
            _hasProcesses = processCountStream == null;
            // Readers can retain the named section while CapFrameX restarts. A writer epoch
            // from the machine-wide monotonic clock prevents its first revision matching the
            // previous process's cached scene/values.
            _designRevision = _metricsRevision = unchecked((ulong)Stopwatch.GetTimestamp());
            _current = new OverlayDesignRuntimeSnapshot(null, "{}", 0, 0);
            _updates = new BehaviorSubject<OverlayDesignRuntimeSnapshot>(_current);
            _designs.Changed += OnDesignChanged;
            _subscriptions.Add(processIdStream.DistinctUntilChanged().Subscribe(pid => Change(() =>
            {
                int previous = _pid;
                _pid = Math.Max(0, pid);
                if (previous > 0 && previous != _pid) HookTargetPolicy.Invalidate(previous);
                _targetChangedUtc = DateTime.UtcNow;
                _channel?.Disable();
            })));
            if (processCountStream != null)
                _subscriptions.Add(processCountStream.Subscribe(count => Change(() => _hasProcesses = count > 0)));
            _subscriptions.Add(configuration.OnValueChanged.Where(change => change.key == nameof(IAppConfiguration.IsOverlayActive)
                || change.key == nameof(IAppConfiguration.EnableHookOverlay) || change.key == nameof(IAppConfiguration.EnableHookFreeOverlay))
                .Subscribe(change => Change(() =>
                {
                    if (change.key == nameof(IAppConfiguration.IsOverlayActive)) _active = (bool)change.value;
                    if (change.key == nameof(IAppConfiguration.EnableHookOverlay)) _hookEnabled = (bool)change.value;
                    if (change.key == nameof(IAppConfiguration.EnableHookFreeOverlay)) _freeEnabled = (bool)change.value;
                })));
            _subscriptions.Add(heartbeat.Subscribe(_ => Change(null)));
            Change(null);
        }

        public OverlayDesignRuntimeSnapshot Current => _current;
        public IObservable<OverlayDesignRuntimeSnapshot> Snapshots => _updates.AsObservable();

        internal void ReportRendererError(string message) => _designs.ReportRuntimeError(message);
        internal void ReportRendererError(LocalizedText message) => _designs.ReportRuntimeError(message.Resolve);
        internal void ReportRendererReady()
        {
            lock (_gate)
            {
                // Applying an empty restart snapshot does not recover a failed sensor stream.
                if (_disposed || _telemetryFailureReported) return;
                _designs.ReportRuntimeReady();
            }
        }

        private void OnDesignChanged(object sender, EventArgs e)
        {
            // Runtime status notifications use the same UI event; they do not change the
            // scene and must not recursively retry a failed renderer in its own callback.
            if (!ReferenceEquals(_observedDesign, _designs.CurrentDesign)) Change(null);
        }

        private void Change(Action mutation)
        {
            Func<string> errorMessage = null;
            bool recovered = false;
            lock (_gate)
            {
                if (_disposed) return;
                try
                {
                    mutation?.Invoke();
                    var design = _designs.CurrentDesign;
                    _observedDesign = design;
                    bool hookVisible = design != null && _active && _hookEnabled && _pid > 0 && _targetAllowed(_pid);
                    // Hardware sampling belongs to the enabled tile overlay, not to the
                    // currently detected game. Loading screens can temporarily lose the PID
                    // or process count without making the machine's sensors unavailable.
                    bool needsTelemetry = design != null && _active && (_hookEnabled || _freeEnabled);
                    if (needsTelemetry)
                    {
                        if (_demand == null)
                        {
                            _demand = _telemetry.AcquirePreview();
                            try
                            {
                                _snapshotSubscription = _telemetry.Snapshots.Subscribe(OnTelemetry, OnTelemetryError);
                                if (_demand == null)
                                    throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_StreamStart"), message => new InvalidOperationException(message));
                            }
                            catch
                            {
                                _snapshotSubscription?.Dispose();
                                _snapshotSubscription = null;
                                _demand?.Dispose();
                                _demand = null;
                                throw;
                            }
                            _ = RefreshCatalogAsync(++_catalogGeneration);
                        }
                    }
                    else
                    {
                        StopDemand();
                    }
                    UpdateSnapshot(design);
                    if (hookVisible)
                    {
                        if (_channel == null) _channel = _createChannel();
                        _channel.Publish(_pid, _current.Design.TemplateJson, _current.MetricsJson,
                            _current.DesignRevision, _current.MetricsRevision);
                        recovered = _channelFailureReported;
                        _channelFailureReported = false;
                    }
                    else
                    {
                        _channel?.Disable();
                        _channel?.Dispose();
                        _channel = null;
                    }
                    if (_telemetryFailureReported && _demand != null && _latestTelemetry != null
                        && _latestTelemetry.TimestampUtc <= DateTime.UtcNow.AddSeconds(1))
                    {
                        // A reconnected stream has recovered only after it delivers a sample.
                        // This applies to hook-free publication as well as the in-game channel.
                        _telemetryFailureReported = false;
                        recovered = true;
                    }
                    // Clear the recovered error before notifying consumers. A renderer can
                    // reject this update synchronously and its newer error must remain visible.
                    if (recovered) _designs.ReportRuntimeReady();
                    _updates.OnNext(_current);
                }
                catch (Exception error)
                {
                    try { _channel?.Disable(); } catch (Exception) { }
                    if (!_channelFailureReported)
                        Log.Warning(error, "Overlay designer: runtime publication failed; classic overlay remains available");
                    _channelFailureReported = true;
                    errorMessage = () => CxLang.Format("OverlayDesignStatus_PublishFailed", OverlayDesignText.ExceptionMessage(error));
                }
            }
            if (errorMessage != null) _designs.ReportRuntimeError(errorMessage);
        }

        private async Task RefreshCatalogAsync(int generation)
        {
            try
            {
                var sources = await _telemetry.GetSourcesAsync().ConfigureAwait(false);
                var aliases = sources.Where(source => !string.IsNullOrEmpty(source.SemanticKey) && source.IsAvailable)
                    .GroupBy(source => source.SemanticKey, StringComparer.Ordinal).Where(group => group.Count() == 1)
                    .ToDictionary(group => group.Key, group => group.Single().Id, StringComparer.Ordinal);
                var hardwareSourceIds = sources.Where(source => source.IsHardwareSensor
                        || (source.SumSourceIdA != null && source.SumSourceIdB != null))
                    .Select(source => source.Id).ToHashSet(StringComparer.Ordinal);
                var processIndependentSourceIds = sources.Where(source => !source.IsProcessSpecific)
                    .Select(source => source.Id).ToHashSet(StringComparer.Ordinal);
                // A cached catalog may complete inside Change. That call will publish the
                // bound scene itself; recursively publishing here would retry a failed
                // channel twice before its next heartbeat and erase the first error.
                bool publishAfterDiscovery = !Monitor.IsEntered(_gate);
                lock (_gate)
                {
                    if (_disposed || generation != _catalogGeneration || _demand == null) return;
                    _aliases = aliases;
                    _hardwareSourceIds = hardwareSourceIds;
                    _processIndependentSourceIds = processIndependentSourceIds;
                    _catalogSources = sources;
                }
                if (publishAfterDiscovery) Change(null);
            }
            catch (Exception error) { Log.Warning(error, "Overlay designer: runtime telemetry discovery failed"); }
        }

        private void OnTelemetry(OverlayTelemetrySnapshot snapshot)
        {
            lock (_gate)
            {
                if (_disposed || _demand == null) return;
                _latestTelemetry = snapshot;
            }
            Change(null);
        }

        private void OnTelemetryError(Exception error)
        {
            lock (_gate)
            {
                if (_disposed) return;
                StopDemand();
                _telemetryFailureReported = true;
                _channel?.Disable();
            }
            _designs.ReportRuntimeError(() => CxLang.Format("OverlayDesignStatus_StreamStopped", OverlayDesignText.ExceptionMessage(error)));
        }

        private void UpdateSnapshot(OverlayRuntimeDesign design)
        {
            if (!ReferenceEquals(_boundSourceDesign, design) || !ReferenceEquals(_boundCatalog, _catalogSources))
            {
                var bound = OverlayRuntimeSourceBinder.Bind(design, _catalogSources);
                _boundSourceDesign = design;
                _boundCatalog = _catalogSources;
                _boundDesign = bound;
            }
            design = _boundDesign;
            bool changed = _current.Design?.ProfileId != design?.ProfileId || _current.Design?.TemplateJson != design?.TemplateJson;
            if (changed) ++_designRevision;
            string metrics = design?.CreateMetrics(GetPublishableTelemetry(), _aliases) ?? "{}";
            if (changed || metrics != _current.MetricsJson || _metricsRevision == 0) ++_metricsRevision;
            _current = new OverlayDesignRuntimeSnapshot(design, metrics, _designRevision, _metricsRevision);
        }

        private OverlayTelemetrySnapshot GetPublishableTelemetry()
        {
            var snapshot = _latestTelemetry;
            var now = DateTime.UtcNow;
            if (snapshot == null || snapshot.TimestampUtc > now.AddSeconds(1)) return null;
            bool expired = now - snapshot.TimestampUtc > TimeSpan.FromSeconds(3);
            if (_hasProcesses && snapshot.TimestampUtc >= _targetChangedUtc
                && !expired) return snapshot;

            // A slow/background hardware poll leaves its last completed sample valid until
            // the next poll, just as in SensorService. Fresh machine-wide readings (PMD,
            // battery, system information) also survive target changes, while game metrics
            // cannot cross targets. Never merge samples: an explicitly missing/NaN
            // reading in the next snapshot must still replace the previous sensor value.
            var retainedSources = expired ? _hardwareSourceIds : _processIndependentSourceIds;
            var values = snapshot.Values.Where(value => retainedSources.Contains(value.Key))
                .ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal);
            var textValues = snapshot.MetricValues.Where(value => value.Value is string && retainedSources.Contains(value.Key))
                .ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal);
            return new OverlayTelemetrySnapshot(snapshot.TimestampUtc, values, textValues);
        }

        private void StopDemand()
        {
            ++_catalogGeneration;
            _snapshotSubscription?.Dispose(); _snapshotSubscription = null;
            _demand?.Dispose(); _demand = null;
            _latestTelemetry = null;
        }

        public void Dispose()
        {
            _designs.Changed -= OnDesignChanged;
            _subscriptions.Dispose();
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                StopDemand();
                _channel?.Dispose(); _channel = null;
                _updates.OnCompleted();
                _updates.Dispose();
            }
        }
    }
}
