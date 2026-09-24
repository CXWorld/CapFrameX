using System;
using System.Reactive.Concurrency;
using System.Threading.Tasks;
using CapFrameX.ApiInterface;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Overlay;
using EmbedIO.WebSockets;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CapFrameX.Test.ApiInterface
{
    [TestClass]
    public class OsdRemoteApiTest
    {
        [TestMethod]
        public async Task GetOsd_RegistersTheReadAsRemoteDemand()
        {
            var overlay = new Mock<IOverlayService>();
            overlay.SetupGet(x => x.CurrentOverlayEntries).Returns(new IOverlayEntry[]
            {
                new OverlayEntryWrapper("CaptureServiceStatus") { GroupName = "Status", ShowOnOverlay = true, Value = "Ready" }
            });
            var demand = new Mock<IRemoteOverlayDemand>();
            var controller = new OSDController(overlay.Object, demand.Object);

            var lines = await controller.GetOsd(showAll: false);

            demand.Verify(x => x.RegisterRequest(), Times.Once);
            CollectionAssert.AreEqual(new[] { "Status  Ready" }, lines);
        }

        [TestMethod]
        public async Task WebsocketConnection_HoldsTheDemandUntilItDisconnects()
        {
            var demand = CreateDemand();
            var module = new TestableOsdWebsocketModule(demand);
            var first = Context("first");
            var second = Context("second");

            await module.Connect(first);
            await module.Connect(second);
            await module.Disconnect(first);
            Assert.IsTrue(demand.IsActive, "The second connection still reads the overlay.");

            await module.Disconnect(second);
            Assert.IsFalse(demand.IsActive);
        }

        [TestMethod]
        public async Task RepeatedWebsocketCallbacks_HoldAndReleaseTheDemandOnce()
        {
            var demand = CreateDemand();
            var module = new TestableOsdWebsocketModule(demand);
            var client = Context("client");

            await module.Connect(client);
            await module.Connect(client);
            await module.Disconnect(client);
            Assert.IsFalse(demand.IsActive, "A repeated connect must not leave a second hold behind.");

            var other = Context("other");
            await module.Connect(other);
            await module.Disconnect(client);
            Assert.IsTrue(demand.IsActive, "A repeated disconnect must not release another connection.");
        }

        private static RemoteOverlayDemand CreateDemand()
            => new RemoteOverlayDemand(new HistoricalScheduler(DateTimeOffset.UtcNow), TimeSpan.FromSeconds(30));

        private static IWebSocketContext Context(string id)
        {
            var context = new Mock<IWebSocketContext>();
            context.SetupGet(x => x.Id).Returns(id);
            return context.Object;
        }

        private sealed class TestableOsdWebsocketModule : OSDWebsocketModule
        {
            public TestableOsdWebsocketModule(IRemoteOverlayDemand demand)
                : base("/ws/osd", new Mock<IOverlayService>().Object, demand)
            {
            }

            public Task Connect(IWebSocketContext context) => OnClientConnectedAsync(context);

            public Task Disconnect(IWebSocketContext context) => OnClientDisconnectedAsync(context);
        }
    }
}
