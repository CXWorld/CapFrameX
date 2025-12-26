using CapFrameX.Service.Input.Models;

namespace CapFrameX.Service.Input.Tests;

/// <summary>
/// Tests for the HotkeyConfiguration model.
/// </summary>
public class HotkeyConfigurationTests
{
    [Fact]
    public void HotkeyConfiguration_CreateDefault_ShouldHaveValidDefaults()
    {
        // Act
        var config = HotkeyConfiguration.CreateDefault();

        // Assert
        Assert.Equal(500, config.DebounceMilliseconds);
        Assert.NotEmpty(config.Hotkeys);
        Assert.True(config.Hotkeys.Count >= 6); // Should have at least 6 default hotkeys
    }

    [Fact]
    public void HotkeyConfiguration_CreateDefault_ShouldHaveAllActions()
    {
        // Act
        var config = HotkeyConfiguration.CreateDefault();

        // Assert - All enum values should have hotkeys
        var allActions = Enum.GetValues<HotkeyAction>();
        foreach (var action in allActions)
        {
            Assert.True(config.Hotkeys.ContainsKey(action),
                $"Default configuration should include hotkey for {action}");
        }
    }

    [Fact]
    public void HotkeyConfiguration_IsValid_WithValidConfig_ShouldReturnTrue()
    {
        // Arrange
        var config = HotkeyConfiguration.CreateDefault();

        // Act
        var isValid = config.IsValid(out var errors);

        // Assert
        Assert.True(isValid);
        Assert.Empty(errors);
    }

    [Fact]
    public void HotkeyConfiguration_IsValid_WithNegativeDebounce_ShouldReturnFalse()
    {
        // Arrange
        var config = HotkeyConfiguration.CreateDefault();
        config.DebounceMilliseconds = -1;

        // Act
        var isValid = config.IsValid(out var errors);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("negative"));
    }

    [Fact]
    public void HotkeyConfiguration_IsValid_WithExcessiveDebounce_ShouldReturnFalse()
    {
        // Arrange
        var config = HotkeyConfiguration.CreateDefault();
        config.DebounceMilliseconds = 10000; // 10 seconds

        // Act
        var isValid = config.IsValid(out var errors);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("5000"));
    }

    [Fact]
    public void HotkeyConfiguration_IsValid_WithDuplicateHotkeys_ShouldReturnFalse()
    {
        // Arrange
        var config = HotkeyConfiguration.CreateDefault();
        var duplicateHotkey = new Hotkey("F11");
        config.Hotkeys[HotkeyAction.Capture] = duplicateHotkey;
        config.Hotkeys[HotkeyAction.ToggleOverlay] = duplicateHotkey;

        // Act
        var isValid = config.IsValid(out var errors);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("multiple actions"));
    }

    [Fact]
    public void HotkeyConfiguration_IsValid_WithMultipleDuplicates_ShouldReportAll()
    {
        // Arrange
        var config = HotkeyConfiguration.CreateDefault();
        var hotkey1 = new Hotkey("F11");
        var hotkey2 = new Hotkey("F12");

        config.Hotkeys[HotkeyAction.Capture] = hotkey1;
        config.Hotkeys[HotkeyAction.ToggleOverlay] = hotkey1;
        config.Hotkeys[HotkeyAction.ResetHistory] = hotkey2;
        config.Hotkeys[HotkeyAction.ResetMetrics] = hotkey2;

        // Act
        var isValid = config.IsValid(out var errors);

        // Assert
        Assert.False(isValid);
        Assert.True(errors.Count >= 2); // Should report both duplicate pairs
    }
}
