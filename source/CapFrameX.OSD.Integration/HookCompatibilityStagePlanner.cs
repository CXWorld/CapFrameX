using System;
using System.Collections.Generic;

namespace CapFrameX.OSD.Integration
{
    /// <summary>
    /// The routing vocabulary the hook understands, in escalation order. Every stage may carry
    /// the early-injection modifier (a gate module) and an injection delay on top.
    /// </summary>
    internal enum HookCompatibilityStageId
    {
        /// <summary>Flags None: vendor proxies are hooked and own the draw.</summary>
        VendorAware = 0,
        /// <summary>Vendor-aware plus the XeSS-FG native present queue route.</summary>
        VendorAwareXeFgQueue = 1,
        /// <summary>RTSS model: draw on native DXGI Presents with observed DIRECT queues.</summary>
        Generic = 2,
        /// <summary>Generic, and the FidelityFX creation/destruction hooks are never installed.</summary>
        GenericNoFfxLifecycle = 3
    }

    internal sealed class HookCompatibilityStage
    {
        internal HookCompatibilityStage(HookCompatibilityStageId id,
            NativeHookCompatibilityFlags flags, string earlyInjectionModule,
            TimeSpan injectionDelay, string source)
        {
            Id = id;
            Flags = flags;
            EarlyInjectionModule = string.IsNullOrWhiteSpace(earlyInjectionModule)
                ? null
                : earlyInjectionModule;
            InjectionDelay = injectionDelay < TimeSpan.Zero ? TimeSpan.Zero : injectionDelay;
            Source = source ?? "evidence";
        }

        internal HookCompatibilityStageId Id { get; }
        internal NativeHookCompatibilityFlags Flags { get; }
        internal string EarlyInjectionModule { get; }
        internal TimeSpan InjectionDelay { get; }
        /// <summary>"catalog", "learned" or "evidence" — where this stage came from.</summary>
        internal string Source { get; }
        internal bool RequiresEarlyInjection => EarlyInjectionModule != null;

        internal bool IsGeneric =>
            (Flags & NativeHookCompatibilityFlags.EnableGenericD3D12PresentRoute) != 0;

        /// <summary>Identity for the "already tried" set and for the learned store.</summary>
        internal string Key =>
            Id + (RequiresEarlyInjection ? "+early" : string.Empty) +
            (InjectionDelay > TimeSpan.Zero ? "+delay" : string.Empty);

        internal string DisplayName
        {
            get
            {
                string name;
                switch (Id)
                {
                    case HookCompatibilityStageId.VendorAware: name = "vendor-aware"; break;
                    case HookCompatibilityStageId.VendorAwareXeFgQueue:
                        name = "vendor-aware + XeSS-FG native queue"; break;
                    case HookCompatibilityStageId.Generic: name = "generic D3D12"; break;
                    case HookCompatibilityStageId.GenericNoFfxLifecycle:
                        name = "generic D3D12 + no FidelityFX lifecycle hooks"; break;
                    default: name = Id.ToString(); break;
                }
                if (RequiresEarlyInjection)
                    name += $" + early injection ({EarlyInjectionModule})";
                if (InjectionDelay > TimeSpan.Zero)
                    name += $" + {InjectionDelay.TotalSeconds:0.#} s delay";
                return name;
            }
        }

        internal static NativeHookCompatibilityFlags FlagsFor(HookCompatibilityStageId id)
        {
            switch (id)
            {
                case HookCompatibilityStageId.VendorAwareXeFgQueue:
                    return NativeHookCompatibilityFlags.EnableXeFgNativePresentQueueRoute;
                case HookCompatibilityStageId.Generic:
                    return NativeHookCompatibilityFlags.EnableGenericD3D12PresentRoute;
                case HookCompatibilityStageId.GenericNoFfxLifecycle:
                    return NativeHookCompatibilityFlags.EnableGenericD3D12PresentRoute |
                           NativeHookCompatibilityFlags.DisableFidelityFxSwapchainLifecycleHooks;
                default:
                    return NativeHookCompatibilityFlags.None;
            }
        }

