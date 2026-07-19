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
