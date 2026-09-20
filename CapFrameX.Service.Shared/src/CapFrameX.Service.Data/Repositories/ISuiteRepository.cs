using CapFrameX.Service.Data.Models;

namespace CapFrameX.Service.Data.Repositories;

/// <summary>
/// Repository interface for Suite operations.
/// Designed for REST API consumption.
/// </summary>
public interface ISuiteRepository
{
    /// <summary>
    /// Get all suites with optional filtering
    /// </summary>
    Task<IEnumerable<Suite>> GetAllAsync(SuiteType? type = null, int skip = 0, int take = 100);

    /// <summary>
    /// Get suite by ID including sessions
    /// </summary>
    Task<Suite?> GetByIdAsync(Guid id, bool includeSessions = false);

    /// <summary>
    /// Create a new suite
    /// </summary>
    Task<Suite> CreateAsync(Suite suite);

    /// <summary>
    /// Update an existing suite
    /// </summary>
    Task<Suite> UpdateAsync(Suite suite);

    /// <summary>
    /// Delete a suite (cascades to sessions and runs)
    /// </summary>
    Task DeleteAsync(Guid id);

    /// <summary>
    /// Get total count of suites
    /// </summary>
    Task<int> CountAsync(SuiteType? type = null);
}
