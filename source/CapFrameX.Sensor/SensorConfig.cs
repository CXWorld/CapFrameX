using CapFrameX.Monitoring.Contracts;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

        public bool IsCapturing { get; set; } = false;

        public bool HasConfigFile
            => File.Exists(Path.Combine(_sensorConfigFolder, CONFIG_FILENAME));

        public int SensorEntryCount
            => _loggingSelectionDict == null ? 0 : _loggingSelectionDict.Count;

        public bool WsSensorsEnabled { get; set; }

        public bool WsActiveSensorsEnabled { get; set; }

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

            if (_loggingSelectionDict.ContainsKey(identifier))
                _loggingSelectionDict[identifier] = isActive;
            else
                _loggingSelectionDict.Add(identifier, isActive);
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
            if (_overlaySelectionDict.ContainsKey(identifier))
                _overlaySelectionDict[identifier] = evaluate;
            else
                _overlaySelectionDict.Add(identifier, evaluate);
        }

        public bool GetSensorEvaluate(string identifier)
        {
            if (!_sensorEvaluateFirstCallSeen.Contains(identifier))
            {
                _sensorEvaluateFirstCallSeen.Add(identifier);
                return true;
            }

            return IsSelectedForLogging(identifier) || IsSelectedForOverlay(identifier);
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
        }

        public void ResetEvaluate()
        {
            _overlaySelectionDict?.Clear();
            _sensorEvaluateFirstCallSeen.Clear();
        }

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
            }
            catch (Exception ex)
            {
                _loggingSelectionDict = new Dictionary<string, bool>(_defaultLoggingSelectionDict);
                _stableLoggingSelectionDict = new Dictionary<string, bool>();
                Log.Logger.Error(ex, "Error while loading sensor config. Default config loading instead...");
            }
        }

        private Dictionary<string, bool> GetSensorEntryDefaults()
            => new Dictionary<string, bool>();

        private async Task<Dictionary<string, bool>> GetInitializedSensorEntryDictionary()
        {
            string json = await ReadAllTextAsync(Path.Combine(_sensorConfigFolder, CONFIG_FILENAME));
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
