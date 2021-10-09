using CapFrameX.ApiInterface;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.PresentMonInterface;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Data;
using DryIoc;
using EmbedIO;
using EmbedIO.Actions;
using EmbedIO.WebApi;
using Serilog;
using Swan.Logging;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Subjects;

namespace CapFrameX.Remote
{
    public static class WebserverFactory
    {

        public static WebServer CreateWebServer(IContainer iocContainer, string hostname, string port = null)
        {
            var options = new WebServerOptions()
            {
                Mode = HttpListenerMode.Microsoft
            };
            options.AddUrlPrefix($"{hostname}:{port ?? GetFreeTcpPort().ToString()}");

            var server = new WebServer(options)
                .WithCors()
                .WithWebApi("/api", m =>
                {
                    m.WithController(() => new CaptureController(iocContainer.Resolve<CaptureManager>()));
                    m.WithController(() => new VersionController(iocContainer.Resolve<IAppVersionProvider>()));
                    m.WithController(() => new OSDController(iocContainer.Resolve<IOverlayService>()));
                })
                .WithModule(new ActionModule("/", HttpVerbs.Any, ctx => ctx.SendDataAsync(new { Message = "Error" })));

            // Listen for state changes.
            server.StateChanged += (s, e) => Log.Logger.Information($"WebServer ({string.Join(",", options.UrlPrefixes)}) State - {e.NewState}");

            return server;
        }

        private static int GetFreeTcpPort()
        {
            TcpListener l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }
    }
}
