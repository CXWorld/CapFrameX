using System;
using System.Collections.Generic;
using System.Linq;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class HookCompatibilityStagePlannerTest
    {
        private const string Hash = "abcd1234";

        [TestMethod]
        public void Plan_ResonanceStartsWithoutTheFidelityFxLifecycleHooks()
        {
            HookTargetEvidence evidence = Evidence(streamline: true, dlssg: true, xefg: true,
                ffxFg: true, loaderCopies: 2, dxgiProxy: true, d3d12: true);

            HookCompatibilityStagePlan plan = HookCompatibilityStagePlanner.Plan(evidence,
                catalog: null, learned: null, Hash, autoCompatibility: true);

            Assert.IsTrue(plan.ProbingEnabled);
            Assert.AreEqual(HookCompatibilityStageId.GenericNoFfxLifecycle, plan.StartStage.Id);
            Assert.IsFalse(plan.StartStage.RequiresEarlyInjection);
            CollectionAssert.AreEqual(
                new[] { "VendorAware", "Generic", "GenericNoFfxLifecycle", "VendorAware+early",
                        "Generic+early", "GenericNoFfxLifecycle+early" },
                plan.Ladder.Select(s => s.Key).ToArray());
        }

        [TestMethod]
        public void Plan_StreamlineLateAttachStartsGeneric_EarlyAttachStartsVendorAware()
        {
            HookTargetEvidence late = Evidence(streamline: true, d3d12: true);
            HookTargetEvidence early = Evidence(streamline: true, d3d12: true,
                attach: HookAttachMode.Early);

            Assert.AreEqual(HookCompatibilityStageId.Generic,
                HookCompatibilityStagePlanner.Plan(late, null, null, Hash, true).StartStage.Id);
            Assert.AreEqual(HookCompatibilityStageId.VendorAware,
                HookCompatibilityStagePlanner.Plan(early, null, null, Hash, true).StartStage.Id);
        }

        [TestMethod]
        public void Plan_XeFgEarlyAttachOffersTheNativeQueueRouteFirst()
        {
            HookTargetEvidence early = Evidence(xefg: true, d3d12: true,
                attach: HookAttachMode.Early);

            HookCompatibilityStagePlan plan = HookCompatibilityStagePlanner.Plan(early, null,
                null, Hash, true);

            Assert.AreEqual(HookCompatibilityStageId.VendorAwareXeFgQueue, plan.StartStage.Id);
            Assert.AreEqual("VendorAware", plan.Ladder[0].Key);
        }

        [TestMethod]
        public void Plan_WithoutEvidenceStartsVendorAware()
        {
            HookCompatibilityStagePlan plan = HookCompatibilityStagePlanner.Plan(
                Evidence(d3d12: true), null, null, Hash, true);

            Assert.AreEqual(0, plan.StartIndex);
            Assert.AreEqual(HookCompatibilityStageId.VendorAware, plan.StartStage.Id);
            // No FG runtime, no early-injection stages: nothing to gate on.
            Assert.IsFalse(plan.Ladder.Any(s => s.RequiresEarlyInjection));
        }

        [TestMethod]
        public void Plan_D3D11TitlesNeverGetTheGenericRoute()
        {
            // The generic route suppresses the D3D11 draw path in the hook.
            HookCompatibilityStagePlan byRuntime = HookCompatibilityStagePlanner.Plan(
                Evidence(streamline: true, d3d12: true, runtime: "D3D11"), null, null, Hash, true);
            HookCompatibilityStagePlan byModules = HookCompatibilityStagePlanner.Plan(
                Evidence(streamline: true, d3d12: false), null, null, Hash, true);

            Assert.IsFalse(byRuntime.Ladder.Any(s => s.IsGeneric));
            Assert.IsFalse(byModules.Ladder.Any(s => s.IsGeneric));
            Assert.AreEqual(HookCompatibilityStageId.VendorAware, byRuntime.StartStage.Id);
        }

        [TestMethod]
        public void Plan_CatalogEntryIsStageOneAndTheLadderContinuesAfterIt()
        {
            HookCompatibilityProfile catalog = new HookCompatibilityProfile("witcher3.exe",
                enableXeFgNativePresentQueueRoute: false, enableGenericD3D12PresentRoute: true,
                disableFidelityFxSwapchainLifecycleHooks: false, TimeSpan.Zero,
                earlyInjectionModule: null, source: "test");

            HookCompatibilityStagePlan plan = HookCompatibilityStagePlanner.Plan(
                Evidence(streamline: true, ffxFg: true, d3d12: true), catalog, null, Hash, true);

            Assert.AreEqual(0, plan.StartIndex);
            Assert.AreEqual("catalog", plan.StartStage.Source);
            Assert.AreEqual(HookCompatibilityStageId.Generic, plan.StartStage.Id);
            CollectionAssert.AreEqual(
                new[] { "Generic", "GenericNoFfxLifecycle", "VendorAware+early", "Generic+early",
                        "GenericNoFfxLifecycle+early" },
                plan.Ladder.Select(s => s.Key).ToArray());
        }

        [TestMethod]
        public void Plan_VerifiedLearnedEntryKeepsTheRemainingLadder()
        {
            HookLearnedProfileEntry learned = Entry(HookCompatibilityStageId.GenericNoFfxLifecycle,
                verified: true, hash: Hash);

            HookCompatibilityStagePlan plan = HookCompatibilityStagePlanner.Plan(
                Evidence(streamline: true, ffxFg: true, d3d12: true), null, learned, Hash, true);

            Assert.AreEqual(6, plan.Ladder.Count);
            Assert.AreEqual(2, plan.StartIndex);
            Assert.AreEqual("learned", plan.StartStage.Source);
            Assert.AreEqual(HookCompatibilityStageId.GenericNoFfxLifecycle, plan.StartStage.Id);
            Assert.IsTrue(plan.ProbingEnabled);
        }

        [DataTestMethod]
        [DataRow("D3D11", true)]
        [DataRow("DXGI", false)]
        public void Plan_D3D11RejectsLearnedPendingStaleAndExhaustedGenericRoutes(
            string runtime, bool d3d12Loaded)
        {
            HookTargetEvidence evidence = Evidence(streamline: true, d3d12: d3d12Loaded,
                runtime: runtime);
            Assert.AreEqual(Evidence(streamline: true, runtime: "D3D12").Signature,
                evidence.Signature, "the graphics API can change without changing the store key");
            HookLearnedProfileEntry verified = Entry(HookCompatibilityStageId.Generic, true, Hash);
            HookLearnedProfileEntry stale = Entry(HookCompatibilityStageId.Generic, true, "old");
            HookLearnedProfileEntry exhausted = Entry(HookCompatibilityStageId.Generic, false, Hash);
            exhausted.Exhausted = true;
            HookLearnedProfileEntry pending = Entry(HookCompatibilityStageId.VendorAware, false, Hash);
            pending.SetPending(HookCompatibilityStage.Create(HookCompatibilityStageId.Generic), "restart");

            foreach (HookLearnedProfileEntry entry in new[] { verified, stale, exhausted, pending })
            {
                HookCompatibilityStagePlan plan = HookCompatibilityStagePlanner.Plan(evidence,
                    null, entry, Hash, true);
                Assert.IsFalse(plan.IsEmpty);
                Assert.AreEqual(HookCompatibilityStageId.VendorAware, plan.StartStage.Id);
                Assert.IsFalse(plan.Ladder.Any(stage => stage.IsGeneric));
            }
        }

        [TestMethod]
        public void Plan_LearnedModifierIsInsertedBeforeItsRemainingEscalations()
        {
            HookLearnedProfileEntry learned = Entry(HookCompatibilityStageId.Generic, true, Hash);
            learned.SetStage(HookCompatibilityStage.Create(HookCompatibilityStageId.Generic,
                injectionDelay: TimeSpan.FromSeconds(2)));
            HookCompatibilityStagePlan plan = HookCompatibilityStagePlanner.Plan(
                Evidence(ffxFg: true), null, learned, Hash, true);

            Assert.AreEqual(TimeSpan.FromSeconds(2), plan.StartStage.InjectionDelay);
            Assert.IsTrue(plan.Ladder.Skip(plan.StartIndex + 1).Any(stage =>
                stage.Id == HookCompatibilityStageId.GenericNoFfxLifecycle));
        }

        [TestMethod]
        public void Plan_LearnedEntryFromAnotherHookBuildIsReVerifiedFromItsStage()
        {
            HookLearnedProfileEntry learned = Entry(HookCompatibilityStageId.Generic,
                verified: true, hash: "ffffffff");

            HookCompatibilityStagePlan plan = HookCompatibilityStagePlanner.Plan(
                Evidence(streamline: true, ffxFg: true, d3d12: true), null, learned, Hash, true);

            Assert.IsTrue(plan.Ladder.Count > 1);
            Assert.AreEqual(HookCompatibilityStageId.Generic, plan.StartStage.Id);
            StringAssert.Contains(plan.Reason, "predates this hook build");
        }

        [TestMethod]
        public void Plan_PendingStageStartsThereAndExhaustedStopsInjecting()
        {
            HookLearnedProfileEntry pending = Entry(HookCompatibilityStageId.Generic,
                verified: false, hash: Hash);
            pending.SetPending(HookCompatibilityStage.Create(
                HookCompatibilityStageId.GenericNoFfxLifecycle), "restart");
            HookLearnedProfileEntry exhausted = Entry(HookCompatibilityStageId.Generic,
                verified: false, hash: Hash);
            exhausted.Exhausted = true;

            HookCompatibilityStagePlan pendingPlan = HookCompatibilityStagePlanner.Plan(
                Evidence(streamline: true, ffxFg: true, d3d12: true), null, pending, Hash, true);
            HookCompatibilityStagePlan exhaustedPlan = HookCompatibilityStagePlanner.Plan(
                Evidence(streamline: true, d3d12: true), null, exhausted, Hash, true);

            Assert.AreEqual(HookCompatibilityStageId.GenericNoFfxLifecycle,
                pendingPlan.StartStage.Id);
            Assert.IsTrue(exhaustedPlan.IsEmpty);
            Assert.IsNull(exhaustedPlan.StartStage);
            StringAssert.Contains(exhaustedPlan.Reason, "reset the learned profiles");
        }

        [TestMethod]
        public void Plan_AutoCompatibilityOffReproducesTheCatalogOnlyBehaviour()
        {
            HookCompatibilityStagePlan plan = HookCompatibilityStagePlanner.Plan(
                Evidence(streamline: true, loaderCopies: 2, dxgiProxy: true, d3d12: true),
                null, Entry(HookCompatibilityStageId.Generic, true, Hash), Hash,
                autoCompatibility: false);

            Assert.IsFalse(plan.ProbingEnabled);
            Assert.AreEqual(1, plan.Ladder.Count);
            Assert.AreEqual(HookCompatibilityStageId.VendorAware, plan.StartStage.Id);
        }

        [TestMethod]
        public void CanEscalateLive_RequiresAHookThatAdvertisesTheChangedBits()
        {
            HookCompatibilityStage vendor = HookCompatibilityStage.Create(HookCompatibilityStageId.VendorAware);
            HookCompatibilityStage generic = HookCompatibilityStage.Create(HookCompatibilityStageId.Generic);
            HookCompatibilityStage noFfx = HookCompatibilityStage.Create(HookCompatibilityStageId.GenericNoFfxLifecycle);
            var v1 = new NativeHookStatusSnapshot { Version = 1 };
            var noCaps = new NativeHookStatusSnapshot { Version = 2, LiveReloadCapabilities = 0 };
            var caps = new NativeHookStatusSnapshot { Version = 2, LiveReloadCapabilities = 0x6 };
            var ffxCaps = new NativeHookStatusSnapshot { Version = 2, LiveReloadCapabilities = 0xE };

            Assert.IsFalse(HookCompatibilityStagePlanner.CanEscalateLive(vendor, generic, v1));
            Assert.IsFalse(HookCompatibilityStagePlanner.CanEscalateLive(vendor, generic, noCaps));
            Assert.IsTrue(HookCompatibilityStagePlanner.CanEscalateLive(vendor, generic, caps));
            // A hook that does not advertise the FidelityFX lifecycle bit needs a fresh process
            // for it; one that does takes the step a mid-session FG switch depends on.
            Assert.IsFalse(HookCompatibilityStagePlanner.CanEscalateLive(generic, noFfx, caps));
            Assert.IsTrue(HookCompatibilityStagePlanner.CanEscalateLive(generic, noFfx, ffxCaps));
            Assert.IsTrue(HookCompatibilityStagePlanner.CanEscalateLive(vendor, noFfx, ffxCaps));
            // Neither routing bit can be taken back: those hooks are only ever neutralized.
            Assert.IsFalse(HookCompatibilityStagePlanner.CanEscalateLive(noFfx, generic, ffxCaps));
            Assert.IsFalse(HookCompatibilityStagePlanner.CanEscalateLive(generic, vendor, caps));
            Assert.IsFalse(HookCompatibilityStagePlanner.CanEscalateLive(generic, vendor, ffxCaps));
            Assert.IsFalse(HookCompatibilityStagePlanner.CanEscalateLive(vendor,
                generic.WithEarlyInjection("d3d12.dll"), caps));
            Assert.IsFalse(HookCompatibilityStagePlanner.CanEscalateLive(generic,
                noFfx.WithEarlyInjection("d3d12.dll"), ffxCaps));
        }

        [TestMethod]
        public void Replan_KeepsTheRunningStageAndNeverRegressesPastIt()
        {
            // What a mid-session switch from DLSS to FSR-FG looks like: the title was planned
            // without FidelityFX evidence and verified the generic route, and now the FidelityFX
            // modules are resident too.
            HookCompatibilityStage generic =
                HookCompatibilityStage.Create(HookCompatibilityStageId.Generic);
            HookTargetEvidence withFfx = Evidence(streamline: true, ffxFg: true, d3d12: true,
                attach: HookAttachMode.Late);

            HookCompatibilityStagePlan plan = HookCompatibilityStagePlanner.Replan(withFfx, null,
                null, "hash", autoCompatibility: true, applied: generic);

            Assert.IsTrue(HookCompatibilityStagePlanner.TryIndexOf(plan.Ladder,
                HookCompatibilityStageId.GenericNoFfxLifecycle, false, out int noFfxIndex));
            Assert.AreEqual(HookCompatibilityStageId.Generic, plan.StartStage.Id);
            Assert.IsTrue(noFfxIndex > plan.StartIndex,
                "the FidelityFX-free stage must sit after the stage in effect");

            // The other direction: the applied stage is no longer warranted by the evidence, but
            // it is what the hook runs, so it must stay the starting point.
            HookCompatibilityStage noFfx =
                HookCompatibilityStage.Create(HookCompatibilityStageId.GenericNoFfxLifecycle);
            HookTargetEvidence withoutFfx = Evidence(streamline: true, d3d12: true,
                attach: HookAttachMode.Late);

            HookCompatibilityStagePlan back = HookCompatibilityStagePlanner.Replan(withoutFfx,
                null, null, "hash", autoCompatibility: true, applied: noFfx);

            Assert.IsTrue(back.StartStage.SameRouting(noFfx),
                "re-planning must not regress below the stage the hook is running");
        }

        [TestMethod]
        public void Replan_StartsAtTheAppliedStageEvenWhenNewEvidencePrefersGeneric()
        {
            HookTargetEvidence evidence = Evidence(streamline: true, ffxFg: true);
            HookCompatibilityStage applied = HookCompatibilityStage.Create(HookCompatibilityStageId.VendorAware);
            Assert.AreEqual(HookCompatibilityStageId.Generic,
                HookCompatibilityStagePlanner.Plan(evidence, null, null, Hash, true).StartStage.Id);

            HookCompatibilityStagePlan plan = HookCompatibilityStagePlanner.Replan(evidence,
                null, Entry(HookCompatibilityStageId.Generic, true, Hash), Hash, true, applied);

            Assert.IsTrue(plan.StartStage.SameRouting(applied));
            Assert.IsTrue(plan.Ladder.Skip(plan.StartIndex + 1).Any(stage => stage.IsGeneric));
        }

        [TestMethod]
        public void ResolveEscalation_FollowsTheMatrix()
        {
            HookTargetEvidence evidence = Evidence(streamline: true, ffxFg: true, d3d12: true);
            IReadOnlyList<HookCompatibilityStage> ladder =
                HookCompatibilityStagePlanner.BuildLadder(evidence, null);
            HookCompatibilityStage vendor = ladder[0];
            HookCompatibilityStage generic = ladder[1];
            HookCompatibilityStage noFfx = ladder[2];
            var snapshot = new NativeHookStatusSnapshot { Version = 2 };
            var tried = new HashSet<string>(StringComparer.Ordinal);

            Assert.AreEqual("Generic", Next(vendor, HookCompatibilityVerdict.ForeignPresenter));
            Assert.AreEqual("GenericNoFfxLifecycle", Next(generic, HookCompatibilityVerdict.ForeignPresenter));
            Assert.AreEqual("GenericNoFfxLifecycle+early", Next(noFfx, HookCompatibilityVerdict.RendererStalled));
            Assert.AreEqual("VendorAware+early", Next(vendor, HookCompatibilityVerdict.EarlyInjectionRequired));
            Assert.AreEqual("Generic+early", Next(generic, HookCompatibilityVerdict.NoQueue));
            Assert.AreEqual("VendorAware+early", Next(vendor, HookCompatibilityVerdict.NoPresent));
            snapshot.InstallPhase = NativeHookInstallPhase.FidelityFxExports;
            Assert.AreEqual("GenericNoFfxLifecycle", Next(vendor, HookCompatibilityVerdict.InstallHung));
            Assert.AreEqual("GenericNoFfxLifecycle", Next(generic, HookCompatibilityVerdict.InstallHung));
            snapshot.InstallPhase = NativeHookInstallPhase.StreamlineProxy;
            Assert.AreEqual("Generic", Next(vendor, HookCompatibilityVerdict.InstallHung));

            // Not routing problems: no next stage, and the entry is not marked exhausted.
            Assert.IsNull(HookCompatibilityStagePlanner.ResolveEscalation(vendor,
                HookCompatibilityVerdict.OsdCreateFailed, snapshot, evidence, ladder, tried,
                out bool exhausted));
            Assert.IsFalse(exhausted);
            // An early stage that still fails has nowhere to go.
            Assert.IsNull(HookCompatibilityStagePlanner.ResolveEscalation(ladder[5],
                HookCompatibilityVerdict.ForeignPresenter, snapshot, evidence, ladder, tried,
                out exhausted));
            Assert.IsTrue(exhausted);

            string Next(HookCompatibilityStage from, HookCompatibilityVerdict verdict)
                => HookCompatibilityStagePlanner.ResolveEscalation(from, verdict, snapshot,
                    evidence, ladder, tried, out _)?.Key;
        }

        [TestMethod]
        public void ResolveEscalation_NeverRevisitsATriedStage()
        {
            HookTargetEvidence evidence = Evidence(streamline: true, d3d12: true);
            IReadOnlyList<HookCompatibilityStage> ladder =
                HookCompatibilityStagePlanner.BuildLadder(evidence, null);
            var tried = new HashSet<string>(StringComparer.Ordinal) { "Generic", "Generic+early" };

            HookCompatibilityStage next = HookCompatibilityStagePlanner.ResolveEscalation(
                ladder[0], HookCompatibilityVerdict.ForeignPresenter,
                new NativeHookStatusSnapshot { Version = 2 }, evidence, ladder, tried,
                out bool exhausted);

            Assert.IsNull(next);
            Assert.IsTrue(exhausted);
        }

        internal static HookTargetEvidence Evidence(bool streamline = false, bool dlssg = false,
            bool xefg = false, bool ffxFg = false, int loaderCopies = 0, bool dxgiProxy = false,
            bool d3d12 = true, string runtime = "DXGI",
            HookAttachMode attach = HookAttachMode.Late)
            => new HookTargetEvidence(HookEvidenceScanState.Complete, streamline, dlssg, false,
                xefg, ffxFg, loaderCopies, dxgiProxy, d3d12, d3d11Loaded: true, runtime, attach,
                null, dxgiProxy ? @"E:\Games\x\dxgi.dll" : null);

        internal static HookLearnedProfileEntry Entry(HookCompatibilityStageId stage,
            bool verified, string hash)
        {
            var entry = new HookLearnedProfileEntry
            {
                ExecutableName = "game",
                EvidenceSignature = "sl",
                Verified = verified,
                HookBuildHash = hash,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            };
            entry.SetStage(HookCompatibilityStage.Create(stage));
            return entry;
        }
    }
}
