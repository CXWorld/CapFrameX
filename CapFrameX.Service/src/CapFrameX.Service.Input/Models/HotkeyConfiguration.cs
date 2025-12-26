namespace CapFrameX.Service.Input.Models;

/// <summary>
/// Configuration for all application hotkeys.
/// </summary>
public sealed class HotkeyConfiguration
{
    /// <summary>
    /// Debounce time in milliseconds to prevent rapid hotkey triggers.
    /// Default: 500ms (matches legacy behavior).
    /// </summary>
    public int DebounceMilliseconds { get; set; } = 500;

    /// <summary>
    /// Hotkey mappings for each action.
    /// </summary>
    public Dictionary<HotkeyAction, Hotkey> Hotkeys { get; set; } = new();

    /// <summary>
    /// Creates a default configuration with standard hotkeys.
    /// </summary>
    public static HotkeyConfiguration CreateDefault()
    {
        return new HotkeyConfiguration
        {
            DebounceMilliseconds = 500,
            Hotkeys = new Dictionary<HotkeyAction, Hotkey>
            {
                [HotkeyAction.Capture] = new Hotkey("F11"),
                [HotkeyAction.ToggleOverlay] = new Hotkey("F10"),
                [HotkeyAction.ConfigureOverlay] = new Hotkey("F9", ModifierKeys.Control),
                [HotkeyAction.ResetHistory] = new Hotkey("F5", ModifierKeys.Control),
                [HotkeyAction.ToggleThreadAffinity] = new Hotkey("F6", ModifierKeys.Control),
                [HotkeyAction.ResetMetrics] = new Hotkey("F7", ModifierKeys.Control)
            }
        };
    }

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (DebounceMilliseconds < 0)
            errors.Add("Debounce time cannot be negative");

        if (DebounceMilliseconds > 5000)
            errors.Add("Debounce time cannot exceed 5000ms");

        // Check for duplicate hotkey assignments
        var hotkeyGroups = Hotkeys
            .GroupBy(kvp => kvp.Value)
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var group in hotkeyGroups)
        {
            var actions = string.Join(", ", group.Select(kvp => kvp.Key));
            errors.Add($"Hotkey '{group.Key}' is assigned to multiple actions: {actions}");
        }

        return errors.Count == 0;
    }
}
