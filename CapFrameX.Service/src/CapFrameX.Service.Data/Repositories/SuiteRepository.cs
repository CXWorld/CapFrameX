using CapFrameX.Service.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CapFrameX.Service.Data.Repositories;

/// <summary>
/// Repository implementation for Suite operations.
/// </summary>
public class SuiteRepository : ISuiteRepository
{
    private readonly CapFrameXDbContext _context;

    public SuiteRepository(CapFrameXDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IEnumerable<Suite>> GetAllAsync(SuiteType? type = null, int skip = 0, int take = 100)
    {
        var query = _context.Suites.AsQueryable();

        if (type.HasValue)
        {
            query = query.Where(s => s.Type == type.Value);
        }

        return await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<Suite?> GetByIdAsync(Guid id, bool includeSessions = false)
    {
        var query = _context.Suites.AsQueryable();

        if (includeSessions)
        {
            query = query.Include(s => s.Sessions);
        }

        return await query.FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<Suite> CreateAsync(Suite suite)
    {
        suite.CreatedAt = DateTime.UtcNow;
        suite.UpdatedAt = DateTime.UtcNow;

        _context.Suites.Add(suite);
        await _context.SaveChangesAsync();

        return suite;
    }

    public async Task<Suite> UpdateAsync(Suite suite)
    {
        suite.UpdatedAt = DateTime.UtcNow;

        _context.Suites.Update(suite);
        await _context.SaveChangesAsync();

        return suite;
    }

    public async Task DeleteAsync(Guid id)
    {
        var suite = await _context.Suites.FindAsync(id);
        if (suite != null)
        {
            _context.Suites.Remove(suite);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<int> CountAsync(SuiteType? type = null)
    {
        var query = _context.Suites.AsQueryable();

        if (type.HasValue)
        {
            query = query.Where(s => s.Type == type.Value);
        }

        return await query.CountAsync();
    }
}
