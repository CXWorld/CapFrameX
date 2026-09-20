using CapFrameX.Service.Api;
using CapFrameX.Service.Application.Records;
using CapFrameX.Service.Core.Platform;
using CapFrameX.Service.Data;
using CapFrameX.Service.Core.Security;
using CapFrameX.Service.Windows.Host;
using CapFrameX.Service.Windows.Platform;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Net.Sockets;

// Composition root of the Windows service. It owns everything platform-specific; the API, the
// capture lifecycle and the records all come from CapFrameX.Service.Shared, so the Linux host maps
// exactly the same endpoints.

var privileges = new WindowsPrivilegeInfo();

if (!privileges.IsElevated)
{
    Console.Error.WriteLine(
        "CapFrameX service: not running with administrator rights. PresentMon needs a real-time "
        + "ETW session and PawnIO needs its device, so there is no unelevated mode. Start it "
        + "through its scheduled task, or repair the installation.");

    return ServiceExitCode.NotElevated;
}

IAppPaths paths = new WindowsAppPaths(portableRoot: PortableMode.RootOrNull());
var tokenStore = new SessionTokenStore(paths, new WindowsSecretFileWriter());

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

    return ServiceExitCode.PortInUse;
}

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options => options.ListenLocalhost(CapFrameXApiOptions.DefaultPort));

builder.Services.AddSingleton(paths);
builder.Services.AddSingleton<IPrivilegeInfo>(privileges);
builder.Services.AddSingleton<ISecretFileWriter, WindowsSecretFileWriter>();
builder.Services.AddSingleton(tokenStore);
builder.Services.AddCapFrameXDatabase(paths);
builder.Services.AddCapFrameXRecordIndex(paths);
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

return ServiceExitCode.Ok;

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
