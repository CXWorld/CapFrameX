using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CapFrameX.Core.Configuration;
using CapFrameX.Core.Hotkey;

namespace CapFrameX.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IGlobalHotkeyService _hotkeyService;

    [ObservableProperty]
    private string _captureHotkey = "F12";

    [ObservableProperty]
    private decimal? _captureDurationSeconds;

    [ObservableProperty]
    private bool _autoStopEnabled;

    [ObservableProperty]
    private bool _isRecordingHotkey;

    [ObservableProperty]
    private string _hotkeyStatus = "";

    public SettingsViewModel(ISettingsService settingsService, IGlobalHotkeyService hotkeyService)
    {
        _settingsService = settingsService;
        _hotkeyService = hotkeyService;

        // Load current settings
        var settings = _settingsService.Settings;
        _captureHotkey = settings.CaptureHotkey;
        _captureDurationSeconds = settings.CaptureDurationSeconds;
        _autoStopEnabled = settings.AutoStopEnabled;

        UpdateHotkeyStatus();
    }

    partial void OnCaptureHotkeyChanged(string value)
    {
        SaveSettings();
        UpdateHotkeyStatus();
    }

    partial void OnCaptureDurationSecondsChanged(decimal? value)
    {
        if (value.HasValue)
            SaveSettings();
    }

    partial void OnAutoStopEnabledChanged(bool value)
    {
        SaveSettings();
    }

    private void SaveSettings()
    {
        _settingsService.UpdateSettings(settings =>
        {
            settings.CaptureHotkey = CaptureHotkey;
            settings.CaptureDurationSeconds = (int)(CaptureDurationSeconds ?? 0);
            settings.AutoStopEnabled = AutoStopEnabled;
        });
    }

    private void UpdateHotkeyStatus()
    {
        HotkeyStatus = $"Current: {CaptureHotkey}";
    }

    [RelayCommand]
    private void SetHotkey(string hotkey)
    {
        CaptureHotkey = hotkey;
    }

    [RelayCommand]
    private void SetDuration(string secondsStr)
    {
        if (int.TryParse(secondsStr, out var seconds))
            CaptureDurationSeconds = seconds;
    }
}
