using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Serilog;
using Newtonsoft.Json;
using CapFrameX.Monitoring.Contracts;
using System.Text;

namespace OpenHardwareMonitor.Hardware
{
    public class SensorConfig : ISensorConfig
    {       
        private static readonly string CONFIG_FILENAME =
            "SensorEntryConfiguration.json";

        private readonly string _sensorConfigFolder;

        private Dictionary<string, bool> _activeSensorsDict;

        private Dictionary<string, bool> _evalSensorsDict
            = new Dictionary<string, bool>();

        public bool IsInitialized { get; set; } = false;

        public bool IsCapturing { get; set; } = false;

        public bool HasConfigFile
            => File.Exists(Path.Combine(_sensorConfigFolder, CONFIG_FILENAME));

        public int SensorEntryCount 
            => _activeSensorsDict == null ? 0 : _activeSensorsDict.Count;

        public bool WsSensorsEnabled { get; set; }
        public bool WsActiveSensorsEnabled { get; set; }

        public SensorConfig(string sensorConfigFolder)
        {
            _sensorConfigFolder = sensorConfigFolder;
            Task.Run(async () => await LoadOrSetDefault());
        }

        public bool GetSensorIsActive(string identifier)
        {
            if (!IsInitialized)
                return true;

            bool isActive = false;
            if (_activeSensorsDict.ContainsKey(identifier))
                isActive = _activeSensorsDict[identifier];

            return isActive;
        }

        public void SetSensorIsActive(string identifier, bool isActive)
        {
            isActive = !IsInitialized || isActive;

            if (_activeSensorsDict.ContainsKey(identifier))
                _activeSensorsDict[identifier] = isActive;
            else
                _activeSensorsDict.Add(identifier, isActive);
        }

        public bool GetSensorEvaluate(string identifier)
        {
            if (!IsInitialized)
                return true;

            bool isActive = false;
            if (_activeSensorsDict.ContainsKey(identifier))
                isActive = _activeSensorsDict[identifier];

            bool evaluate = false;
            if (_evalSensorsDict.ContainsKey(identifier))
                evaluate = _evalSensorsDict[identifier];

            return (isActive && (IsCapturing || WsActiveSensorsEnabled)) || evaluate || WsSensorsEnabled;
        }

        public void SetSensorEvaluate(string identifier, bool evaluate)
        {
            evaluate = !IsInitialized || evaluate;

            if (_evalSensorsDict.ContainsKey(identifier))
                _evalSensorsDict[identifier] = evaluate;
            else
                _evalSensorsDict.Add(identifier, evaluate);
        }

        public async Task Save()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_activeSensorsDict);

                if (!Directory.Exists(_sensorConfigFolder))
                    Directory.CreateDirectory(_sensorConfigFolder);

                using (StreamWriter outputFile = new StreamWriter(Path.Combine(_sensorConfigFolder, CONFIG_FILENAME)))
                {
                    await outputFile.WriteAsync(json);
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Error while saving sensor config.");
            }
        }

        public void ResetConfig()
            => _activeSensorsDict?.Clear();

        public void ResetEvaluate()
            => _evalSensorsDict?.Clear();

        private async Task LoadOrSetDefault()
        {
            try
            {
                _activeSensorsDict = await GetInitializedSensorEntryDictionary();
            }
            catch (Exception ex)
            {
                _activeSensorsDict = await GetSensorEntryDefaults();
                Log.Logger.Error(ex, "Error while loading sensor config. Default config loading instead...");
            }
        }

        private async Task<Dictionary<string, bool>> GetSensorEntryDefaults()
            => await Task.FromResult(new Dictionary<string, bool>());

        private async Task<Dictionary<string, bool>> GetInitializedSensorEntryDictionary()
        {
            string json = await ReadAllTextAsync(Path.Combine(_sensorConfigFolder, CONFIG_FILENAME));
            return JsonConvert.DeserializeObject<Dictionary<string, bool>>(json);
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
            var copy = new Dictionary<string, bool>();
            foreach (var item in _activeSensorsDict)
            {
                copy.Add(item.Key, item.Value);
            }

            return copy;
        }
    }
}
