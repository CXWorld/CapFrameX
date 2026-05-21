using CapFrameX.Service.Api;
using CapFrameX.Service.Api.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Configure Windows Service
if (OperatingSystem.IsWindows())
{
    builder.Host.UseWindowsService();
}

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS for Angular frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp", policy =>
    {
        policy.WithOrigins(
                "http://localhost:4200",
                "http://127.0.0.1:4200",
                "tauri://localhost",
                "app://capframex",
                "capframex://app")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Configure Kestrel to listen on port 1337
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(1337);
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
app.UseAuthorization();
app.MapControllers();

app.Run();