        /// <summary>The stage id a raw flag word maps to (the most specific one it contains).</summary>
        internal static HookCompatibilityStageId IdFor(NativeHookCompatibilityFlags flags)
        {
            if ((flags & NativeHookCompatibilityFlags.DisableFidelityFxSwapchainLifecycleHooks) != 0)
                return HookCompatibilityStageId.GenericNoFfxLifecycle;
            if ((flags & NativeHookCompatibilityFlags.EnableGenericD3D12PresentRoute) != 0)
                return HookCompatibilityStageId.Generic;
            if ((flags & NativeHookCompatibilityFlags.EnableXeFgNativePresentQueueRoute) != 0)
                return HookCompatibilityStageId.VendorAwareXeFgQueue;
            return HookCompatibilityStageId.VendorAware;
        }

        internal static HookCompatibilityStage Create(HookCompatibilityStageId id,
            string earlyInjectionModule = null, TimeSpan injectionDelay = default,
            string source = "evidence")
            => new HookCompatibilityStage(id, FlagsFor(id), earlyInjectionModule,
                injectionDelay, source);

        internal static HookCompatibilityStage FromCatalog(HookCompatibilityProfile profile)
        {
            if (profile == null) return null;
            // A catalog entry may combine bits the ladder keeps apart (XeFg route + generic);
            // publish exactly what it says and classify it by its most specific bit.
            return new HookCompatibilityStage(IdFor(profile.NativeFlags), profile.NativeFlags,
                profile.EarlyInjectionModule, profile.InjectionDelay, "catalog");
        }

        internal HookCompatibilityStage WithEarlyInjection(string gateModule)
            => new HookCompatibilityStage(Id, Flags, gateModule, InjectionDelay, Source);

        internal bool SameRouting(HookCompatibilityStage other)
            => other != null && Id == other.Id && Flags == other.Flags &&
               string.Equals(EarlyInjectionModule, other.EarlyInjectionModule,
                   StringComparison.OrdinalIgnoreCase) &&
               InjectionDelay == other.InjectionDelay;
    }

    internal sealed class HookCompatibilityStagePlan
    {
        internal HookCompatibilityStagePlan(IReadOnlyList<HookCompatibilityStage> ladder,
            int startIndex, string reason, bool probingEnabled, HookTargetEvidence evidence)
        {
            Ladder = ladder ?? Array.Empty<HookCompatibilityStage>();
            StartIndex = Ladder.Count == 0 ? 0 : Math.Max(0, Math.Min(startIndex, Ladder.Count - 1));
            Reason = reason;
            ProbingEnabled = probingEnabled;
            Evidence = evidence;
        }

        internal IReadOnlyList<HookCompatibilityStage> Ladder { get; }
        internal int StartIndex { get; }
        internal string Reason { get; }
        /// <summary>False: run the start stage only, never escalate or learn (legacy behaviour).</summary>
        internal bool ProbingEnabled { get; }
        internal HookTargetEvidence Evidence { get; }
        internal bool IsEmpty => Ladder.Count == 0;
        internal HookCompatibilityStage StartStage => IsEmpty ? null : Ladder[StartIndex];
    }

    /// <summary>
    /// Pure: evidence + catalog entry + learned entry → ordered ladder and the stage to start
    /// with. Nothing here touches a process, a file or a clock.
    /// </summary>
    internal static class HookCompatibilityStagePlanner
    {
        internal const string GateModuleD3D12 = "d3d12.dll";

