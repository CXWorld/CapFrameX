# Dev Plan: Arrow Lake OOBMSM NGU/D2D Sensor Support

Status: research/implementation plan
Owner context: CapFrameX / LibreHardwareMonitor integration; the PTL pipeline is already live and validated, ARL is the next platform to bring online.
Primary goal: surface NGU and D2D fabric clocks on Arrow Lake-S / -H / -U through the existing `IntelOOBMSM.bin` PawnIO module + `IntelOobmsmClocks` wrapper, with at least the same fidelity as HWiNFO64.

## 1. State as of 2026-05-10

PTL is fully shipped and dynamically validated:

- Idle: NCLK 550 MHz (ratio 11 × 50), D2D 300 MHz (ratio 6 × 50)
- Under load: NCLK rises to 1200 MHz (ratio 24 × 50), D2D constant
- Both bit fields decoded straight from the LNL `Container_2` (Groupname `0x82F8`) layout, which PTL re-uses binary-identically.

ARL inherits the kernel module and CPU detection but has **no platform offsets and no GUID match** wired up yet. Running CapFrameX on ARL today loads the module successfully but no OOBMSM-derived sensors appear because `IntelOobmsmClocks.IsReady` is `false` for ARL.

## 2. What we know about ARL fabric clocks

From SkatterBencher's ARL overclocking series and Intel's published OC documentation:

| Clock | Reference | Default | Min | Max | Runtime |
|---|---|---|---|---|---|
| NGU | 100 MHz | 26X = 2.6 GHz | 4X | 85X | variable |
| D2D | 100 MHz | 21X = 2.1 GHz | 15X | 40X | **boot-fixed** |

ARL CPUID family/model values (already covered in `IntelOOBMSM.p` Z. 83-88):

- `0x06C5` — ARROWLAKE_H (mobile, P+E)
- `0x06C6` — ARROWLAKE-S (desktop, LGA-1851)
- `0x06B5` — ARROWLAKE-U (mobile, low-power)

OOBMSM still sits at PCI bus/device/function `00:0A.0` on ARL (canonical Core Ultra slot — not changed since MTL).

## 3. What we don't know

These are the open questions Phase 1 must answer:

- Whether ARL exposes Punit telemetry through TPMI (cap_id `0x0023`, like PTL/LNL) or VSEC (cap_id `0x000B`, like MTL) — possibly both, since ARL straddles the MTL-style desktop fabric and the LNL-style mobile fabric.
- The OOBMSM Punit-telemetry GUID(s). PTL uses `0x03086000` (normal) and `0x03086100` (fixed). ARL is almost certainly different.
- The container byte offsets and bit positions for NGU and D2D. Intel's public `intel/Intel-PMT/xml` directory contains MTL, LNL, PTL, GNR, BMG, CWF, SPR — but **no ARL**. The layout must be derived empirically from the live hardware.
- Whether ARL Desktop exposes multiple OOBMSM instances (multi-tile). Current kernel module only probes the single canonical slot.

## 4. What's already in place

| Layer | File | Notes |
|---|---|---|
| Kernel CPU detection | `CX.PawnIO.Modules/IntelOOBMSM.p` Z. 83-88 | All three ARL family/model entries present |
| PCI probe + BAR0 map | `CX.PawnIO.Modules/IntelOOBMSM.p` `oobmsm_init` | Architecture-agnostic, works on ARL same as PTL |
| Platform enum | `LibreHardwareMonitorLib/PawnIo/IntelOobmsm.cs` Z. 54 | `Platform.Arl` exists |
| CPUID → Platform mapping | `LibreHardwareMonitorLib/PawnIo/IntelOobmsm.cs` Z. 353-356 | Resolves `0xC5`/`0xC6`/`0xB5` → `Arl` |
| Allow-list | `LibreHardwareMonitorLib/PawnIo/IntelOobmsm.cs` Z. 379 | ARL already in `IsPlatformValidated` (anticipatory) |
| Diagnostic skript | `Test-IntelOobmsm.ps1` | Platform-agnostic 6-stage triage |

## 5. What's missing

