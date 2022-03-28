using CapFrameX.Contracts.Sensor;
using EmbedIO.WebSockets;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace CapFrameX.ApiInterface
{
    public class SensorWebsocketModule : WebSocketModule
    {
        public SensorWebsocketModule(string path, ISensorService sensorService) : base(path, true)
        {
            sensorService.SensorSnapshotStream
                .Where(x => ActiveContexts.Count > 0)
                .Subscribe(sensors =>
                {
                    BroadcastAsync(JsonConvert.SerializeObject(new
                    {
                        Timestamp = sensors.Item1,
                        Sensors = sensors.Item2.Select(x => new {
                            x.Key.Name,
                            x.Key.Value,
                            x.Key.SensorType
                        })
                    }));
                });

        }

        protected override Task OnMessageReceivedAsync(IWebSocketContext context, byte[] buffer, IWebSocketReceiveResult result)
        {
            return SendAsync(context, "Welcome to CXRemote Sensor Websocket interface!");
        }
    }
}
