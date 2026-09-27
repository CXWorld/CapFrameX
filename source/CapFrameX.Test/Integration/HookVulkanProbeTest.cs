using System;
using System.IO.MemoryMappedFiles;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class HookVulkanProbeTest
    {
        private static VulkanProbeSnapshot Sample(ulong tick = 1000, ulong count = 10,
            VulkanProbeResult result = VulkanProbeResult.Composited)
            => new VulkanProbeSnapshot
            {
                Context = 42, Build = 99, Generation = 1, Bitness = 64, Capabilities = 3,
                TickMs = tick, Successes = count, Result = result, AppliedRevision = 7,
                RequestedRoute = VulkanCompositeRoute.Graphics, ActualRoute = VulkanCompositeRoute.Graphics
            };

        private static HookVulkanProbeSession Session()
        {
            var session = new HookVulkanProbeSession(Sample(), null);
            session.Published(7, 1000);
            return session;
        }

        [TestMethod]
        public void FreshAcknowledgedCompositesAreRequiredToLearn()
        {
            var session = Session();
            Assert.AreEqual(VulkanProbeAction.None, session.Observe(Sample(), 1000, true));
            Assert.AreEqual(VulkanProbeAction.None, session.Observe(Sample(2000, 20), 2000, true));
            Assert.AreEqual(VulkanProbeAction.Learn, session.Observe(Sample(3000, 30), 3000, true));
            Assert.IsTrue(session.Learned);
        }

        [TestMethod]
        public void PresentHeartbeatWithoutFreshCompositesDoesNotLearn()
        {
            var session = Session();
            for (ulong tick = 1000; tick < 6000; tick += 1000)
                Assert.AreEqual(VulkanProbeAction.None, session.Observe(Sample(tick, 10), tick, true));
            Assert.IsFalse(session.Learned);
        }

        [TestMethod]
        public void PreviouslyLearnedRouteContinuesToBeMonitoredAndEscalates()
        {
            var session = Session();
            session.Observe(Sample(), 1000, true);
            session.Observe(Sample(3000, 30), 3000, true);
            session.Observe(Sample(4000, 30, VulkanProbeResult.Failed), 4000, true);
            var action = session.Observe(Sample(9000, 30, VulkanProbeResult.Failed), 9000, true);
            Assert.IsTrue((action & VulkanProbeAction.Publish) != 0);
            Assert.IsTrue((action & VulkanProbeAction.Invalidate) != 0);
            Assert.AreEqual(VulkanCompositeRoute.Compute, session.Route);
            Assert.IsFalse(session.Learned);
        }

        [TestMethod]
        public void OldStageAcknowledgementCannotVerifyNewRoute()
        {
            var session = Session();
            var sample = Sample(8000, 999);
            sample.AppliedRevision = 6;
            session.Observe(sample, 8000, true);
            Assert.IsFalse(session.Learned);
            Assert.IsTrue(session.AcknowledgementFailed);
        }

        [TestMethod]
        public void HiddenAndStaleSamplesNeitherLearnNorExhaust()
        {
            var session = Session();
            session.Observe(Sample(), 50000, true);
            session.Observe(Sample(60000), 60000, false);
            Assert.IsFalse(session.Learned);
            Assert.IsFalse(session.Exhausted);
            Assert.IsFalse(session.AcknowledgementFailed);
        }

        [TestMethod]
        public void ComputeOnlyOrHdrCapabilitiesNeverTryGraphics()
        {
            var sample = Sample(); sample.Capabilities = 2;
            var session = new HookVulkanProbeSession(sample, null);
            Assert.AreEqual(VulkanCompositeRoute.Compute, session.Route);
            Assert.AreEqual(1, session.StageCount);
        }

        [TestMethod]
        public void ChangedSwapchainAndDeviceEvidenceRequireFreshVerification()
        {
            var session = Session(); var sample = Sample();
            sample.Generation++; Assert.IsFalse(session.Matches(sample));
            sample = Sample(); sample.Build++; Assert.IsFalse(session.Matches(sample));
            sample = Sample(); sample.Driver++; Assert.IsFalse(session.Matches(sample));
            sample = Sample(); sample.Capabilities = 2; Assert.IsFalse(session.Matches(sample));
        }

        [TestMethod]
        public void VerifiedComputeProfileIsReusedButStillMustBeVerified()
        {
            var entry = new HookLearnedProfileEntry
            { StageId = HookCompatibilityStageId.VulkanCompute, Verified = true, HookBuildHash = "old" };
            var session = new HookVulkanProbeSession(Sample(), entry);
            Assert.AreEqual(VulkanCompositeRoute.Compute, session.Route);
            Assert.IsFalse(session.Learned);
        }

        [TestMethod]
        public void AllNativeRoutesFailedSuspendsWithoutRequestingDxgi()
        {
            var session = Session();
            session.Observe(Sample(1000, 0, VulkanProbeResult.Failed), 1000, true);
            session.Observe(Sample(6000, 0, VulkanProbeResult.Failed), 6000, true);
            session.Published(8, 6000);
            var sample = Sample(7000, 0, VulkanProbeResult.Failed);
            sample.RequestedRoute = sample.ActualRoute = VulkanCompositeRoute.Compute;
            sample.AppliedRevision = 8;
            session.Observe(sample, 7000, true);
            sample.TickMs = 12000;
            Assert.IsTrue((session.Observe(sample, 12000, true) & VulkanProbeAction.Exhausted) != 0);
            Assert.AreEqual(VulkanCompositeRoute.Suspended, session.Route);
            Assert.AreEqual(NativeHookCompatibilityFlags.None, session.Stage.Flags);
        }

        [TestMethod]
        public void ChannelRejectsTornOrWrongProcessSnapshotsAndKeepsHeartbeatRevisionStable()
        {
            int pid = Environment.ProcessId;
            using var mapping = MemoryMappedFile.CreateNew(HookVulkanCompatibilityChannel.MappingName(pid), 192);
            using var view = mapping.CreateViewAccessor();
            view.Write(0, HookVulkanCompatibilityChannel.Magic);
            view.Write(4, 1u); view.Write(8, (uint)pid); view.Write(12, 192u);
            Assert.IsTrue(HookVulkanCompatibilityChannel.TryOpen(pid, out var channel));
            using (channel)
            {
                uint revision = channel.Publish(VulkanCompositeRoute.Compute, 42, true, 1000, true);
                Assert.AreEqual(revision, channel.Publish(VulkanCompositeRoute.Compute, 42, true, 2000, false));
                Assert.AreEqual(42u, view.ReadUInt32(32));
                Assert.AreEqual(2u, view.ReadUInt32(24));
                view.Write(64, 1u); view.Write(68, 2u);
                Assert.IsFalse(channel.TryRead(out _));
                view.Write(68, 1u); view.Write(92, 64u);
                view.Write(96, 42u); view.Write(104, 99u); view.Write(112, 1u);
                Assert.IsTrue(channel.TryRead(out _));
                view.Write(8, (uint)pid + 1);
                Assert.IsFalse(channel.TryRead(out _));
            }
        }
    }
}
