using System;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class HookCompatibilityProfileTest
    {
        [TestMethod]
        public void Catalog_ContainsCuratedD3D11ReleaseHookExceptions()
        {
            foreach (string executable in new[]
            {
                "BF1.exe",
                "BF1_CTE.exe",
                "GearGame.exe",
                "MetroExodus.exe"
            })
            {
                Assert.IsTrue(HookCompatibilityProfileCatalog.TryGet(executable,
                    out HookCompatibilityProfile profile), executable);
                Assert.IsTrue(profile.DisableDxgiSwapchainReleaseHook, executable);
                Assert.AreEqual(TimeSpan.Zero, profile.InjectionDelay, executable);
            }
        }

        [TestMethod]
        public void Catalog_AppliesDirt5DelayAndNormalizesExecutablePaths()
        {
            Assert.IsTrue(HookCompatibilityProfileCatalog.TryGet(
                @"C:\Games\DIRT5.EXE", out HookCompatibilityProfile profile));
            Assert.AreEqual("dirt5.exe", profile.ExecutableName);
            Assert.AreEqual(TimeSpan.FromSeconds(15), profile.InjectionDelay);
            Assert.AreEqual(NativeHookCompatibilityFlags.None, profile.NativeFlags);
            Assert.IsFalse(HookCompatibilityProfileCatalog.TryGet(
                "unprofiled_game.exe", out _));
        }

        [TestMethod]
        public void Catalog_UsesGenericD3D12RouteForLegoBatman()
        {
            Assert.IsTrue(HookCompatibilityProfileCatalog.TryGet(
                "LEGOBatmanLotDK-Win64-Shipping.exe",
                out HookCompatibilityProfile profile));
            Assert.IsFalse(profile.RequiresEarlyInjection);
            Assert.IsNull(profile.EarlyInjectionModule);
            Assert.IsFalse(profile.DisableDxgiSwapchainReleaseHook);
            Assert.IsFalse(profile.EnableXeFgNativePresentQueueRoute);
            Assert.IsTrue(profile.EnableGenericD3D12PresentRoute);
            Assert.AreEqual(
                NativeHookCompatibilityFlags.EnableGenericD3D12PresentRoute,
                profile.NativeFlags);
            Assert.AreEqual(TimeSpan.Zero, profile.InjectionDelay);
            CollectionAssert.DoesNotContain(
                new System.Collections.Generic.List<HookCompatibilityProfile>(
                    HookCompatibilityProfileCatalog.GetEarlyInjectionProfiles()), profile);
        }

        [TestMethod]
        public void Catalog_UsesGenericD3D12RouteForTheWitcher3()
        {
            Assert.IsTrue(HookCompatibilityProfileCatalog.TryGet(
                "witcher3.exe", out HookCompatibilityProfile profile));
            Assert.IsFalse(profile.RequiresEarlyInjection);
            Assert.IsNull(profile.EarlyInjectionModule);
            Assert.IsFalse(profile.DisableDxgiSwapchainReleaseHook);
            Assert.IsFalse(profile.EnableXeFgNativePresentQueueRoute);
            Assert.IsTrue(profile.EnableGenericD3D12PresentRoute);
            Assert.AreEqual(
                NativeHookCompatibilityFlags.EnableGenericD3D12PresentRoute,
                profile.NativeFlags);
            Assert.AreEqual(TimeSpan.Zero, profile.InjectionDelay);
            CollectionAssert.DoesNotContain(
                new System.Collections.Generic.List<HookCompatibilityProfile>(
                    HookCompatibilityProfileCatalog.GetEarlyInjectionProfiles()), profile);
        }

        [TestMethod]
        public void Catalog_UsesEarlyGenericFidelityFxRouteForTheLastOfUsPartII()
        {
            Assert.IsTrue(HookCompatibilityProfileCatalog.TryGet(
                "tlou-ii.exe", out HookCompatibilityProfile profile));
            Assert.IsTrue(profile.RequiresEarlyInjection);
            Assert.AreEqual("d3d12.dll", profile.EarlyInjectionModule);
            Assert.IsTrue(profile.DisableDxgiSwapchainReleaseHook);
            Assert.IsFalse(profile.EnableXeFgNativePresentQueueRoute);
            Assert.IsTrue(profile.EnableGenericD3D12PresentRoute);
            Assert.AreEqual(
                NativeHookCompatibilityFlags.DisableDxgiSwapchainReleaseHook |
                NativeHookCompatibilityFlags.EnableGenericD3D12PresentRoute,
                profile.NativeFlags);
            Assert.AreEqual(TimeSpan.Zero, profile.InjectionDelay);
            CollectionAssert.Contains(
                new System.Collections.Generic.List<HookCompatibilityProfile>(
                    HookCompatibilityProfileCatalog.GetEarlyInjectionProfiles()), profile);
        }

        [TestMethod]
        public void Catalog_UsesGenericD3D12RouteForStalker2()
        {
            Assert.IsTrue(HookCompatibilityProfileCatalog.TryGet(
                "Stalker2-Win64-Shipping.exe",
                out HookCompatibilityProfile profile));
            Assert.IsFalse(profile.RequiresEarlyInjection);
            Assert.IsNull(profile.EarlyInjectionModule);
            Assert.IsFalse(profile.DisableDxgiSwapchainReleaseHook);
            Assert.IsFalse(profile.EnableXeFgNativePresentQueueRoute);
            Assert.IsTrue(profile.EnableGenericD3D12PresentRoute);
            Assert.AreEqual(
                NativeHookCompatibilityFlags.EnableGenericD3D12PresentRoute,
                profile.NativeFlags);
            Assert.AreEqual(TimeSpan.Zero, profile.InjectionDelay);
            CollectionAssert.DoesNotContain(
                new System.Collections.Generic.List<HookCompatibilityProfile>(
                    HookCompatibilityProfileCatalog.GetEarlyInjectionProfiles()), profile);
        }

        [TestMethod]
        public void Catalog_UsesGenericD3D12RouteForDyingLightTheBeast()
        {
            Assert.IsTrue(HookCompatibilityProfileCatalog.TryGet(
                "DyingLightGame_TheBeast_x64_rwdi.exe",
                out HookCompatibilityProfile profile));
            Assert.IsFalse(profile.RequiresEarlyInjection);
            Assert.IsNull(profile.EarlyInjectionModule);
            Assert.IsTrue(profile.DisableDxgiSwapchainReleaseHook);
            Assert.IsFalse(profile.EnableXeFgNativePresentQueueRoute);
            Assert.IsTrue(profile.EnableGenericD3D12PresentRoute);
            Assert.AreEqual(
                NativeHookCompatibilityFlags.DisableDxgiSwapchainReleaseHook |
                NativeHookCompatibilityFlags.EnableGenericD3D12PresentRoute,
                profile.NativeFlags);
            Assert.AreEqual(TimeSpan.Zero, profile.InjectionDelay);
            CollectionAssert.DoesNotContain(
                new System.Collections.Generic.List<HookCompatibilityProfile>(
                    HookCompatibilityProfileCatalog.GetEarlyInjectionProfiles()), profile);
        }

        [TestMethod]
        public void Catalog_UsesGenericD3D12RouteAndEarlyInjectionForJediSurvivor()
        {
            Assert.IsTrue(HookCompatibilityProfileCatalog.TryGet(
                "JediSurvivor.exe", out HookCompatibilityProfile profile));
            Assert.IsTrue(profile.RequiresEarlyInjection);
            Assert.AreEqual("d3d12.dll", profile.EarlyInjectionModule);
            Assert.IsFalse(profile.DisableDxgiSwapchainReleaseHook);
            Assert.IsFalse(profile.EnableXeFgNativePresentQueueRoute);
            Assert.IsTrue(profile.EnableGenericD3D12PresentRoute);
            Assert.AreEqual(
                NativeHookCompatibilityFlags.EnableGenericD3D12PresentRoute,
                profile.NativeFlags);
            Assert.AreEqual(TimeSpan.Zero, profile.InjectionDelay);
            CollectionAssert.Contains(
                new System.Collections.Generic.List<HookCompatibilityProfile>(
                    HookCompatibilityProfileCatalog.GetEarlyInjectionProfiles()), profile);
        }

        [TestMethod]
        public void Catalog_UsesGenericD3D12RouteAndEarlyInjectionForTheLastCaretaker()
        {
            Assert.IsTrue(HookCompatibilityProfileCatalog.TryGet(
                "VoyageSteam-Win64-Shipping.exe",
                out HookCompatibilityProfile profile));
            Assert.IsTrue(profile.RequiresEarlyInjection);
            Assert.AreEqual("d3d12.dll", profile.EarlyInjectionModule);
            Assert.IsTrue(profile.DisableDxgiSwapchainReleaseHook);
            Assert.IsFalse(profile.EnableXeFgNativePresentQueueRoute);
            Assert.IsTrue(profile.EnableGenericD3D12PresentRoute);
            Assert.AreEqual(
                NativeHookCompatibilityFlags.DisableDxgiSwapchainReleaseHook |
                NativeHookCompatibilityFlags.EnableGenericD3D12PresentRoute,
                profile.NativeFlags);
            Assert.AreEqual(TimeSpan.Zero, profile.InjectionDelay);
            CollectionAssert.Contains(
                new System.Collections.Generic.List<HookCompatibilityProfile>(
                    HookCompatibilityProfileCatalog.GetEarlyInjectionProfiles()), profile);
        }

        [TestMethod]
        public void CompatibilityDelay_IsAppliedOncePerPid()
        {
            long timestamp = 1000;
            var delay = new InjectionCompatibilityDelay(() => timestamp, 1000);

            Assert.AreEqual(TimeSpan.FromSeconds(15),
                delay.GetRemainingDelay(42, TimeSpan.FromSeconds(15)));
            timestamp += 9000;
            Assert.AreEqual(TimeSpan.FromSeconds(6),
                delay.GetRemainingDelay(42, TimeSpan.FromSeconds(15)));
            timestamp += 6000;
            Assert.AreEqual(TimeSpan.Zero,
                delay.GetRemainingDelay(42, TimeSpan.FromSeconds(15)));

            delay.Reset(42);
            Assert.AreEqual(TimeSpan.FromSeconds(15),
                delay.GetRemainingDelay(42, TimeSpan.FromSeconds(15)));
        }

        [TestMethod]
        public void CompatibilityChannel_PublishesNativeFlags()
        {
            int processId = Process.GetCurrentProcess().Id;
            NativeHookCompatibilityFlags expected =
                NativeHookCompatibilityFlags.DisableDxgiSwapchainReleaseHook;

            Assert.IsTrue(HookCompatibilityChannel.TryCreate(processId, expected,
                out HookCompatibilityChannel channel, out string error), error);
            using (channel)
            using (MemoryMappedFile mapping = MemoryMappedFile.OpenExisting(
                HookCompatibilityChannel.GetMappingName(processId),
                MemoryMappedFileRights.Read))
            using (MemoryMappedViewAccessor view = mapping.CreateViewAccessor(
                0, HookCompatibilityChannel.ChannelSize, MemoryMappedFileAccess.Read))
            {
                Assert.AreEqual(HookCompatibilityChannel.Magic,
                    view.ReadInt32(HookCompatibilityChannel.MagicOffset));
                Assert.AreEqual(HookCompatibilityChannel.Version,
                    view.ReadInt32(HookCompatibilityChannel.VersionOffset));
                Assert.AreEqual(processId,
                    view.ReadInt32(HookCompatibilityChannel.ProcessIdOffset));
                Assert.AreEqual(unchecked((int)(uint)expected),
                    view.ReadInt32(HookCompatibilityChannel.FlagsOffset));
            }
        }
    }
}
