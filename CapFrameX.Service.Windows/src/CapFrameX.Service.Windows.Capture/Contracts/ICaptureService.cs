using System.Reactive.Subjects;

namespace CapFrameX.Service.Capture.Contracts;

/// <summary>
/// Interface for capture services that monitor frame timing data from external tools.
/// </summary>
public interface ICaptureService
{
    /// <summary>
    /// Mapping of parameter names to their indices in the captured data array.
    /// </summary>
    IReadOnlyDictionary<string, int> ParameterNameIndexMapping { get; }

    /// <summary>
    /// Observable stream of captured frame data.
    /// Each string[] represents a parsed CSV line with frame timing metrics.
    /// </summary>
    IObservable<string[]> FrameDataStream { get; }

    /// <summary>
    /// Stream indicating whether capture mode is currently active.
    /// </summary>
    Subject<bool> IsCaptureModeActiveStream { get; }

    /// <summary>
    /// Starts the capture service with the specified configuration.
    /// </summary>
    /// <param name="startInfo">Configuration for starting the capture process.</param>
    /// <returns>True if the service started successfully; otherwise, false.</returns>
    bool StartCaptureService(IServiceStartInfo startInfo);

    /// <summary>
    /// Stops the capture service and cleans up resources.
    /// </summary>
    /// <returns>True if the service stopped successfully; otherwise, false.</returns>
    bool StopCaptureService();

    /// <summary>
    /// Gets all currently monitored processes, filtered by the provided set.
    /// </summary>
    /// <param name="filter">Set of process names to filter.</param>
    /// <returns>Enumerable of (ProcessName, ProcessID) tuples.</returns>
    IEnumerable<(string ProcessName, int ProcessId)> GetAllFilteredProcesses(HashSet<string> filter);
}
