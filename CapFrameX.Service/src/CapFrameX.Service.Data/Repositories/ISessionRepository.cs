using CapFrameX.Service.Data.Models;

namespace CapFrameX.Service.Data.Repositories;

/// <summary>
/// Repository interface for Session operations.
/// Designed for REST API consumption.
/// </summary>
public interface ISessionRepository
{
    /// <summary>
    /// Get all sessions with optional filtering
    /// </summary>
    Task<IEnumerable<Session>> GetAllAsync(
        Guid? suiteId = null,
        string? gameName = null,
        string? processor = null,
        string? gpu = null,
        int skip = 0,
        int take = 100);

    /// <summary>
    /// Get session by ID including runs
    /// </summary>
    Task<Session?> GetByIdAsync(Guid id, bool includeRuns = false);

    /// <summary>
    /// Get sessions by suite ID
    /// </summary>
    Task<IEnumerable<Session>> GetBySuiteIdAsync(Guid suiteId, bool includeRuns = false);

    /// <summary>
    /// Create a new session
    /// </summary>
    Task<Session> CreateAsync(Session session);

    /// <summary>
    /// Update an existing session
    /// </summary>
    Task<Session> UpdateAsync(Session session);

    /// <summary>
    /// Delete a session (cascades to runs)
    /// </summary>
    Task DeleteAsync(Guid id);

    /// <summary>
    /// Get total count of sessions
    /// </summary>
    Task<int> CountAsync(Guid? suiteId = null, string? gameName = null);

    /// <summary>
    /// Search sessions by game name
    /// </summary>
    Task<IEnumerable<Session>> SearchByGameAsync(string searchTerm, int skip = 0, int take = 50);
}
