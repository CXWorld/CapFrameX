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
        public void Catalog_DoesNotContainDxgiLifecycleOnlyExceptions()
        {
            foreach (string executable in new[]
            {
                "BF1.exe",
                "BF1_CTE.exe",
                "GearGame.exe",
                "MetroExodus.exe",
                "AoE2DE_s.exe",
                "AoMRT_s.exe"
            })
            {
                Assert.IsFalse(HookCompatibilityProfileCatalog.TryGet(executable,
                    out _), executable);
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
            Assert.IsFalse(profile.EnableXeFgNativePresentQueueRoute);
            Assert.IsTrue(profile.EnableGenericD3D12PresentRoute);
            Assert.IsFalse(profile.DisableFidelityFxSwapchainLifecycleHooks);
            Assert.AreEqual(
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
            Assert.IsFalse(profile.EnableXeFgNativePresentQueueRoute);
            Assert.IsTrue(profile.EnableGenericD3D12PresentRoute);
            Assert.IsTrue(profile.DisableFidelityFxSwapchainLifecycleHooks);
            Assert.AreEqual(
                NativeHookCompatibilityFlags.EnableGenericD3D12PresentRoute |
                NativeHookCompatibilityFlags.DisableFidelityFxSwapchainLifecycleHooks,
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
        public void Catalog_UsesGenericD3D12RouteAndEarlyInjectionForJediSurvivor()
        {
            Assert.IsTrue(HookCompatibilityProfileCatalog.TryGet(
                "JediSurvivor.exe", out HookCompatibilityProfile profile));
            Assert.IsTrue(profile.RequiresEarlyInjection);
            Assert.AreEqual("d3d12.dll", profile.EarlyInjectionModule);
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
        public void Catalog_LeavesAPlagueTaleLegacyToTheAutomaticProbing()
        {
            // The reference case for the evidence-driven ladder: its signature starts on the
            // generic route without FidelityFX lifecycle hooks (HookCompatibilityStagePlannerTest).
            Assert.IsFalse(HookCompatibilityProfileCatalog.TryGet("Resonance.exe", out _));
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
                NativeHookCompatibilityFlags.EnableGenericD3D12PresentRoute;

            Assert.IsTrue(HookCompatibilityChannel.TryCreate(processId, expected,
                out HookCompatibilityChannel channel, out string error), error);
            using (channel)
            {
                // The legacy 16-byte mapping for a version-1 hook, written once.
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

                // The version-2 mapping the hook keeps mapped and polls.
                using (MemoryMappedFile mapping = MemoryMappedFile.OpenExisting(
                    HookCompatibilityChannel.GetMappingNameV2(processId),
                    MemoryMappedFileRights.Read))
                using (MemoryMappedViewAccessor view = mapping.CreateViewAccessor(
                    0, HookCompatibilityChannel.ChannelSizeV2, MemoryMappedFileAccess.Read))
                {
                    Assert.AreEqual(HookCompatibilityChannel.MagicV2,
                        view.ReadInt32(HookCompatibilityChannel.MagicOffset));
                    Assert.AreEqual(HookCompatibilityChannel.Version2,
                        view.ReadInt32(HookCompatibilityChannel.VersionOffset));
                    Assert.AreEqual(processId,
                        view.ReadInt32(HookCompatibilityChannel.ProcessIdOffset));
                    Assert.AreEqual(unchecked((int)(uint)expected),
                        view.ReadInt32(HookCompatibilityChannel.FlagsOffset));
                    Assert.AreEqual(1, view.ReadInt32(HookCompatibilityChannel.SequenceOffset));
                    Assert.AreEqual(Environment.ProcessId,
                        view.ReadInt32(HookCompatibilityChannel.HostPidOffset));
                    Assert.AreEqual(1, channel.Sequence);
                    Assert.AreEqual(channel.Sequence,
                        view.ReadInt32(HookCompatibilityChannel.PublicationSequenceOffset));

                    // A stage change rewrites the payload and bumps the sequence last.
                    NativeHookCompatibilityFlags next = expected |
                        NativeHookCompatibilityFlags.DisableFidelityFxSwapchainLifecycleHooks;
                    Assert.IsTrue(channel.TryPublish(next, stageId: 3, stageIndex: 3, stageCount: 4,
                        probeActive: true, hostFlags: 3, out error), error);
                    Assert.AreEqual(2, channel.Sequence);
                    Assert.AreEqual(2, view.ReadInt32(HookCompatibilityChannel.SequenceOffset));
                    Assert.AreEqual(channel.Sequence,
                        view.ReadInt32(HookCompatibilityChannel.PublicationSequenceOffset));
                    Assert.AreEqual(unchecked((int)(uint)next),
                        view.ReadInt32(HookCompatibilityChannel.FlagsOffset));
                    Assert.AreEqual(3, view.ReadInt32(HookCompatibilityChannel.StageIdOffset));
                    Assert.AreEqual(3, view.ReadInt32(HookCompatibilityChannel.StageIndexOffset));
                    Assert.AreEqual(4, view.ReadInt32(HookCompatibilityChannel.StageCountOffset));
                    Assert.AreEqual(1, view.ReadInt32(HookCompatibilityChannel.ProbeActiveOffset));
                    Assert.AreEqual(3, view.ReadInt32(HookCompatibilityChannel.HostFlagsOffset));
                }
            }
        }

        [DataTestMethod]
        [DataRow(1, 2)]
        [DataRow(int.MaxValue, int.MinValue)]
        [DataRow(-1, 1)]
        public void CompatibilityChannel_RecoversAnInterruptedPublicationAndHandlesWraparound(
            int committed, int expected)
        {
            int processId = Environment.ProcessId;
            using (MemoryMappedFile mapping = MemoryMappedFile.CreateOrOpen(
                HookCompatibilityChannel.GetMappingNameV2(processId), HookCompatibilityChannel.ChannelSizeV2))
            using (MemoryMappedViewAccessor view = mapping.CreateViewAccessor())
            {
                view.Write(HookCompatibilityChannel.MagicOffset, HookCompatibilityChannel.MagicV2);
                view.Write(HookCompatibilityChannel.VersionOffset, HookCompatibilityChannel.Version2);
                view.Write(HookCompatibilityChannel.ProcessIdOffset, processId);
                view.Write(HookCompatibilityChannel.SequenceOffset, committed);
                view.Write(HookCompatibilityChannel.PublicationSequenceOffset, expected);
                view.Write(HookCompatibilityChannel.FlagsOffset, 12); // interrupted host write

                Assert.IsTrue(HookCompatibilityChannel.TryCreate(processId,
                    NativeHookCompatibilityFlags.None, out HookCompatibilityChannel channel,
                    out string error), error);
                using (channel)
                {
                    Assert.AreEqual(expected, channel.Sequence);
                    Assert.AreEqual(expected, view.ReadInt32(HookCompatibilityChannel.SequenceOffset));
                    Assert.AreEqual(expected,
                        view.ReadInt32(HookCompatibilityChannel.PublicationSequenceOffset));
                    Assert.AreEqual(0, view.ReadInt32(HookCompatibilityChannel.FlagsOffset));
                }
            }
        }

        [TestMethod]
        public void CompatibilityChannel_ExistsForFlagsNoneWithoutALegacyMapping()
        {
            // A vendor-aware start still needs the version-2 mapping so a live escalation can
            // reach the hook later; a version-1 hook has nothing to read for flags None.
            int processId = Process.GetCurrentProcess().Id;

            Assert.IsTrue(HookCompatibilityChannel.TryCreate(processId,
                NativeHookCompatibilityFlags.None, stageId: 0, stageIndex: 1, stageCount: 3,
                probeActive: true, hostFlags: 0, out HookCompatibilityChannel channel,
                out string error), error);
            using (channel)
            {
                using (MemoryMappedFile.OpenExisting(
                    HookCompatibilityChannel.GetMappingNameV2(processId),
                    MemoryMappedFileRights.Read))
                {
                }
                bool legacyExists = true;
                try
                {
                    using (MemoryMappedFile.OpenExisting(
                        HookCompatibilityChannel.GetMappingName(processId),
                        MemoryMappedFileRights.Read))
                    {
                    }
                }
                catch (System.IO.FileNotFoundException)
                {
                    legacyExists = false;
                }
                Assert.IsFalse(legacyExists);
            }
        }
    }
}
