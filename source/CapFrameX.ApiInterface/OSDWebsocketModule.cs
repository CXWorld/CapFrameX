using CapFrameX.Contracts.Overlay;
using EmbedIO.WebSockets;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.ApiInterface
{
    public class OSDWebsocketModule : WebSocketModule
    {
        private readonly IRemoteOverlayDemand _remoteOverlayDemand;
        // Keyed by context, so every connection releases its demand exactly once.
        private readonly ConcurrentDictionary<string, IDisposable> _clientDemands =
            new ConcurrentDictionary<string, IDisposable>();

        public OSDWebsocketModule(string path, IOverlayService overlayService,
            IRemoteOverlayDemand remoteOverlayDemand) : base(path, true)
        {
            _remoteOverlayDemand = remoteOverlayDemand;
            overlayService.OSDUpdateNotifier = (_) =>
            {
                if (ActiveContexts.Count > 0)
                {
                    BroadcastAsync(JsonConvert.SerializeObject(OSDController.GetEntries(overlayService, false)));
                }
            };

        }

        protected override Task OnClientConnectedAsync(IWebSocketContext context)
        {
            var demand = _remoteOverlayDemand.AcquireStreamingClient();
            if (!_clientDemands.TryAdd(context.Id, demand))
                demand.Dispose();
            return Task.CompletedTask;
        }

        protected override Task OnClientDisconnectedAsync(IWebSocketContext context)
        {
            if (_clientDemands.TryRemove(context.Id, out var demand))
                demand.Dispose();
            return Task.CompletedTask;
        }

        protected override Task OnMessageReceivedAsync(IWebSocketContext context, byte[] buffer, IWebSocketReceiveResult result)
        {
            return SendAsync(context, "Welcome to CXRemote OSD Websocket interface!");
        }
    }
}
