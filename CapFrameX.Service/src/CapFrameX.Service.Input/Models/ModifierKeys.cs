namespace CapFrameX.Service.Input.Models;

/// <summary>
/// Modifier keys for hotkey combinations.
/// </summary>
[Flags]
public enum ModifierKeys
{
    None = 0,
    Control = 1 << 0,
    Shift = 1 << 1,
    Alt = 1 << 2,
    Windows = 1 << 3
}
