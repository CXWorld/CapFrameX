using CapFrameX.Service.Input.Models;

namespace CapFrameX.Service.Input.Contracts;

/// <summary>
/// Manages global hotkeys with debouncing and persistence.
/// </summary>
public interface IHotkeyManager : IDisposable
{
    /// <summary>
    /// Registers a handler for a specific hotkey action.
    /// </summary>
    /// <param name="action">The action to register</param>
    /// <param name="handler">The handler to invoke when the hotkey is triggered</param>
    void RegisterHandler(HotkeyAction action, Action handler);

    /// <summary>
    /// Unregisters the handler for a specific action.
    /// </summary>
    void UnregisterHandler(HotkeyAction action);

    /// <summary>
    /// Updates the hotkey for a specific action.
    /// </summary>
    /// <param name="action">The action to update</param>
    /// <param name="hotkey">The new hotkey</param>
    /// <returns>True if successful, false if hotkey is already assigned to another action</returns>
    bool UpdateHotkey(HotkeyAction action, Hotkey hotkey);

    /// <summary>
    /// Gets the current hotkey configuration.
    /// </summary>
    HotkeyConfiguration GetConfiguration();

    /// <summary>
    /// Updates the entire configuration.
    /// </summary>
    /// <param name="configuration">The new configuration</param>
    /// <returns>True if successful, false if configuration is invalid</returns>
    bool UpdateConfiguration(HotkeyConfiguration configuration);

    /// <summary>
    /// Saves the current configuration to persistent storage.
    /// </summary>
    Task SaveConfigurationAsync();

    /// <summary>
    /// Loads configuration from persistent storage.
    /// </summary>
    Task LoadConfigurationAsync();

    /// <summary>
    /// Enables hotkey processing.
    /// </summary>
    void Enable();

    /// <summary>
    /// Disables hotkey processing.
    /// </summary>
    void Disable();

    /// <summary>
    /// Gets whether hotkey processing is currently enabled.
    /// </summary>
    bool IsEnabled { get; }
}
