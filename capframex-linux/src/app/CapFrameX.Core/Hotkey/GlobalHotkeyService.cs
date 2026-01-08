using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using SharpHook;
using SharpHook.Native;

namespace CapFrameX.Core.Hotkey;

public interface IGlobalHotkeyService : IDisposable
{
    IObservable<Unit> CaptureHotkeyPressed { get; }
    void SetCaptureHotkey(string hotkeyString);
    void Start();
    void Stop();
    bool IsRunning { get; }
}

public class GlobalHotkeyService : IGlobalHotkeyService
{
    private TaskPoolGlobalHook? _hook;
    private readonly Subject<Unit> _captureHotkeyPressed = new();

    private KeyCode _captureKey = KeyCode.VcF12;
    private bool _requireCtrl;
    private bool _requireShift;
    private bool _requireAlt;

    private bool _isRunning;

    public IObservable<Unit> CaptureHotkeyPressed => _captureHotkeyPressed.AsObservable();
    public bool IsRunning => _isRunning;

    public void SetCaptureHotkey(string hotkeyString)
    {
        ParseHotkey(hotkeyString, out _captureKey, out _requireCtrl, out _requireShift, out _requireAlt);
        Console.WriteLine($"[GlobalHotkeyService] Hotkey set to: {hotkeyString} (Key={_captureKey}, Ctrl={_requireCtrl}, Shift={_requireShift}, Alt={_requireAlt})");
    }

