# CapFrameX.Service.Input

Cross-platform keyboard hotkey management service with debouncing and persistent configuration.

## Features

### Hotkey Management
- **Configurable Hotkeys**: Define hotkeys with modifier keys (Control, Shift, Alt, Windows)
- **Multiple Actions**: Support for 6 different hotkey actions (Capture, Overlay, etc.)
- **Duplicate Detection**: Prevents assigning the same hotkey to multiple actions
- **Runtime Updates**: Change hotkeys without restarting the application

### Debouncing (Anti-Bounce)
- **Time-Based Throttling**: Prevents rapid hotkey triggers (default: 500ms)
- **Per-Action Debouncing**: Each action has its own debounce timer
- **Configurable Timing**: Adjust debounce time from 0-5000ms
- **Thread-Safe**: Lock-free debounce checks using `Stopwatch.GetTimestamp()`

**Why Debouncing Matters**:
The legacy CapFrameX had issues where users could accidentally trigger capture twice in quick succession, causing:
- Starting and immediately stopping capture
- Duplicate actions firing
- Confusing state transitions

The debouncing mechanism prevents these issues by ignoring hotkey presses within the debounce window.

### Configuration Persistence
- **JSON Storage**: Human-readable configuration format
- **Automatic Save/Load**: Persist settings across application restarts
- **Default Location**: `%LocalAppData%\CapFrameX\hotkeys.json`
- **Custom Paths**: Support for custom configuration file locations
- **Validation**: Configuration validation before applying changes

## Architecture

### Models

#### `Hotkey`
Represents a keyboard combination with a trigger key and optional modifiers.

```csharp
// Simple hotkey
var hotkey = new Hotkey("F11");

// With modifiers
var hotkey = new Hotkey("F11", ModifierKeys.Control | ModifierKeys.Shift);

// From string
var hotkey = Hotkey.Parse("Control+Shift+F11");
```

#### `HotkeyAction`
Enum defining available actions:
- `Capture` - Start/stop frame capture
- `ToggleOverlay` - Show/hide overlay
- `ConfigureOverlay` - Open overlay configuration
- `ResetHistory` - Reset capture history
- `ToggleThreadAffinity` - Toggle thread affinity
- `ResetMetrics` - Reset metrics

#### `HotkeyConfiguration`
Complete configuration with all hotkeys and debounce settings.

```csharp
var config = HotkeyConfiguration.CreateDefault();
config.DebounceMilliseconds = 1000; // 1 second

config.Hotkeys[HotkeyAction.Capture] = new Hotkey("F11");
```

### Service

#### `IHotkeyManager`
Main service interface for hotkey management.

**Key Methods**:
- `RegisterHandler(action, handler)` - Register callback for an action
- `UpdateHotkey(action, hotkey)` - Change hotkey binding
- `TriggerHotkey(action)` - Programmatically trigger an action (respects debouncing)
- `Enable() / Disable()` - Enable/disable hotkey processing
- `SaveConfigurationAsync() / LoadConfigurationAsync()` - Persist configuration

## Usage

### Basic Setup

```csharp
using CapFrameX.Service.Input;
using CapFrameX.Service.Input.Models;
using Microsoft.Extensions.Logging;

// Create manager
var logger = loggerFactory.CreateLogger<HotkeyManager>();
var hotkeyManager = new HotkeyManager(logger);

// Load persisted configuration
await hotkeyManager.LoadConfigurationAsync();

// Register handlers
hotkeyManager.RegisterHandler(HotkeyAction.Capture, () =>
{
    Console.WriteLine("Capture hotkey triggered!");
    // Start/stop capture logic
});

hotkeyManager.RegisterHandler(HotkeyAction.ToggleOverlay, () =>
{
    Console.WriteLine("Toggle overlay");
});
```

### Changing Hotkeys

```csharp
// Update single hotkey
var newHotkey = new Hotkey("F12", ModifierKeys.Control);
if (hotkeyManager.UpdateHotkey(HotkeyAction.Capture, newHotkey))
{
    await hotkeyManager.SaveConfigurationAsync();
    Console.WriteLine("Hotkey updated!");
}
else
{
    Console.WriteLine("Hotkey already in use!");
}
```

### Complete Configuration Update

```csharp
var config = new HotkeyConfiguration
{
    DebounceMilliseconds = 750,
    Hotkeys = new Dictionary<HotkeyAction, Hotkey>
    {
        [HotkeyAction.Capture] = new Hotkey("F11"),
        [HotkeyAction.ToggleOverlay] = Hotkey.Parse("Control+F10"),
        [HotkeyAction.ResetHistory] = Hotkey.Parse("Control+Shift+R")
    }
};

if (hotkeyManager.UpdateConfiguration(config))
{
    await hotkeyManager.SaveConfigurationAsync();
}
```