| Item | File | Action |
|---|---|---|
| ARL platform offsets | `IntelOobmsmClocks.cs` Z. 372 | currently `default` → empty `PlatformOffsets`. Needs `ClockField` for NGU and D2D. |
| GUID match in PFS walk | `IntelOobmsmClocks.cs` Z. 109, 180 | hardcoded to `GuidPtlNormalPunit = 0x03086000`. Needs ARL GUID constant + per-platform dispatch. |
| Discovery path | `IntelOobmsmClocks.cs` ctor body | only walks TPMI cap with `DiscoveryOffset != 0`. If ARL uses VSEC, the cap-walk will skip it. Needs branch by `CapId`. |
| `Validated = true` for ARL | `IntelOobmsmClocks.cs` Z. 372 | set after HWiNFO cross-check. |

## 6. Phased approach

### Phase 1 — Cap discovery on real ARL hardware

Run `pwsh -File .\Test-IntelOobmsm.ps1` on the ARL system. Stages [0]–[4] are platform-agnostic and will succeed; Stage [5] will report `IsReady=False`.

The critical output is **Stage [3] Extended-capability walk**. Each line follows:

```
cfg=0x___  id=0xXXXX (kind)  ver=_  vsec_id=0x____  len=__  TBIR=_  disc=0xXXXXXXXX
```

Decision tree:

- If at least one **TPMI** cap (`id=0x0023`) is present with non-zero `disc`: ARL goes through the same PFS walk as PTL/LNL. Phase 2 only needs a new GUID.
- If no TPMI cap is present but **VSEC** caps (`id=0x000B`) are: ARL needs a separate VSEC-direct discovery path (no PFS table; the `disc` offset already points at the telemetry aperture).
- If both: prefer TPMI (newer, used by PTL/LNL).

Capture this output verbatim into the next section of this doc before proceeding.

### Phase 2 — Adapt discovery path

