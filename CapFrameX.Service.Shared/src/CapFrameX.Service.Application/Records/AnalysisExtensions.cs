using CapFrameX.Service.Analysis;
using CapFrameX.Service.Application.Settings;
using CapFrameX.Service.Records;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CapFrameX.Service.Application.Records;

/// <summary>
/// Registers the analysis with a composition root.
/// </summary>
public static class AnalysisExtensions
{
    /// <summary>
    /// Registers the analysis, its settings and the cache that keeps captures out of the file
    /// system between calls.
    /// </summary>
    /// <remarks>
    /// The cache is a singleton on purpose: a scope lives for one request, and a cache that did
    /// too would be a cache of nothing.
    /// </remarks>
    /// <param name="services">The host's service collection.</param>
    public static IServiceCollection AddCapFrameXAnalysis(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<AnalysisSettings>();
        services.AddSingleton<SessionCache>();
        services.AddSingleton<AnalysisService>();
        services.AddSingleton<SettingsStore>();
        services.TryAddSingleton<RecordFileReader>();
        services.TryAddSingleton<RecordFileWriter>();
        services.AddScoped<RecordLibrary>();
        services.AddScoped<RecordAnalyzer>();
        services.AddScoped<RecordStore>();

        return services;
    }
}
