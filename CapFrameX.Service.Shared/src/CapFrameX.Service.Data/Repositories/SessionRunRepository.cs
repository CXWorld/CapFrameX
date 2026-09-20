using CapFrameX.Service.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace CapFrameX.Service.Data.Repositories;

/// <summary>
/// Repository implementation for SessionRun operations.
/// </summary>
public class SessionRunRepository : ISessionRunRepository
{
    private readonly CapFrameXDbContext _context;

    public SessionRunRepository(CapFrameXDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IEnumerable<SessionRun>> GetAllAsync(
        Guid? sessionId = null,
        double? minAverageFps = null,
        double? maxAverageFps = null,
        string? sortBy = null,
        bool descending = true,
        int skip = 0,
        int take = 100)
    {
        var query = _context.SessionRuns.AsQueryable();

        if (sessionId.HasValue)
        {
            query = query.Where(r => r.SessionId == sessionId.Value);
        }

        if (minAverageFps.HasValue)
        {
            query = query.Where(r => r.AverageFps >= minAverageFps.Value);
        }

        if (maxAverageFps.HasValue)
        {
            query = query.Where(r => r.AverageFps <= maxAverageFps.Value);
        }

        // Apply sorting
        query = ApplySorting(query, sortBy, descending);

        return await query
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<SessionRun?> GetByIdAsync(Guid id)
    {
        return await _context.SessionRuns.FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<IEnumerable<SessionRun>> GetBySessionIdAsync(Guid sessionId)
    {
        return await _context.SessionRuns
            .Where(r => r.SessionId == sessionId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<SessionRun> CreateAsync(SessionRun run)
    {
        run.CreatedAt = DateTime.UtcNow;

        _context.SessionRuns.Add(run);
        await _context.SaveChangesAsync();

        return run;
    }

    public async Task<SessionRun> UpdateAsync(SessionRun run)
    {
        _context.SessionRuns.Update(run);
        await _context.SaveChangesAsync();

        return run;
    }

    public async Task DeleteAsync(Guid id)
    {
        var run = await _context.SessionRuns.FindAsync(id);
        if (run != null)
        {
            _context.SessionRuns.Remove(run);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<int> CountAsync(Guid? sessionId = null)
    {
        var query = _context.SessionRuns.AsQueryable();

        if (sessionId.HasValue)
        {
            query = query.Where(r => r.SessionId == sessionId.Value);
        }

        return await query.CountAsync();
    }

    public async Task<IEnumerable<SessionRun>> GetTopRunsAsync(string metric = "AverageFps", int count = 10)
    {
        var query = _context.SessionRuns.AsQueryable();

        // Apply sorting based on metric
        query = ApplySorting(query, metric, descending: true);

        return await query
            .Take(count)
            .ToListAsync();
    }

    private IQueryable<SessionRun> ApplySorting(IQueryable<SessionRun> query, string? sortBy, bool descending)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            return descending
                ? query.OrderByDescending(r => r.CreatedAt)
                : query.OrderBy(r => r.CreatedAt);
        }

        Expression<Func<SessionRun, object?>> keySelector = sortBy.ToLowerInvariant() switch
        {
            "averagefps" => r => r.AverageFps,
            "p1fps" => r => r.P1Fps,
            "p99fps" => r => r.P99Fps,
            "p95fps" => r => r.P95Fps,
            "maxfps" => r => r.MaxFps,
            "medianfps" => r => r.MedianFps,
            "sampletime" => r => r.SampleTime,
            "avgcputemp" => r => r.AvgCpuTemp,
            "avggputemp" => r => r.AvgGpuTemp,
            "avgcpupower" => r => r.AvgCpuPower,
            "avggpupower" => r => r.AvgGpuPower,
            "avgcpuusage" => r => r.AvgCpuUsage,
            "avggpuusage" => r => r.AvgGpuUsage,
            _ => r => r.CreatedAt
        };

        return descending
            ? query.OrderByDescending(keySelector)
            : query.OrderBy(keySelector);
    }
}
