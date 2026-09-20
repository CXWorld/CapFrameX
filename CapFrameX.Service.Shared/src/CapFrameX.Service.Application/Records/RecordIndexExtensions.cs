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
    /// Registers the index and the background service that keeps it following the capture folder.
    /// </summary>
    /// <remarks>
    /// Call this after the database: hosted services start in the order they were registered, and
    /// the migration step runs to completion before the next one begins, which is what keeps the
    /// first scan from meeting a schema that is not there yet.
    /// </remarks>
    /// <param name="services">The host's service collection.</param>
    /// <param name="paths">Supplies the capture directory.</param>
    public static IServiceCollection AddCapFrameXRecordIndex(this IServiceCollection services, IAppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(paths);

        services.AddSingleton(new RecordIndexOptions { CaptureDirectory = paths.CaptureDirectory });
        services.TryAddSingleton<RecordFileReader>();
        services.AddScoped<RecordIndex>();
        services.AddHostedService<RecordIndexer>();

        return services;
    }
}
