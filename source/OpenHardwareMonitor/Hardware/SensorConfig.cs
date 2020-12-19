using CapFrameX.Contracts.Sensor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Serilog;
using Newtonsoft.Json;

namespace OpenHardwareMonitor.Hardware
{
    /// <summary>
    /// Already implemented: 
    /// GPU Power (Nvidia/AMD)
    /// GPU Memory (Dedicated/Shared) Usage (Nvidia/AMD)
    /// </summary>
    public class SensorConfig : ISensorConfig
    {
        private static readonly string SENSOR_CONFIG_FOLDER
            = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    @"CapFrameX\SensorConfiguration\");

        private static readonly string GetConfigurationFileName = 
            "SensorEntryConfiguration.json";

        private readonly TaskCompletionSource<bool> _taskCompletionSource
            = new TaskCompletionSource<bool>();

        private Dictionary<string, bool> _activeSensorsDict;

        public bool IsInitialized { get; set; } = false;

        public bool GlobalIsActivated { get; set; } = false;

        public SensorConfig()
        {
            _ = Task.Run(async () => await LoadOrSetDefault())
                .ContinueWith(task => _taskCompletionSource.SetResult(true));
        }

        public bool GetSensorIsActive(string identifier)
        {
            if (!IsInitialized || GlobalIsActivated)
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

        public async Task Save()
        {
            try
            {               
                var json = JsonConvert.SerializeObject(_activeSensorsDict);

                if (!Directory.Exists(SENSOR_CONFIG_FOLDER))
                    Directory.CreateDirectory(SENSOR_CONFIG_FOLDER);

                using (StreamWriter outputFile = new StreamWriter(GetConfigurationFileName))
                {
                    await outputFile.WriteAsync(json);
                }
            }
            catch 
            {
                Log.Logger.Error("Error while saving sensor config.");
            }
        }

        private async Task LoadOrSetDefault()
        {
            try
            {
                _activeSensorsDict = await GetInitializedSensorEntryDictionary();
            }
            catch
            {
                _activeSensorsDict = await GetSensorEntryDefaults();
            }
        }

        private Task<Dictionary<string, bool>> GetSensorEntryDefaults()
        {
            throw new NotImplementedException();
        }

        private async Task<Dictionary<string, bool>> GetInitializedSensorEntryDictionary()
        {
            string json = await FileExtensions.ReadAllTextAsync(GetConfigurationFileName);
            var overlayEntriesFromJson = JsonConvert.DeserializeObject<OverlayEntryPersistence>(json)
                .OverlayEntries.ToBlockingCollection<IOverlayEntry>();
        }
    }
}