**TPMI case** (most likely if ARL inherits PTL's design):

1. Add `GuidArlNormalPunit` constant alongside `GuidPtlNormalPunit`.
2. In `IntelOobmsmClocks` ctor, replace the hardcoded `if (guid != GuidPtlNormalPunit) continue;` with a per-platform GUID set lookup.
3. Confirm GUID by walking the PFS table and comparing each PFS entry's GUID against the candidate list.

**VSEC case**:

1. Add a separate `WalkVsec` discovery path in the ctor: iterate VSEC caps, identify the OOBMSM telemetry VSEC by its `vsec_id` (needs Phase 1 data), use its `disc` offset directly as the sub-aperture base — no PFS walk.
2. Keep the TPMI path for PTL/LNL untouched; dispatch by platform.

### Phase 3 — Container layout determination

Three complementary strategies, in order of cheapness:

**(A) Layout inheritance probe.** Before deriving a new layout, try the two known layouts as decoder candidates:

- LNL Container_2 layout: `0x82F8` bits[34..41] / [50..57] × 50 MHz
- MTL layout: `0x6348` bits[48..55] × 100 MHz

If either produces values inside the plausibility window (100–10000 MHz) that **also** match HWiNFO at idle, we are done. PTL inheriting LNL is the precedent.

**(B) Pattern match against known constants.** D2D is boot-fixed at default ratio 21 (= 2.1 GHz). At BIOS defaults, dump the entire OOBMSM aperture (BAR0 64 KB) and grep for any 8-bit field equal to `0x15` (=21) inside an 8-byte container. NGU at idle should sit between ratio 4 (floor) and 26 (default boot ratio). Cross-reference candidate offsets against the LNL/MTL layouts as priors.

**(C) Differential snapshot.** Capture aperture dumps at three load levels: deep idle, single-thread load, full all-core load. NGU should rise monotonically. D2D should stay put. Any bit field that flips with NGU's expected curve is the candidate; bits that stay constant across all three are D2D candidates.

### Phase 4 — Wire ARL into the platform table

In `IntelOobmsmClocks.GetPlatformOffsets`:

```csharp
case IntelOobmsm.Platform.Arl:
    return new PlatformOffsets(
        ngu: new ClockField(<container>, <lsb>, <msb>, <multiplier>),
        d2d: new ClockField(<container>, <lsb>, <msb>, <multiplier>),
        validated: false);  // until cross-checked
```

### Phase 5 — Cross-validate against HWiNFO

- Compare against HWiNFO64 stable v8.46+ (the build that landed the PTL fix; same release stream covers ARL going back to launch).
- Capture readings at idle, single-thread load, all-core load.
- Confirm D2D constancy (boot-fixed assumption).
- Confirm NGU dynamic range matches HWiNFO.
- Flip `validated: true` once two independent cross-checks agree.

## 7. Risks and known traps

- **Multi-tile**: ARL Desktop may host multiple OOBMSM functions (one per tile). Current kernel only probes `00:0A.0`. If ARL exposes additional `00:0A.x` or `00:0B.0` instances, NGU might live in a different function than the one we open. Detection: scan `00:0A.0..00:0A.7` and `00:0B.0` during Phase 1.
- **Cap-header offset asymmetry**: TPMI cap entry header is at `+0x0C`, VSEC at `+0x08` (the wrapper handles both). If ARL has hybrid caps, double-check the entry-header offset interpretation.
- **PFS multi-match**: PTL has two GUIDs at separate PFS entries. ARL might too. The current PFS walker breaks on first GUID match. Phase 2 should accumulate all matching entries, not just the first.
- **Runtime variability assumption**: SkatterBencher's "runtime-variable NGU" claim was made in an OC context. Some firmware revisions may freeze NGU at boot in OS-runtime (similar to D2D). If we observe a static NGU on stock BIOS, that's not a bug — it's expected.
- **HWiNFO label drift**: HWiNFO labels the SoC NoC clock "NGU/NCLK" with a slash on PTL/LNL but might split them on ARL desktop where compute-tile NGU and SoC NCLK are physically distinct. Be ready to expose two sensors if the cap-walk shows two telemetry sub-features.

## 8. Action items — concrete next step

1. **You**: run `pwsh -File .\Test-IntelOobmsm.ps1` on the ARL machine.
2. Paste Stage [0]–[3] output verbatim into this doc as section "9. Phase 1 raw output".
3. **Me**: decide TPMI vs VSEC, draft Phase 2 patch.

Until Phase 1 data lands, no code edit is justified — both Phase 2 directions diverge significantly and committing speculatively risks regressing PTL.

## 9. Phase 1 raw output — ARL-S desktop (Core Ultra 9 285K)

Run on 2026-05-10 against a Core Ultra 9 285K (family 6, model 0xC6, "Arrowlake-S"). PawnIO unrestricted driver loaded, testsigning ON.

### 9.1 `Test-IntelOobmsm.ps1` — kernel-module load fails

```
[1] Constructing IntelOobmsm (kernel module load)...
    IsLoaded         : False
    DetectedPlatform : Arl
    IsValidated      : True            (← anticipatory allow-list, misleading on ARL-S)
    PawnIO module    : NOT loaded (main() returned error)
STOP: kernel module did not load.
```

CPUID resolution to `Platform.Arl` works. The kernel module's `oobmsm_init()` fails because the canonical `00:0A.0` slot does not respond.

### 9.2 Full PCI sweep — no Intel function at 00:0A.0 or 00:0B.0

A throwaway scanner walks bus 0..0xFF reading vendor/device + class code via `HalGetBusDataByOffset` (same path the production module uses).

Intel functions found:

| BDF      | DID    | Class    | What                                       |
|---       |---     |---       |---                                         |
| 00:00.0  | 0x7D1A | 0x0600   | Host bridge                                |
| 00:01.0  | 0x7ECC | 0x0604   | PCIe RP                                    |
| 00:04.0  | 0xAD03 | **0x1180** | Innovation Platform Framework Processor Participant |
| 00:06.0  | 0xAE4D | 0x0604   | PCIe RP                                    |
| 00:07.0/1| 0x7EC4/5| 0x0604  | PCIe RP                                    |
| 00:08.0  | 0xAE4C | **0x0880** | GNA Scoring Accelerator (per PnP label)  |
| 00:0D.0/2| 0x7EC0/2| 0x0C03  | xHCI                                       |
| 00:0E.0  | 0xAD0B | 0x0104   | VMD                                        |
| 00:14.0  | 0xAE7F | 0x0500   | RAM controller (IMC)                       |
| 00:1F.0/5| 0xAE0D / 0xAE23 | 0x0601 / 0x0C80 | ISA bridge / SMBus            |
| 80:14.0  | 0x7F6E | 0x0C03   | xHCI (PCH)                                 |
| 80:14.5  | 0x7F2F | **0x0000** | Error Aggregation Handler (EAH)          |
| 80:15.0/2| 0x7F4C/E | 0x0C80 | I2C                                        |
| 80:16.0  | 0x7F68 | 0x0780   | Management Engine                          |
| 80:1C.0..3| 0x7F38..3B | 0x0604 | PCIe RPs                              |
| 80:1F.0/3/4/5 | 0x7F04 / 0x7F50 / 0x7F23 / 0x7F24 | 0x0601 / 0x0403 / 0x0C05 / 0x0C80 | LPC / HDA / SMBus / SPI |

**There is no Intel function at bus 0 device 0x0A or 0x0B, nor anywhere on bus 80.** The compute tile (bus 0) and PCH (bus 80) are the only Intel-populated SoC segments.

### 9.3 Ext-cap chain inspection on every plausible candidate

Walked `>= 0x100` ext-cap chain on the OOBMSM-shaped candidates:

| Candidate | Class | BAR0 phys           | Ext-cap chain |
|---        |---    |---                  |---            |
| 00:04.0   | 0x1180 (Signal Processing)   | 0x000003FF_BFFC0000 | reads as 0xFFFFFFFF — no ECAM |
| 00:08.0   | 0x0880 (System peripheral)   | 0x000003FF_BFFFF000 | empty (header dword = 0)       |
| 80:14.5   | 0x0000 (EAH)                 | none                | empty                          |
| 00:14.0   | 0x0500 (RAM ctrl / IMC)      | 0x000000B4_13070000 | empty                          |
| 80:16.0   | 0x0780 (Management Engine)   | 0x00000080_00215000 | empty                          |

**No Intel function on ARL-S desktop carries a TPMI (`0x0023`) or VSEC (`0x000B`) extended capability.** The OOBMSM mechanism — TPMI cap → PFS table → GUID-matched discovery aperture inside BAR0 — has no entry point on this SKU.

### 9.4 Conclusion

ARL-S desktop **cannot** reuse the OOBMSM pipeline. The premise in section 2 ("OOBMSM still sits at 00:0A.0 on ARL — not changed since MTL") was extrapolated from mobile parts and does not hold for the desktop tile architecture. Phase 2 (TPMI vs VSEC discovery) is moot on this platform — both alternatives require a TPMI/VSEC-bearing endpoint that isn't there.

ARL-H (mobile, 0xC5) and ARL-U (mobile low-power, 0xB5) remain **untested**. They share more SoC structure with LNL/PTL than with the desktop die and may still expose OOBMSM at 00:0A.0 normally. Don't commit code yet that gates ARL-S out of the platform table — keep the platform offsets entry empty until -H or -U is on the bench, then validate per-SKU.

## 10. Action items — revised after Phase 1

1. **Don't** add an ARL platform-offsets entry yet. The `default → empty` fallback in `IntelOobmsmClocks.GetPlatformOffsets` already does the right thing on ARL-S (no sensors surface).
2. **Soft-fail the kernel module on missing 00:0A.0** so other CapFrameX pipelines don't see an unrelated PawnIO load error in the log on ARL-S. Either:
   - Make `oobmsm_init` return `STATUS_SUCCESS` when the canonical slot is empty (leave `g_bar_va = NULL`; gate IOCTLs on `g_bar_va != NULL`), or
   - Lift the probe to the C# side and only attempt `LoadModuleFromResource` when the platform's canonical slot has been pre-confirmed.

   The first option is cheaper and matches the existing IntelMCHBAR pattern; the second avoids loading a useless kernel module at all. Pick one when ARL-H/-U data lands so the same change covers both.
3. **ARL-S desktop NGU/D2D resolved separately** via the Intel OC Mailbox relocated to MSR 0x607/0x608 — see §11. SkatterBencher's NGU/D2D OC method on ARL-S goes through BIOS variables; that's a configuration path, not a runtime read (different mechanism).
4. **Phase 2 deferred** until ARL-H or ARL-U hardware is available.

## 11. ARL-S desktop outcome — OC Mailbox on MSR 0x607 / 0x608

Phase 1 (§ 9) established that ARL-S desktop has no PCI-exposed OOBMSM endpoint at `00:0A.0`, so the OOBMSM/TPMI pipeline used for PTL/LNL does not apply on this SKU. ARL-H and ARL-U mobile remain untested and are still expected to go through OOBMSM.

The mechanism that actually surfaces NGU and D2D on ARL-S desktop is the **Intel OC Mailbox relocated from the legacy `MSR 0x150` onto a new MSR pair**:

- **MSR `0x607`** — interface register (command word, bit 31 = run bit)
- **MSR `0x608`** — data register (input/output word)

Verified on a Core Ultra 9 285K (family 6, model 0xC6) on 2026-05-12.

### 11.1 Protocol

```
WrMSR 0x607  ←  (command | 0x80000000)        // bit 31 = run bit
poll RdMSR 0x607 until bit 31 clears          // ~999 retries
read RdMSR 0x608                              // result word
```

### 11.2 Commands

| Command   | Result mask    | Scale     | Meaning                       |
|---        |---             |---        |---                            |
| `0x1237`  | `& 0x7FFF`     | × 100 MHz | D2D ratio (boot-fixed)        |
| `0x0022`  | `(>>8) & 0xFF` | × 100 MHz | NGU ratio (runtime variable)  |

For BIOS-set 2800 MHz both commands return ratio `0x1C`; for 3200 MHz both return `0x20`.

### 11.3 Integration

| File | Change |
|---|---|
| `CX.PawnIO.Modules/IntelMSR.p` | Added `MSR_OC_MAILBOX_IF` (0x607) and `MSR_OC_MAILBOX_DATA` (0x608) to both `is_allowed_msr_read` and `is_allowed_msr_write`. |
| `source/LibreHardwareMonitorLib/Resources/PawnIO/IntelMSR.bin` | Rebuilt unsigned `.amx` + 4-byte zero header (loads via unrestricted PawnIO driver). Original signed `.bin` archived as `.signed.bak` next to it. |
| `source/LibreHardwareMonitorLib/PawnIo/IntelMsr.cs` | Added `WriteMsr(uint index, ulong value)` and affinity overload. |
| `source/LibreHardwareMonitorLib/PawnIo/IntelOcMailbox.cs` (new) | Wraps the mailbox protocol; exposes `IsReady`, `TryRead(out Sample)` with `HasNgu`/`HasD2d`/`NguMhz`/`D2dMhz`. |
| `source/LibreHardwareMonitorLib/Hardware/Cpu/IntelCpu.cs` | Added `SupportsOcMailbox(arch)` predicate (ArrowLake, NovaLake) and an `else if` branch that activates the mailbox sensors when `IntelOobmsmClocks` isn't ready. Refresh loop reads `IntelOcMailbox.Sample` into the existing `_nguClock`/`_d2dClock` sensor slots. |

Smoke-test (`Test-LibIntelOcMailbox.ps1` against the compiled DLL) and the live CapFrameX UI both confirm D2D Clock and NGU Clock sensors register and display the BIOS-set ratio × 100 MHz on Core Ultra 9 285K.

### 11.4 Open follow-ups

- **Upstream PR** to `namazso/PawnIO-Modules` adding 0x607/0x608 to `IntelMSR.p`, then drop the signed `IntelMSR.bin` back into CapFrameX. Until merged the local build runs on the unrestricted PawnIO driver.
- **Mobile ARL-H / ARL-U**: untested. Likely the same 0x607/0x608 channel; `SupportsOcMailbox` doesn't restrict by sub-family, so `IsReady` probes gracefully no-op if the channel is absent.
- **Affinity pinning** during the mailbox protocol: not needed on single-socket desktop; revisit if multi-socket ARL successors arrive.
- **Per-D2D-stack commands** and finer per-component telemetry remain unmapped — could light up granular "Die-to-Die stack" sensors in the future, not part of the v1 ship.

## 12. References

- SkatterBencher — Arrow Lake NGU Overclocking: https://skatterbencher.com/2024/10/24/arrow-lake-ngu-overclocking/
- SkatterBencher — Arrow Lake D2D Overclocking: https://skatterbencher.com/2024/10/24/arrow-lake-d2d-overclocking/
- Intel PMT XML (LNL/MTL/PTL reference layouts): https://github.com/intel/Intel-PMT/tree/master/xml
- HWiNFO version-history (NGU/D2D fix in v8.46): https://www.hwinfo.com/version-history/
- Existing PTL implementation: `source/LibreHardwareMonitorLib/PawnIo/IntelOobmsmClocks.cs`
- Existing kernel module: `CX.PawnIO.Modules/IntelOOBMSM.p`
- ARL-S OC Mailbox wrapper: `source/LibreHardwareMonitorLib/PawnIo/IntelOcMailbox.cs`
- Diagnostic harness: `Test-IntelOobmsm.ps1`
