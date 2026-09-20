using CapFrameX.Service.Input.Contracts;
using CapFrameX.Service.Input.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace CapFrameX.Service.Input;

/// <summary>
/// Manages global hotkeys with debouncing and persistence.
/// Thread-safe implementation with configurable debounce timing.
/// </summary>
public sealed class HotkeyManager : IHotkeyManager
{
    private readonly ILogger<HotkeyManager> _logger;
    private readonly string _configurationFilePath;
    private readonly object _configLock = new();

    // Configuration
    private HotkeyConfiguration _configuration;

    // Handler management
    private readonly ConcurrentDictionary<HotkeyAction, Action> _handlers = new();

    // Debounce tracking - maps each action to its last trigger time
    private readonly ConcurrentDictionary<HotkeyAction, long> _lastTriggerTimes = new();

    // Enable/disable state
    private volatile bool _isEnabled = true;
    private volatile bool _isDisposed;

    public HotkeyManager(ILogger<HotkeyManager> logger, string? configurationFilePath = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configurationFilePath = configurationFilePath ?? GetDefaultConfigPath();
        _configuration = HotkeyConfiguration.CreateDefault();
    }

    public bool IsEnabled => _isEnabled && !_isDisposed;

    /// <summary>
    /// Simulates a hotkey trigger for testing or programmatic invocation.
    /// Respects debouncing rules.
    /// </summary>
    /// <param name="action">The action to trigger</param>
    /// <returns>True if the action was invoked, false if debounced</returns>
    public bool TriggerHotkey(HotkeyAction action)
    {
        ThrowIfDisposed();

        if (!_isEnabled)
        {
            _logger.LogDebug("Hotkey trigger ignored - manager is disabled");
            return false;
        }

        // Check if handler is registered
        if (!_handlers.TryGetValue(action, out var handler))
        {
            _logger.LogWarning("No handler registered for action {Action}", action);
            return false;
        }

        // Apply debouncing
        var now = Stopwatch.GetTimestamp();
        var debounceMs = _configuration.DebounceMilliseconds;
        var debounceTicks = debounceMs * Stopwatch.Frequency / 1000;

        if (_lastTriggerTimes.TryGetValue(action, out var lastTrigger))
        {
            var elapsed = now - lastTrigger;
            if (elapsed < debounceTicks)
            {
                var remainingMs = (debounceTicks - elapsed) * 1000 / Stopwatch.Frequency;
                _logger.LogDebug("Hotkey {Action} debounced. {RemainingMs}ms remaining",
                    action, remainingMs);
                return false;
            }
        }

        // Update last trigger time
        _lastTriggerTimes[action] = now;

        // Invoke handler
        _logger.LogInformation("Hotkey {Action} triggered", action);

        try
        {
            handler.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invoking handler for action {Action}", action);
            return false;
        }
    }

    public void RegisterHandler(HotkeyAction action, Action handler)
    {
        ThrowIfDisposed();

        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        _handlers[action] = handler;
        _logger.LogInformation("Handler registered for action {Action}", action);
    }

    public void UnregisterHandler(HotkeyAction action)
    {
        ThrowIfDisposed();

        if (_handlers.TryRemove(action, out _))
        {
            _lastTriggerTimes.TryRemove(action, out _);
            _logger.LogInformation("Handler unregistered for action {Action}", action);
        }
    }

    public bool UpdateHotkey(HotkeyAction action, Hotkey hotkey)
    {
        ThrowIfDisposed();

        if (hotkey == null)
            throw new ArgumentNullException(nameof(hotkey));

        lock (_configLock)
        {
            // Check if hotkey is already assigned to a different action
            var existingAssignment = _configuration.Hotkeys
                .Where(kvp => kvp.Value.Equals(hotkey) && kvp.Key != action)
                .Select(kvp => (HotkeyAction?)kvp.Key)
                .FirstOrDefault();

            if (existingAssignment.HasValue)
            {
                _logger.LogWarning(
                    "Hotkey {Hotkey} is already assigned to action {ExistingAction}",
                    hotkey, existingAssignment.Value);
                return false;
            }

            _configuration.Hotkeys[action] = hotkey;
            _logger.LogInformation("Hotkey for action {Action} updated to {Hotkey}", action, hotkey);
            return true;
        }
    }

