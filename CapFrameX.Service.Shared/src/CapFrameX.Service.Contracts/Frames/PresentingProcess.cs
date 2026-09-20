namespace CapFrameX.Service.Contracts.Frames;

/// <summary>A process the frame source currently sees presenting.</summary>
/// <param name="ProcessId">Operating system process id.</param>
/// <param name="ProcessName">Executable name without extension.</param>
/// <param name="GraphicsApi">Graphics API the source detected, when it knows one.</param>
public sealed record PresentingProcess(int ProcessId, string ProcessName, string? GraphicsApi = null);
