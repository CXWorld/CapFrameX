using System.Text.Json;
using CapFrameX.Service.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CapFrameX.Service.Api.Controllers;

[ApiController]
[Route("api/events")]
public sealed class EventsController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly BridgeEventStream _eventStream;

    public EventsController(BridgeEventStream eventStream)
    {
        _eventStream = eventStream;
    }

    [HttpGet]
    [Produces("text/event-stream")]
    public async Task Stream(CancellationToken cancellationToken)
    {
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        await using var subscription = _eventStream.Subscribe();
        await Response.WriteAsync("retry: 5000\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);

        await foreach (var envelope in subscription.Reader.ReadAllAsync(cancellationToken))
        {
            var payload = JsonSerializer.Serialize(envelope, JsonOptions);

            await Response.WriteAsync($"id: {envelope.Sequence}\n", cancellationToken);
            await Response.WriteAsync($"event: {envelope.Type}\n", cancellationToken);
            await Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }
}
