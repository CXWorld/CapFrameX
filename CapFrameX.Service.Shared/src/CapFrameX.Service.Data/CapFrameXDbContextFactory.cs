using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CapFrameX.Service.Data;

/// <summary>
/// Design-time factory for creating CapFrameXDbContext.
/// Used by EF Core tools for migrations.
/// </summary>
public class CapFrameXDbContextFactory : IDesignTimeDbContextFactory<CapFrameXDbContext>
{
    public CapFrameXDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CapFrameXDbContext>();

        // Use default database location for migrations
        var defaultDbPath = GetDefaultDatabasePath();
        optionsBuilder.UseSqlite($"Data Source={defaultDbPath}");

        return new CapFrameXDbContext(optionsBuilder.Options);
    }

    /// <summary>
    /// Gets the default database path in LocalApplicationData
    /// </summary>
    public static string GetDefaultDatabasePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dbDirectory = Path.Combine(appData, "CapFrameX");

        // Ensure directory exists
        if (!Directory.Exists(dbDirectory))
        {
            Directory.CreateDirectory(dbDirectory);
        }

        return Path.Combine(dbDirectory, "capframex.db");
    }

    /// <summary>
    /// Creates DbContext with custom database path
    /// </summary>
    public static CapFrameXDbContext Create(string? databasePath = null)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CapFrameXDbContext>();
        var dbPath = databasePath ?? GetDefaultDatabasePath();
        optionsBuilder.UseSqlite($"Data Source={dbPath}");

        return new CapFrameXDbContext(optionsBuilder.Options);
    }
}
