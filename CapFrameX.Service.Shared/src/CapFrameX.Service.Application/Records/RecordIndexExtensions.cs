using CapFrameX.Service.Core.Platform;
using CapFrameX.Service.Records;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CapFrameX.Service.Application.Records;

/// <summary>
/// Registers the record index with a composition root.
/// </summary>
public static class RecordIndexExtensions
{
    /// <summary>
    /// Registers the index itself - what projects a capture file onto a row, and what re-reads one
    /// after the service changed it.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <param name="paths">Supplies the capture directory.</param>
    public static IServiceCollection AddCapFrameXRecordIndex(this IServiceCollection services, IAppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(paths);

        services.AddSingleton(new RecordIndexOptions { CaptureDirectory = paths.CaptureDirectory });
        services.TryAddSingleton<RecordFileReader>();
        services.AddScoped<RecordIndex>();

        return services;
    }

    /// <summary>
    /// Registers the background service that keeps the index following the capture folder.
    /// </summary>
    /// <remarks>
    /// Separate from the index because they are wanted separately: an API that edits a record needs
    /// the index to re-read it, while a folder watcher scanning in the background is a thing a host
    /// runs and a test would rather not. Call it after the database - hosted services start in the
    /// order they were registered, and the migration step runs to completion before the next one
    /// begins, which is what keeps the first scan from meeting a schema that is not there yet.
    /// </remarks>
    /// <param name="services">The host's service collection.</param>
    public static IServiceCollection AddCapFrameXRecordWatcher(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHostedService<RecordIndexer>();

        return services;
    }
}
