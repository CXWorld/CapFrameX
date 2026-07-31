using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Latency;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.RTSS;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Data;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Extensions.NetStandard;
using CapFrameX.Monitoring.Contracts;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Hardware.Gpu;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace CapFrameX.Sensor
{
    public class SensorService : ISensorService
    {
        private GpuSensorCache _gpuSensorCache;
        private readonly object _lockComputer = new object();
        private readonly object _lockSensorUpdate = new object();
        private readonly ISensorConfig _sensorConfig;
        private readonly IRTSSService _rTSSService;
        private readonly IAppConfiguration _appConfiguration;
        private readonly ILogger<SensorService> _logger;
        private readonly IDisposable _logDisposable;
        private readonly Task<IPmcReaderSensorPlugin> _pmcReaderInitializationTask;
        private readonly AmdFlmSensorSource _amdFlmSensorSource;

        private Computer _computer;
        private SessionSensorDataLive _sessionSensorDataLive;
        private bool _isLoggingActive = false;
        private bool _isServiceAlive = true;

        // Upper bound for the LibreHardwareMonitor computer teardown at shutdown. Vendor GPU
        // libraries (AMD ADLX, Intel IGCL) can deadlock in their COM teardown on some systems;
        // if Computer.Close() overruns this, we abandon it so the application still exits.
        private static readonly TimeSpan _shutdownComputerTimeout = TimeSpan.FromSeconds(3);

        private ISubject<TimeSpan> _sensorUpdateSubject;
        private ISubject<TimeSpan> _osdUpdateSubject;
        private ISubject<TimeSpan> _loggingUpdateSubject;
        private TimeSpan _currentLoggingTimespan;
        private TimeSpan _currentOSDTimespan;

        private TimeSpan CurrentSensorTimespan
        {
            get
            {
                if (_currentLoggingTimespan < _currentOSDTimespan)
                {
                    return _currentLoggingTimespan;
                }
                return _currentOSDTimespan;
            }
        }

        public IObservable<(DateTime, Dictionary<ISensorEntry, float>)> SensorSnapshotStream { get; private set; }

        public IObservable<TimeSpan> OsdUpdateStream => _osdUpdateSubject.AsObservable();

        public Subject<bool> IsLoggingActiveStream { get; }

        public bool UseSensorLogging => _appConfiguration.UseSensorLogging;

        public bool IsOverlayActive => _appConfiguration.IsOverlayActive;

        public Func<bool> IsSensorWebsocketActive { get; set; } = () => false;

        public TaskCompletionSource<bool> SensorServiceCompletionSource { get; }
           = new TaskCompletionSource<bool>();

        public SensorService(IAppConfiguration appConfig, ISensorConfig sensorConfig,
            IAmdFlmService amdFlmService, IRTSSService rTSSService,
            ILogger<SensorService> logger)
        {
            _appConfiguration = appConfig;
            _sensorConfig = sensorConfig;
            _rTSSService = rTSSService;
            _logger = logger;
            _amdFlmSensorSource = new AmdFlmSensorSource(amdFlmService, appConfig);
            _currentOSDTimespan = TimeSpan.FromMilliseconds(_appConfiguration.OSDRefreshPeriod);
            _currentLoggingTimespan = TimeSpan.FromMilliseconds(_appConfiguration.SensorLoggingRefreshPeriod);
            _loggingUpdateSubject = new BehaviorSubject<TimeSpan>(_currentLoggingTimespan);
            _osdUpdateSubject = new BehaviorSubject<TimeSpan>(_currentOSDTimespan);
            _sensorUpdateSubject = new BehaviorSubject<TimeSpan>(CurrentSensorTimespan);
            IsLoggingActiveStream = new Subject<bool>();

            _sensorConfig.SensorLoggingRefreshPeriod = _appConfiguration.SensorLoggingRefreshPeriod;

            Observable.FromAsync(() => StartOpenHardwareMonitor())
               .Delay(TimeSpan.FromMilliseconds(500))
               .Subscribe(t =>
               {
                   SensorServiceCompletionSource.SetResult(true);
               });

            var coreSensorStream = _sensorUpdateSubject
               .Select(timespan => Observable.Concat(Observable.Return(-1L), Observable.Interval(timespan)))
               .Switch()
               .Where(_ => _isServiceAlive)
               .Where((_, idx) => idx == 0 || IsOverlayActive || (_isLoggingActive && UseSensorLogging) || IsSensorWebsocketActive())
               .SelectMany(_ => GetTimeStampedSensorValues());

            _pmcReaderInitializationTask = InitializePmcReaderPluginAsync();
            var pluginSensorStream = CreatePmcReaderSensorStream()
                .Where(_ => _isServiceAlive)
                .Where(_ => IsOverlayActive || (_isLoggingActive && UseSensorLogging) || IsSensorWebsocketActive());

            SensorSnapshotStream = coreSensorStream
               .CombineLatest(
                    pluginSensorStream.StartWith((DateTime.UtcNow, new Dictionary<ISensorEntry, float>())),
                    MergeSensorSnapshots)
               .Replay(0)
               .RefCount();

            _logDisposable = SensorSnapshotStream
                 .Sample(_loggingUpdateSubject.Select(timespan => Observable.Concat(Observable.Return(-1L), Observable.Interval(timespan))).Switch())
                 .Where(_ => _isServiceAlive)
                 .Where(_ => _isLoggingActive && UseSensorLogging)
                 .SubscribeOn(Scheduler.Default)
                 .Subscribe(sensorData => LogCurrentValues(sensorData.Item2, sensorData.Item1));

            _logger.LogDebug("{componentName} Ready", this.GetType().Name);
        }

        public void SetLoggingInterval(TimeSpan timeSpan)
        {
            _currentLoggingTimespan = timeSpan;
            UpdateSensorInterval();
            _loggingUpdateSubject.OnNext(timeSpan);
        }

        public void SetOSDInterval(TimeSpan timeSpan)
        {
            _currentOSDTimespan = timeSpan;
            UpdateSensorInterval();
            _osdUpdateSubject.OnNext(timeSpan);
        }

        public string GetSensorTypeString(EOverlayEntryType entryType, string stableIdentifier)
        {
            if (stableIdentifier == null)
                return string.Empty;

            // StableIdentifier format: "{HardwareName}/{sensorTypeLowercase}/{sensorName}"
            var parts = stableIdentifier.Split('/');
            if (parts.Length < 2)
                return string.Empty;

            string prefix;
            switch (entryType)
            {
                case EOverlayEntryType.CPU:
                    prefix = "CPU";
                    break;
                case EOverlayEntryType.GPU:
                    prefix = "GPU";
                    break;
                case EOverlayEntryType.HDD:
                    prefix = "Storage";
                    break;
                default:
                    return string.Empty;
            }

            var sensorSubtype = parts[1];
            switch (sensorSubtype)
            {
                case "load":
                    return $"{prefix} Load";
                case "clock":
                    return $"{prefix} Clock";
                case "power":
                    return $"{prefix} Power";
                case "temperature":
                    return $"{prefix} Temperature";
                case "voltage":
                    return $"{prefix} Voltage";
                case "throughput":
                    return $"{prefix} Throughput";
                case "factor":
                    return prefix == "GPU" ? "GPU Limits" : string.Empty;
                default:
                    return string.Empty;
            }
        }

        private Task StartOpenHardwareMonitor()
        {
            return Task.Run(() =>
            {
                try
                {
                    var simulationConfiguration = _appConfiguration.HardwareSimulationConfiguration;
                    _computer = simulationConfiguration != null
                        ? new Computer(simulationConfiguration, _sensorConfig)
                        : new Computer(_sensorConfig);
                    _computer.Open();
                    _computer.IsCpuEnabled = true;
                    _computer.IsGpuEnabled = true;
                    _computer.IsMemoryEnabled = true;
                    _computer.IsStorageEnabled = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while starting OpenHardwareMonitor");
                }
            });
        }

        private void UpdateSensorInterval()
        {
            _sensorConfig.SensorLoggingRefreshPeriod = _appConfiguration.SensorLoggingRefreshPeriod;
            _sensorUpdateSubject.OnNext(CurrentSensorTimespan);
        }

        public IEnumerable<string> GetDetectedGpus()
        {
            IEnumerable<IHardware> gpus = null;
            lock (_lockComputer)
            {
                gpus = _computer?.Hardware
               .Where(hdw => hdw.HardwareType == HardwareType.GpuAmd
                   || hdw.HardwareType == HardwareType.GpuNvidia
                   || hdw.HardwareType == HardwareType.GpuIntel);
            }

            return gpus.Select(gpu => gpu.Name);
        }

        public ISessionSensorData2 GetSensorSessionData()
        {
            return UseSensorLogging ? _sessionSensorDataLive
                .ToSessionSensorData() : null;
        }

        public void StartSensorLogging()
        {
            if (UseSensorLogging)
            {
                _sessionSensorDataLive = new SessionSensorDataLive();
                // Logging must be activated after creating a session data object
                // because of time stamp consistency
                _isLoggingActive = true;
                _sensorUpdateSubject.OnNext(CurrentSensorTimespan);
            }
        }

        public async Task StopSensorLogging()
        {
            await Task.Delay(_currentLoggingTimespan);
            _isLoggingActive = false;
        }

        public async Task<IEnumerable<ISensorEntry>> GetSensorEntries()
        {
            await SensorServiceCompletionSource.Task;
            var entries = new List<ISensorEntry>();
            try
            {
                var sensors = GetSensors();
                if (sensors != null)
                {
                    foreach (var sensor in sensors)
                    {
                        if (sensor != null)
                        {
                            entries.Add(new SensorEntry()
                            {
                                Identifier = sensor.Identifier.ToString(),
                                SortKey = sensor.PresentationSortKey,
                                Value = sensor.Value,
                                Name = sensor.Name,
                                SensorType = sensor.SensorType.ToString(),
                                HardwareType = sensor.Hardware.HardwareType.ToString(),
                                HardwareName = sensor.Hardware.Name,
                                IsPresentationDefault = sensor.IsPresentationDefault
                            });
                        }
                    }
                }

                var pmcReaderPlugin = await _pmcReaderInitializationTask.ConfigureAwait(false);
                if (pmcReaderPlugin != null)
                {
                    var pluginEntries = await pmcReaderPlugin.GetSensorEntriesAsync().ConfigureAwait(false);
                    if (pluginEntries != null)
                        entries.AddRange(pluginEntries);
                }
            }
            catch
            {
                // Don't write periodic log entries
            }

            entries.Add(_amdFlmSensorSource.CreateEntry());
            return entries;
        }

        private static (DateTime, Dictionary<ISensorEntry, float>) MergeSensorSnapshots(
            (DateTime Timestamp, Dictionary<ISensorEntry, float> Values) coreSnapshot,
            (DateTime Timestamp, Dictionary<ISensorEntry, float> Values) pluginSnapshot)
        {
            var merged = new Dictionary<string, KeyValuePair<ISensorEntry, float>>(StringComparer.Ordinal);

            foreach (var entry in coreSnapshot.Values)
                merged[entry.Key.Identifier] = entry;

            foreach (var entry in pluginSnapshot.Values)
                merged[entry.Key.Identifier] = entry;

            var timestamp = coreSnapshot.Timestamp >= pluginSnapshot.Timestamp
                ? coreSnapshot.Timestamp
                : pluginSnapshot.Timestamp;

            return (timestamp, merged.Values.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
        }

        private void LogCurrentValues(Dictionary<ISensorEntry, float> currentValues, DateTime timestamp)
        {
            _sessionSensorDataLive.AddMeasureTime(timestamp);
            foreach (var sensorPair in currentValues)
            {
                if (_sensorConfig.IsSelectedForLogging(sensorPair.Key.Identifier))
                {
                    _sessionSensorDataLive.AddSensorValue(sensorPair.Key, sensorPair.Value);
                }
            }
            _sessionSensorDataLive.CompleteMeasure();
        }

        private async Task<(DateTime, Dictionary<ISensorEntry, float>)> GetTimeStampedSensorValues()
        {
            await SensorServiceCompletionSource.Task;
            var dict = new ConcurrentDictionary<ISensorEntry, float>();
            try
            {
                var sensors = GetSensors();
                if (sensors != null)
                {
                    foreach (var sensor in sensors)
                    {
                        if (sensor.Value != null)
                            dict.TryAdd(new SensorEntry()
                            {
                                Identifier = sensor.Identifier.ToString(),
                                Value = sensor.Value,
                                Name = sensor.Name,
                                SensorType = sensor.SensorType.ToString(),
                                HardwareType = sensor.Hardware.HardwareType.ToString(),
                                HardwareName = sensor.Hardware.Name
                            },
                            sensor.Value.Value);
                    }
                }
            }
            catch
            {
                // Don't write periodic log entries
            }

            if (_appConfiguration.UseAmdFlmLatency)
            {
                var amdFlmEntry = _amdFlmSensorSource.CreateEntry();
                dict.TryAdd(amdFlmEntry, (float)amdFlmEntry.Value);
            }

            return (DateTime.UtcNow, dict.ToDictionary(x => x.Key, x => x.Value));
        }

        private IObservable<(DateTime, Dictionary<ISensorEntry, float>)> CreatePmcReaderSensorStream()
        {
            return Observable.FromAsync(() => _pmcReaderInitializationTask)
                .SelectMany(plugin => plugin?.SensorSnapshotStream
                    ?? Observable.Empty<(DateTime, Dictionary<ISensorEntry, float>)>());
        }

        private async Task<IPmcReaderSensorPlugin> InitializePmcReaderPluginAsync()
        {
#if DEBUG
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
#endif
            // Assembly loading and plugin construction can execute arbitrary plugin code, so they
            // belong to the asynchronous setup path as well as the expensive hardware discovery.
            var plugin = await Task.Run(() => TryLoadPmcReaderPlugin()).ConfigureAwait(false);
            if (plugin == null)
                return null;

            try
            {
                await plugin.InitializeAsync(_sensorUpdateSubject.AsObservable()).ConfigureAwait(false);
#if DEBUG
                stopwatch.Stop();
                _logger.LogDebug("PmcReader plugin setup completed asynchronously in {elapsedMilliseconds:F2} ms.",
                    stopwatch.Elapsed.TotalMilliseconds);
#endif
                return plugin;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize PmcReader plugin.");
                try
                {
                    plugin.Dispose();
                }
                catch
                {
                    // Preserve the initialization exception; teardown is best-effort here.
                }
                return null;
            }
        }

        private IPmcReaderSensorPlugin TryLoadPmcReaderPlugin()
        {
            try
            {
                var baseDir = AppContext.BaseDirectory;
                var pluginPath = Path.Combine(baseDir, "CapFrameX.PmcReader.Plugin.dll");
                if (!File.Exists(pluginPath))
                {
                    _logger.LogInformation("PmcReader plugin not present ({pluginPath}); PMC sensors disabled.", pluginPath);
                    return null;
                }
                // Use UnsafeLoadFrom instead of LoadFrom: the plugin DLL ships inside
                // the portable zip / installer download, so Windows tags it with the
                // "Mark of the Web" (Zone.Identifier stream). On .NET Framework,
                // Assembly.LoadFrom refuses such a local-but-remote-origin assembly with
                // FileLoadException/NotSupportedException (HRESULT 0x80131515) unless
                // loadFromRemoteSources is enabled. UnsafeLoadFrom loads the trusted
                // local plugin while bypassing that remote-source check, so the plugin
                // works without users having to unblock the file.
                var assembly = Assembly.UnsafeLoadFrom(pluginPath);
                var pluginType = assembly.GetTypes()
                    .FirstOrDefault(t => typeof(IPmcReaderSensorPlugin).IsAssignableFrom(t)
                        && t.IsClass
                        && !t.IsAbstract);

                if (pluginType == null)
                {
                    _logger.LogWarning("PmcReader plugin assembly loaded but no IPmcReaderSensorPlugin implementation was found.");
                    return null;
                }

                _logger.LogInformation("PmcReader plugin loaded.");
                return (IPmcReaderSensorPlugin)Activator.CreateInstance(pluginType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load PmcReader plugin.");
                return null;
            }
        }

        private IEnumerable<ISensor> GetSensors()
        {
            List<ISensor> sensors;
            GpuSensorCache gpuCache;
            List<IHardware> hardware;

            lock (_lockSensorUpdate)
            {
                // Only protect the mutable Computer graph while taking a snapshot. Vendor and
                // driver I/O must not run while holding _lockComputer, otherwise one stalled
                // device blocks unrelated metadata/UI access and shutdown coordination.
                lock (_lockComputer)
                {
                    hardware = _computer?.Hardware?.ToList() ?? new List<IHardware>();
                }

                sensors = new List<ISensor>(capacity: 1024);

                foreach (var hw in hardware)
                {
                    hw.Update();
                    CollectSensors(hw, sensors);
                }

                lock (_lockComputer)
                {
                    // Cache GPU count + GPU sensors once while the Computer graph is stable.
                    gpuCache = GetOrBuildGpuCacheLocked();
                }
            }

            var selectedAdapter = _appConfiguration.GraphicsAdapter;

            // If a specific adapter was selected, filter GPU sensors to that adapter name.
            if (!string.Equals(selectedAdapter, "Auto", StringComparison.Ordinal))
            {
                if (gpuCache.SensorIdsByAdapterName.TryGetValue(selectedAdapter, out var allowedGpuIds))
                {
                    return sensors.Where(s =>
                    {
                        if (!IsGpu(s.Hardware.HardwareType))
                            return true;

                        // Only pay Identifier.ToString() for GPU sensors
                        return allowedGpuIds.Contains(s.Identifier.ToString());
                    });
                }

                // Selected adapter not found: keep non-GPU sensors, drop GPU sensors.
                return sensors.Where(s => !IsGpu(s.Hardware.HardwareType));
            }

            // Auto behavior: if only one GPU, do nothing.
            if (gpuCache.SensorIdsByAdapterName.Count <= 1)
                return sensors;

            // Auto behavior: filter iGPUs for GPU sensors only
            return sensors.Where(s =>
            {
                if (!IsGpu(s.Hardware.HardwareType))
                    return true;

                // Use cached per-sensor GPU info when available (avoids repeated casts / name checks)
                var id = s.Identifier.ToString();
                if (gpuCache.SensorsById.TryGetValue(id, out var info))
                {                   
                    return info.IsDiscreteGpu;
                }

                // Fallback (should be rare)
                return (s.Hardware as GenericGpu)?.IsDiscreteGpu ?? true;
            });
        }

        private GpuSensorCache GetOrBuildGpuCacheLocked()
        {
            // Callers must hold _lockComputer
            if (_gpuSensorCache != null)
                return _gpuSensorCache;

            var sensorsById = new Dictionary<string, GpuSensorInfo>(StringComparer.Ordinal);
            var idsByAdapterName = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            int gpuCount = 0;
            foreach (var hw in _computer.Hardware)
            {
                if (!IsGpu(hw.HardwareType))
                    continue;

                gpuCount++;
                AddGpuSensorsToCache(hw, sensorsById, idsByAdapterName);
            }

            _gpuSensorCache = new GpuSensorCache(gpuCount, sensorsById, idsByAdapterName);
            return _gpuSensorCache;
        }

        private static void AddGpuSensorsToCache(
            IHardware gpuHardware,
            Dictionary<string, GpuSensorInfo> sensorsById,
            Dictionary<string, HashSet<string>> idsByAdapterName)
        {
            var adapterName = gpuHardware.Name;
            var isDiscrete = (gpuHardware as GenericGpu)?.IsDiscreteGpu ?? true;

            void addSensor(ISensor s)
            {
                // Key requirement: Identifier.ToString()
                var id = s.Identifier.ToString();

                // Avoid exceptions on duplicates; first wins is fine for caching
                if (!sensorsById.ContainsKey(id))
                    sensorsById[id] = new GpuSensorInfo(s, adapterName, isDiscrete);

                if (!idsByAdapterName.TryGetValue(adapterName, out var set))
                {
                    set = new HashSet<string>(StringComparer.Ordinal);
                    idsByAdapterName[adapterName] = set;
                }
                set.Add(id);
            }

            foreach (var s in gpuHardware.Sensors)
                addSensor(s);

            foreach (var sub in gpuHardware.SubHardware)
            {
                // sub.Update() is done by the caller during the main update pass,
                // but harmless if called again; avoid repeating it here.
                foreach (var s in sub.Sensors)
                    addSensor(s);
            }
        }

        private static void CollectSensors(IHardware hardware, List<ISensor> target)
        {
            // hardware.Sensors is typically an array, so AddRange is efficient
            target.AddRange(hardware.Sensors);

            foreach (var sub in hardware.SubHardware)
            {
                sub.Update();
                target.AddRange(sub.Sensors);
            }
        }

        private static bool IsGpu(HardwareType type) =>
            type is HardwareType.GpuAmd || type is HardwareType.GpuNvidia || type is HardwareType.GpuIntel;

        public void ShutdownSensorService()
        {
            _isServiceAlive = false;
            _logDisposable?.Dispose();
            _amdFlmSensorSource.Dispose();

            // Initialization may still be running when the application closes. Dispose the plugin
            // on the default scheduler once that one-time task finishes without blocking shutdown.
            _ = _pmcReaderInitializationTask.ContinueWith(
                task =>
                {
                    try
                    {
                        task.Result?.Dispose();
                    }
                    catch
                    {
                        // Ignore teardown exceptions during shutdown.
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnRanToCompletion,
                TaskScheduler.Default);

            // Close the LibreHardwareMonitor computer on a background thread bounded by a timeout.
            // Vendor GPU libraries (AMD ADLX, Intel IGCL) tear down COM objects in Computer.Close();
            // on some systems that teardown can deadlock (e.g. a COM/STA apartment mismatch, or a
            // foreign vendor DLL that was loaded without an active GPU). Bounding it guarantees the
            // application shutdown never hangs — if the teardown overruns, the stuck background
            // thread is abandoned (IsBackground) and reclaimed when the process exits.
            var closeThread = new Thread(() =>
            {
                try
                {
                    lock (_lockSensorUpdate)
                    {
                        lock (_lockComputer)
                        {
                            _computer?.Close();
                        }
                    }
                }
                catch
                {
                    // Ignore teardown exceptions during shutdown.
                }
            })
            {
                IsBackground = true,
                Name = "SensorServiceShutdown"
            };

            closeThread.Start();

            if (!closeThread.Join(_shutdownComputerTimeout))
            {
                _logger?.LogWarning("Sensor service teardown timed out after {Timeout} ms; abandoning " +
                    "Computer.Close() (likely a GPU driver/COM deadlock) and continuing shutdown.",
                    (int)_shutdownComputerTimeout.TotalMilliseconds);
            }
        }

        public string GetGpuDriverVersion()
        {
            IHardware gpu = null;
            lock (_lockComputer)
            {
                gpu = _computer?.Hardware
               .FirstOrDefault(hdw => hdw.HardwareType == HardwareType.GpuAmd
                   || hdw.HardwareType == HardwareType.GpuNvidia
                   || hdw.HardwareType == HardwareType.GpuIntel);
            }

            return gpu != null ? gpu.GetDriverVersion() : "Unknown";
        }

        public string GetCpuName()
        {
            bool hasCustomInfo = _appConfiguration.HardwareInfoSource
              .ConvertToEnum<EHardwareInfoSource>() == EHardwareInfoSource.Custom;

            if (!hasCustomInfo)
            {
                IHardware cpu = null;
                lock (_lockComputer)
                {
                    cpu = _computer?.Hardware
                        .FirstOrDefault(hdw => hdw.HardwareType == HardwareType.Cpu);
                }

                return cpu != null ? cpu.Name : "Unknown";
            }
            else
            {
                return _appConfiguration.CustomCpuDescription;
            }
        }

        public string GetGpuName()
        {
            bool hasCustomInfo = _appConfiguration.HardwareInfoSource
                .ConvertToEnum<EHardwareInfoSource>() == EHardwareInfoSource.Custom;

            if (!hasCustomInfo)
            {
                if(_appConfiguration.GraphicsAdapter != "Auto")
                {
                    return _appConfiguration.GraphicsAdapter;
                }

                List<IHardware> gpus = null;
                lock (_lockComputer)
                {
                    gpus = _computer?.Hardware
                       .Where(hdw => hdw.HardwareType == HardwareType.GpuAmd
                           || hdw.HardwareType == HardwareType.GpuNvidia
                           || hdw.HardwareType == HardwareType.GpuIntel).ToList();
                }

                if (gpus != null && gpus.Count == 1)
                {
                    return gpus[0].Name;
                }
                else if (gpus != null && gpus.Count > 1)
                {
                    var discreteGpu = gpus.FirstOrDefault(g => (g as GenericGpu)?.IsDiscreteGpu ?? true);
                    if (discreteGpu != null)
                        return discreteGpu.Name;
                    return gpus[0].Name;
                }

                return "Unknown";
            }
            else
            {
                return _appConfiguration.CustomGpuDescription;
            }
        }

        public ECpuVendor GetCpuVendor()
        {
            lock (_lockComputer)
            {
                var cpu = _computer?.Hardware
                    .FirstOrDefault(hdw => hdw.HardwareType == HardwareType.Cpu);
                if (cpu == null)
                    return ECpuVendor.Unknown;

                var identifier = cpu.Identifier.ToString().ToLowerInvariant();
                if (identifier.Contains("amdcpu"))
                    return ECpuVendor.Amd;
                if (identifier.Contains("intelcpu"))
                    return ECpuVendor.Intel;

                return ECpuVendor.Unknown;
            }
        }

        public EGpuVendor GetGpuVendor()
        {
            var gpu = GetPrimaryGpuHardware();
            if (gpu == null)
                return EGpuVendor.Unknown;

            switch (gpu.HardwareType)
            {
                case HardwareType.GpuNvidia:
                    return EGpuVendor.Nvidia;
                case HardwareType.GpuAmd:
                    return EGpuVendor.Amd;
                case HardwareType.GpuIntel:
                    return EGpuVendor.Intel;
                default:
                    return EGpuVendor.Unknown;
            }
        }

        private IHardware GetPrimaryGpuHardware()
        {
            lock (_lockComputer)
            {
                var gpus = _computer?.Hardware
                    .Where(hdw => hdw.HardwareType == HardwareType.GpuAmd
                        || hdw.HardwareType == HardwareType.GpuNvidia
                        || hdw.HardwareType == HardwareType.GpuIntel)
                    .ToList();

                if (gpus == null || gpus.Count == 0)
                    return null;

                var selectedAdapter = _appConfiguration.GraphicsAdapter;
                if (!string.Equals(selectedAdapter, "Auto", StringComparison.Ordinal))
                    return gpus.FirstOrDefault(gpu => string.Equals(gpu.Name, selectedAdapter, StringComparison.Ordinal));

                if (gpus.Count == 1)
                    return gpus[0];

                var discreteGpu = gpus.FirstOrDefault(gpu => (gpu as GenericGpu)?.IsDiscreteGpu ?? true);
                return discreteGpu ?? gpus[0];
            }
        }
    }
}
