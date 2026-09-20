using CapFrameX.Service.Core.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CapFrameX.Service.Api.Security;

/// <summary>
/// Enforces <see cref="LocalApiGuard"/> on every request before it reaches a controller.
/// </summary>
/// <remarks>
/// This sits in front of everything on purpose. On Windows the service runs elevated, so an
/// unauthenticated route is not merely a data leak: it lends administrator rights to whatever
/// asked. Refusals answer with problem details and never say which check failed - the caller
/// learns "no", not how close it came.
/// </remarks>
/// <param name="next">The rest of the pipeline.</param>
/// <param name="guard">The rules to apply.</param>
/// <param name="logger">Receives the real reason, which the response omits.</param>
public sealed class LocalApiGuardMiddleware(
    RequestDelegate next,
    LocalApiGuard guard,
    ILogger<LocalApiGuardMiddleware> logger)
{
    /// <summary>
    /// Paths whose clients cannot set a request header, so the token may arrive in the query.
    /// </summary>
    private static readonly string[] StreamPaths = ["/api/events", "/api/stream"];

    /// <summary>Judges the request and either passes it on or refuses it.</summary>
    /// <param name="context">The request.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        var decision = guard.Evaluate(new LocalApiRequest(
            HeaderToken: context.Request.Headers[LocalApiGuard.TokenHeaderName].FirstOrDefault(),
            QueryToken: context.Request.Query[LocalApiGuard.TokenQueryName].FirstOrDefault(),
            Host: context.Request.Headers.Host.FirstOrDefault() ?? string.Empty,
            Origin: context.Request.Headers.Origin.FirstOrDefault(),
            AllowQueryToken: IsStreamPath(path)));

        if (decision.IsAllowed)
        {
            await next(context);
            return;
        }

        logger.LogWarning(
            "Refused {Method} {Path}: {Reason}.",
            context.Request.Method,
            path,
            decision.Reason);

        await WriteRefusalAsync(context, decision.Reason);
    }

    private static bool IsStreamPath(string path) =>
        StreamPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    private static async Task WriteRefusalAsync(HttpContext context, LocalApiDenialReason reason)
    {
        // A caller that is out of bounds gets 403 and a caller that simply did not authenticate
        // gets 401, which is what an HTTP client expects; the body says no more than that.
        var status = reason is LocalApiDenialReason.MissingToken or LocalApiDenialReason.InvalidToken
            ? StatusCodes.Status401Unauthorized
            : StatusCodes.Status403Forbidden;

        context.Response.StatusCode = status;

        // The content type has to go through WriteAsJsonAsync: setting Response.ContentType first
        // is overwritten by the serializer's own default.
        var problem = new ProblemDetails
        {
            Status = status,
            Title = status == StatusCodes.Status401Unauthorized ? "Unauthorized" : "Forbidden",
            Detail = "This API only serves the CapFrameX frontend on this machine.",
        };

        await context.Response.WriteAsJsonAsync(
            problem,
            (System.Text.Json.JsonSerializerOptions?)null,
            contentType: "application/problem+json");
    }
}
