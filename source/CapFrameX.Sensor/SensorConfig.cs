using CapFrameX.Monitoring.Contracts;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CapFrameX.Sensor
{
    public class SensorConfig : ISensorConfig
    {
        private static readonly string CONFIG_FILENAME =
            "SensorEntryConfiguration.json";

        private static readonly string STABLE_CONFIG_FILENAME =
            "SensorEntryConfigurationStable.json";

        private readonly string _sensorConfigFolder;

        private Dictionary<string, bool> _loggingSelectionDict;

        private Dictionary<string, bool> _defaultLoggingSelectionDict;

        private Dictionary<string, bool> _stableLoggingSelectionDict;

        private Dictionary<string, bool> _overlaySelectionDict
            = new Dictionary<string, bool>();
        private readonly HashSet<string> _sensorEvaluateFirstCallSeen
            = new HashSet<string>();

        private volatile bool _isCapturing;
        private volatile bool _isSensorLoggingActive;
        private volatile bool _wsSensorsEnabled;
        private volatile bool _wsActiveSensorsEnabled;
        private int _selectedOverlaySensorCount;
        private int _selectedPmcLoggingSensorCount;
        private int _selectedPmcOverlaySensorCount;

        public bool IsCapturing
        {
            get => _isCapturing;
            set => _isCapturing = value;
        }

        public bool IsSensorLoggingActive
        {
            get => _isSensorLoggingActive;
            set => _isSensorLoggingActive = value;
        }

        public bool HasConfigFile
            => File.Exists(Path.Combine(_sensorConfigFolder, CONFIG_FILENAME));

        public int SensorEntryCount
            => _loggingSelectionDict == null ? 0 : _loggingSelectionDict.Count;

        public bool WsSensorsEnabled
        {
            get => _wsSensorsEnabled;
            set => _wsSensorsEnabled = value;
        }

        public bool WsActiveSensorsEnabled
        {
            get => _wsActiveSensorsEnabled;
            set => _wsActiveSensorsEnabled = value;
        }

        public bool HasSelectedOverlaySensors
            => Volatile.Read(ref _selectedOverlaySensorCount) > 0;

        public bool HasSelectedPmcOverlaySensors
            => Volatile.Read(ref _selectedPmcOverlaySensorCount) > 0;

        public bool HasSelectedPmcLoggingSensors
            => Volatile.Read(ref _selectedPmcLoggingSensorCount) > 0;

        // Written from the UI thread, read from the sensor update loop.
        private volatile bool _evaluateAllSensors;

        public bool EvaluateAllSensors
        {
            get => _evaluateAllSensors;
            set => _evaluateAllSensors = value;
        }

        public int SensorLoggingRefreshPeriod { get; set; }

        public SensorConfig(string sensorConfigFolder)
        {
            _sensorConfigFolder = sensorConfigFolder;
            _defaultLoggingSelectionDict = GetSensorEntryDefaults();
            Task.Run(async () => await LoadOrSetDefault()).Wait();
        }

        public bool IsSelectedForLogging(string identifier)
        {
            bool isActive = false;
            if (_loggingSelectionDict.ContainsKey(identifier))
                isActive = _loggingSelectionDict[identifier];

            return isActive;
        }

        public void SelectForLogging(string identifier, bool isActive)
        {
            bool wasSelected = _loggingSelectionDict.TryGetValue(identifier, out bool selected) && selected;
            _loggingSelectionDict[identifier] = isActive;

            if (!IsPmcSensorIdentifier(identifier) || wasSelected == isActive)
                return;

            if (isActive)
                Interlocked.Increment(ref _selectedPmcLoggingSensorCount);
            else
                Interlocked.Decrement(ref _selectedPmcLoggingSensorCount);
        }

        public bool IsSelectedForLoggingByStableId(string stableIdentifier)
        {
            if (stableIdentifier == null || _stableLoggingSelectionDict == null)
                return false;

            return _stableLoggingSelectionDict.TryGetValue(stableIdentifier, out bool isActive) && isActive;
        }

        public void SelectStableForLogging(string stableIdentifier, bool isActive)
        {
            if (stableIdentifier == null) return;

            if (_stableLoggingSelectionDict == null)
                _stableLoggingSelectionDict = new Dictionary<string, bool>();

            if (_stableLoggingSelectionDict.ContainsKey(stableIdentifier))
                _stableLoggingSelectionDict[stableIdentifier] = isActive;
            else
                _stableLoggingSelectionDict.Add(stableIdentifier, isActive);
        }

        public Dictionary<string, bool> GetStableSensorConfigCopy()
        {
            if (_stableLoggingSelectionDict == null) return new Dictionary<string, bool>();

            return new Dictionary<string, bool>(_stableLoggingSelectionDict);
        }

        public bool IsSelectedForOverlay(string identifier)
        {
            bool isSelected = false;
            if (_overlaySelectionDict.ContainsKey(identifier))
                isSelected = _overlaySelectionDict[identifier];

            return isSelected;
        }

        public void SelectForOverlay(string identifier, bool evaluate)
        {
            bool wasSelected = _overlaySelectionDict.TryGetValue(identifier, out bool selected) && selected;
            _overlaySelectionDict[identifier] = evaluate;

            if (!IsHardwareSensorIdentifier(identifier) || wasSelected == evaluate)
                return;

            if (evaluate)
                Interlocked.Increment(ref _selectedOverlaySensorCount);
            else
                Interlocked.Decrement(ref _selectedOverlaySensorCount);

            if (IsPmcSensorIdentifier(identifier))
            {
                if (evaluate)
                    Interlocked.Increment(ref _selectedPmcOverlaySensorCount);
                else
                    Interlocked.Decrement(ref _selectedPmcOverlaySensorCount);
            }
        }

        public bool GetSensorEvaluate(string identifier)
        {
            // Keep registering first calls even while EvaluateAllSensors is set, so the
            // per-identifier first-read bookkeeping stays consistent once the flag drops.
            if (!_sensorEvaluateFirstCallSeen.Contains(identifier))
            {
                _sensorEvaluateFirstCallSeen.Add(identifier);
                return true;
            }

            // A saved logging selection describes what a capture should contain; it must not
            // keep vendor APIs, SMU/PMC counters and storage SMART queries active between
            // captures. The active-sensors websocket intentionally uses that same selection.
            return _evaluateAllSensors
                || _wsSensorsEnabled
                || ((_isSensorLoggingActive || _wsActiveSensorsEnabled) && IsSelectedForLogging(identifier))
                || IsSelectedForOverlay(identifier);
        }

        public async Task Save()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_loggingSelectionDict);

                if (!Directory.Exists(_sensorConfigFolder))
                    Directory.CreateDirectory(_sensorConfigFolder);

                using (StreamWriter outputFile = new StreamWriter(Path.Combine(_sensorConfigFolder, CONFIG_FILENAME)))
                {
                    await outputFile.WriteAsync(json);
                }

                // Save stable config alongside the main config
                if (_stableLoggingSelectionDict != null && _stableLoggingSelectionDict.Any())
                {
                    var stableJson = JsonConvert.SerializeObject(_stableLoggingSelectionDict);
                    using (StreamWriter outputFile = new StreamWriter(Path.Combine(_sensorConfigFolder, STABLE_CONFIG_FILENAME)))
                    {
                        await outputFile.WriteAsync(stableJson);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Error while saving sensor config.");
            }
        }

        public void ResetConfig()
        {
            _loggingSelectionDict?.Clear();
            _stableLoggingSelectionDict?.Clear();
            Interlocked.Exchange(ref _selectedPmcLoggingSensorCount, 0);
        }

        public void ResetEvaluate()
        {
            _overlaySelectionDict?.Clear();
            _sensorEvaluateFirstCallSeen.Clear();
            Interlocked.Exchange(ref _selectedOverlaySensorCount, 0);
            Interlocked.Exchange(ref _selectedPmcOverlaySensorCount, 0);
        }

        private static bool IsHardwareSensorIdentifier(string identifier)
            => !string.IsNullOrEmpty(identifier) &&
               (identifier[0] == '/' || IsPmcSensorIdentifier(identifier));

        private static bool IsPmcSensorIdentifier(string identifier)
            => identifier?.StartsWith("pmcreader/", StringComparison.Ordinal) == true;

        private async Task LoadOrSetDefault()
        {
            try
            {
                _loggingSelectionDict = await GetInitializedSensorEntryDictionary();

                // Load default as fallback
                if (_loggingSelectionDict == null || !_loggingSelectionDict.Values.Any())
                {
                    _loggingSelectionDict = new Dictionary<string, bool>(_defaultLoggingSelectionDict);
                }

                // Load stable config (non-fatal if missing)
                _stableLoggingSelectionDict = await LoadStableConfig();
                UpdatePmcLoggingSensorCount();
            }
            catch (Exception ex)
            {
                _loggingSelectionDict = new Dictionary<string, bool>(_defaultLoggingSelectionDict);
                _stableLoggingSelectionDict = new Dictionary<string, bool>();
                UpdatePmcLoggingSensorCount();
                Log.Logger.Error(ex, "Error while loading sensor config. Default config loading instead...");
            }
        }

        private void UpdatePmcLoggingSensorCount()
        {
            int count = _loggingSelectionDict?.Count(entry =>
                entry.Value && IsPmcSensorIdentifier(entry.Key)) ?? 0;
            Interlocked.Exchange(ref _selectedPmcLoggingSensorCount, count);
        }

        private Dictionary<string, bool> GetSensorEntryDefaults()
            => new Dictionary<string, bool>();

        private async Task<Dictionary<string, bool>> GetInitializedSensorEntryDictionary()
        {
            var path = Path.Combine(_sensorConfigFolder, CONFIG_FILENAME);

            if (!File.Exists(path))
            {
                Log.Logger.Debug("Sensor config file not found at {Path}; using defaults.", path);
                return null;
            }

            string json = await ReadAllTextAsync(path);
            return JsonConvert.DeserializeObject<Dictionary<string, bool>>(json);
        }

        private async Task<Dictionary<string, bool>> LoadStableConfig()
        {
            try
            {
                var path = Path.Combine(_sensorConfigFolder, STABLE_CONFIG_FILENAME);
                if (!File.Exists(path))
                    return new Dictionary<string, bool>();

                string json = await ReadAllTextAsync(path);
                return JsonConvert.DeserializeObject<Dictionary<string, bool>>(json)
                    ?? new Dictionary<string, bool>();
            }
            catch
            {
                return new Dictionary<string, bool>();
            }
        }

        private async Task<string> ReadAllTextAsync(string filePath)
        {
            var stringBuilder = new StringBuilder();
            using (var fileStream = File.OpenRead(filePath))
            using (var streamReader = new StreamReader(fileStream))
            {
                string line = await streamReader.ReadLineAsync();
                while (line != null)
                {
                    stringBuilder.AppendLine(line);
                    line = await streamReader.ReadLineAsync();
                }
                return stringBuilder.ToString();
            }
        }

        public Dictionary<string, bool> GetSensorConfigCopy()
        {
            // _loggingSelectionDict is null return empty dict
            if (_loggingSelectionDict == null) return new Dictionary<string, bool>();

            var copy = new Dictionary<string, bool>();
            foreach (var item in _loggingSelectionDict)
            {
                copy.Add(item.Key, item.Value);
            }

            return copy;
        }
    }
}
