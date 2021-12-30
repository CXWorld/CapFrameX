using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace CapFrameX.Configuration
{
    public class JsonSettingsStorage : ISettingsStorage
    {
        private readonly object _iOLock = new object();
        private readonly string _jsonFilePath 
            = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 
                "CapFrameX", "Configuration", "AppSettings.json");
        private readonly ILogger<JsonSettingsStorage> _logger;
        private readonly Subject<int> _saveFileSubject = new Subject<int>();
        private readonly Dictionary<string, object> _configDictionary = new Dictionary<string, object>();

        public JsonSettingsStorage(ILogger<JsonSettingsStorage> logger)
        {
            _logger = logger;
            _saveFileSubject
                .AsObservable()
                .Throttle(TimeSpan.FromMilliseconds(1000))
                .SelectMany(_ => Observable.Timer(TimeSpan.FromMilliseconds(200)).Select(x => Save()).Retry(4))
                .Subscribe();
        }

        public T GetValue<T>(string key)
        {
            if (_configDictionary.TryGetValue(key, out var value)) {
                if (value is long) value = Convert.ToInt32(value);
                if(!(value is T val))
                {
                    if(value is JObject jObject)
                    {
                        var convertedObject = jObject.ToObject(typeof(T));
                        if(convertedObject is T convertedObjectTyped) {
                            return convertedObjectTyped;
                        }
                    }
                    else if (value is JArray jArray)
                    {
                        var convertedObject = jArray.ToObject(typeof(T));
                        if (convertedObject is T convertedObjectTyped)
                        {
                            return convertedObjectTyped;
                        }
                    }

                    throw new InvalidOperationException($"Value of Key {key} has invalid Format: Expected value of type " +
                        $"{typeof(T).Name} but found {value.GetType().Name}");
                }
                return val;
            }
            throw new KeyNotFoundException($"Key {key} not found in Dictionary");
        }

        public void SetValue(string key, object value)
        {
            if (!_configDictionary.TryGetValue(key, out var oldValue) || !oldValue.Equals(value))
            {
                _configDictionary[key] = value;
                _saveFileSubject.OnNext(default);
            }
        }

        public Task Load()
        {
            try
            {
                lock (_iOLock)
                {
                    var file = new FileInfo(_jsonFilePath);
                    if (!file.Exists)
                    {
                        _logger.LogInformation($"File {_jsonFilePath} does not exist. Creating it...");
                        Directory.CreateDirectory(file.DirectoryName);
                        File.WriteAllText(file.FullName, "{}");
                    }

                    var fileContent = File.ReadAllText(file.FullName);
                    var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(fileContent);

                    if (dict is null)
                    {
                        throw new Exception($"Configurationfile is corrupted.");
                    }

                    foreach (var kvp in dict)
                    {
                        _configDictionary.Add(kvp.Key, kvp.Value);
                    }

                    return Task.FromResult(true);
                }
            } catch(Exception exc)
            {
                var newException = new JsonSettingsStorageException($"Unable to load Configuration from path {_jsonFilePath}. Please delete and try again.", exc);
                _logger.LogError(exc, newException.Message);
                throw newException;
            }
        }

        private Task<bool> Save()
        {
            try
            {
                lock (_iOLock)
                {
                    var file = new FileInfo(_jsonFilePath);
                    var fileContent = JsonConvert.SerializeObject(_configDictionary.ToDictionary(x => x.Key, x => x.Value), Formatting.Indented);
                    if (string.IsNullOrWhiteSpace(fileContent))
                    {
                        _logger.LogError("Error writing Configurationfile. Cannot create config from Dictionary", _configDictionary);
                        return Task.FromResult(false);
                    }
                    else
                    {
                        File.WriteAllText(file.FullName, fileContent);
                        return Task.FromResult(true);
                    }
                }
            }
            catch (Exception exc)
            {
                _logger.LogError(exc, $"Unable to save Configuration to path {_jsonFilePath}");
                throw;
            }
        }
    }

    internal class JsonSettingsStorageException : Exception
    {
        public JsonSettingsStorageException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