    public void Start()
    {
        if (_isRunning) return;

        try
        {
            _hook = new TaskPoolGlobalHook();
            _hook.KeyPressed += OnKeyPressed;

            // Run hook on background thread
            Task.Run(() =>
            {
                try
                {
                    _hook.Run();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GlobalHotkeyService] Hook error: {ex.Message}");
                }
            });

            _isRunning = true;
            Console.WriteLine("[GlobalHotkeyService] Started global keyboard hook");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GlobalHotkeyService] Failed to start hook: {ex.Message}");
        }
    }

    public void Stop()
    {
        if (!_isRunning) return;

        try
        {
            _hook?.Dispose();
            _hook = null;
            _isRunning = false;
            Console.WriteLine("[GlobalHotkeyService] Stopped global keyboard hook");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GlobalHotkeyService] Error stopping hook: {ex.Message}");
        }
    }

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        // Check if pressed key matches capture hotkey
        if (e.Data.KeyCode == _captureKey && CheckModifiers(e.RawEvent.Mask))
        {
            Console.WriteLine($"[GlobalHotkeyService] Capture hotkey pressed!");
            _captureHotkeyPressed.OnNext(Unit.Default);
        }
    }

    private bool CheckModifiers(ModifierMask currentModifiers)
    {
        var ctrlPressed = currentModifiers.HasFlag(ModifierMask.LeftCtrl) || currentModifiers.HasFlag(ModifierMask.RightCtrl);
        var shiftPressed = currentModifiers.HasFlag(ModifierMask.LeftShift) || currentModifiers.HasFlag(ModifierMask.RightShift);
        var altPressed = currentModifiers.HasFlag(ModifierMask.LeftAlt) || currentModifiers.HasFlag(ModifierMask.RightAlt);

        // Check required modifiers match
        if (_requireCtrl != ctrlPressed) return false;
        if (_requireShift != shiftPressed) return false;
        if (_requireAlt != altPressed) return false;

        return true;
    }

    private static void ParseHotkey(string hotkeyString, out KeyCode keyCode, out bool requireCtrl, out bool requireShift, out bool requireAlt)
    {
        requireCtrl = false;
        requireShift = false;
        requireAlt = false;
        keyCode = KeyCode.VcF12; // Default

        if (string.IsNullOrWhiteSpace(hotkeyString))
            return;

        var parts = hotkeyString.Split('+');

        foreach (var part in parts)
        {
            var trimmed = part.Trim();

            // Check for modifiers
            if (trimmed.Equals("Control", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("Ctrl", StringComparison.OrdinalIgnoreCase))
            {
                requireCtrl = true;
            }
            else if (trimmed.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            {
                requireShift = true;
            }
            else if (trimmed.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            {
                requireAlt = true;
            }
            else
            {
                // Parse key code
                keyCode = ParseKeyCode(trimmed);
            }
        }
    }

    private static KeyCode ParseKeyCode(string keyName)
    {
        // Function keys F1-F24
        if (keyName.StartsWith("F", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(keyName.Substring(1), out var fNum) && fNum >= 1 && fNum <= 24)
        {
            return fNum switch
            {
                1 => KeyCode.VcF1,
                2 => KeyCode.VcF2,
                3 => KeyCode.VcF3,
                4 => KeyCode.VcF4,
                5 => KeyCode.VcF5,
                6 => KeyCode.VcF6,
                7 => KeyCode.VcF7,
                8 => KeyCode.VcF8,
                9 => KeyCode.VcF9,
                10 => KeyCode.VcF10,
                11 => KeyCode.VcF11,
                12 => KeyCode.VcF12,
                13 => KeyCode.VcF13,
                14 => KeyCode.VcF14,
                15 => KeyCode.VcF15,
                16 => KeyCode.VcF16,
                17 => KeyCode.VcF17,
                18 => KeyCode.VcF18,
                19 => KeyCode.VcF19,
                20 => KeyCode.VcF20,
                21 => KeyCode.VcF21,
                22 => KeyCode.VcF22,
                23 => KeyCode.VcF23,
                24 => KeyCode.VcF24,
                _ => KeyCode.VcF12
            };
        }

        // Common keys mapping
        return keyName.ToUpperInvariant() switch
        {
            "A" => KeyCode.VcA,
            "B" => KeyCode.VcB,
            "C" => KeyCode.VcC,
            "D" => KeyCode.VcD,
            "E" => KeyCode.VcE,
            "G" => KeyCode.VcG,
            "H" => KeyCode.VcH,
            "I" => KeyCode.VcI,
            "J" => KeyCode.VcJ,
            "K" => KeyCode.VcK,
            "L" => KeyCode.VcL,
            "M" => KeyCode.VcM,
            "N" => KeyCode.VcN,
            "O" => KeyCode.VcO,
            "P" => KeyCode.VcP,
            "Q" => KeyCode.VcQ,
            "R" => KeyCode.VcR,
            "S" => KeyCode.VcS,
            "T" => KeyCode.VcT,
            "U" => KeyCode.VcU,
            "V" => KeyCode.VcV,
            "W" => KeyCode.VcW,
            "X" => KeyCode.VcX,
            "Y" => KeyCode.VcY,
            "Z" => KeyCode.VcZ,
            "SPACE" => KeyCode.VcSpace,
            "ENTER" => KeyCode.VcEnter,
            "ESCAPE" or "ESC" => KeyCode.VcEscape,
            "TAB" => KeyCode.VcTab,
            "BACKSPACE" => KeyCode.VcBackspace,
            "INSERT" => KeyCode.VcInsert,
            "DELETE" => KeyCode.VcDelete,
            "HOME" => KeyCode.VcHome,
            "END" => KeyCode.VcEnd,
            "PAGEUP" => KeyCode.VcPageUp,
            "PAGEDOWN" => KeyCode.VcPageDown,
            "PAUSE" => KeyCode.VcPause,
            "PRINTSCREEN" => KeyCode.VcPrintScreen,
            // Number keys
            "0" => KeyCode.Vc0,
            "1" => KeyCode.Vc1,
            "2" => KeyCode.Vc2,
            "3" => KeyCode.Vc3,
            "4" => KeyCode.Vc4,
            "5" => KeyCode.Vc5,
            "6" => KeyCode.Vc6,
            "7" => KeyCode.Vc7,
            "8" => KeyCode.Vc8,
            "9" => KeyCode.Vc9,
            // Punctuation and special characters
            "," or "COMMA" => KeyCode.VcComma,
            "." or "PERIOD" => KeyCode.VcPeriod,
            "/" or "SLASH" => KeyCode.VcSlash,
            "\\" or "BACKSLASH" => KeyCode.VcBackslash,
            "-" or "MINUS" => KeyCode.VcMinus,
            "=" or "EQUALS" => KeyCode.VcEquals,
            "[" or "OPENBRACKET" => KeyCode.VcOpenBracket,
            "]" or "CLOSEBRACKET" => KeyCode.VcCloseBracket,
            ";" or "SEMICOLON" => KeyCode.VcSemicolon,
            "'" or "QUOTE" => KeyCode.VcQuote,
            "`" or "BACKQUOTE" => KeyCode.VcBackQuote,
            // European keyboard extra key (< > | key next to left shift)
            "<" or ">" or "LESS" or "GREATER" or "OEM102" => KeyCode.Vc102,
            _ => KeyCode.VcF12 // Default fallback
        };
    }

    public void Dispose()
    {
        Stop();
        _captureHotkeyPressed.Dispose();
    }
}