        internal static HookCompatibilityStagePlan Plan(HookTargetEvidence evidence,
            HookCompatibilityProfile catalog, HookLearnedProfileEntry learned,
            string hookBuildHash, bool autoCompatibility)
        {
            evidence = evidence ?? HookTargetEvidence.Unknown(null, HookAttachMode.Late);
            HookCompatibilityStage catalogStage = HookCompatibilityStage.FromCatalog(catalog);

            if (!autoCompatibility)
            {
                HookCompatibilityStage legacy = catalogStage ??
                    HookCompatibilityStage.Create(HookCompatibilityStageId.VendorAware);
                return new HookCompatibilityStagePlan(new[] { legacy }, 0,
                    "automatic compatibility probing is off", probingEnabled: false, evidence);
            }

            bool learnedCurrent = learned != null && learned.MatchesHookBuild(hookBuildHash);
            if (learnedCurrent && learned.Exhausted)
            {
                return new HookCompatibilityStagePlan(Array.Empty<HookCompatibilityStage>(), 0,
                    "every compatibility stage failed for this title on this hook build; " +
                    "reset the learned profiles to probe again", probingEnabled: true, evidence);
            }
            if (learnedCurrent && learned.Verified && learned.PendingStageId == null)
            {
                HookCompatibilityStage verified = learned.ToStage();
                if (verified != null)
                {
                    return new HookCompatibilityStagePlan(new[] { verified }, 0,
                        $"learned profile ({learned.EvidenceSignature})", probingEnabled: true,
                        evidence);
                }
            }

            List<HookCompatibilityStage> ladder = BuildLadder(evidence, catalogStage);
            int startIndex = 0;
            string reason;

            HookCompatibilityStage pending = learnedCurrent ? learned.PendingStage() : null;
            HookCompatibilityStage learnedStage = learned?.ToStage();
            if (pending != null)
            {
                startIndex = IndexOfOrAppend(ladder, pending);
                reason = $"learned profile scheduled stage {startIndex + 1}/{ladder.Count} " +
                    $"({pending.DisplayName}) for this launch";
            }
            else if (learnedStage != null && !learnedCurrent)
            {
                // The hook changed underneath a learned entry: re-verify from the stage that
                // used to work rather than from the very beginning.
                startIndex = IndexOfOrAppend(ladder, learnedStage);
                reason = $"learned profile predates this hook build; re-verifying from " +
                    $"stage {startIndex + 1}/{ladder.Count} ({learnedStage.DisplayName})";
            }
            else if (catalogStage != null)
            {
                reason = $"catalog profile {catalogStage.DisplayName} is stage 1/{ladder.Count}";
            }
            else if (evidence.SuggestsFidelityFxArmingHazard &&
                     TryIndexOf(ladder, HookCompatibilityStageId.GenericNoFfxLifecycle, false,
                         out int noFfxIndex))
            {
                startIndex = noFfxIndex;
                reason = "evidence of a FidelityFX loader duplicate or a dxgi.dll proxy; " +
                    "starting without the FidelityFX lifecycle hooks";
            }
            else if (evidence.HasFrameGenerationRuntime &&
                     evidence.AttachMode == HookAttachMode.Late &&
                     TryIndexOf(ladder, HookCompatibilityStageId.Generic, false,
                         out int genericIndex))
            {
                startIndex = genericIndex;
                reason = "a frame-generation runtime is resident and the swapchain already " +
                    "exists; starting on the generic D3D12 route";
            }
            else if (evidence.XeFg && evidence.AttachMode == HookAttachMode.Early &&
                     TryIndexOf(ladder, HookCompatibilityStageId.VendorAwareXeFgQueue, false,
                         out int xefgIndex))
            {
                startIndex = xefgIndex;
                reason = "XeSS-FG is resident and the hook attached early; starting on the " +
                    "XeSS-FG native queue route";
            }
            else
            {
                reason = evidence.IsKnown
                    ? "no routing evidence; starting vendor-aware"
                    : "module list unreadable; starting vendor-aware";
            }

            return new HookCompatibilityStagePlan(ladder, startIndex, reason,
                probingEnabled: true, evidence);
        }

