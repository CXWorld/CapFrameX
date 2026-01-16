using CapFrameX.Contracts.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
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
        private readonly string _jsonFilePath;
        private readonly ILogger<JsonSettingsStorage> _logger;
        private readonly Subject<int> _saveFileSubject = new Subject<int>();

        // Changed to ConcurrentDictionary for thread-safe access
        private readonly ConcurrentDictionary<string, object> _configDictionary = new ConcurrentDictionary<string, object>();

        private TaskCompletionSource<bool> _saveCompletionSource = null;
        private readonly object _lock = new object();

        public JsonSettingsStorage(ILogger<JsonSettingsStorage> logger, IPathService pathService)
        {
            _logger = logger;
            _jsonFilePath = Path.Combine(pathService.ConfigFolder, "AppSettings.json");

            _saveFileSubject
                .AsObservable()
                .Do(_ =>
                {
                    lock (_lock)
                    {
                        if (_saveCompletionSource == null || _saveCompletionSource.Task.IsCompleted)
                            _saveCompletionSource = new TaskCompletionSource<bool>();
                    }
                })
                .Throttle(TimeSpan.FromMilliseconds(1000))
                .SelectMany(_ =>
                    Observable.Timer(TimeSpan.FromMilliseconds(200))
                        .Select(x =>
                        {
                            Save();
                            return true;
                        })
                        .Do(result =>
                        {
                            lock (_lock)
                            {
                                if (_saveCompletionSource != null)
                                    _saveCompletionSource.TrySetResult(true);
                            }
                        })
                        .Retry(4)
                )
                .Subscribe();
        }

        public T GetValue<T>(string key)
        {
            // ConcurrentDictionary.TryGetValue is thread-safe
            if (_configDictionary.TryGetValue(key, out var value))
            {
                if (value is long) value = Convert.ToInt32(value);
                if (!(value is T val))
                {
                    if (value is JObject jObject)
                    {
                        var convertedObject = jObject.ToObject(typeof(T));
                        if (convertedObject is T convertedObjectTyped)
                        {
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
            // Use AddOrUpdate for atomic check-and-set
            var valueChanged = false;
            _configDictionary.AddOrUpdate(key,
                // Add factory: key doesn't exist
                addValue => { valueChanged = true; return value; },
                // Update factory: key exists, check if value changed
                (k, oldValue) =>
                {
                    if (!oldValue.Equals(value))
                    {
                        valueChanged = true;
                        return value;
                    }
                    return oldValue;
                });

            if (valueChanged)
            {
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
                        _configDictionary.TryAdd(kvp.Key, kvp.Value);
                    }

                    return Task.FromResult(true);
                }
            }
            catch (Exception exc)
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

                    // ToArray() creates an atomic snapshot of the ConcurrentDictionary
                    var snapshot = _configDictionary.ToArray();
                    var fileContent = JsonConvert.SerializeObject(
                        snapshot.ToDictionary(x => x.Key, x => x.Value),
                        Formatting.Indented);

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

        public Task WaitForPendingSaveAsync()
        {
            lock (_lock)
            {
                return _saveCompletionSource != null ? _saveCompletionSource.Task : Task.CompletedTask;
            }
        }
    }

    internal class JsonSettingsStorageException : Exception
    {
        public JsonSettingsStorageException(string message, Exception innerException) : base(message, innerException) { }
    }
}