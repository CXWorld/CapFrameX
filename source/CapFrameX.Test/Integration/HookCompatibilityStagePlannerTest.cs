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

        [TestMethod]
        [DataRow(true, Hash, false)]
        [DataRow(true, "old", false)]
        [DataRow(false, Hash, true)]
        public void Plan_CatalogDoesNotRestoreALearnedRouteBeforeItsStartingStage(
            bool verified, string learnedHash, bool pending)
        {
            var catalog = new HookCompatibilityProfile("ForzaHorizon6.exe",
                enableXeFgNativePresentQueueRoute: false, enableGenericD3D12PresentRoute: true,
                disableFidelityFxSwapchainLifecycleHooks: true, TimeSpan.Zero,
                earlyInjectionModule: null, source: "test");
            HookLearnedProfileEntry learned = Entry(HookCompatibilityStageId.Generic,
                verified, learnedHash);
            if (pending)
                learned.SetPending(HookCompatibilityStage.Create(HookCompatibilityStageId.Generic),
                    "restart");

            HookCompatibilityStagePlan plan = HookCompatibilityStagePlanner.Plan(
                Evidence(streamline: true, d3d12: true), catalog, learned, Hash, true);

            Assert.AreEqual("catalog", plan.StartStage.Source);
            Assert.AreEqual(catalog.NativeFlags, plan.StartStage.Flags);
            Assert.IsFalse(plan.Ladder.Any(stage =>
                stage.Id == HookCompatibilityStageId.Generic && !stage.RequiresEarlyInjection));
        }

        [TestMethod]
        [DataRow(true, Hash, false)]
        [DataRow(true, "old", false)]
        [DataRow(false, Hash, true)]
        public void Plan_CatalogDelayIsNotReplacedByAnOlderLearnedDelay(
            bool verified, string learnedHash, bool pending)
        {
            var catalog = new HookCompatibilityProfile("game.exe",
                enableXeFgNativePresentQueueRoute: false, enableGenericD3D12PresentRoute: true,
                disableFidelityFxSwapchainLifecycleHooks: false, TimeSpan.FromSeconds(15),
                earlyInjectionModule: null, source: "test");
            HookLearnedProfileEntry learned = Entry(HookCompatibilityStageId.Generic,
                verified, learnedHash);
            if (pending)
                learned.SetPending(HookCompatibilityStage.Create(HookCompatibilityStageId.Generic),
                    "restart");

            HookCompatibilityStagePlan plan = HookCompatibilityStagePlanner.Plan(
                Evidence(streamline: true, d3d12: true), catalog, learned, Hash, true);

            Assert.AreEqual("catalog", plan.StartStage.Source);
            Assert.AreEqual(TimeSpan.FromSeconds(15), plan.StartStage.InjectionDelay);
        }

        [TestMethod]
        public void Plan_CatalogCombinedFlagsAreNotReplacedByALearnedStageWithTheSameId()
        {
            var catalog = new HookCompatibilityProfile("game.exe",
                enableXeFgNativePresentQueueRoute: true, enableGenericD3D12PresentRoute: true,
                disableFidelityFxSwapchainLifecycleHooks: false, TimeSpan.Zero,
                earlyInjectionModule: null, source: "test");
            HookLearnedProfileEntry learned = Entry(HookCompatibilityStageId.Generic, true, Hash);

            HookCompatibilityStagePlan plan = HookCompatibilityStagePlanner.Plan(
                Evidence(streamline: true, xefg: true, d3d12: true), catalog, learned, Hash, true);

            Assert.AreEqual("catalog", plan.StartStage.Source);
            Assert.AreEqual(catalog.NativeFlags, plan.StartStage.Flags);
        }

        [TestMethod]
        [DataRow(true, Hash, false)]
        [DataRow(true, "old", false)]
        [DataRow(false, Hash, true)]
        public void Plan_CatalogKeepsALearnedEscalationFromItsRemainingLadder(
            bool verified, string learnedHash, bool pending)
        {
            var catalog = new HookCompatibilityProfile("game.exe",
                enableXeFgNativePresentQueueRoute: false, enableGenericD3D12PresentRoute: true,
                disableFidelityFxSwapchainLifecycleHooks: true, TimeSpan.Zero,
                earlyInjectionModule: null, source: "test");
            HookCompatibilityStage escalation = HookCompatibilityStage.Create(
                HookCompatibilityStageId.GenericNoFfxLifecycle, "d3d12.dll");
            HookLearnedProfileEntry learned = Entry(pending ? HookCompatibilityStageId.Generic :
                HookCompatibilityStageId.GenericNoFfxLifecycle,
                verified, learnedHash);
            if (pending)
                learned.SetPending(escalation, "restart");
            else
                learned.SetStage(escalation);

            // Early attach: a late one runs the early stage without early injection.
            HookCompatibilityStagePlan plan = HookCompatibilityStagePlanner.Plan(
                Evidence(streamline: true, ffxFg: true, d3d12: true, attach: HookAttachMode.Early),
                catalog, learned, Hash, true);

            Assert.IsTrue(escalation.SameRouting(plan.StartStage));
            Assert.IsTrue(plan.StartIndex > 0);
        }

        [TestMethod]
        public void Plan_LateAttachRunsALearnedEarlyStageWithoutEarlyInjection()
        {
            // Metro 2033 Redux on 2026-10-25, 09:55: the game was already running when CapFrameX
            // started, the learned generic + early injection stage ran late, ended with NoQueue and
            // exhausted the entry although early injection never happened. A late launch now runs
            // the same routing without it, and the early stage stays reachable.
            HookCompatibilityStage genericEarly = HookCompatibilityStage.Create(
                HookCompatibilityStageId.Generic, HookCompatibilityStagePlanner.GateModuleD3D12);
            HookLearnedProfileEntry pending = Entry(HookCompatibilityStageId.Generic,
                verified: false, hash: Hash);
            pending.SetPending(genericEarly, "restart");
            HookLearnedProfileEntry verified = Entry(HookCompatibilityStageId.Generic,
                verified: true, hash: Hash);
            verified.SetStage(genericEarly);

            foreach (HookLearnedProfileEntry entry in new[] { pending, verified })
            {
                HookCompatibilityStagePlan late = HookCompatibilityStagePlanner.Plan(
                    Evidence(ffxFg: true, d3d12: true), null, entry, Hash, true);
                Assert.AreEqual("Generic", late.StartStage.Key);
                Assert.AreEqual("learned", late.StartStage.Source);
                StringAssert.Contains(late.Reason, "without early injection");
                AssertNoQueueLeadsBackTo("Generic+early", late);

                HookCompatibilityStagePlan early = HookCompatibilityStagePlanner.Plan(
                    Evidence(ffxFg: true, d3d12: true, attach: HookAttachMode.Early), null, entry,
                    Hash, true);
                Assert.AreEqual("Generic+early", early.StartStage.Key);
            }
        }

        [TestMethod]
        public void Plan_LateAttachRunsAnEarlyProfileWithoutEarlyInjection()
        {
            // A profile with early injection keeps only early stages on its ladder; a game that
            // already runs gets the profile's other settings and no exhaustion.
            var catalog = new HookCompatibilityProfile("JediSurvivor.exe",
                enableXeFgNativePresentQueueRoute: false, enableGenericD3D12PresentRoute: true,
                disableFidelityFxSwapchainLifecycleHooks: false, TimeSpan.Zero,
                earlyInjectionModule: "d3d12.dll", source: "test");

            HookCompatibilityStagePlan late = HookCompatibilityStagePlanner.Plan(
                Evidence(streamline: true, d3d12: true), catalog, null, Hash, true);

            Assert.IsFalse(late.StartStage.RequiresEarlyInjection);
            Assert.AreEqual(catalog.NativeFlags, late.StartStage.Flags);
            Assert.AreEqual("catalog", late.StartStage.Source);
            AssertNoQueueLeadsBackTo("Generic+early", late);
        }

        [TestMethod]
        public void LearnedStartStage_IsTheStagePlanStartsTheEntryWith()
        {
            // The manager registers early-injection targets from LearnedStartStage; an entry from
            // an older hook build used to drop its target although Plan re-verifies from its stage.
            HookCompatibilityStage genericEarly = HookCompatibilityStage.Create(
                HookCompatibilityStageId.Generic, HookCompatibilityStagePlanner.GateModuleD3D12);
            HookLearnedProfileEntry pending = Entry(HookCompatibilityStageId.Generic, false, Hash);
            pending.SetPending(genericEarly, "restart");
            HookLearnedProfileEntry verified = Entry(HookCompatibilityStageId.Generic, true, Hash);
            verified.SetStage(genericEarly);
            HookLearnedProfileEntry unverified = Entry(HookCompatibilityStageId.Generic, false, Hash);
            HookLearnedProfileEntry olderBuild = Entry(HookCompatibilityStageId.Generic, true, "old");
            olderBuild.SetStage(genericEarly);
            HookLearnedProfileEntry olderPending = Entry(HookCompatibilityStageId.Generic, false, "old");
            olderPending.SetPending(genericEarly, "restart");

            var expected = new (HookLearnedProfileEntry entry, string key)[]
            {
                (pending, "Generic+early"), (verified, "Generic+early"), (unverified, null),
                (olderBuild, "Generic+early"), (olderPending, "Generic")
            };
            foreach (var (entry, key) in expected)
            {
                Assert.AreEqual(key,
                    HookCompatibilityStagePlanner.LearnedStartStage(entry, Hash)?.Key);
                HookCompatibilityStagePlan plan = HookCompatibilityStagePlanner.Plan(
                    Evidence(ffxFg: true, d3d12: true, attach: HookAttachMode.Early), null, entry,
                    Hash, true);
                if (key == null)
                    Assert.AreNotEqual("learned", plan.StartStage.Source);
                else
                    Assert.AreEqual(key, plan.StartStage.Key);
            }
            Assert.IsNull(HookCompatibilityStagePlanner.LearnedStartStage(null, Hash));
        }

        private static void AssertNoQueueLeadsBackTo(string expectedKey,
            HookCompatibilityStagePlan plan)
        {
            var tried = new HashSet<string>(StringComparer.Ordinal) { plan.StartStage.Key };
            HookCompatibilityStage next = HookCompatibilityStagePlanner.ResolveEscalation(
                plan.StartStage, HookCompatibilityVerdict.NoQueue,
                new NativeHookStatusSnapshot { Version = 2, Api = NativeHookApi.D3D12 },
                plan.Evidence, plan.Ladder, tried, out bool exhausted);
            Assert.AreEqual(expectedKey, next?.Key);
            Assert.IsFalse(exhausted);
        }

        [TestMethod]
        public void Plan_MatchingLearnedCatalogStageRemainsReusable()
        {
            var catalog = new HookCompatibilityProfile("game.exe",
                enableXeFgNativePresentQueueRoute: false, enableGenericD3D12PresentRoute: true,
                disableFidelityFxSwapchainLifecycleHooks: true, TimeSpan.Zero,
                earlyInjectionModule: null, source: "test");
            HookLearnedProfileEntry learned = Entry(HookCompatibilityStageId.GenericNoFfxLifecycle,
                true, Hash);

            HookCompatibilityStagePlan plan = HookCompatibilityStagePlanner.Plan(
                Evidence(streamline: true, ffxFg: true, d3d12: true), catalog, learned, Hash, true);

            Assert.AreEqual("learned", plan.StartStage.Source);
            Assert.AreEqual(0, plan.StartIndex);
            Assert.AreEqual(catalog.NativeFlags, plan.StartStage.Flags);
        }

        [TestMethod]
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
        public void Plan_PendingStageIsReusedButExhaustionDoesNotBlockANewLaunch()
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
            Assert.IsFalse(exhaustedPlan.IsEmpty);
            Assert.IsNotNull(exhaustedPlan.StartStage);
            Assert.IsTrue(exhaustedPlan.ProbingEnabled);
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
        public void ResolveEscalation_NoD3D12SwapchainContinuesOnTheRoutesThatDrawOnD3D11()
        {
            // Metro 2033 Redux with FSR: amd_fidelityfx_dx12.dll (FSR-FG) and d3d12.dll are
            // resident, so a late attach starts on the generic route of a 6-stage ladder, but the
            // game presents D3D11.
            HookTargetEvidence evidence = Evidence(ffxFg: true, d3d12: true);
            HookCompatibilityStagePlan plan = HookCompatibilityStagePlanner.Plan(evidence, null,
                null, Hash, true);
            Assert.AreEqual(6, plan.Ladder.Count);
            Assert.AreEqual("Generic", plan.StartStage.Key);
            var snapshot = new NativeHookStatusSnapshot { Version = 2 };
            var tried = new HashSet<string>(StringComparer.Ordinal) { plan.StartStage.Key };

            // Every generic stage is passed over, an early one too: vendor-aware comes first...
            Assert.AreEqual("VendorAware", Next(plan.StartStage, plan.Ladder));
            Assert.AreEqual("VendorAware",
                Next(plan.Ladder.Single(stage => stage.Key == "Generic+early"), plan.Ladder));
            // ...then its early injection, and after that the ladder is exhausted.
            tried.Add("VendorAware");
            Assert.AreEqual("VendorAware+early", Next(plan.StartStage, plan.Ladder));
            tried.Add("VendorAware+early");
            Assert.IsNull(HookCompatibilityStagePlanner.ResolveEscalation(plan.StartStage,
                HookCompatibilityVerdict.NoD3D12Swapchain, snapshot, evidence, plan.Ladder, tried,
                out bool exhausted));
            Assert.IsTrue(exhausted);

            // A profile that names the generic route dropped vendor-aware from its ladder; only
            // the early vendor-aware stage is left to try.
            var catalog = new HookCompatibilityProfile("metro.exe",
                enableXeFgNativePresentQueueRoute: false, enableGenericD3D12PresentRoute: true,
                disableFidelityFxSwapchainLifecycleHooks: false, TimeSpan.Zero,
                earlyInjectionModule: null, source: "test");
            HookCompatibilityStagePlan profiled = HookCompatibilityStagePlanner.Plan(evidence,
                catalog, null, Hash, true);
            tried.Clear();
            tried.Add(profiled.StartStage.Key);
            Assert.AreEqual("VendorAware+early", Next(profiled.StartStage, profiled.Ladder));

            // The restart scheduled for vendor-aware applies on the next launch.
            HookLearnedProfileEntry learned = Entry(HookCompatibilityStageId.Generic,
                verified: false, hash: Hash);
            learned.SetPending(HookCompatibilityStage.Create(HookCompatibilityStageId.VendorAware),
                "restart");
            HookCompatibilityStagePlan next = HookCompatibilityStagePlanner.Plan(evidence, null,
                learned, Hash, true);
            Assert.AreEqual(0, next.StartIndex);
            Assert.AreEqual(HookCompatibilityStageId.VendorAware, next.StartStage.Id);
            StringAssert.Contains(next.Reason, "scheduled stage 1/6 (vendor-aware)");

            string Next(HookCompatibilityStage from, IReadOnlyList<HookCompatibilityStage> ladder)
                => HookCompatibilityStagePlanner.ResolveEscalation(from,
                    HookCompatibilityVerdict.NoD3D12Swapchain, snapshot, evidence, ladder, tried,
                    out _)?.Key;
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
