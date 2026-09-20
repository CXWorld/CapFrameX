using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CapFrameX.Service.Data;

/// <summary>
/// Brings the database schema up to date before the service starts answering.
/// </summary>
/// <remarks>
/// Migrations, not <c>EnsureCreated</c>: an installation that already holds a user's records has to
/// be upgraded rather than recreated, and <c>EnsureCreated</c> cannot do that. The service refuses
/// to start when the schema cannot be brought up, because serving an API on a half-migrated
/// database would corrupt what it stores.
/// </remarks>
/// <param name="services">Provides a scope for the context.</param>
/// <param name="logger">Records what was applied.</param>
public sealed class DatabaseMigrationService(
    IServiceProvider services,
    ILogger<DatabaseMigrationService> logger) : IHostedService
{
    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CapFrameXDbContext>();

        var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();

        if (pending.Length == 0)
        {
            logger.LogInformation("Database schema is up to date.");
            return;
        }

        logger.LogInformation("Applying {Count} database migration(s): {Migrations}.", pending.Length, pending);

        await context.Database.MigrateAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
