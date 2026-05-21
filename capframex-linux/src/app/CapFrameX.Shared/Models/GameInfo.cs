using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CapFrameX.Shared.Models;

/// <summary>
/// Information about a detected game process
/// </summary>
public class GameInfo : INotifyPropertyChanged
{
    private int _pid;
    private string _name = string.Empty;
    private string _exePath = string.Empty;
    private string _launcher = string.Empty;
    private string _gpuName = string.Empty;
    private int _resolutionWidth;
    private int _resolutionHeight;
    private DateTime _detectedTime;
    private bool _isCapturing;
    private bool _presentTimingSupported;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Pid
    {
        get => _pid;
        set => SetField(ref _pid, value);
    }

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public string ExePath
    {
        get => _exePath;
        set => SetField(ref _exePath, value);
    }

    public string Launcher
    {
        get => _launcher;
        set => SetField(ref _launcher, value);
    }

    public string GpuName
    {
        get => _gpuName;
        set => SetField(ref _gpuName, value);
    }

    public int ResolutionWidth
    {
        get => _resolutionWidth;
        set
        {
            if (SetField(ref _resolutionWidth, value))
                OnPropertyChanged(nameof(Resolution));
        }
    }

    public int ResolutionHeight
    {
        get => _resolutionHeight;
        set
        {
            if (SetField(ref _resolutionHeight, value))
                OnPropertyChanged(nameof(Resolution));
        }
    }

    public DateTime DetectedTime
    {
        get => _detectedTime;
        set => SetField(ref _detectedTime, value);
    }

    public bool IsCapturing
    {
        get => _isCapturing;
        set => SetField(ref _isCapturing, value);
    }

    public bool PresentTimingSupported
    {
        get => _presentTimingSupported;
        set => SetField(ref _presentTimingSupported, value);
    }

    /// <summary>
    /// Display name for timing mode
    /// </summary>
    public string TimingMode => _presentTimingSupported ? "Present Timing" : "Layer Timing";

    public string Resolution => ResolutionWidth > 0 && ResolutionHeight > 0
        ? $"{ResolutionWidth}x{ResolutionHeight}"
        : string.Empty;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
