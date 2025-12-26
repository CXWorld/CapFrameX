using CapFrameX.Service.Input.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace CapFrameX.Service.Input.Tests;

/// <summary>
/// Tests for the HotkeyManager service including debouncing.
/// </summary>
public class HotkeyManagerTests : IDisposable
{
    private readonly HotkeyManager _manager;
    private readonly string _tempConfigPath;

    public HotkeyManagerTests()
    {
        _tempConfigPath = Path.Combine(Path.GetTempPath(), $"hotkeys_test_{Guid.NewGuid()}.json");
        _manager = new HotkeyManager(NullLogger<HotkeyManager>.Instance, _tempConfigPath);
    }

    [Fact]
    public void HotkeyManager_Constructor_ShouldInitializeWithDefaults()
    {
        // Assert
        Assert.True(_manager.IsEnabled);
        var config = _manager.GetConfiguration();
        Assert.Equal(500, config.DebounceMilliseconds);
    }

    [Fact]
    public void RegisterHandler_ShouldAllowHandlerRegistration()
    {
        // Arrange
        var called = false;
        Action handler = () => called = true;

        // Act
        _manager.RegisterHandler(HotkeyAction.Capture, handler);
        _manager.TriggerHotkey(HotkeyAction.Capture);

        // Assert
        Assert.True(called);
    }

    [Fact]
    public void UnregisterHandler_ShouldRemoveHandler()
    {
        // Arrange
        var called = false;
        Action handler = () => called = true;
        _manager.RegisterHandler(HotkeyAction.Capture, handler);

        // Act
        _manager.UnregisterHandler(HotkeyAction.Capture);
        _manager.TriggerHotkey(HotkeyAction.Capture);

        // Assert
        Assert.False(called);
    }

