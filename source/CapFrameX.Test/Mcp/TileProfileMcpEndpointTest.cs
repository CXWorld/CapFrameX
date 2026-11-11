using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Mcp;
using CapFrameX.Mcp.Tools;
using CapFrameX.OSD.Integration;
using CapFrameX.View.OverlayDesignAuthoring;
using DryIoc;
using EmbedIO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace CapFrameX.Test.Mcp
{
    /// <summary>
    /// A client's view of the tile tools: JSON-RPC over HTTP to the real MCP module, the real
    /// authoring service and a separate UI thread, as in CapFrameX.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class TileProfileMcpEndpointTest
    {
        private string _folder;
        private Subject<(string key, object value)> _changes;
        private OverlayDesignService _designService;
        private Thread _uiThread;
        private Dispatcher _dispatcher;
        private WebServer _server;
        private CancellationTokenSource _stop;
        private HttpClient _http;
        private string _endpoint;
        private int _requestId;

        [TestInitialize]
        public void Initialize()
        {
            _folder = Path.Combine(Path.GetTempPath(), "CapFrameX-mcp-tile-tests", Guid.NewGuid().ToString("N"));
            var configuration = new Mock<IAppConfiguration>();
            configuration.SetupAllProperties();
            configuration.Object.EnableHookFreeOverlay = true;
            _changes = new Subject<(string, object)>();
            configuration.SetupGet(config => config.OnValueChanged).Returns(_changes);
            _designService = new OverlayDesignService(_folder, configuration.Object, () => configuration.Object.IsOverlayActive = true);

            using var ready = new ManualResetEventSlim();
            _uiThread = new Thread(() =>
            {
                _dispatcher = Dispatcher.CurrentDispatcher;
                ready.Set();
                Dispatcher.Run();
            }) { IsBackground = true };
            _uiThread.SetApartmentState(ApartmentState.STA);
            _uiThread.Start();
            ready.Wait();

            var container = new Container();
            container.RegisterInstance<IOverlayDesignAuthoringService>(
                new OverlayDesignAuthoringService(_folder, _designService, null, _dispatcher, null));
            container.Register<TileProfileTools>(Reuse.Singleton);
            int port = FreePort();
            _endpoint = $"http://127.0.0.1:{port}/mcp";
            _server = new WebServer(options => options.WithUrlPrefix($"http://127.0.0.1:{port}/").WithMode(HttpListenerMode.EmbedIO))
                .WithModule(new McpModule("/mcp", container, typeof(McpModule).Assembly));
            _stop = new CancellationTokenSource();
            _ = _server.RunAsync(_stop.Token);
            Assert.IsTrue(SpinWait.SpinUntil(() => _server.State == WebServerState.Listening, TimeSpan.FromSeconds(10)),
                "The MCP test server did not start.");
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        }

        [TestCleanup]
        public void Cleanup()
        {
            _http?.Dispose();
            _stop?.Cancel();
            _server?.Dispose();
            _dispatcher?.InvokeShutdown();
            _uiThread?.Join(TimeSpan.FromSeconds(5));
            _designService?.Dispose();
            _changes?.Dispose();
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        [TestMethod]
        public async Task AClientCreatesRendersAndEditsAProfile()
        {
            var initialized = await Request("initialize", new JObject { ["protocolVersion"] = "2024-11-05" });
            StringAssert.Contains((string)initialized["result"]["instructions"], "cfx_get_tile_design_guide",
                "Clients learn from the server instructions where the design format is documented.");
            var tools = (JArray)(await Request("tools/list", null))["result"]["tools"];
            var names = tools.Select(tool => (string)tool["name"]).ToArray();
            CollectionAssert.IsSubsetOf(new[] { "cfx_get_tile_design_guide", "cfx_create_tile_profile", "cfx_render_tile_profile", "cfx_edit_tile_profile" }, names);

            var created = await CallText("cfx_create_tile_profile", new JObject
            {
                ["name"] = "Over HTTP",
                ["document"] = JObject.Parse(@"{ ""width"": 300, ""tiles"": [
                    { ""kind"": ""text"", ""text"": ""CapFrameX"", ""columnSpan"": 2 },
                    { ""kind"": ""metric"", ""key"": ""fps"", ""label"": ""FPS"" },
                    { ""kind"": ""gauge"", ""key"": ""gpuLoad"", ""label"": ""GPU"", ""unit"": ""%"" } ] }")
            });
            Assert.AreEqual("Over HTTP", (string)created["profile"]["name"]);
            Assert.AreEqual(3, ((JArray)created["outline"]["tiles"]).Count);

            var render = await Call("cfx_render_tile_profile", new JObject { ["profile"] = "Over HTTP", ["liveValues"] = false });
            var content = (JArray)render["result"]["content"];
            Assert.AreEqual("image", (string)content[0]["type"]);
            Assert.AreEqual("image/png", (string)content[0]["mimeType"]);
            CollectionAssert.AreEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, Convert.FromBase64String((string)content[0]["data"]).Take(4).ToArray());
            Assert.AreEqual(3, ((JArray)JObject.Parse((string)content[1]["text"])["tiles"]).Count);

            var edited = await CallText("cfx_edit_tile_profile", new JObject
            {
                ["profile"] = "Over HTTP",
                ["operations"] = JArray.Parse(@"[ { ""op"": ""updateTile"", ""path"": ""1"", ""values"": { ""valueSize"": 44 } } ]"),
                ["includeDocument"] = true
            });
            Assert.AreEqual(44, (double)edited["document"]["tiles"][1]["valueSize"]);

            var rejected = await Call("cfx_edit_tile_profile", new JObject
            {
                ["profile"] = "Over HTTP",
                ["operations"] = JArray.Parse(@"[ { ""op"": ""updateTile"", ""path"": ""9"", ""values"": { ""label"": ""x"" } } ]")
            });
            Assert.IsTrue((bool)rejected["result"]["isError"]);
            StringAssert.Contains((string)rejected["result"]["content"][0]["text"], "Operation 0 (updateTile)");
        }

        private async Task<JObject> CallText(string tool, JObject arguments)
        {
            var response = await Call(tool, arguments);
            Assert.IsNull(response["result"]["isError"], (string)response["result"]["content"]?[0]?["text"]);
            return JObject.Parse((string)response["result"]["content"][0]["text"]);
        }

        private Task<JObject> Call(string tool, JObject arguments) =>
            Request("tools/call", new JObject { ["name"] = tool, ["arguments"] = arguments });

        private async Task<JObject> Request(string method, JObject parameters)
        {
            var request = new JObject { ["jsonrpc"] = "2.0", ["id"] = ++_requestId, ["method"] = method };
            if (parameters != null) request["params"] = parameters;
            using var response = await _http.PostAsync(_endpoint, new StringContent(request.ToString(), Encoding.UTF8, "application/json"));
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            return JObject.Parse(await response.Content.ReadAsStringAsync());
        }

        private static int FreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
