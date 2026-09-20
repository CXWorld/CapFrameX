using CapFrameX.Service.Api;
using CapFrameX.Service.Application.Records;
using CapFrameX.Service.Core.Platform;
using CapFrameX.Service.Data;
using CapFrameX.Service.Core.Security;
using CapFrameX.Service.Linux.Platform;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Net.Sockets;

// Composition root of the Linux service. It maps the same endpoints as the Windows host, so one
// frontend serves either platform; what differs is behind the platform ports.
//
// Unlike the Windows service this one runs as the ordinary user: the Vulkan layer delivers presents
// without privileges, and the kernel exports its telemetry to everyone. The only privileged piece
// is the optional telemetry helper, which is a separate process.

IAppPaths paths = new XdgAppPaths();
var tokenStore = new SessionTokenStore(paths, new PosixSecretFileWriter());

// Default start order: the frontend starts first and hands the token to this process. Started on
// its own, the service issues one and publishes it for a frontend that attaches later.
var token = Environment.GetEnvironmentVariable("CAPFRAMEX_SERVICE_TOKEN") is { Length: > 0 } supplied
    ? new SessionToken(supplied)
    : SessionToken.Generate();

if (IsPortTaken(CapFrameXApiOptions.DefaultPort))
{
    Console.Error.WriteLine(
        $"CapFrameX service: port {CapFrameXApiOptions.DefaultPort} is already in use, most likely "
        + "by another CapFrameX service.");

    return 3;
}

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options => options.ListenLocalhost(CapFrameXApiOptions.DefaultPort));

builder.Services.AddSingleton(paths);
builder.Services.AddSingleton<ISecretFileWriter, PosixSecretFileWriter>();
builder.Services.AddSingleton(tokenStore);
builder.Services.AddCapFrameXDatabase(paths);
builder.Services.AddCapFrameXRecordIndex(paths);
builder.Services.AddCapFrameXRecordWatcher();
builder.Services.AddCapFrameXAnalysis();
builder.Services.AddSingleton<IFileTrash>(new XdgFileTrash());
builder.Services.AddCapFrameXApi(new CapFrameXApiOptions { Token = token });

var app = builder.Build();
app.MapCapFrameXApi();

tokenStore.Publish(token);

// A token left behind after shutdown would keep authenticating; it goes when the service does.
app.Lifetime.ApplicationStopped.Register(tokenStore.Revoke);

try
{
    await app.RunAsync();
}
finally
{
    tokenStore.Revoke();
}

return 0;

static bool IsPortTaken(int port)
{
    try
    {
        using var probe = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        probe.Bind(new IPEndPoint(IPAddress.Loopback, port));
        return false;
    }
    catch (SocketException)
    {
        return true;
    }
}
