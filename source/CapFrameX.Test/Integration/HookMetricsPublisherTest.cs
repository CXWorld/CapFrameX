using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Reactive.Subjects;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class HookMetricsPublisherTest
    {
        private const int GamePid = 4321;
        private const int OtherGamePid = 8765;

        [TestMethod]
        public void HookNotSelected_EntriesNeverCheckTheTargetPolicy()
        {
            // RTSS or the hook-free overlay renders: the policy's module scan ran every 2 s and
            // logged a block for anti-cheat titles the hook never serves.
            using var context = new PublisherContext(hookEnabled: false, overlayActive: true);
            context.BlockedPids.Add(GamePid);
            context.Pids.OnNext(GamePid);

            for (int i = 0; i < 5; i++) context.Tick();

            CollectionAssert.AreEqual(Array.Empty<int>(), context.PolicyChecks);
            Assert.IsNull(context.PublishedTargetPid(), "No metrics channel may exist for an unselected hook.");
        }

        [TestMethod]
        public void HookNotSelected_PidAndVisibilityChangesNeverCheckTheTargetPolicy()
        {
            using var context = new PublisherContext(hookEnabled: false, overlayActive: true);

            context.Pids.OnNext(GamePid);
            context.Pids.OnNext(OtherGamePid);
            context.SetOverlayActive(false);
            context.SetOverlayActive(true);
            context.Tick();

            CollectionAssert.AreEqual(Array.Empty<int>(), context.PolicyChecks);
        }

        [TestMethod]
        public void HookSelectedAndVisible_EveryTickChecksTheSelectedPid()
        {
            using var context = new PublisherContext(hookEnabled: true, overlayActive: true);
            context.Pids.OnNext(GamePid);

            context.Tick();
            context.Tick();
            context.Tick();

            CollectionAssert.AreEqual(new[] { GamePid, GamePid, GamePid, GamePid }, context.PolicyChecks,
                "One check for the PID change and one per tick.");
            Assert.AreEqual(GamePid, context.PublishedTargetPid());
        }

        [TestMethod]
        public void HookSelectedButHidden_EntriesNeverCheckTheTargetPolicy()
        {
            // A remote API client keeps the entries flowing while the overlay is switched off.
            using var context = new PublisherContext(hookEnabled: true, overlayActive: false);
            context.Pids.OnNext(GamePid);

            context.Tick();
            context.Tick();

            CollectionAssert.AreEqual(Array.Empty<int>(), context.PolicyChecks);
            Assert.AreEqual(0, context.PublishedTargetPid());
        }

        [TestMethod]
        public void SelectingTheHook_ChecksThePolicyBeforePublishingTheTarget()
        {
            using var context = new PublisherContext(hookEnabled: false, overlayActive: true);
            context.Pids.OnNext(GamePid);

            context.SetHookEnabled(true);

            CollectionAssert.AreEqual(new[] { GamePid }, context.PolicyChecks);
            Assert.AreEqual(GamePid, context.PublishedTargetPid());
        }

        [TestMethod]
        public void SelectingTheHook_ForAGameChosenWhileItWasOff_DoesNotReuseTheOldVerdict()
        {
            using var context = new PublisherContext(hookEnabled: true, overlayActive: true);
            context.Pids.OnNext(GamePid);
            Assert.AreEqual(GamePid, context.PublishedTargetPid());

            context.SetHookEnabled(false);
            context.BlockedPids.Add(OtherGamePid);
            context.Pids.OnNext(OtherGamePid);
            context.Tick();
            int checksWhileOff = context.PolicyChecks.Count;
            context.SetHookEnabled(true);

            Assert.AreEqual(1, checksWhileOff, "Only the check from before the switch.");
            CollectionAssert.AreEqual(new[] { GamePid, OtherGamePid }, context.PolicyChecks);
            Assert.AreEqual(0, context.PublishedTargetPid(),
                "The allowed verdict of the previous game must not let the hook draw for a blocked one.");
        }

        [TestMethod]
        public void ShowingTheOverlay_ChecksAGameChosenWhileHidden()
        {
            using var context = new PublisherContext(hookEnabled: true, overlayActive: true);
            context.Pids.OnNext(GamePid);
            context.SetOverlayActive(false);
            Assert.AreEqual(0, context.PublishedTargetPid());

            context.BlockedPids.Add(OtherGamePid);
            context.Pids.OnNext(OtherGamePid);
            CollectionAssert.AreEqual(new[] { GamePid }, context.PolicyChecks,
                "A hidden hook does not check the new game yet.");

            context.SetOverlayActive(true);

            CollectionAssert.AreEqual(new[] { GamePid, OtherGamePid }, context.PolicyChecks);
            Assert.AreEqual(0, context.PublishedTargetPid());

            context.BlockedPids.Remove(OtherGamePid);
            context.Tick();
            Assert.AreEqual(OtherGamePid, context.PublishedTargetPid(),
                "A game that passes the policy later is picked up by the next tick.");
        }

        [TestMethod]
        public void SwitchingTheHookOff_StopsTheChecksAndWithdrawsTheChannel()
        {
            using var context = new PublisherContext(hookEnabled: true, overlayActive: true);
            context.Pids.OnNext(GamePid);
            context.Tick();
            int checksWhileOn = context.PolicyChecks.Count;

            context.SetHookEnabled(false);
            context.Tick();
            context.Tick();

            Assert.AreEqual(2, checksWhileOn);
            Assert.AreEqual(checksWhileOn, context.PolicyChecks.Count);
            Assert.IsNull(context.PublishedTargetPid());
        }

        private sealed class PublisherContext : IDisposable
        {
            private const int OffTargetPid = 16;
            private readonly string _metricsMap =
                @"Local\CfxOsdMetricsPublisherTest_" + Guid.NewGuid().ToString("N");
            private readonly string _placementMap =
                @"Local\CfxOsdPlacementPublisherTest_" + Guid.NewGuid().ToString("N");
            private readonly Subject<IOverlayEntry[]> _entries = new Subject<IOverlayEntry[]>();
            private readonly Subject<(string key, object value)> _changes =
                new Subject<(string key, object value)>();
            private readonly Mock<IAppConfiguration> _configuration = new Mock<IAppConfiguration>();
            private readonly HookMetricsPublisher _publisher;

            public readonly Subject<int> Pids = new Subject<int>();
            public readonly List<int> PolicyChecks = new List<int>();
            public readonly HashSet<int> BlockedPids = new HashSet<int>();

            public PublisherContext(bool hookEnabled, bool overlayActive)
            {
                _configuration.SetupAllProperties();
                _configuration.SetupGet(configuration => configuration.OnValueChanged).Returns(_changes);
                _configuration.Object.EnableHookOverlay = hookEnabled;
                _configuration.Object.IsOverlayActive = overlayActive;
                var overlay = new Mock<IOverlayService>();
                overlay.SetupGet(service => service.OnDictionaryUpdated).Returns(_entries);
                overlay.SetupGet(service => service.CurrentOverlayEntries).Returns(Array.Empty<IOverlayEntry>());
                _publisher = new HookMetricsPublisher(overlay.Object, _configuration.Object, Pids,
                    pixelOrbit: null, CheckPolicy, _metricsMap, _placementMap);
            }

            public void Tick() => _entries.OnNext(Array.Empty<IOverlayEntry>());

            public void SetHookEnabled(bool enabled)
            {
                _configuration.Object.EnableHookOverlay = enabled;
                _changes.OnNext((nameof(IAppConfiguration.EnableHookOverlay), enabled));
            }

            public void SetOverlayActive(bool active)
            {
                _configuration.Object.IsOverlayActive = active;
                _changes.OnNext((nameof(IAppConfiguration.IsOverlayActive), active));
            }

            // The PID the hook would draw for, or null while the publisher holds no channel.
            public int? PublishedTargetPid()
            {
                try
                {
                    using var map = MemoryMappedFile.OpenExisting(_metricsMap, MemoryMappedFileRights.Read);
                    using var view = map.CreateViewAccessor(0, 32, MemoryMappedFileAccess.Read);
                    return view.ReadInt32(OffTargetPid);
                }
                catch (FileNotFoundException)
                {
                    return null;
                }
            }

            private bool CheckPolicy(int pid, out string reason)
            {
                PolicyChecks.Add(pid);
                reason = BlockedPids.Contains(pid) ? "anti-cheat module" : null;
                return reason == null;
            }

            public void Dispose()
            {
                _publisher.Dispose();
                _entries.Dispose();
                _changes.Dispose();
                Pids.Dispose();
            }
        }
    }
}
