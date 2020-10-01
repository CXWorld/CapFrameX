using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.PresentMonInterface;
using CapFrameX.Contracts.Sensor;
using DryIoc;
using EmbedIO;
using EmbedIO.Actions;
using EmbedIO.WebApi;
using Swan.Logging;
using System.Reactive.Subjects;

namespace CapFrameX.Remote
{
    public static class WebserverFactory
    {

        public static WebServer CreateWebServer(IContainer iocContainer, string hostnameAndPort)
        {
            var server = new WebServer(hostnameAndPort)
                .WithCors()
                .WithWebApi("/api", m =>
                {
                    m.WithController(() => new CaptureController(iocContainer.Resolve<ISubject<int>>()));
                    m.WithController(() => new VersionController(iocContainer.Resolve<IAppVersionProvider>()));
                })
                .WithModule(new ActionModule("/", HttpVerbs.Any, ctx => ctx.SendDataAsync(new { Message = "Error" })));

            // Listen for state changes.
            server.StateChanged += (s, e) => System.Console.WriteLine($"WebServer New State - {e.NewState}");

            return server;
        }
    }
}