### Programmatic Triggering

```csharp
// Trigger an action programmatically
// This respects the debounce timer
bool triggered = hotkeyManager.TriggerHotkey(HotkeyAction.Capture);

if (!triggered)
{
    Console.WriteLine("Action was debounced");
}
```

### Enable/Disable

```csharp
// Temporarily disable all hotkeys
hotkeyManager.Disable();

// Re-enable
hotkeyManager.Enable();

// Check status
bool isActive = hotkeyManager.IsEnabled;
```

## Debouncing Behavior

### Example Timeline

```
Time: 0ms    -> User presses F11 -> ✅ Capture triggered (callCount = 1)
Time: 100ms  -> User presses F11 -> ❌ Debounced (too soon)
Time: 200ms  -> User presses F11 -> ❌ Debounced (too soon)
Time: 600ms  -> User presses F11 -> ✅ Capture triggered (callCount = 2)
```

### Per-Action Debouncing

```csharp
manager.TriggerHotkey(HotkeyAction.Capture);        // ✅ Triggers
manager.TriggerHotkey(HotkeyAction.ToggleOverlay);  // ✅ Triggers (different action)
manager.TriggerHotkey(HotkeyAction.Capture);        // ❌ Debounced
manager.TriggerHotkey(HotkeyAction.ToggleOverlay);  // ❌ Debounced
```

## Configuration File Format

**Location**: `%LocalAppData%\CapFrameX\hotkeys.json`

```json
{
  "DebounceMilliseconds": 500,
  "Hotkeys": {
    "Capture": "F11",
    "ToggleOverlay": "F10",
    "ConfigureOverlay": "Control+F9",
    "ResetHistory": "Control+F5",
    "ToggleThreadAffinity": "Control+F6",
    "ResetMetrics": "Control+F7"
  }
}
```

## Testing

### Unit Tests (47 tests)

**HotkeyTests** (16 tests)
- Hotkey parsing and validation
- String formatting
- Equality and hashing
- Case-insensitive key comparison

**HotkeyConfigurationTests** (8 tests)
- Default configuration
- Validation rules
- Duplicate detection

**HotkeyManagerTests** (23 tests)
- Handler registration/unregistration
- Hotkey triggering
- Debouncing behavior
- Configuration persistence
- Enable/disable functionality
- Thread safety

### Running Tests

```bash
dotnet test CapFrameX.Service/tests/CapFrameX.Service.Input.Tests
```

## Design Decisions

### Why Debouncing Instead of Queuing?

The legacy system used a simple lock flag with 500ms delay. This approach:
- ✅ Prevents accidental rapid triggers
- ✅ Minimal overhead (no queue management)
- ✅ Predictable behavior
- ✅ Matches user expectations (ignore rapid presses)

### Why Lock-Free Debounce Checks?

Uses `Stopwatch.GetTimestamp()` and `ConcurrentDictionary` for:
- **Performance**: No lock contention on hotkey triggers
- **Accuracy**: High-precision timing
- **Scalability**: Multiple actions can be checked concurrently

### Why Nullable Pattern for Duplicate Detection?

Fixed enum pitfall where `default(KeyValuePair<Enum,T>).Key` equals the first enum value (0).
Using `HotkeyAction?` makes the "not found" case explicit.

## Comparison with Legacy

### Legacy (`CapFrameX.Hotkey`)
- ✅ Windows-specific (`Gma.System.MouseKeyHook`)
- ✅ Global system hooks
- ✅ Automatic key capture
- ❌ Heavy dependency on WinForms
- ❌ Not cross-platform
- ❌ Complex chord logic

### New (`CapFrameX.Service.Input`)
- ✅ Cross-platform ready
- ✅ Lightweight (no UI dependencies)
- ✅ Clean separation of concerns
- ✅ Testable (47 unit tests)
- ✅ Thread-safe
- ❌ Requires external key capture (planned for integration layer)

## Integration Notes

This library provides the **core hotkey management logic** but does not include:
- Global keyboard hooks (platform-specific)
- Key event capture
- UI for hotkey configuration

These will be provided by:
- **Windows**: `CapFrameX.Service.Input.Windows` (using Win32 hooks)
- **Linux**: `CapFrameX.Service.Input.Linux` (using X11/Wayland)
- **UI**: Angular/Tauri frontend for configuration

## Dependencies

- `Microsoft.Extensions.Logging.Abstractions` - Logging interface
- `System.Text.Json` - Configuration serialization

## License

Part of CapFrameX - Frame capture and analysis tool
