using CapFrameX.Service.Api;
using CapFrameX.Service.Api.Security;
using CapFrameX.Service.Api.Services;
using CapFrameX.Service.Core.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

// The loopback port the API listens on. The guard needs the same value: it rejects any request
// whose Host header names something else, which is what tells a local caller apart from a web page
// that pointed its own name at 127.0.0.1.
const int ApiPort = 1337;

var builder = WebApplication.CreateBuilder(args);

// Configure Windows Service
if (OperatingSystem.IsWindows())
{
    builder.Host.UseWindowsService();
}

// The host that starts this service supplies the token it will hand to the frontend. Without one
// the service still comes up with a fresh token, so a developer run is not blocked - but nothing
// can talk to it until the token is read from the log.
var token = Environment.GetEnvironmentVariable("CAPFRAMEX_SERVICE_TOKEN") is { Length: > 0 } supplied
    ? new SessionToken(supplied)
    : SessionToken.Generate();

builder.Services.AddSingleton(token);
builder.Services.AddSingleton(new LocalApiGuard(token, ApiPort));

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS for the frontend. The guard is what actually refuses a foreign origin; this only keeps the
// browser from discarding a legitimate response.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp", policy =>
    {
        policy.WithOrigins(
                "http://localhost:4200",
                "http://127.0.0.1:4200",
                "app://capframex",
                "capframex://app")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Configure Kestrel to listen on the loopback port
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(ApiPort);
});

// Add background services
builder.Services.AddSingleton<BridgeEventStream>();
builder.Services.AddHostedService<BridgeHeartbeatService>();
builder.Services.AddHostedService<Worker>();

// TODO: Register application services (event bus, handlers, etc.)
// TODO: Register infrastructure services (named pipes server, repositories, etc.)

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAngularApp");

// Before anything else: on Windows this process is elevated, so an unguarded route would lend
// administrator rights to whatever asked for it.
app.UseMiddleware<LocalApiGuardMiddleware>();

app.MapControllers();

app.Run();

/// <summary>Entry point, made addressable so integration tests can host this API.</summary>
public partial class Program;
