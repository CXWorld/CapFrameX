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
        private const int SaveAttempts = 4;

        private readonly object _iOLock = new object();
        private readonly string _jsonFilePath;
        private readonly ILogger<JsonSettingsStorage> _logger;
        private readonly Subject<int> _saveFileSubject = new Subject<int>();
        private readonly ReplaySubject<Exception> _saveFailures = new ReplaySubject<Exception>(1);

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
                        .Retry(SaveAttempts)
                        // A save that failed every attempt must not end this subscription: the
                        // error would reach Rx's default handler on the thread pool and terminate
                        // the process (AppSettings.json in a folder that Controlled folder access
                        // protects, or in an archive program's temporary folder that was deleted).
                        .Catch<bool, Exception>(exception =>
                        {
                            OnSaveFailed(exception);
                            return Observable.Return(false);
                        })
                        .Do(result =>
                        {
                            // Also after a failure, so that closing the window does not wait
                            // for a save that never comes.
                            lock (_lock)
                            {
                                if (_saveCompletionSource != null)
                                    _saveCompletionSource.TrySetResult(result);
                            }
                        })
                )
                .Subscribe();
        }

        /// <summary>
        /// Path of the settings file, AppSettings.json in the configuration folder.
        /// </summary>
        public string FilePath => _jsonFilePath;

        /// <summary>
        /// The error of every save that failed all its attempts. The settings stay in memory, and
        /// the next change tries again. Replays the latest failure to a late subscriber, so a
        /// failure during startup is not missed.
        /// </summary>
        public IObservable<Exception> SaveFailures => _saveFailures.AsObservable();

        private void OnSaveFailed(Exception exception)
        {
            _logger.LogError(exception, "Configuration was not saved to {Path} after {Attempts} attempts. The settings stay in memory, and the next change tries again.",
                _jsonFilePath, SaveAttempts);

            try
            {
                _saveFailures.OnNext(exception);
            }
            catch (Exception notifyException)
            {
                // A failing subscriber must not take the save pipeline down either.
                _logger.LogError(notifyException, "Error while reporting a failed configuration save.");
            }
        }

        public T GetValue<T>(string key)
        {
            // ConcurrentDictionary.TryGetValue is thread-safe
            if (_configDictionary.TryGetValue(key, out var value))
            {
                // Explicit null is a valid setting for optional values, such as the
                // active overlay design. It must remain distinct from a missing key.
                if (value == null && default(T) is null)
                {
                    return default;
                }
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
                        $"{typeof(T).Name} but found {value?.GetType().Name ?? "null"}");
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
                    if (!Equals(oldValue, value))
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
                _logger.LogError(exc, "{Message}", newException.Message);
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
                        _logger.LogError("Error writing configuration file {Path}. Serializing {EntryCount} settings produced no content.",
                            _jsonFilePath, snapshot.Length);
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
                _logger.LogError(exc, "Unable to save Configuration to path {Path}", _jsonFilePath);
                throw;
            }
        }

        /// <summary>
        /// Completes when the pending save has finished, successfully or not.
        /// </summary>
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
