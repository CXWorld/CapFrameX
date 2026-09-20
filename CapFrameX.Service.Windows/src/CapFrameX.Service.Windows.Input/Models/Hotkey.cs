using System.Text;

namespace CapFrameX.Service.Input.Models;

/// <summary>
/// Represents a keyboard hotkey combination.
/// </summary>
public sealed class Hotkey : IEquatable<Hotkey>
{
    /// <summary>
    /// The primary key that triggers the hotkey.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Modifier keys that must be pressed (Control, Shift, Alt, Windows).
    /// </summary>
    public ModifierKeys Modifiers { get; }

    public Hotkey(string key, ModifierKeys modifiers = ModifierKeys.None)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        Key = key;
        Modifiers = modifiers;
    }

    /// <summary>
    /// Creates a hotkey from a string like "Control+Shift+F12" or "F12".
    /// </summary>
    public static Hotkey Parse(string hotkeyString)
    {
        if (string.IsNullOrWhiteSpace(hotkeyString))
            throw new ArgumentException("Hotkey string cannot be null or empty", nameof(hotkeyString));

        var parts = hotkeyString.Split('+', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            throw new FormatException("Invalid hotkey string format");

        var modifiers = ModifierKeys.None;
        string key = parts[^1]; // Last part is always the key

        // Parse modifiers
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (Enum.TryParse<ModifierKeys>(parts[i], true, out var modifier))
            {
                modifiers |= modifier;
            }
            else
            {
                throw new FormatException($"Unknown modifier: {parts[i]}");
            }
        }

        return new Hotkey(key, modifiers);
    }

    /// <summary>
    /// Tries to parse a hotkey string. Returns true if successful.
    /// </summary>
    public static bool TryParse(string hotkeyString, out Hotkey? hotkey)
    {
        try
        {
            hotkey = Parse(hotkeyString);
            return true;
        }
        catch
        {
            hotkey = null;
            return false;
        }
    }

    /// <summary>
    /// Returns the hotkey as a string like "Control+Shift+F12".
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder();

        if (Modifiers.HasFlag(ModifierKeys.Control))
            sb.Append("Control+");
        if (Modifiers.HasFlag(ModifierKeys.Shift))
            sb.Append("Shift+");
        if (Modifiers.HasFlag(ModifierKeys.Alt))
            sb.Append("Alt+");
        if (Modifiers.HasFlag(ModifierKeys.Windows))
            sb.Append("Windows+");

        sb.Append(Key);
        return sb.ToString();
    }

    public bool Equals(Hotkey? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(Key, other.Key, StringComparison.OrdinalIgnoreCase)
               && Modifiers == other.Modifiers;
    }

    public override bool Equals(object? obj) => obj is Hotkey other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Key.ToLowerInvariant(), Modifiers);

    public static bool operator ==(Hotkey? left, Hotkey? right) => Equals(left, right);
    public static bool operator !=(Hotkey? left, Hotkey? right) => !Equals(left, right);
}