        /// <summary>
        /// Canonical order V → VX → G → GN → V+E → G+E → GN+E, filtered by what the evidence
        /// can support. A catalog stage replaces the head of the ladder.
        /// </summary>
        internal static List<HookCompatibilityStage> BuildLadder(HookTargetEvidence evidence,
            HookCompatibilityStage catalogStage)
        {
            var ladder = new List<HookCompatibilityStage>();
            bool generic = AllowsGenericRoute(evidence);
            bool noFfx = generic && evidence.HasFidelityFxEvidence;
            string gate = ResolveEarlyInjectionGateModule(evidence);
            bool early = gate != null && evidence.HasFrameGenerationRuntime;

            ladder.Add(HookCompatibilityStage.Create(HookCompatibilityStageId.VendorAware));
            if (evidence.XeFg && evidence.AttachMode == HookAttachMode.Early)
                ladder.Add(HookCompatibilityStage.Create(HookCompatibilityStageId.VendorAwareXeFgQueue));
            if (generic)
                ladder.Add(HookCompatibilityStage.Create(HookCompatibilityStageId.Generic));
            if (noFfx)
                ladder.Add(HookCompatibilityStage.Create(HookCompatibilityStageId.GenericNoFfxLifecycle));
            if (early)
            {
                ladder.Add(HookCompatibilityStage.Create(HookCompatibilityStageId.VendorAware, gate));
                if (generic)
                    ladder.Add(HookCompatibilityStage.Create(HookCompatibilityStageId.Generic, gate));
                if (noFfx)
                    ladder.Add(HookCompatibilityStage.Create(
                        HookCompatibilityStageId.GenericNoFfxLifecycle, gate));
            }

            if (catalogStage != null)
            {
                // The catalog entry is stage 1; the ladder continues after its canonical
                // position so a locally failing curated entry still escalates.
                int rank = Rank(catalogStage);
                ladder.RemoveAll(stage => Rank(stage) <= rank);
                ladder.Insert(0, catalogStage);
            }
            return ladder;
        }

        internal static bool AllowsGenericRoute(HookTargetEvidence evidence)
        {
            // Under the generic route the hook never draws on a D3D11 swapchain
            // (overlay.cpp: knownD3D12Route suppresses OverlayD3D11OnPresent).
            if (string.Equals(evidence.Runtime, "D3D11", StringComparison.OrdinalIgnoreCase))
                return false;
            if (string.Equals(evidence.Runtime, "D3D12", StringComparison.OrdinalIgnoreCase))
                return true;
            return evidence.IsKnown && evidence.D3D12Loaded;
        }

        internal static string ResolveEarlyInjectionGateModule(HookTargetEvidence evidence)
        {
            if (!evidence.IsKnown) return null;
            if (evidence.D3D12Loaded) return GateModuleD3D12;
            if (evidence.Streamline) return HookTargetEvidence.StreamlineModules[0];
            return null;
        }

