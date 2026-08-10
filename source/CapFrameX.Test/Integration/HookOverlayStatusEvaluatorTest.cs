using System.Diagnostics;
using System.Reactive.Subjects;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

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

        [TestMethod]
        public void IsDxgiInjectionAllowed_SuppressedDuringTheVulkanLayerStartupGrace()
        {
            bool allowed = HookOverlayManager.IsDxgiInjectionAllowed(
                probeSucceeded: true, hasRecentVulkanPresent: false,
                vulkanLayerLoaded: true, vulkanLayerHasEverPresented: false,
                vulkanLayerFirstPresentGraceElapsed: false);

            Assert.IsFalse(allowed);
        }

        [TestMethod]
        public void IsDxgiInjectionAllowed_AllowsDxgiWhenLoadedLayerNeverPresents()
        {
            // Crysis Remastered creates Vulkan objects for an auxiliary API while its actual
            // swapchain remains DXGI. Module presence alone must not block that title forever.
            bool allowed = HookOverlayManager.IsDxgiInjectionAllowed(
                probeSucceeded: true, hasRecentVulkanPresent: false,
                vulkanLayerLoaded: true, vulkanLayerHasEverPresented: false,
                vulkanLayerFirstPresentGraceElapsed: true);

            Assert.IsTrue(allowed);
        }

        [TestMethod]
        public void IsDxgiInjectionAllowed_DoesNotExpireAnEstablishedVulkanRenderer()
        {
            // A paused Vulkan title can have no recent heartbeat. Once it has presented, the
            // startup timeout must never reinterpret it as a DXGI renderer.
            bool allowed = HookOverlayManager.IsDxgiInjectionAllowed(
                probeSucceeded: true, hasRecentVulkanPresent: false,
                vulkanLayerLoaded: true, vulkanLayerHasEverPresented: true,
                vulkanLayerFirstPresentGraceElapsed: true);

            Assert.IsFalse(allowed);
        }

        [TestMethod]
        public void HasVulkanLayerFirstPresentGraceElapsed_UsesTheConfiguredBoundary()
        {
            long graceTicks = System.Math.Max(1,
                System.Diagnostics.Stopwatch.Frequency *
                (long)HookOverlayManager.VulkanLayerFirstPresentGraceMs / 1000L);
            long since = 1234;

            Assert.IsFalse(HookOverlayManager.HasVulkanLayerFirstPresentGraceElapsed(
                since, since + graceTicks - 1));
            Assert.IsTrue(HookOverlayManager.HasVulkanLayerFirstPresentGraceElapsed(
                since, since + graceTicks));
            Assert.IsFalse(HookOverlayManager.HasVulkanLayerFirstPresentGraceElapsed(
                since, since - 1));
        }

        [TestMethod]
        public void IsDxgiInjectionAllowed_AllowsDxgiWithoutTheVulkanLayer()
        {
            bool allowed = HookOverlayManager.IsDxgiInjectionAllowed(
                probeSucceeded: true, hasRecentVulkanPresent: false,
                vulkanLayerLoaded: false);

            Assert.IsTrue(allowed);
        }

        [TestMethod]
        public void IsDxgiInjectionAllowed_SuppressedWhenTheLayerCheckIsInconclusive()
        {
            // An unreadable module list is not evidence that no Vulkan layer is present, and
            // injection cannot be undone once it happened.
            bool allowed = HookOverlayManager.IsDxgiInjectionAllowed(
                probeSucceeded: true, hasRecentVulkanPresent: false,
                vulkanLayerLoaded: false, vulkanLayerCheckInconclusive: true);

            Assert.IsFalse(allowed);
        }

        [TestMethod]
        public void IsDxgiInjectionAllowed_NeverOverridesVulkanOrProbeFailure()
        {
            Assert.IsFalse(HookOverlayManager.IsDxgiInjectionAllowed(
                probeSucceeded: true, hasRecentVulkanPresent: true,
                vulkanLayerLoaded: false));
            Assert.IsFalse(HookOverlayManager.IsDxgiInjectionAllowed(
                probeSucceeded: false, hasRecentVulkanPresent: false,
                vulkanLayerLoaded: false));
        }

        [TestMethod]
        public void HasRendererInitializationStalled_TrueOnceTheTimeoutElapsed()
        {
            ulong since = 1000;

            Assert.IsFalse(HookOverlayManager.HasRendererInitializationStalled(
                since, since + HookOverlayManager.HookRendererReadyTimeoutMs - 1));
            Assert.IsTrue(HookOverlayManager.HasRendererInitializationStalled(
                since, since + HookOverlayManager.HookRendererReadyTimeoutMs));
        }

        [TestMethod]
        public void HasRendererInitializationStalled_IgnoresUntrackedOrRolledBackClock()
        {
            Assert.IsFalse(HookOverlayManager.HasRendererInitializationStalled(
                initializingSinceTickMs: 0, nowTickMs: ulong.MaxValue));
            Assert.IsFalse(HookOverlayManager.HasRendererInitializationStalled(
                initializingSinceTickMs: 5000, nowTickMs: 4000));
        }

        [TestMethod]
        public void HasFirstPresentTimedOut_TrueAfterFifteenSeconds()
        {
            ulong since = 1000;

            Assert.AreEqual(15000UL, HookOverlayManager.HookFirstPresentTimeoutMs);
            Assert.IsFalse(HookOverlayManager.HasFirstPresentTimedOut(
                since, since + HookOverlayManager.HookFirstPresentTimeoutMs - 1));
            Assert.IsTrue(HookOverlayManager.HasFirstPresentTimedOut(
                since, since + HookOverlayManager.HookFirstPresentTimeoutMs));
        }

        [TestMethod]
        public void HasFirstPresentTimedOut_IgnoresUntrackedOrRolledBackClock()
        {
            Assert.IsFalse(HookOverlayManager.HasFirstPresentTimedOut(
                waitingSinceTickMs: 0, nowTickMs: ulong.MaxValue));
            Assert.IsFalse(HookOverlayManager.HasFirstPresentTimedOut(
                waitingSinceTickMs: 5000, nowTickMs: 4000));
        }

        [TestMethod]
        public void EarlyInjectionTargetMatch_RequiresTheSameExecutableIdentity()
        {
            const string path =
                @"C:\Games\Dying Light The Beast\DyingLightGame_TheBeast_x64_rwdi.exe";

            Assert.IsTrue(HookOverlayManager.IsEarlyInjectionTargetMatch(
                "DyingLightGame_TheBeast_x64_rwdi", path,
                "dyinglightgame_thebeast_x64_rwdi", path.ToUpperInvariant()));
            Assert.IsTrue(HookOverlayManager.IsEarlyInjectionTargetMatch(
                "DyingLightGame_TheBeast_x64_rwdi", null,
                "dyinglightgame_thebeast_x64_rwdi", path));
            Assert.IsFalse(HookOverlayManager.IsEarlyInjectionTargetMatch(
                "DyingLightGame_TheBeast_x64_rwdi", path,
                "DyingLightGame_TheBeast_x64_rwdi", @"D:\Other\game.exe"));
            Assert.IsFalse(HookOverlayManager.IsEarlyInjectionTargetMatch(
                "DyingLightGame_TheBeast_x64_rwdi", path,
                "unrelated", path));
        }

        [TestMethod]
        public void EarlyInjectionModuleGate_RequiresAMappedModule()
        {
            int processId = Process.GetCurrentProcess().Id;

            Assert.IsTrue(HookTargetPolicy.TryFindLoadedModule(processId,
                new[] { "kernel32.dll" }, out string loadedModule,
                out string error), error);
            Assert.IsTrue(string.Equals("kernel32.dll", loadedModule,
                System.StringComparison.OrdinalIgnoreCase));

            Assert.IsTrue(HookTargetPolicy.TryFindLoadedModule(processId,
                new[] { "definitely-not-loaded-cfx-test.dll" },
                out loadedModule, out error), error);
            Assert.IsNull(loadedModule);
        }

        [TestMethod]
        public void Constructor_AcceptsTheBehaviorSubjectsSynchronousInitialPid()
        {
            using var changes = new Subject<(string key, object value)>();
            using var pids = new BehaviorSubject<int>(0);
            using var rows = new Subject<string[]>();
            var configuration = new Mock<IAppConfiguration>();
            configuration.SetupGet(x => x.OnValueChanged).Returns(changes);
            configuration.SetupGet(x => x.EnableHookOverlay).Returns(true);
            configuration.SetupGet(x => x.IsOverlayActive).Returns(true);

            using var manager = new HookOverlayManager(configuration.Object, pids, rows,
                processIdColumnIndex: 0, runtimeColumnIndex: 1,
                dllPathOverride: @"C:\missing\cfx_osd_hook.dll");
        }

        [TestMethod]
        public void ShouldUseHookFreeFallback_UsesFallbackForD3D9()
        {
            bool useFallback = HookOverlayManager.ShouldUseHookFreeFallback(
                hookEnabled: true, processId: 42, runtime: "D3D9");

            Assert.IsTrue(useFallback);
        }

        [TestMethod]
        public void ShouldUseHookFreeFallback_DoesNotReplaceNativeRenderers()
        {
            Assert.IsFalse(HookOverlayManager.ShouldUseHookFreeFallback(
                hookEnabled: true, processId: 42, runtime: "DXGI"));
            Assert.IsFalse(HookOverlayManager.ShouldUseHookFreeFallback(
                hookEnabled: true, processId: 42, runtime: "D3D11"));
            Assert.IsFalse(HookOverlayManager.ShouldUseHookFreeFallback(
                hookEnabled: true, processId: 42, runtime: "D3D12"));
            Assert.IsFalse(HookOverlayManager.ShouldUseHookFreeFallback(
                hookEnabled: true, processId: 42, runtime: "Vulkan"));
        }

        [TestMethod]
        public void ShouldUseHookFreeFallback_UsesFallbackForBlockedTarget()
        {
            bool useFallback = HookOverlayManager.ShouldUseHookFreeFallback(
                hookEnabled: true, processId: 42, runtime: "DXGI",
                targetBlockReason: "process 'cs2' is on the in-game injection blacklist");

            Assert.IsTrue(useFallback);
        }

        [TestMethod]
        public void ShouldUseHookFreeFallback_UsesFallbackForNativeHookFailure()
        {
            bool useFallback = HookOverlayManager.ShouldUseHookFreeFallback(
                hookEnabled: true, processId: 42, runtime: "D3D11",
                nativeFallbackReason: "in-game hook injection failed");

            Assert.IsTrue(useFallback);
        }

        [TestMethod]
        public void ShouldUseHookFreeFallback_WaitsForEnabledHookAndValidTarget()
        {
            Assert.IsFalse(HookOverlayManager.ShouldUseHookFreeFallback(
                hookEnabled: false, processId: 42, runtime: "D3D9"));
            Assert.IsFalse(HookOverlayManager.ShouldUseHookFreeFallback(
                hookEnabled: true, processId: 0, runtime: "D3D9"));
            Assert.IsFalse(HookOverlayManager.ShouldUseHookFreeFallback(
                hookEnabled: true, processId: 42, runtime: null));
            Assert.IsFalse(HookOverlayManager.ShouldUseHookFreeFallback(
                hookEnabled: true, processId: 42, runtime: "<error>"));
        }

        [TestMethod]
        public void ShouldUseHookFreeFallback_DoesNotOutliveTargetProcess()
        {
            Assert.IsFalse(HookOverlayManager.ShouldUseHookFreeFallback(
                hookEnabled: true, processId: 42, runtime: "DXGI",
                targetBlockReason: "process identity check failed",
                targetProcessAlive: false));
            Assert.IsFalse(HookOverlayManager.ShouldUseHookFreeFallback(
                hookEnabled: true, processId: 42, runtime: "D3D11",
                nativeFallbackReason: "native hook status was unavailable",
                targetProcessAlive: false));
            Assert.IsFalse(HookOverlayManager.ShouldUseHookFreeFallback(
                hookEnabled: true, processId: 42, runtime: "D3D9",
                targetProcessAlive: false));
        }

        [TestMethod]
        public void CreateHookFreeFallbackStatus_ReportsFallbackWhileVisible()
        {
            HookOverlayStatus status = HookOverlayManager.CreateHookFreeFallbackStatus(
                processId: 42, runtime: "D3D9", visible: true);

            Assert.AreEqual(EHookOverlayStatus.Fallback, status.State);
            StringAssert.Contains(status.Detail, "hook-free fallback is active");
        }

        [TestMethod]
        public void CreateHookFreeFallbackStatus_ReportsHiddenWhenOverlayIsOff()
        {
            HookOverlayStatus status = HookOverlayManager.CreateHookFreeFallbackStatus(
                processId: 42, runtime: "D3D9", visible: false);

            Assert.AreEqual(EHookOverlayStatus.Hidden, status.State);
            StringAssert.Contains(status.Detail, "hook-free fallback is hidden");
        }

        [TestMethod]
        public void CreateHookFreeFallbackStatus_ReportsNativeFailureReason()
        {
            HookOverlayStatus status = HookOverlayManager.CreateHookFreeFallbackStatus(
                processId: 42, runtime: "DXGI", visible: true,
                fallbackReason: "native hook did not publish status within 3 seconds after injection");

            Assert.AreEqual(EHookOverlayStatus.Fallback, status.State);
            StringAssert.Contains(status.Detail, "did not publish status within 3 seconds");
        }

        [TestMethod]
        public void HasHookStatusTimedOut_WaitsForGracePeriod()
        {
            Assert.IsFalse(HookOverlayManager.HasHookStatusTimedOut(
                injectionSucceeded: true, injectionSucceededTickMs: 1000,
                lastNativeStatusTickMs: 0, hasNativeStatus: false,
                nowTickMs: 1000 + HookOverlayManager.HookHandshakeTimeoutMs - 1));
            Assert.IsTrue(HookOverlayManager.HasHookStatusTimedOut(
                injectionSucceeded: true, injectionSucceededTickMs: 1000,
                lastNativeStatusTickMs: 0, hasNativeStatus: false,
                nowTickMs: 1000 + HookOverlayManager.HookHandshakeTimeoutMs));
        }

        [TestMethod]
        public void HasHookStatusTimedOut_DoesNotFallbackWithNativeStatus()
        {
            Assert.IsFalse(HookOverlayManager.HasHookStatusTimedOut(
                injectionSucceeded: true, injectionSucceededTickMs: 1000,
                lastNativeStatusTickMs: 0, hasNativeStatus: true,
                nowTickMs: 1000 + HookOverlayManager.HookHandshakeTimeoutMs));
        }

        [TestMethod]
        public void HookTargetPolicy_BlacklistsProtectedProcessesForInjectionOnly()
        {
            Assert.IsTrue(HookTargetPolicy.IsInjectionBlacklisted("CS2.exe", out string reason));
            StringAssert.Contains(reason, "in-game injection blacklist");
            Assert.IsTrue(HookTargetPolicy.IsInjectionBlacklisted(
                "VALORANT-Win64-Shipping.exe", out _));
            Assert.IsFalse(HookTargetPolicy.IsInjectionBlacklisted("hl2.exe", out _));
        }

        [TestMethod]
        public void HookTargetPolicy_OnlyTreatsLoaderReadinessErrorsAsTransient()
        {
            Assert.IsTrue(HookTargetPolicy.IsTransientStartupFailure(
                "module scan returned no modules (18)"));
            Assert.IsTrue(HookTargetPolicy.IsTransientStartupFailure(
                "process identity check failed (process is still starting)"));
            Assert.IsFalse(HookTargetPolicy.IsTransientStartupFailure(
                "anti-cheat module 'beclient_x64.dll' matched 'beclient'"));
            Assert.IsFalse(HookTargetPolicy.IsTransientStartupFailure(
                "process 'cs2' is on the in-game injection blacklist"));
        }

        /// <summary>
        /// The capture file's ResolutionInfo is written from this value. It used to come only
        /// from RTSS, which is never started while the in-game overlay renders — so every
        /// capture taken that way recorded an empty resolution.
        /// </summary>
        [TestMethod]
        public void EvaluateNative_PublishesTheHooksSwapchainExtent()
        {
            var snapshot = ReadySnapshot();
            snapshot.ResolutionX = 2560;
            snapshot.ResolutionY = 1440;

            var status = HookOverlayStatusEvaluator.EvaluateNative(4711, "DXGI", snapshot, Now);

            Assert.AreEqual("2560x1440", status.RenderResolution);
        }

        [TestMethod]
        public void EvaluateNative_ReportsAnUnmeasuredExtentAsUnknown()
        {
            // A hook that has not presented yet publishes 0/0, and so does an older hook build
            // that does not know the field at all. Neither may turn into a "0x0" in the file.
            var status = HookOverlayStatusEvaluator.EvaluateNative(4711, "DXGI", ReadySnapshot(),
                Now);

            Assert.IsNull(status.RenderResolution);
            Assert.IsNull(HookOverlayStatusEvaluator.FormatResolution(0, 0));
            Assert.IsNull(HookOverlayStatusEvaluator.FormatResolution(3840, 0));
            Assert.IsNull(HookOverlayStatusEvaluator.FormatResolution(0, 2160));
        }

        /// <summary>
        /// The capture file's ApiInfo. RTSS answers only for processes it hooked itself, so with
        /// the in-game overlay the hook's own proven device type is the only source.
        /// </summary>
        [TestMethod]
        public void EvaluateNative_PublishesTheProvenDeviceType()
        {
            var d3d11 = ReadySnapshot();
            d3d11.Api = NativeHookApi.D3D11;
            var d3d12 = ReadySnapshot();
            d3d12.Api = NativeHookApi.D3D12;

            // Spelled the way RTSS spells it, so both sources produce comparable records.
            Assert.AreEqual("DX11",
                HookOverlayStatusEvaluator.EvaluateNative(1, "DXGI", d3d11, Now).RenderApi);
            Assert.AreEqual("DX12",
                HookOverlayStatusEvaluator.EvaluateNative(1, "DXGI", d3d12, Now).RenderApi);
        }

        [TestMethod]
        public void EvaluateNative_ReportsAnUnprovenDeviceTypeAsUnknown()
        {
            // Before the first present, and for any hook older than the field, this reads 0.
            Assert.IsNull(
                HookOverlayStatusEvaluator.EvaluateNative(1, "DXGI", ReadySnapshot(), Now)
                    .RenderApi);
            Assert.IsNull(HookOverlayStatusEvaluator.FormatApi(NativeHookApi.Unknown));
        }

        [TestMethod]
        public void EvaluateVulkan_ReportsVulkanOnceTheLayerIsLoaded()
        {
            var loaded = new VulkanActivitySnapshot
            {
                IsLayerLoaded = true,
                LastVulkanPresentTickMs = (long)Now - 250
            };
            var notLoaded = new VulkanActivitySnapshot { IsLayerLoaded = false };

            Assert.AreEqual("Vulkan", HookOverlayStatusEvaluator
                .EvaluateVulkan(1, "Vulkan", loaded, Now, overlayVisible: true).RenderApi);
            // Nothing is proven before the layer is in the process.
            Assert.IsNull(HookOverlayStatusEvaluator
                .EvaluateVulkan(1, "Vulkan", notLoaded, Now, overlayVisible: true).RenderApi);
        }

        [TestMethod]
        public void EvaluateVulkan_PublishesTheLayersSwapchainExtent()
        {
            var native = new VulkanActivitySnapshot
            {
                IsLayerLoaded = true,
                LastVulkanPresentTickMs = (long)Now - 250,
                ResolutionX = 3440,
                ResolutionY = 1440
            };

            var active = HookOverlayStatusEvaluator.EvaluateVulkan(1, "Vulkan", native, Now,
                overlayVisible: true);
            var hidden = HookOverlayStatusEvaluator.EvaluateVulkan(1, "Vulkan", native, Now,
                overlayVisible: false);

            Assert.AreEqual(EHookOverlayStatus.Active, active.State);
            Assert.AreEqual("3440x1440", active.RenderResolution);
            // A capture may well be taken with the overlay switched off.
            Assert.AreEqual(EHookOverlayStatus.Hidden, hidden.State);
            Assert.AreEqual("3440x1440", hidden.RenderResolution);
        }

        [TestMethod]
        public void EvaluateVulkan_ReportsAnUnmeasuredExtentAsUnknown()
        {
            // An older layer leaves the packed word at 0, which must not become "0x0".
            var native = new VulkanActivitySnapshot
            {
                IsLayerLoaded = true,
                LastVulkanPresentTickMs = (long)Now - 250
            };

            Assert.IsNull(HookOverlayStatusEvaluator
                .EvaluateVulkan(1, "Vulkan", native, Now, overlayVisible: true)
                .RenderResolution);
        }

        [TestMethod]
        public void EvaluateNative_KeepsTheExtentAcrossEveryLiveState()
        {
            // The resolution must survive the states a capture can end in — a game paused at the
            // end of a run (Idle) or an overlay toggled off (Hidden) still has a resolution.
            var idle = ReadySnapshot();
            idle.ResolutionX = 1920;
            idle.ResolutionY = 1080;
            idle.LastHeartbeatTickMs = (long)(Now - HookStatusProbe.HeartbeatStaleAfterMs - 1);

            var hidden = ReadySnapshot();
            hidden.ResolutionX = 1920;
            hidden.ResolutionY = 1080;
            hidden.Flags &= ~NativeHookStatusFlags.Visible;

            var idleStatus = HookOverlayStatusEvaluator.EvaluateNative(1, "DXGI", idle, Now);
            var hiddenStatus = HookOverlayStatusEvaluator.EvaluateNative(1, "DXGI", hidden, Now);

            Assert.AreEqual(EHookOverlayStatus.Idle, idleStatus.State);
            Assert.AreEqual("1920x1080", idleStatus.RenderResolution);
            Assert.AreEqual(EHookOverlayStatus.Hidden, hiddenStatus.State);
            Assert.AreEqual("1920x1080", hiddenStatus.RenderResolution);
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
