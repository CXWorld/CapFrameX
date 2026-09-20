using CapFrameX.Service.Input.Models;

namespace CapFrameX.Service.Input.Tests;

/// <summary>
/// Tests for the Hotkey model.
/// </summary>
public class HotkeyTests
{
    [Fact]
    public void Hotkey_Constructor_ShouldCreateValidHotkey()
    {
        // Act
        var hotkey = new Hotkey("F11", ModifierKeys.Control | ModifierKeys.Shift);

        // Assert
        Assert.Equal("F11", hotkey.Key);
        Assert.Equal(ModifierKeys.Control | ModifierKeys.Shift, hotkey.Modifiers);
    }

    [Fact]
    public void Hotkey_Constructor_WithNullKey_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new Hotkey(null!));
        Assert.Throws<ArgumentException>(() => new Hotkey(""));
        Assert.Throws<ArgumentException>(() => new Hotkey("   "));
    }

    [Theory]
    [InlineData("F11", "F11")]
    [InlineData("Control+F11", "Control+F11")]
    [InlineData("Shift+F11", "Shift+F11")]
    [InlineData("Alt+F11", "Alt+F11")]
    [InlineData("Windows+F11", "Windows+F11")]
    [InlineData("Control+Shift+F11", "Control+Shift+F11")]
    [InlineData("Control+Alt+F11", "Control+Alt+F11")]
    [InlineData("Control+Shift+Alt+F11", "Control+Shift+Alt+F11")]
    public void Hotkey_Parse_ShouldParseValidHotkeyStrings(string input, string expected)
    {
        // Act
        var hotkey = Hotkey.Parse(input);

        // Assert
        Assert.Equal(expected, hotkey.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("+")]
    [InlineData("InvalidModifier+F11")]
    public void Hotkey_Parse_WithInvalidString_ShouldThrow(string input)
    {
        // Act & Assert - Should throw ArgumentException or FormatException
        Assert.ThrowsAny<Exception>(() => Hotkey.Parse(input));
    }

    [Fact]
    public void Hotkey_TryParse_WithValidString_ShouldReturnTrue()
    {
        // Act
        var result = Hotkey.TryParse("Control+F11", out var hotkey);

        // Assert
        Assert.True(result);
        Assert.NotNull(hotkey);
        Assert.Equal("Control+F11", hotkey.ToString());
    }

    [Fact]
    public void Hotkey_TryParse_WithInvalidString_ShouldReturnFalse()
    {
        // Act
        var result = Hotkey.TryParse("Invalid+F11", out var hotkey);

        // Assert
        Assert.False(result);
        Assert.Null(hotkey);
    }

    [Fact]
    public void Hotkey_ToString_ShouldFormatCorrectly()
    {
        // Arrange
        var hotkey = new Hotkey("F12", ModifierKeys.Control | ModifierKeys.Alt);

        // Act
        var result = hotkey.ToString();

        // Assert
        Assert.Equal("Control+Alt+F12", result);
    }

    [Fact]
    public void Hotkey_Equals_WithSameHotkey_ShouldReturnTrue()
    {
        // Arrange
        var hotkey1 = new Hotkey("F11", ModifierKeys.Control);
        var hotkey2 = new Hotkey("F11", ModifierKeys.Control);

        // Act & Assert
        Assert.Equal(hotkey1, hotkey2);
        Assert.True(hotkey1 == hotkey2);
        Assert.False(hotkey1 != hotkey2);
    }

    [Fact]
    public void Hotkey_Equals_WithDifferentKey_ShouldReturnFalse()
    {
        // Arrange
        var hotkey1 = new Hotkey("F11", ModifierKeys.Control);
        var hotkey2 = new Hotkey("F12", ModifierKeys.Control);

        // Act & Assert
        Assert.NotEqual(hotkey1, hotkey2);
    }

    [Fact]
    public void Hotkey_Equals_WithDifferentModifiers_ShouldReturnFalse()
    {
        // Arrange
        var hotkey1 = new Hotkey("F11", ModifierKeys.Control);
        var hotkey2 = new Hotkey("F11", ModifierKeys.Shift);

        // Act & Assert
        Assert.NotEqual(hotkey1, hotkey2);
    }

    [Fact]
    public void Hotkey_Equals_CaseInsensitive_ShouldReturnTrue()
    {
        // Arrange
        var hotkey1 = new Hotkey("f11", ModifierKeys.Control);
        var hotkey2 = new Hotkey("F11", ModifierKeys.Control);

        // Act & Assert
        Assert.Equal(hotkey1, hotkey2);
    }

    [Fact]
    public void Hotkey_GetHashCode_WithSameHotkey_ShouldBeSame()
    {
        // Arrange
        var hotkey1 = new Hotkey("F11", ModifierKeys.Control);
        var hotkey2 = new Hotkey("F11", ModifierKeys.Control);

        // Act & Assert
        Assert.Equal(hotkey1.GetHashCode(), hotkey2.GetHashCode());
    }
}
