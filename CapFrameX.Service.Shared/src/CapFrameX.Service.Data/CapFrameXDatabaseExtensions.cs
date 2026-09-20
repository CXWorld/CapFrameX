using CapFrameX.Service.Core.Platform;
using CapFrameX.Service.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CapFrameX.Service.Data;

/// <summary>
/// Registers the service database with a composition root.
/// </summary>
public static class CapFrameXDatabaseExtensions
{
    /// <summary>File name of the SQLite database inside the data directory.</summary>
    public const string FileName = "capframex.db";

    /// <summary>
    /// Registers the database, its repositories and the schema migration step.
    /// </summary>
    /// <remarks>
    /// The path comes from <see cref="IAppPaths"/> rather than from a known folder, so portable
    /// mode keeps the database next to the binaries and the Linux service puts it under XDG -
    /// a hard-coded <c>LocalApplicationData</c> would silently ignore both.
    /// </remarks>
    /// <param name="services">The host's service collection.</param>
    /// <param name="paths">Supplies the data directory.</param>
    public static IServiceCollection AddCapFrameXDatabase(this IServiceCollection services, IAppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(paths);

        var databasePath = DatabasePath(paths);

        services.AddDbContext<CapFrameXDbContext>(options => options.UseSqlite($"Data Source={databasePath}"));
        services.AddScoped<ISuiteRepository, SuiteRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<ISessionRunRepository, SessionRunRepository>();
        services.AddHostedService<DatabaseMigrationService>();

        return services;
    }

    /// <summary>Full path of the database file for a given layout.</summary>
    /// <param name="paths">Supplies the data directory.</param>
    public static string DatabasePath(IAppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        return Path.Combine(paths.DataDirectory, FileName);
    }
}
