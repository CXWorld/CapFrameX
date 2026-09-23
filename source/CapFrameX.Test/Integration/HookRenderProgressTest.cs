using System;
using System.IO.MemoryMappedFiles;
using System.Linq;
using CapFrameX.Contracts.Overlay;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class HookRenderProgressTest
    {
        private static HookCompatibilityProbeSession Session()
        {
            var stage = HookCompatibilityStage.Create(HookCompatibilityStageId.VendorAware);
            var plan = new HookCompatibilityStagePlan(new[] { stage }, 0, "test", true, null);
            var session = new HookCompatibilityProbeSession(42, plan);
            session.OnInjectionSucceeded(1000);
            return session;
        }

        private static NativeHookStatusSnapshot Sample(long lastDraw, ulong draws, uint generation = 1)
            => new NativeHookStatusSnapshot
            {
                Version = 2, Api = NativeHookApi.D3D11, Flags = (NativeHookStatusFlags)127,
                MetricsEntryCount = 53, LastHeartbeatTickMs = lastDraw, AppliedSequence = 1,
                Progress = new HookRenderProgress
                {
                    Generation = generation, Draws = draws, Presents = draws,
                    LastDrawTickMs = lastDraw, AppliedSequence = 1, RouteSource = 1
                }
            };

        [TestMethod]
        public void PaybackFrozenHeartbeatAndRenderedFlags_CannotLearn()
        {
            var session = Session();
            var frozen = Sample(1546, 1);
            for (ulong now = 1796; now <= 4046; now += 250)
                Assert.IsFalse(session.Observe(true, frozen, EHookOverlayStatus.Active, now)
                    .Any(a => a.Kind == HookProbeActionKind.Learn));
            session.Observe(true, frozen, EHookOverlayStatus.Idle, 5046);
            Assert.AreEqual(HookProbeSettlement.None, session.Settlement);
        }

        [TestMethod]
        public void FreshPresentsWithoutNewDraws_CannotLearn()
        {
            var session = Session();
            for (ulong now = 1250; now < 10000; now += 250)
            {
                var sample = Sample((long)now, 1);
                var progress = sample.Progress.Value;
                progress.Presents = now;
                sample.Progress = progress;
                Assert.IsFalse(session.Observe(true, sample, EHookOverlayStatus.Active, now)
                    .Any(a => a.Kind == HookProbeActionKind.Learn));
            }
        }

        [TestMethod]
        public void DrawsStopAfterLearning_RevokesVerificationAndEventuallyFallsBack()
        {
            var session = Session();
            session.Observe(true, Sample(1250, 10), EHookOverlayStatus.Active, 1250);
            session.Observe(true, Sample(3250, 30), EHookOverlayStatus.Active, 3250);
            Assert.AreEqual(HookProbeSettlement.Learned, session.Settlement);

            bool invalidated = false, fallback = false;
            for (ulong now = 3500; now <= 27000; now += 250)
            {
                var sample = Sample(3250, 30);
                sample.LastHeartbeatTickMs = (long)now;
                var progress = sample.Progress.Value;
                progress.Presents = now;
                sample.Progress = progress;
                var actions = session.Observe(true, sample, EHookOverlayStatus.Active, now);
                Assert.IsFalse(actions.Any(a => a.Kind == HookProbeActionKind.Learn));
                invalidated |= actions.Any(a => a.Kind == HookProbeActionKind.InvalidateVerification);
                fallback |= actions.Any(a => a.Kind == HookProbeActionKind.SetFallback && a.Reason != null);
            }
            Assert.IsTrue(invalidated, "A previously working route must lose its verified status.");
            Assert.IsTrue(fallback, "Fresh Presents alone must not prevent stalled-renderer recovery.");
        }

        [TestMethod]
        public void LegacyHook_RemainsObservableButCannotVerify()
        {
            var session = Session();
            var sample = Sample(1250, 1);
            sample.Progress = null;
            session.Observe(true, sample, EHookOverlayStatus.Active, 1250);
            Assert.IsFalse(session.Observe(true, sample, EHookOverlayStatus.Active, 3250)
                .Any(a => a.Kind == HookProbeActionKind.Learn));
            Assert.IsTrue(session.HasRendered);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void PauseOrSwapchainChange_RequiresFreshVerification(bool swapchain)
        {
            var session = Session();
            session.Observe(true, Sample(1250, 10), EHookOverlayStatus.Active, 1250);
            Assert.IsTrue(session.Observe(true, Sample(3250, 30), EHookOverlayStatus.Active, 3250)
                .Any(a => a.Kind == HookProbeActionKind.Learn));
            uint generation = swapchain ? 2u : 1u;
            var changed = session.Observe(true, Sample(3500, 31, generation),
                swapchain ? EHookOverlayStatus.Active : EHookOverlayStatus.Hidden, 3500);
            Assert.IsTrue(changed.Any(a => a.Kind == HookProbeActionKind.InvalidateVerification));
            Assert.IsFalse(changed.Any(a => a.Kind == HookProbeActionKind.GiveUp));
            session.Observe(true, Sample(3750, 32, generation), EHookOverlayStatus.Active, 3750);
            Assert.IsTrue(session.Observe(true, Sample(5750, 50, generation), EHookOverlayStatus.Active, 5750)
                .Any(a => a.Kind == HookProbeActionKind.Learn));
        }

        [TestMethod]
        public void WrongAppliedStageOrPendingRestart_CannotVerify()
        {
            var verifier = new HookRenderProgressVerifier();
            var sample = Sample(1000, 1);
            verifier.Observe(sample, true, 0, 1000);
            sample = Sample(4000, 100);
            var progress = sample.Progress.Value;
            progress.AppliedFlags = 4;
            sample.Progress = progress;
            Assert.IsFalse(verifier.Observe(sample, true, 0, 4000));
            progress.AppliedFlags = 0;
            progress.PendingRestartFlags = 8;
            sample.Progress = progress;
            Assert.IsFalse(verifier.Observe(sample, true, 0, 4000));
        }

        [TestMethod]
        public void CounterResetAboveOriginalBaseline_InvalidatesConfirmation()
        {
            var verifier = new HookRenderProgressVerifier();
            verifier.Observe(Sample(1000, 10), true, 0, 1000);
            Assert.IsTrue(verifier.Observe(Sample(3000, 100), true, 0, 3000));
            Assert.IsFalse(verifier.Observe(Sample(3500, 50), true, 0, 3500));
        }

        [TestMethod]
        public void ProgressWire_RejectsIncompletePublicationAndPreserves64BitCounters()
        {
            using var mapping = MemoryMappedFile.CreateNew("cfx-progress-test-" + Guid.NewGuid(), 64);
            using var view = mapping.CreateViewAccessor();
            view.Write(0, HookRenderProgressProbe.Magic);
            view.Write(4, 1); view.Write(8, 42); view.Write(12, 64);
            view.Write(16, 1); view.Write(20, 7u);
            view.Write(24, (ulong)uint.MaxValue + 100); view.Write(32, (ulong)uint.MaxValue + 10);
            view.Write(40, 12345L); view.Write(48, 1); view.Write(52, 4u); view.Write(56, 3u);
            Assert.IsFalse(HookRenderProgressProbe.TryRead(view, 42, out _));
            view.Write(16, 2);
            Assert.IsTrue(HookRenderProgressProbe.TryRead(view, 42, out var sample));
            Assert.AreEqual((ulong)uint.MaxValue + 10, sample.Draws);
            Assert.AreEqual(7u, sample.Generation);
            Assert.IsFalse(HookRenderProgressProbe.TryRead(view, 43, out _));
        }
    }
}
