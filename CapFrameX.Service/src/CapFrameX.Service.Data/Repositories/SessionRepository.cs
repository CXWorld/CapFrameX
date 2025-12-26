using CapFrameX.Service.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CapFrameX.Service.Data.Repositories;

/// <summary>
/// Repository implementation for Session operations.
/// </summary>
public class SessionRepository : ISessionRepository
{
    private readonly CapFrameXDbContext _context;

    public SessionRepository(CapFrameXDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IEnumerable<Session>> GetAllAsync(
        Guid? suiteId = null,
        string? gameName = null,
        string? processor = null,
        string? gpu = null,
        int skip = 0,
        int take = 100)
    {
        var query = _context.Sessions.AsQueryable();

        if (suiteId.HasValue)
        {
            query = query.Where(s => s.SuiteId == suiteId.Value);
        }

        if (!string.IsNullOrWhiteSpace(gameName))
        {
            query = query.Where(s => s.GameName.Contains(gameName));
        }

        if (!string.IsNullOrWhiteSpace(processor))
        {
            query = query.Where(s => s.Processor.Contains(processor));
        }

        if (!string.IsNullOrWhiteSpace(gpu))
        {
            query = query.Where(s => s.Gpu.Contains(gpu));
        }

        return await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<Session?> GetByIdAsync(Guid id, bool includeRuns = false)
    {
        var query = _context.Sessions.AsQueryable();

        if (includeRuns)
        {
            query = query.Include(s => s.Runs);
        }

        return await query.FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<IEnumerable<Session>> GetBySuiteIdAsync(Guid suiteId, bool includeRuns = false)
    {
        var query = _context.Sessions.Where(s => s.SuiteId == suiteId);

        if (includeRuns)
        {
            query = query.Include(s => s.Runs);
        }

        return await query
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task<Session> CreateAsync(Session session)
    {
        session.CreatedAt = DateTime.UtcNow;

        _context.Sessions.Add(session);
        await _context.SaveChangesAsync();

        return session;
    }

    public async Task<Session> UpdateAsync(Session session)
    {
        _context.Sessions.Update(session);
        await _context.SaveChangesAsync();

        return session;
    }

    public async Task DeleteAsync(Guid id)
    {
        var session = await _context.Sessions.FindAsync(id);
        if (session != null)
        {
            _context.Sessions.Remove(session);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<int> CountAsync(Guid? suiteId = null, string? gameName = null)
    {
        var query = _context.Sessions.AsQueryable();

        if (suiteId.HasValue)
        {
            query = query.Where(s => s.SuiteId == suiteId.Value);
        }

        if (!string.IsNullOrWhiteSpace(gameName))
        {
            query = query.Where(s => s.GameName.Contains(gameName));
        }

        return await query.CountAsync();
    }

    public async Task<IEnumerable<Session>> SearchByGameAsync(string searchTerm, int skip = 0, int take = 50)
    {
        return await _context.Sessions
            .Where(s => s.GameName.Contains(searchTerm) || s.ProcessName.Contains(searchTerm))
            .OrderByDescending(s => s.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }
}
