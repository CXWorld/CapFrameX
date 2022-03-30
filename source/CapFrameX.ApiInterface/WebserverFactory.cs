using CapFrameX.ApiInterface;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.PresentMonInterface;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Data;
using CapFrameX.Monitoring.Contracts;
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

        public static string OsdHttpUrl;
        public static string OsdWSUrl;
        public static string SensorsWSUrl;
        public static string ActiveSensorsWSUrl;

        public static WebServer CreateWebServer(IContainer iocContainer, string hostname, bool useRandomPort)
        {
            var config = iocContainer.Resolve<IAppConfiguration>();
            var port = useRandomPort ? GetFreeTcpPort().ToString() : config.WebservicePort;

            config.WebservicePort = port;

            var options = new WebServerOptions()
            {
                Mode = HttpListenerMode.Microsoft
            };

            options.AddUrlPrefix($"{hostname}:{port}");

            var server = new WebServer(options)
                .WithCors()
                .WithWebApi("/api", m =>
                {
                    m.WithController(() => new CaptureController(iocContainer.Resolve<CaptureManager>()));
                    m.WithController(() => new VersionController(iocContainer.Resolve<IAppVersionProvider>()));
                    m.WithController(() => new OSDController(iocContainer.Resolve<IOverlayService>()));
                })
                .WithModule(new OSDWebsocketModule("/ws/osd", iocContainer.Resolve<IOverlayService>()))
                .WithModule(new SensorWebsocketModule("/ws/sensors", iocContainer.Resolve<ISensorService>(), iocContainer.Resolve<ISensorConfig>(), (_, __) => true, (sensorConfig, isActive) => sensorConfig.WsSensorsEnabled = isActive))
                .WithModule(new SensorWebsocketModule("/ws/activesensors", iocContainer.Resolve<ISensorService>(), iocContainer.Resolve<ISensorConfig>(), (sensor, sensorConfig) => sensorConfig.GetSensorIsActive(sensor.Key.Identifier), (sensorConfig, isActive) => sensorConfig.WsActiveSensorsEnabled = isActive))
                .WithModule(new ActionModule("/", HttpVerbs.Any, ctx => ctx.SendDataAsync(new { Message = "Error" })));

            OsdHttpUrl = "http://localhost:" + port + "/api/osd";
            OsdWSUrl = "ws://localhost:" + port + "/ws/osd";
            SensorsWSUrl = "ws://localhost:" + port + "/ws/sensors";
            ActiveSensorsWSUrl = "ws://localhost:" + port + "/ws/activesensors";

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
