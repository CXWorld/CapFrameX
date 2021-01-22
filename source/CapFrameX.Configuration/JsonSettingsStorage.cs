using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.Configuration
{
    public class JsonSettingsStorage : ISettingsStorage
    {
        private readonly string _jsonFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CapFrameX", "Configuration", "settings.json");
        private readonly ILogger<JsonSettingsStorage> _logger;
        private readonly Subject<int> _saveFileSubject = new Subject<int>();
        private Dictionary<string, object> _configDictionary;

        public JsonSettingsStorage(ILogger<JsonSettingsStorage> logger)
        {
            _logger = logger;
            _saveFileSubject
                .AsObservable()
                .Throttle(TimeSpan.FromMilliseconds(500))
                .Subscribe(async x => await Save());
        }

        public T GetValue<T>(string key)
        {
            if (_configDictionary.TryGetValue(key, out var value)) {
                if (value is Int64) value = Convert.ToInt32(value);
                if(!(value is T val))
                {
                    throw new InvalidOperationException($"Value of Key {key} has invalid Format: Expected value of type {typeof(T).Name} but found {value.GetType().Name}");
                }
                return val;
            }
            throw new KeyNotFoundException($"Key {key} not found in Dictionary");
        }

        public void SetValue(string key, object value)
        {
            _configDictionary[key] = value;
            _saveFileSubject.OnNext(default);
        }

        public Task Load()
        {
            try
            {
                var file = new FileInfo(_jsonFilePath);
                if(!file.Exists)
                {
                    _logger.LogInformation($"File {_jsonFilePath} does not exist. Creating it...");
                    Directory.CreateDirectory(file.DirectoryName);
                    File.WriteAllText(file.FullName, "{}");
                }
                var fileContent = File.ReadAllText(file.FullName);
                _configDictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(fileContent);
                if(_configDictionary is null)
                {
                    throw new Exception($"Configurationfile {file.FullName} is corrupted");
                }
                return Task.FromResult(true);
            } catch(Exception exc)
            {
                _logger.LogError(exc, $"Unable to load Configuration from path {_jsonFilePath}");
                throw;
            }
        }

        private Task Save()
        {
            try
            {
                var file = new FileInfo(_jsonFilePath);
                var fileContent = JsonConvert.SerializeObject(_configDictionary, Formatting.Indented);
                File.WriteAllText(file.FullName, fileContent);
                return Task.FromResult(true);
            }
            catch (Exception exc)
            {
                _logger.LogError(exc, $"Unable to save Configuration to path {_jsonFilePath}");
                throw;
            }
        }
    }
}