    public HotkeyConfiguration GetConfiguration()
    {
        ThrowIfDisposed();

        lock (_configLock)
        {
            // Return a deep copy to prevent external modifications
            return new HotkeyConfiguration
            {
                DebounceMilliseconds = _configuration.DebounceMilliseconds,
                Hotkeys = new Dictionary<HotkeyAction, Hotkey>(_configuration.Hotkeys)
            };
        }
    }

    public bool UpdateConfiguration(HotkeyConfiguration configuration)
    {
        ThrowIfDisposed();

        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        // Validate configuration
        if (!configuration.IsValid(out var errors))
        {
            _logger.LogWarning("Invalid configuration: {Errors}", string.Join("; ", errors));
            return false;
        }

        lock (_configLock)
        {
            _configuration = new HotkeyConfiguration
            {
                DebounceMilliseconds = configuration.DebounceMilliseconds,
                Hotkeys = new Dictionary<HotkeyAction, Hotkey>(configuration.Hotkeys)
            };

            _logger.LogInformation("Configuration updated successfully");
            return true;
        }
    }

    public async Task SaveConfigurationAsync()
    {
        ThrowIfDisposed();

        try
        {
            HotkeyConfiguration configToSave;
            lock (_configLock)
            {
                configToSave = GetConfiguration();
            }

            // Prepare DTO for serialization
            var dto = new HotkeyConfigurationDto
            {
                DebounceMilliseconds = configToSave.DebounceMilliseconds,
                Hotkeys = configToSave.Hotkeys.ToDictionary(
                    kvp => kvp.Key.ToString(),
                    kvp => kvp.Value.ToString())
            };

            var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            var directory = Path.GetDirectoryName(_configurationFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(_configurationFilePath, json);
            _logger.LogInformation("Configuration saved to {Path}", _configurationFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save configuration to {Path}", _configurationFilePath);
            throw;
        }
    }

    public async Task LoadConfigurationAsync()
    {
        ThrowIfDisposed();

        try
        {
            if (!File.Exists(_configurationFilePath))
            {
                _logger.LogInformation("Configuration file not found, using defaults");
                return;
            }

            var json = await File.ReadAllTextAsync(_configurationFilePath);
            var dto = JsonSerializer.Deserialize<HotkeyConfigurationDto>(json);

            if (dto == null)
            {
                _logger.LogWarning("Failed to deserialize configuration, using defaults");
                return;
            }

            // Convert DTO to configuration
            var config = new HotkeyConfiguration
            {
                DebounceMilliseconds = dto.DebounceMilliseconds
            };

            foreach (var kvp in dto.Hotkeys)
            {
                if (Enum.TryParse<HotkeyAction>(kvp.Key, out var action) &&
                    Hotkey.TryParse(kvp.Value, out var hotkey))
                {
                    config.Hotkeys[action] = hotkey!;
                }
                else
                {
                    _logger.LogWarning("Invalid hotkey entry: {Key} = {Value}", kvp.Key, kvp.Value);
                }
            }

            // Validate and apply
            if (config.IsValid(out var errors))
            {
                UpdateConfiguration(config);
                _logger.LogInformation("Configuration loaded from {Path}", _configurationFilePath);
            }
            else
            {
                _logger.LogWarning("Loaded configuration is invalid: {Errors}", string.Join("; ", errors));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load configuration from {Path}", _configurationFilePath);
        }
    }

    public void Enable()
    {
        ThrowIfDisposed();
        _isEnabled = true;
        _logger.LogInformation("Hotkey manager enabled");
    }

    public void Disable()
    {
        ThrowIfDisposed();
        _isEnabled = false;
        _logger.LogInformation("Hotkey manager disabled");
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        _isDisposed = true;
        _isEnabled = false;
        _handlers.Clear();
        _lastTriggerTimes.Clear();

        _logger.LogInformation("Hotkey manager disposed");
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(HotkeyManager));
    }

    private static string GetDefaultConfigPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "CapFrameX", "hotkeys.json");
    }

    // DTO for JSON serialization
    private sealed class HotkeyConfigurationDto
    {
        public int DebounceMilliseconds { get; set; }
        public Dictionary<string, string> Hotkeys { get; set; } = new();
    }
}
