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
        private readonly IOverlayService _overlayService;

        public OSDWebsocketModule(string path, IOverlayService overlayService) : base(path, true)
        {
            _overlayService = overlayService;

            Observable.Interval(TimeSpan.FromSeconds(1))
                .SelectMany(async _ =>
                {
                    await SendUpdate();
                    return _;
                })
                .Subscribe();
        }

        protected override Task OnMessageReceivedAsync(IWebSocketContext context, byte[] buffer, IWebSocketReceiveResult result)
        {
            return SendAsync(context, "Welcome to CXRemote OSD Websocket interface!");
        }

        private async Task SendUpdate()
        {
            await BroadcastAsync(JsonConvert.SerializeObject(OSDController.GetEntries(_overlayService, false)));
        }
    }
}
