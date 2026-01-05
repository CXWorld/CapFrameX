namespace CapFrameX.Shared.Models;

/// <summary>
/// Information about a detected game process
/// </summary>
public class GameInfo
{
    public int Pid { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ExePath { get; set; } = string.Empty;
    public string Launcher { get; set; } = string.Empty;
    public DateTime DetectedTime { get; set; }
    public bool IsCapturing { get; set; }
}
