using CapFrameX.Contracts.Overlay;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class HookOverlayStatusEvaluatorTest
    {
        private const ulong Now = 10000;

        [TestMethod]
        public void EvaluateNative_ReturnsActiveForFreshRenderedHeartbeat()
        {
            NativeHookStatusSnapshot native = ReadySnapshot();

            HookOverlayStatus status = HookOverlayStatusEvaluator.EvaluateNative(
                42, "DXGI", native, Now);

            Assert.AreEqual(EHookOverlayStatus.Active, status.State);
            Assert.AreEqual(500, status.HeartbeatAgeMilliseconds);
            Assert.AreEqual(1, status.SteadyRefcount);
            Assert.AreEqual(0, status.ReleaseThreshold);
        }

        [TestMethod]
        public void EvaluateNative_ReturnsIdleAfterThreeMissedHeartbeats()
        {
            NativeHookStatusSnapshot native = ReadySnapshot();
            native.LastHeartbeatTickMs = (long)(Now - HookStatusProbe.HeartbeatStaleAfterMs - 1);

            HookOverlayStatus status = HookOverlayStatusEvaluator.EvaluateNative(
                42, "DXGI", native, Now);

            Assert.AreEqual(EHookOverlayStatus.Idle, status.State);
        }

        [TestMethod]
        public void EvaluateNative_ReturnsHiddenWhilePresentRemainsLive()
        {
            NativeHookStatusSnapshot native = ReadySnapshot();
            native.Flags &= ~NativeHookStatusFlags.Visible;

            HookOverlayStatus status = HookOverlayStatusEvaluator.EvaluateNative(
                42, "DXGI", native, Now);

            Assert.AreEqual(EHookOverlayStatus.Hidden, status.State);
        }

        [TestMethod]
        public void EvaluateNative_WaitsForFirstPresent()
        {
            var native = new NativeHookStatusSnapshot
            {
                Flags = NativeHookStatusFlags.Loaded | NativeHookStatusFlags.HooksArmed
            };

            HookOverlayStatus status = HookOverlayStatusEvaluator.EvaluateNative(
                42, "DXGI", native, Now);

            Assert.AreEqual(EHookOverlayStatus.Waiting, status.State);
        }

        [TestMethod]
        public void EvaluateNative_ReportsNativeRendererError()
        {
            NativeHookStatusSnapshot native = ReadySnapshot();
            native.Flags |= NativeHookStatusFlags.Error;
            native.LastError = 2;

            HookOverlayStatus status = HookOverlayStatusEvaluator.EvaluateNative(
                42, "DXGI", native, Now);

            Assert.AreEqual(EHookOverlayStatus.Error, status.State);
            StringAssert.Contains(status.Detail, "renderer creation failed");
        }

        [TestMethod]
        public void EvaluateVulkan_ReturnsActiveForFreshLayerPresent()
        {
            var native = new VulkanActivitySnapshot
            {
                IsLayerLoaded = true,
                LastVulkanPresentTickMs = (long)Now - 250
            };

            HookOverlayStatus status = HookOverlayStatusEvaluator.EvaluateVulkan(
                42, "Vulkan", native, Now, overlayVisible: true);

            Assert.AreEqual(EHookOverlayStatus.Active, status.State);
            Assert.AreEqual(250, status.HeartbeatAgeMilliseconds);
        }

        [TestMethod]
        public void EvaluateVulkan_ReturnsIdleForStaleLayerPresent()
        {
            var native = new VulkanActivitySnapshot
            {
                IsLayerLoaded = true,
                LastVulkanPresentTickMs =
                    (long)(Now - HookStatusProbe.HeartbeatStaleAfterMs - 1)
            };

            HookOverlayStatus status = HookOverlayStatusEvaluator.EvaluateVulkan(
                42, "Vulkan", native, Now, overlayVisible: true);

            Assert.AreEqual(EHookOverlayStatus.Idle, status.State);
        }

        [TestMethod]
        public void EvaluateVulkan_ReturnsHiddenForLiveLayerWhenOverlayIsHidden()
        {
            var native = new VulkanActivitySnapshot
            {
                IsLayerLoaded = true,
                LastVulkanPresentTickMs = (long)Now - 250
            };

            HookOverlayStatus status = HookOverlayStatusEvaluator.EvaluateVulkan(
                42, "Vulkan", native, Now, overlayVisible: false);

            Assert.AreEqual(EHookOverlayStatus.Hidden, status.State);
        }

        [TestMethod]
        public void EvaluateVulkan_ReturnsErrorWhenCompositorYieldsToDxgi()
        {
            var native = new VulkanActivitySnapshot
            {
                IsLayerLoaded = true,
                LastVulkanPresentTickMs = (long)Now - 250,
                PreferredBackend = 1
            };

            HookOverlayStatus status = HookOverlayStatusEvaluator.EvaluateVulkan(
                42, "Vulkan", native, Now, overlayVisible: true);

            Assert.AreEqual(EHookOverlayStatus.Error, status.State);
        }

        [TestMethod]
        public void ShouldUseVulkanStatus_PrefersFreshVulkanEvidenceOverReportedDxgi()
        {
            bool useVulkan = HookOverlayManager.ShouldUseVulkanStatus("DXGI",
                hasVulkanStatus: true, vulkanHeartbeatAgeMs: 250,
                hasDxgiStatus: false, dxgiTransitionStarted: false);

            Assert.IsTrue(useVulkan);
        }

        [TestMethod]
        public void ShouldUseVulkanStatus_KeepsPausedVulkanWithoutDxgiTakeover()
        {
            bool useVulkan = HookOverlayManager.ShouldUseVulkanStatus("DXGI",
                hasVulkanStatus: true, vulkanHeartbeatAgeMs: 10000,
                hasDxgiStatus: false, dxgiTransitionStarted: false);

            Assert.IsTrue(useVulkan);
        }

        [TestMethod]
        public void ShouldUseVulkanStatus_AllowsConfirmedDxgiTakeoverAfterVulkanStops()
        {
            bool useVulkan = HookOverlayManager.ShouldUseVulkanStatus("DXGI",
                hasVulkanStatus: true, vulkanHeartbeatAgeMs: 10000,
                hasDxgiStatus: true, dxgiTransitionStarted: true);

            Assert.IsFalse(useVulkan);
        }

        private static NativeHookStatusSnapshot ReadySnapshot()
        {
            return new NativeHookStatusSnapshot
            {
                Flags = NativeHookStatusFlags.Loaded |
                        NativeHookStatusFlags.HooksArmed |
                        NativeHookStatusFlags.PresentSeen |
                        NativeHookStatusFlags.RendererReady |
                        NativeHookStatusFlags.Visible |
                        NativeHookStatusFlags.MetricsConnected |
                        NativeHookStatusFlags.Rendered,
                LastHeartbeatTickMs = (long)Now - 500,
                MetricsEntryCount = 52,
                SteadyRefcount = 1,
                ReleaseThreshold = 0
            };
        }
    }
}