        /// <summary>
        /// The stage to move to after <paramref name="verdict"/> ended <paramref name="from"/>,
        /// or null when the ladder has nothing left. <paramref name="exhausted"/> says whether
        /// that null means "every route was tried" (persist as exhausted) or "this failure is
        /// not a routing problem" (leave the learned entry alone).
        /// </summary>
        internal static HookCompatibilityStage ResolveEscalation(HookCompatibilityStage from,
            HookCompatibilityVerdict verdict, NativeHookStatusSnapshot snapshot,
            HookTargetEvidence evidence, IReadOnlyList<HookCompatibilityStage> ladder,
            ISet<string> tried, out bool exhausted)
        {
            exhausted = false;
            if (from == null || ladder == null) return null;
            bool early = from.RequiresEarlyInjection;
            HookCompatibilityStageId id = from.Id;
            bool ffxEvidence = evidence != null && evidence.HasFidelityFxEvidence;
            bool fgEvidence = evidence != null && evidence.HasFrameGenerationRuntime;
            bool vendor = id == HookCompatibilityStageId.VendorAware ||
                          id == HookCompatibilityStageId.VendorAwareXeFgQueue;
            // Local functions cannot write an out parameter; they write this and the method
            // copies it out once.
            bool noneLeft = false;

            HookCompatibilityStage Next(HookCompatibilityStageId target, bool withEarly)
            {
                if (TryIndexOf(ladder, target, withEarly, out int index) &&
                    !tried.Contains(ladder[index].Key))
                    return ladder[index];
                return null;
            }

            HookCompatibilityStage Escalate(params (HookCompatibilityStageId id, bool early)[] candidates)
            {
                foreach (var candidate in candidates)
                {
                    HookCompatibilityStage next = Next(candidate.id, candidate.early);
                    if (next != null) return next;
                }
                noneLeft = true;
                return null;
            }

            HookCompatibilityStage Exhausted()
            {
                noneLeft = true;
                return null;
            }

            HookCompatibilityStage Resolve()
            {
                switch (verdict)
                {
                    case HookCompatibilityVerdict.OsdCreateFailed:
                    case HookCompatibilityVerdict.StatusTimeout:
                        return null;

                    case HookCompatibilityVerdict.ForeignPresenter:
                    case HookCompatibilityVerdict.RendererStalled:
                        if (early) return Exhausted();
                        if (vendor) return Escalate((HookCompatibilityStageId.Generic, false),
                            (HookCompatibilityStageId.GenericNoFfxLifecycle, false),
                            (HookCompatibilityStageId.Generic, true));
                        if (id == HookCompatibilityStageId.Generic)
                            return Escalate((HookCompatibilityStageId.GenericNoFfxLifecycle, false),
                                (HookCompatibilityStageId.Generic, true));
                        return Escalate((HookCompatibilityStageId.GenericNoFfxLifecycle, true));

                    case HookCompatibilityVerdict.EarlyInjectionRequired:
                        if (early) return Exhausted();
                        return Escalate((id, true));

                    case HookCompatibilityVerdict.InstallHung:
                    {
                        if (early || id == HookCompatibilityStageId.GenericNoFfxLifecycle)
                            return Exhausted();
                        NativeHookInstallPhase phase = snapshot.InstallPhase;
                        if (phase == NativeHookInstallPhase.FidelityFxExports)
                            return Escalate((HookCompatibilityStageId.GenericNoFfxLifecycle, false));
                        if (phase == NativeHookInstallPhase.StreamlineProxy ||
                            phase == NativeHookInstallPhase.XeFgProxy)
                        {
                            if (vendor) return Escalate((HookCompatibilityStageId.Generic, false));
                            return Exhausted();
                        }
                        // Unknown phase (version-1 hook) or a phase without a dedicated skip.
                        if (vendor)
                            return ffxEvidence
                                ? Escalate((HookCompatibilityStageId.GenericNoFfxLifecycle, false),
                                    (HookCompatibilityStageId.Generic, false))
                                : Escalate((HookCompatibilityStageId.Generic, false));
                        if (ffxEvidence)
                            return Escalate((HookCompatibilityStageId.GenericNoFfxLifecycle, false));
                        return Exhausted();
                    }

                    case HookCompatibilityVerdict.InstallFailed:
                        if (early) return Exhausted();
                        if (vendor) return Escalate((HookCompatibilityStageId.Generic, false),
                            (HookCompatibilityStageId.GenericNoFfxLifecycle, false));
                        if (id == HookCompatibilityStageId.Generic)
                            return Escalate((HookCompatibilityStageId.GenericNoFfxLifecycle, false));
                        return Exhausted();

                    case HookCompatibilityVerdict.NoQueue:
                        if (early) return Exhausted();
                        if (vendor) return Escalate((HookCompatibilityStageId.Generic, false));
                        return Escalate((id, true));

                    case HookCompatibilityVerdict.NoPresent:
                        if (early) return Exhausted();
                        if (vendor && !fgEvidence) return null; // a non-DXGI presenter, not routing
                        return Escalate((id, true));

                    default:
                        return null;
                }
            }

            HookCompatibilityStage result = Resolve();
            exhausted = noneLeft;
            return result;
        }

