using CapFrameX.Service.Data.Models;

namespace CapFrameX.Service.Data.Repositories;

/// <summary>
/// Repository interface for SessionRun operations.
/// Designed for REST API consumption.
/// </summary>
public interface ISessionRunRepository
{
    /// <summary>
    /// Get all runs with optional filtering and sorting
    /// </summary>
    Task<IEnumerable<SessionRun>> GetAllAsync(
        Guid? sessionId = null,
        double? minAverageFps = null,
        double? maxAverageFps = null,
        string? sortBy = null,
        bool descending = true,
        int skip = 0,
        int take = 100);

    /// <summary>
    /// Get run by ID
    /// </summary>
    Task<SessionRun?> GetByIdAsync(Guid id);

    /// <summary>
    /// Get runs by session ID
    /// </summary>
    Task<IEnumerable<SessionRun>> GetBySessionIdAsync(Guid sessionId);

    /// <summary>
    /// Create a new run
    /// </summary>
    Task<SessionRun> CreateAsync(SessionRun run);

    /// <summary>
    /// Update an existing run
    /// </summary>
    Task<SessionRun> UpdateAsync(SessionRun run);

    /// <summary>
    /// Delete a run
    /// </summary>
    Task DeleteAsync(Guid id);

    /// <summary>
    /// Get total count of runs
    /// </summary>
    Task<int> CountAsync(Guid? sessionId = null);

    /// <summary>
    /// Get top performing runs
    /// </summary>
    Task<IEnumerable<SessionRun>> GetTopRunsAsync(
        string metric = "AverageFps",
        int count = 10);
}
