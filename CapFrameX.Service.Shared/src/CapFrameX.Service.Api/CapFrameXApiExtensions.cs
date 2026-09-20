using CapFrameX.Service.Api.Security;
using CapFrameX.Service.Api.Services;
using CapFrameX.Service.Core.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace CapFrameX.Service.Api;

/// <summary>
/// The API as both composition roots consume it.
/// </summary>
/// <remarks>
/// Routes exist here and nowhere else: the Windows and the Linux host map the same endpoints, so
/// one frontend serves either platform and the contract cannot drift between them. A host adds its
/// platform services and calls these two methods.
/// </remarks>
public static class CapFrameXApiExtensions
{
    /// <summary>Registers the API's own services.</summary>
    /// <param name="services">The host's service collection.</param>
    /// <param name="options">What the host has to tell the API about itself.</param>
    public static IServiceCollection AddCapFrameXApi(this IServiceCollection services, CapFrameXApiOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton(options);
        services.AddSingleton(options.Token);
        services.AddSingleton(new LocalApiGuard(options.Token, options.Port, options.FrontendDevPort));

        services.AddControllers().AddApplicationPart(typeof(CapFrameXApiExtensions).Assembly);
        services.AddEndpointsApiExplorer();

        // The guard is what refuses a foreign origin; CORS only keeps the browser from discarding
        // a legitimate response.
        services.AddCors(cors => cors.AddPolicy(
            CorsPolicyName,
            policy => policy
                .WithOrigins(
                    $"http://localhost:{options.FrontendDevPort}",
                    $"http://127.0.0.1:{options.FrontendDevPort}",
                    "app://capframex",
                    "capframex://app")
                .AllowAnyHeader()
                .AllowAnyMethod()));

        services.AddSingleton<BridgeEventStream>();
        services.AddHostedService<BridgeHeartbeatService>();

        return services;
    }

    /// <summary>Puts the API into the request pipeline and maps its routes.</summary>
    /// <param name="app">The host's application.</param>
    public static WebApplication MapCapFrameXApi(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseCors(CorsPolicyName);

        // In front of everything: on Windows this process is elevated, so an unguarded route would
        // lend administrator rights to whatever asked for it.
        app.UseMiddleware<LocalApiGuardMiddleware>();

        app.MapControllers();

        return app;
    }

    private const string CorsPolicyName = "CapFrameXFrontend";
}
