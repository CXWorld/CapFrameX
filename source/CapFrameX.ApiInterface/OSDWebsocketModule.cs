using CapFrameX.Contracts.Overlay;
using EmbedIO.WebSockets;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.ApiInterface
{
    public class OSDWebsocketModule : WebSocketModule
    {

        public OSDWebsocketModule(string path, IOverlayService overlayService) : base(path, true)
        {
            overlayService.OSDUpdateNotifier = (_) =>
            {
                if (ActiveContexts.Count > 0)
                {
                    BroadcastAsync(JsonConvert.SerializeObject(OSDController.GetEntries(overlayService, false)));
                }
            };

        }

        protected override Task OnMessageReceivedAsync(IWebSocketContext context, byte[] buffer, IWebSocketReceiveResult result)
        {
            return SendAsync(context, "Welcome to CXRemote OSD Websocket interface!");
        }
    }
}
