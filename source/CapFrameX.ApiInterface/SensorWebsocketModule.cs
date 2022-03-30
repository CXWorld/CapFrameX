using CapFrameX.Contracts.Sensor;
using CapFrameX.Monitoring.Contracts;
using EmbedIO.WebSockets;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace CapFrameX.ApiInterface
{
    public class SensorWebsocketModule : WebSocketModule
    {
        private readonly ISensorService sensorService;
        private readonly ISensorConfig sensorConfig;
        private readonly Action<ISensorConfig, bool> wsActivePropertySetterAction;

        public SensorWebsocketModule(
            string path, 
            ISensorService sensorService,
            ISensorConfig sensorConfig,
            Func<KeyValuePair<ISensorEntry, float>, ISensorConfig, bool> sensorFilter,
            Action<ISensorConfig, bool> wsActivePropertySetterAction) : base(path, true)
        {
            this.sensorService = sensorService;
            this.sensorConfig = sensorConfig;
            this.wsActivePropertySetterAction = wsActivePropertySetterAction;
            SetupSensorService();
            sensorService.SensorSnapshotStream
                .Where(x => ActiveContexts.Count > 0)
                .Subscribe(sensors =>
                {
                    BroadcastAsync(JsonConvert.SerializeObject(new
                    {
                        Timestamp = sensors.Item1,
                        Sensors = sensors.Item2.Where(s => sensorFilter(s, sensorConfig)).Select(x => new {
                            x.Key.Name,
                            x.Key.Value,
                            x.Key.SensorType
                        })
                    }));
                });
        }

        protected override Task OnClientConnectedAsync(IWebSocketContext context)
        {
            wsActivePropertySetterAction(sensorConfig, ActiveContexts.Count > 0);
            return Task.CompletedTask;
        }

        protected override Task OnClientDisconnectedAsync(IWebSocketContext context)
        {
            wsActivePropertySetterAction(sensorConfig, ActiveContexts.Count > 0);
            return Task.CompletedTask;
        }

        protected override Task OnMessageReceivedAsync(IWebSocketContext context, byte[] buffer, IWebSocketReceiveResult result)
        {
            return SendAsync(context, "Welcome to CXRemote Sensor Websocket interface!");
        }

        private void SetupSensorService()
        {
            var originalFunc = sensorService.IsSensorWebsocketActive;
            sensorService.IsSensorWebsocketActive = () => ActiveContexts.Count > 0 || originalFunc();
        }
    }
}