    [Fact]
    public void TriggerHotkey_WithoutHandler_ShouldReturnFalse()
    {
        // Act
        var result = _manager.TriggerHotkey(HotkeyAction.Capture);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void TriggerHotkey_WithHandler_ShouldInvokeHandler()
    {
        // Arrange
        var callCount = 0;
        _manager.RegisterHandler(HotkeyAction.Capture, () => callCount++);

        // Act
        _manager.TriggerHotkey(HotkeyAction.Capture);

        // Assert
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task TriggerHotkey_RapidFire_ShouldDebounce()
    {
        // Arrange
        var callCount = 0;
        _manager.RegisterHandler(HotkeyAction.Capture, () => callCount++);

        // Act - Trigger multiple times rapidly
        _manager.TriggerHotkey(HotkeyAction.Capture); // Should succeed
        var result2 = _manager.TriggerHotkey(HotkeyAction.Capture); // Should be debounced
        var result3 = _manager.TriggerHotkey(HotkeyAction.Capture); // Should be debounced

        // Assert
        Assert.Equal(1, callCount);
        Assert.False(result2);
        Assert.False(result3);

        // Wait for debounce to expire
        await Task.Delay(600);

        // Should work again
        var result4 = _manager.TriggerHotkey(HotkeyAction.Capture);
        Assert.True(result4);
        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task TriggerHotkey_DifferentActions_ShouldNotInterfere()
    {
        // Arrange
        var captureCount = 0;
        var overlayCount = 0;
        _manager.RegisterHandler(HotkeyAction.Capture, () => captureCount++);
        _manager.RegisterHandler(HotkeyAction.ToggleOverlay, () => overlayCount++);

        // Act - Trigger both actions rapidly
        _manager.TriggerHotkey(HotkeyAction.Capture);
        _manager.TriggerHotkey(HotkeyAction.ToggleOverlay);
        _manager.TriggerHotkey(HotkeyAction.Capture); // Debounced
        _manager.TriggerHotkey(HotkeyAction.ToggleOverlay); // Debounced

        // Assert - Each action should have its own debounce timer
        Assert.Equal(1, captureCount);
        Assert.Equal(1, overlayCount);
    }

    [Fact]
    public void UpdateHotkey_WithValidHotkey_ShouldSucceed()
    {
        // Arrange
        var newHotkey = new Hotkey("F12", ModifierKeys.Control);

        // Act
        var result = _manager.UpdateHotkey(HotkeyAction.Capture, newHotkey);

        // Assert
        Assert.True(result);
        var config = _manager.GetConfiguration();
        Assert.Equal(newHotkey, config.Hotkeys[HotkeyAction.Capture]);
    }

    [Fact]
    public void UpdateHotkey_WithDuplicateHotkey_ShouldFail()
    {
        // Arrange - Use a unique hotkey not in defaults
        var hotkey = new Hotkey("F20", ModifierKeys.Control | ModifierKeys.Shift);
        var firstUpdate = _manager.UpdateHotkey(HotkeyAction.Capture, hotkey);
        Assert.True(firstUpdate, "First hotkey assignment should succeed");

        // Act - Try to assign same hotkey to different action
        var result = _manager.UpdateHotkey(HotkeyAction.ToggleOverlay, hotkey);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void UpdateConfiguration_WithValidConfig_ShouldSucceed()
    {
        // Arrange
        var newConfig = new HotkeyConfiguration
        {
            DebounceMilliseconds = 1000,
            Hotkeys = new Dictionary<HotkeyAction, Hotkey>
            {
                [HotkeyAction.Capture] = new Hotkey("F1"),
                [HotkeyAction.ToggleOverlay] = new Hotkey("F2"),
                [HotkeyAction.ConfigureOverlay] = new Hotkey("F3"),
                [HotkeyAction.ResetHistory] = new Hotkey("F4"),
                [HotkeyAction.ToggleThreadAffinity] = new Hotkey("F5"),
                [HotkeyAction.ResetMetrics] = new Hotkey("F6")
            }
        };

        // Act
        var result = _manager.UpdateConfiguration(newConfig);

        // Assert
        Assert.True(result);
        var config = _manager.GetConfiguration();
        Assert.Equal(1000, config.DebounceMilliseconds);
        Assert.Equal(new Hotkey("F1"), config.Hotkeys[HotkeyAction.Capture]);
    }

    [Fact]
    public void UpdateConfiguration_WithInvalidConfig_ShouldFail()
    {
        // Arrange
        var invalidConfig = new HotkeyConfiguration
        {
            DebounceMilliseconds = -1 // Invalid
        };

        // Act
        var result = _manager.UpdateConfiguration(invalidConfig);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Disable_ShouldPreventHotkeyTriggers()
    {
        // Arrange
        var callCount = 0;
        _manager.RegisterHandler(HotkeyAction.Capture, () => callCount++);

        // Act
        _manager.Disable();
        _manager.TriggerHotkey(HotkeyAction.Capture);

        // Assert
        Assert.False(_manager.IsEnabled);
        Assert.Equal(0, callCount);
    }

    [Fact]
    public void Enable_ShouldAllowHotkeyTriggers()
    {
        // Arrange
        var callCount = 0;
        _manager.RegisterHandler(HotkeyAction.Capture, () => callCount++);
        _manager.Disable();

        // Act
        _manager.Enable();
        _manager.TriggerHotkey(HotkeyAction.Capture);

        // Assert
        Assert.True(_manager.IsEnabled);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task SaveConfiguration_ShouldPersistToFile()
    {
        // Arrange
        var newHotkey = new Hotkey("F12", ModifierKeys.Control);
        _manager.UpdateHotkey(HotkeyAction.Capture, newHotkey);

        // Act
        await _manager.SaveConfigurationAsync();

        // Assert
        Assert.True(File.Exists(_tempConfigPath));
        var json = await File.ReadAllTextAsync(_tempConfigPath);
        Assert.Contains("F12", json);
        Assert.Contains("Control", json);
    }

    [Fact]
    public async Task LoadConfiguration_ShouldRestoreFromFile()
    {
        // Arrange - Save configuration
        var newHotkey = new Hotkey("F12", ModifierKeys.Control);
        _manager.UpdateHotkey(HotkeyAction.Capture, newHotkey);
        await _manager.SaveConfigurationAsync();

        // Create new manager instance
        using var newManager = new HotkeyManager(NullLogger<HotkeyManager>.Instance, _tempConfigPath);

        // Act
        await newManager.LoadConfigurationAsync();

        // Assert
        var config = newManager.GetConfiguration();
        Assert.Equal(newHotkey, config.Hotkeys[HotkeyAction.Capture]);
    }

    [Fact]
    public async Task LoadConfiguration_WithMissingFile_ShouldUseDefaults()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.json");
        using var manager = new HotkeyManager(NullLogger<HotkeyManager>.Instance, nonExistentPath);

        // Act
        await manager.LoadConfigurationAsync();

        // Assert - Should not throw and use defaults
        var config = manager.GetConfiguration();
        Assert.Equal(500, config.DebounceMilliseconds);
    }

    [Fact]
    public void Dispose_ShouldCleanUpResources()
    {
        // Arrange
        var callCount = 0;
        _manager.RegisterHandler(HotkeyAction.Capture, () => callCount++);

        // Act
        _manager.Dispose();

        // Assert
        Assert.False(_manager.IsEnabled);
        Assert.Throws<ObjectDisposedException>(() => _manager.TriggerHotkey(HotkeyAction.Capture));
    }

    [Fact]
    public async Task Debounce_WithCustomTime_ShouldRespectConfiguration()
    {
        // Arrange
        var callCount = 0;
        var config = HotkeyConfiguration.CreateDefault();
        config.DebounceMilliseconds = 100; // Short debounce for testing
        _manager.UpdateConfiguration(config);
        _manager.RegisterHandler(HotkeyAction.Capture, () => callCount++);

        // Act
        _manager.TriggerHotkey(HotkeyAction.Capture);
        await Task.Delay(50); // Less than debounce
        _manager.TriggerHotkey(HotkeyAction.Capture);

        // Assert - Second trigger should be debounced
        Assert.Equal(1, callCount);

        // Wait for debounce to expire
        await Task.Delay(60);
        _manager.TriggerHotkey(HotkeyAction.Capture);

        // Assert - Should trigger again
        Assert.Equal(2, callCount);
    }

    public void Dispose()
    {
        _manager?.Dispose();

        if (File.Exists(_tempConfigPath))
        {
            File.Delete(_tempConfigPath);
        }
    }
}