        /// <summary>
        /// Both routing bits work by neutralizing hooks that are already installed rather than
        /// by installing any, so a running hook can switch them on but never back off.
        /// </summary>
        private const NativeHookCompatibilityFlags LiveTurnOnOnlyFlags =
            NativeHookCompatibilityFlags.EnableGenericD3D12PresentRoute |
            NativeHookCompatibilityFlags.DisableFidelityFxSwapchainLifecycleHooks;

        /// <summary>
        /// Whether the hook can move from <paramref name="from"/> to <paramref name="to"/>
        /// without a fresh process. Requires a hook that advertises live reload for every bit
        /// that changes. Injection timing (gate module, delay) is always an install-time
        /// decision, and a routing bit that is already in effect can never be taken back.
        /// </summary>
        internal static bool CanEscalateLive(HookCompatibilityStage from,
            HookCompatibilityStage to, NativeHookStatusSnapshot status)
        {
            if (from == null || to == null) return false;
            if (status.Version < HookStatusProbe.Version2 || status.LiveReloadCapabilities == 0)
                return false;
            uint added = (uint)(to.Flags & ~from.Flags);
            uint changed = (uint)(to.Flags ^ from.Flags);
            if (added == 0) return false;
            if ((changed & ~status.LiveReloadCapabilities) != 0) return false;
            if ((from.Flags & LiveTurnOnOnlyFlags & ~to.Flags) != 0) return false;
            if (!string.Equals(to.EarlyInjectionModule, from.EarlyInjectionModule,
                    StringComparison.OrdinalIgnoreCase))
                return false;
            return to.InjectionDelay == from.InjectionDelay;
        }

        /// <summary>
        /// Re-plan for a process whose evidence changed while the hook was already running — a
        /// frame-generation switch in the game's own menu is the case this exists for. The
        /// ladder is rebuilt from the new evidence, but the stage the hook currently runs stays
        /// in it and is never regressed past: it is the empirical starting point, and whatever
        /// the re-probe ends up learning has to describe what is actually in effect.
        /// </summary>
        internal static HookCompatibilityStagePlan Replan(HookTargetEvidence evidence,
            HookCompatibilityProfile catalog, HookLearnedProfileEntry learned,
            string hookBuildHash, bool autoCompatibility, HookCompatibilityStage applied)
        {
            HookCompatibilityStagePlan plan = Plan(evidence, catalog, learned, hookBuildHash,
                autoCompatibility);
            if (applied == null || plan.IsEmpty) return plan;
            var ladder = new List<HookCompatibilityStage>(plan.Ladder);
            int appliedIndex = IndexOfOrAppend(ladder, applied);
            int startIndex = Math.Max(plan.StartIndex, appliedIndex);
            if (ladder.Count == plan.Ladder.Count && startIndex == plan.StartIndex) return plan;
            return new HookCompatibilityStagePlan(ladder, startIndex, plan.Reason,
                plan.ProbingEnabled, evidence);
        }

        internal static bool TryIndexOf(IReadOnlyList<HookCompatibilityStage> ladder,
            HookCompatibilityStageId id, bool early, out int index)
        {
            for (int i = 0; i < ladder.Count; i++)
            {
                if (ladder[i].Id == id && ladder[i].RequiresEarlyInjection == early)
                {
                    index = i;
                    return true;
                }
            }
            index = -1;
            return false;
        }

        private static int IndexOfOrAppend(List<HookCompatibilityStage> ladder,
            HookCompatibilityStage stage)
        {
            for (int i = 0; i < ladder.Count; i++)
            {
                if (ladder[i].SameRouting(stage)) return i;
            }
            ladder.Add(stage);
            return ladder.Count - 1;
        }

        private static int Rank(HookCompatibilityStage stage)
            => (stage.RequiresEarlyInjection ? 10 : 0) + (int)stage.Id;
    }
}
