using CapFrameX.Contracts.Configuration;
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
using System.Threading.Tasks;

namespace CapFrameX.Sensor
{
    public class SensorService : ISensorService
    {
        private GpuSensorCache _gpuSensorCache;
        private readonly object _lockComputer = new object();
        private readonly ISensorConfig _sensorConfig;
        private readonly IRTSSService _rTSSService;
        private readonly IAppConfiguration _appConfiguration;
        private readonly ILogger<SensorService> _logger;
        private readonly IDisposable _logDisposable;

        private Computer _computer;
        private IPmcReaderSensorPlugin _pmcReaderPlugin;
        private SessionSensorDataLive _sessionSensorDataLive;
        private bool _isLoggingActive = false;
        private bool _isServiceAlive = true;

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
            IRTSSService rTSSService, ILogger<SensorService> logger)
        {
            _appConfiguration = appConfig;
            _sensorConfig = sensorConfig;
            _rTSSService = rTSSService;
            _logger = logger;
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

            var pluginSensorStream = InitializePmcReaderPlugin()
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

        public string GetSensorTypeString(string identifier)
        {
            if (identifier == null)
                return string.Empty;

            string SensorType;

            if (identifier.Contains("cpu"))
            {
                if (identifier.Contains("load"))
                    SensorType = "CPU Load";
                else if (identifier.Contains("clock"))
                    SensorType = "CPU Clock";
                else if (identifier.Contains("power"))
                    SensorType = "CPU Power";
                else if (identifier.Contains("temperature"))
                    SensorType = "CPU Temperature";
                else if (identifier.Contains("voltage"))
                    SensorType = "CPU Voltage";
                else
                    SensorType = string.Empty;
            }

            else if (identifier.Contains("gpu"))
            {
                if (identifier.Contains("load"))
                    SensorType = "GPU Load";
                else if (identifier.Contains("clock"))
                    SensorType = "GPU Clock";
                else if (identifier.Contains("power"))
                    SensorType = "GPU Power";
                else if (identifier.Contains("temperature"))
                    SensorType = "GPU Temperature";
                else if (identifier.Contains("voltage"))
                    SensorType = "GPU Voltage";
                else if (identifier.Contains("factor"))
                    SensorType = "GPU Limits";
                else
                    SensorType = string.Empty;
            }

            else
                SensorType = string.Empty;

            return SensorType;
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

                if (_pmcReaderPlugin != null)
                {
                    var pluginEntries = await _pmcReaderPlugin.GetSensorEntriesAsync();
                    if (pluginEntries != null)
                        entries.AddRange(pluginEntries);
                }
            }
            catch
            {
                // Don't write periodic log entries
            }

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

            return (DateTime.UtcNow, dict.ToDictionary(x => x.Key, x => x.Value));
        }

        private IObservable<(DateTime, Dictionary<ISensorEntry, float>)> InitializePmcReaderPlugin()
        {
            _pmcReaderPlugin = TryLoadPmcReaderPlugin();
            if (_pmcReaderPlugin == null)
                return Observable.Empty<(DateTime, Dictionary<ISensorEntry, float>)>();

            try
            {
                _pmcReaderPlugin.InitializeAsync(_sensorUpdateSubject.AsObservable())
                    .ConfigureAwait(false);
            }
            catch
            {
                return Observable.Empty<(DateTime, Dictionary<ISensorEntry, float>)>();
            }

            return _pmcReaderPlugin.SensorSnapshotStream
                ?? Observable.Empty<(DateTime, Dictionary<ISensorEntry, float>)>();
        }

        private IPmcReaderSensorPlugin TryLoadPmcReaderPlugin()
        {
            try
            {
                var baseDir = AppContext.BaseDirectory;
                var pluginPath = Path.Combine(baseDir, "CapFrameX.PmcReader.Plugin.dll");
                if (!File.Exists(pluginPath))
                    return null;
                var assembly = Assembly.LoadFrom(pluginPath);
                var pluginType = assembly.GetTypes()
                    .FirstOrDefault(t => typeof(IPmcReaderSensorPlugin).IsAssignableFrom(t)
                        && t.IsClass
                        && !t.IsAbstract);

                return pluginType == null
                    ? null
                    : (IPmcReaderSensorPlugin)Activator.CreateInstance(pluginType);
            }
            catch
            {
                return null;
            }
        }

        private IEnumerable<ISensor> GetSensors()
        {
            List<ISensor> sensors;
            GpuSensorCache gpuCache;

            lock (_lockComputer)
            {
                // Update + collect sensors in a single pass; materialize under lock.
                sensors = new List<ISensor>(capacity: 1024);

                foreach (var hw in _computer.Hardware)
                {
                    hw.Update();
                    CollectSensors(hw, sensors);
                }

                // Cache GPU count + GPU sensors once
                gpuCache = GetOrBuildGpuCacheLocked();
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

            lock (_lockComputer)
            {
                _computer?.Close();
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
