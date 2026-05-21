# Dev Plan: Panther Lake IMC Memory Clock via PawnIO.Modules

Status: research/implementation plan
Owner context: CapFrameX / LibreHardwareMonitor integration later, upstream PR first in `namazso/PawnIO.Modules`
Primary goal: add a safe PawnIO module that can read the Intel client IMC/QCLK multiplier or equivalent clock-ratio data for Panther Lake-class CPUs, without WinRing0-style generic hardware access.

Research update 2026-05-01 after local `pmcreader-plugin/intel-perfmon` refresh:

- Panther Lake perfmon files are now V1.05, `DatePublished` 2026-02-26.
- `mapfile.csv` maps both `GenuineIntel-6-CC` and `GenuineIntel-6-D5` to PTL.
- `GenuineIntel-6-CF` is still mapped to Emerald Rapids in `mapfile.csv`; do not treat `0xCF` as Panther Lake.
- `PTL/events/pantherlake_uncore.json` still exposes only iMC data/CAS events and no IMC/QCLK ratio or memory-clock event.

## 1. Problem Statement

CapFrameX/LibreHardwareMonitor currently exposes an Intel `Memory Clock` sensor by reading `MSR_UNCORE_PERF_STATUS` (`0x621`) and calculating:

```text
memoryClockMHz = uncoreCurRatio * busClockMHz
```

That is valid only on older Intel client platforms where the IMC/memory I/O clock is tied to the uncore ratio. Panther Lake is intentionally not supported because uncore/ring/NGU and IMC clocks are decoupled. Simply adding Panther Lake to the current guard would produce a plausible but wrong value.

The concrete target is not DRAM bandwidth, CAS counts, PMU clockticks, or uncore ratio. The target is the IMC/QCLK ratio or a register-derived equivalent that can be combined with the measured bus clock to produce a memory clock.

## 2. Current Local Findings To Preserve Across Codex Instances

CapFrameX local paths:

- `source/LibreHardwareMonitorLib/Hardware/Cpu/IntelCpu.cs`
- `source/LibreHardwareMonitorLib/PawnIo/PawnIo.cs`
- `source/LibreHardwareMonitorLib/PawnIo/IntelMsr.cs`
- `source/LibreHardwareMonitorLib/Resources/PawnIo/`
- `pmcreader-plugin/intel-perfmon/PTL/events/pantherlake_uncore.json`
- `pmcreader-plugin/PmcReader/Interop/Ring0.cs`
- `pmcreader-plugin/PmcReader/Intel/SkylakeClientUncore.cs`

Known CapFrameX behavior:

- Constructor creates the `Memory Clock` sensor only if `SupportsUncoreMemoryClock(...)` is true and `MSR_UNCORE_PERF_STATUS & 0x7F` is non-zero.
- Update path reads `MSR_UNCORE_PERF_STATUS`, masks `curRatio = eax & 0x7F`, and sets `_memoryClock.Value = curRatio * newBusClock`.
- `SupportsUncoreMemoryClock` excludes Ice Lake and later client platforms, including Panther Lake, because IMC and uncore are decoupled.
- Current Panther Lake local perfmon JSON V1.05 only has `UNC_M_CAS_COUNT_RD`, `UNC_M_CAS_COUNT_WR`, and `UNC_M_TOTAL_DATA`. It does not expose an IMC ratio or memory clock.
- Updated local `mapfile.csv` maps `GenuineIntel-6-CC` and `GenuineIntel-6-D5` to Panther Lake. CapFrameX currently maps `0xCC`, reserved `0xCD`, reserved `0xCE`, and reserved `0xCF` to Panther Lake, but does not yet include `0xD5`.
- Existing `IntelMSR.bin` exports `ioctl_read_msr` and `ioctl_write_msr`, but MSR-only access is insufficient for Panther Lake IMC ratio.
- Existing `SmbusIntelSkylakeIMC.bin` exports SMBus IOCTLs and contains internal PCI/MMIO helper strings, but it is not a Panther Lake memory-clock module and should not be repurposed.

Known PawnIO.Modules upstream structure as of 2026-05-01:

- Repo: `https://github.com/namazso/PawnIO.Modules`
- Root contains one `.p` source per module, e.g. `IntelMSR.p`, `SmbusIntelSkylakeIMC.p`.
- `IntelMSR.p` is a good minimal-style reference: allowlisted MSRs, `DEFINE_IOCTL_SIZED`, Doxygen-style IOCTL docs, `main()` gates `ARCH_X64` and Intel vendor.
- `SmbusIntelSkylakeIMC.p` is a useful reference for `pci_config_read_dword(...)` and fixed PCI/device probing patterns.
- Latest observed release: `0.2.4` on 2026-03-17.

## 3. Upstream Requirements From PawnIO.Modules

From the PawnIO.Modules contribution guidelines, a new official module should:

- Be LGPL 2.1 compatible.
- Compile with no CI warnings.
- Have no security vulnerabilities.
- Not expose functionality that aids compromising software security measures.
- Have Doxygen-style documentation on every exposed IOCTL.
- Be usable by everyone, without Pawn-side lockdown.
- Be simple.
- Best-effort ensure it only runs on supported hardware.
- Avoid dynamic-sized arrays where possible.
- Replace WinRing0-like access, not first-party drivers if those exist.
- Provide reference code and/or datasheets in the PR.

Implication for this module: do not expose generic PCI config or arbitrary physical-memory read/write IOCTLs. Export only a high-level "read IMC clock ratio" operation, and keep all addresses/registers hardcoded and allowlisted inside the module.

## 4. Proposed Upstream Module

Preferred file name:

```text
IntelClientImcClock.p
```

Alternative names:

```text
IntelImcClock.p
IntelClientMemoryClock.p
IntelMemClock.p
```

Preferred module scope:

- Intel x64 only.
- Read-only.
- Client/SoC memory-controller clock ratio only.
- Initially targeted at Panther Lake and related Core Ultra client platforms, but implemented as a small source table so validated MTL/ARL/LNL/PTL variants can be enabled independently.

Do not modify `IntelMSR.p` for this. Adding PCI/MMIO reads there would enlarge the MSR module's scope and make review/security posture worse.

## 5. External API Design

Keep the external API narrow. Recommended: one IOCTL first.

```pawn
/// Read Intel client IMC/QCLK clock-ratio information.
///
/// @param in_size Must be 0
/// @param out [0] = ABI version, currently 1
/// @param out [1] = source enum
/// @param out [2] = ratio
/// @param out [3] = reference clock mode enum
/// @param out [4] = gear enum or 0 if unknown/not applicable
/// @param out [5] = raw register value low dword
/// @param out [6] = flags
/// @param out_size Must be 7
/// @return STATUS_SUCCESS if a supported source produced valid data.
///         STATUS_NOT_SUPPORTED if the CPU/platform/register source is unsupported.
///         Other NTSTATUS on PCI/MMIO read failure.
DEFINE_IOCTL_SIZED(ioctl_read_imc_clock, 0, 7)
```

Suggested enums:

```text
ABI_VERSION = 1

source:
  0 = Unknown
  1 = MCHBAR_MEMSS_PMA_BIOS_DATA
  2 = MCHBAR_SA_PERF_STATUS
  3 = PMT_MEMSS_QCLK_STATUS

reference clock mode:
  0 = Unknown
  1 = BCLK_DIV_3       // 33.333 MHz when BCLK is 100 MHz
  2 = BCLK             // 100 MHz when BCLK is 100 MHz
  3 = BCLK_MUL_4_DIV_3 // 133.333 MHz when BCLK is 100 MHz

gear:
  0 = Unknown/not applicable
  1 = Gear1
  2 = Gear2
  4 = Gear4

flags:
  bit 0 = value is expected to be static/locked after MRC
  bit 1 = value may be live/current
  bit 2 = source is validated on Panther Lake hardware
  bit 3 = source is a fallback and should be treated as experimental
```

Reason for returning ratio/ref/gear instead of only MHz:

- The CapFrameX side already has a measured bus clock.
- The ratio interpretation differs between sources (`33.33`, `100`, `133.33` MHz references).
- DDR/LPDDR display semantics need validation. For a UI sensor, "Memory Clock" usually means half effective DDR rate, not necessarily the raw controller/QCLK clock.
- Returning raw data makes validation possible without changing the module ABI.

Optional second IOCTL, only if useful for diagnostics:

```pawn
DEFINE_IOCTL_SIZED(ioctl_probe_imc_clock, 0, 7)
```

But avoid adding it unless needed. A single read IOCTL that returns `STATUS_NOT_SUPPORTED` is simpler and easier to review.

## 6. Register Strategy

### 6.1 MCHBAR Access

Common first step:

1. Check `get_arch() == ARCH_X64`.
2. Check `get_cpu_vendor() == CpuVendor_Intel`.
3. Read CPUID family/model/stepping.
4. Allowlist supported models.
5. Read host bridge PCI config `B0:D0:F0`, offsets `0x48` and `0x4C`.
6. Require `MCHBAREN` bit 0 set.
7. Derive MCHBAR base. For recent Intel client CFG/MEM docs the region is 128 KB aligned, base in bits 41:17.
8. Never write MCHBAR enable bits. If disabled, return `STATUS_NOT_SUPPORTED`.

Recommended helper shape:

```pawn
stock NTSTATUS:read_mchbar_base(&base)
stock NTSTATUS:read_mchbar_dword(offset, &value)
stock bool:is_supported_cpu_model(model)
```

Security rule: only `read_mchbar_dword` with compile-time constants or source-table offsets. Do not pass a user-controlled offset into physical memory access.

### 6.2 Source A: `MEMSS_PMA_CR_BIOS_DATA`

Candidate offset:

```text
MCHBAR + 0x13D10
```

Observed public documentation for Core Ultra 200H/200U:

- Register: `MEMSS_PMA_CR_BIOS_DATA`
- Offset: `0x13d10`
- Bit 8: `GEAR_TYPE`
- Bits 7:0: `QCLK_RATIO`
- Reference: `33.33 MHz`
- Description says it communicates locked Qclk ratio after memory init/training.

Implementation extraction:

```text
raw = read32(MCHBAR + 0x13D10)
ratio = raw & 0xFF
gear = ((raw >> 8) & 0x1) ? 4 : 2
refMode = BCLK_DIV_3
flags = STATIC_LOCKED | EXPERIMENTAL_UNTIL_PTL_VALIDATED
```

Important caveat:

- This may be "locked/configured QCLK" after MRC, not a live SAGV/current workpoint. If the desired sensor should show dynamic memory-clock changes, this source might be insufficient.
- It is still highly relevant for the user's requested "Multiplikator des IMCs", especially if CapFrameX wants the configured memory clock rather than dynamic SAGV.

### 6.3 Source B: `SA_PERF_STATUS`

Candidate offset:

```text
MCHBAR + 0x5918
```

Public older-client docs define:

- Bits 9:2: `QCLK_RATIO`
- Bit 10: `QCLK_REFERENCE`
- Reference bit 0: `133.33 MHz` (`BCLK * 4 / 3`)
- Reference bit 1: `100 MHz` (`BCLK`)

Implementation extraction if used:

```text
raw = read32(MCHBAR + 0x5918)
ratio = (raw >> 2) & 0xFF
refMode = ((raw >> 10) & 1) ? BCLK : BCLK_MUL_4_DIV_3
gear = Unknown
```

Important caveat:

- Intel's Core Ultra H/U CFG/MEM docs explicitly warn that `SA_PERF_STATUS.QCLK_RATIO` is not defined properly and recommend PMBAR offsets instead.
- Therefore do not enable this source for Panther Lake unless PTL documentation or hardware validation proves it is correct.
- It may remain useful for Alder/Raptor-style platforms in a future broader module.

### 6.4 Source C: PMT / PMBAR QCLK Status

Candidate offsets mentioned by Intel docs for Core Ultra H/U:

```text
PMBAR + 0x9618
PMBAR + 0x9620
PMBAR + 0x9628
```

Current status:

- These are referenced as alternatives for broken `SA_PERF_STATUS.QCLK_RATIO`.
- Exact PMBAR discovery and bitfield semantics still need to be confirmed from the platform's CFG/MEM/PMT documentation or Linux/Intel PMT sources.
- This may be the best candidate for a live/current clock source if `MEMSS_PMA_CR_BIOS_DATA` is only static.

Implementation guidance:

- Do not add PMT source until bitfields are documented.
- If added, implement as a separate source enum and keep fixed offsets only.
- Add PR references to Intel PMT docs/datasheet and any Linux source used.

### 6.5 Do Not Use QGV Point Tables Alone

Linux i915 uses QGV/DCLK point data, for example `MTL_MEM_SS_INFO_QGV_POINT_LOW(point)` at display MMIO offset `0x45710`, with DCLK in multiples of 16.666 MHz.

This enumerates available SAGV/QGV workpoints and timing data. It is not by itself the current active memory ratio. Do not use it for a live sensor unless there is a separately validated active QGV index/status register.

### 6.6 Do Not Use PMON/Freerunning Counters For This

Panther Lake uncore PMON and iMC freerunning support is relevant for bandwidth/counting, not for the requested IMC multiplier. Local `pantherlake_uncore.json` confirms only data/CAS events. Linux PTL uncore patches add PMON/freerunning plumbing but not an IMC ratio sensor.

## 7. CPU/Device Allowlist Plan

Start strict:

```text
Intel Family 6, Model 0xCC: Panther Lake
Intel Family 6, Model 0xD5: Panther Lake
```

Be careful with:

```text
0xCD, 0xCE, 0xCF
```

CapFrameX currently labels these as Panther Lake reserved, but local Intel perfmon `mapfile.csv` maps `GenuineIntel-6-CF` to Emerald Rapids. Do not blindly allowlist `0xCF` in the PawnIO module without independent confirmation. As of the 2026-05-01 local refresh, `0xD5` has stronger evidence for Panther Lake than `0xCD/0xCE/0xCF`.

Suggested implementation:

```pawn
stock bool:is_supported_model(model)
{
    switch (model)
    {
        case 0xCC:
        case 0xD5:
            return true;
        default:
            return false;
    }
    return false;
}
```

Later expand only after evidence:

- Public PTL-H/ULX/other model mapping.
- Real hardware dumps.
- Intel perfmon map update.
- Linux kernel CPUID/model additions.

## 8. Formula For CapFrameX Integration Later

Do not bake this into the PawnIO module. Keep it for the consumer wrapper.

Reference conversion from bus clock:

```text
if refMode == BCLK_DIV_3:
    refMHz = busClockMHz / 3
if refMode == BCLK:
    refMHz = busClockMHz
if refMode == BCLK_MUL_4_DIV_3:
    refMHz = busClockMHz * 4 / 3

qclkMHz = ratio * refMHz
```

Final "Memory Clock" UI value needs hardware validation:

- If `qclkMHz` matches HWiNFO/CPU-Z "DRAM Frequency" directly, use it.
- If `qclkMHz` matches effective MT/s, divide by 2 for the classic memory clock sensor.
- If gear affects conversion, use the returned gear enum and validate against DDR5/LPDDR5 systems.

Expected examples to test:

```text
DDR5-5600: UI memory clock usually about 2800 MHz
DDR5-6400: UI memory clock usually about 3200 MHz
LPDDR5X-8533: UI memory clock convention may differ; validate carefully
```

## 9. Implementation Steps In PawnIO.Modules

1. Fork and clone:

```powershell
git clone https://github.com/<your-user>/PawnIO.Modules.git
cd PawnIO.Modules
git remote add upstream https://github.com/namazso/PawnIO.Modules.git
git checkout -b intel-client-imc-clock
```

2. Inspect existing style:

```powershell
rg -n "DEFINE_IOCTL_SIZED|Doxygen|main\\(|get_arch|get_cpu_vendor" .
rg -n "pci_config_read_dword|virtual_read_dword|virtual_read_qword|MCHBAR" .
```

3. Add new file:

```text
IntelClientImcClock.p
```

4. Start from the `IntelMSR.p` license/header and IOCTL style.

5. Implement constants:

```pawn
#define PCI_BUS_HOST 0
#define PCI_DEV_HOST 0
#define PCI_FUNC_HOST 0
#define PCI_MCHBAR_LO 0x48
#define PCI_MCHBAR_HI 0x4C
#define MCHBAR_ENABLE 0x1
#define MCHBAR_MASK 0x000003FFFFFE0000 // verify Pawn cell width / syntax

#define MEMSS_PMA_CR_BIOS_DATA 0x13D10
#define SA_PERF_STATUS 0x5918
```

6. Implement CPUID/model helpers. If PawnIO has a `cpuid` primitive, use existing usage in modules. If not visible, inspect `include/pawnio.inc` and existing modules.

7. Implement `read_mchbar_base`.

8. Implement `try_read_memss_pma_bios_data`.

9. Leave `try_read_sa_perf_status` compiled but disabled for Panther Lake unless validated, or do not implement in first PR to keep it tight.

10. Export only `ioctl_read_imc_clock`.

11. `main()` gates:

```pawn
NTSTATUS:main()
{
    if (get_arch() != ARCH_X64)
        return STATUS_NOT_SUPPORTED;

    if (get_cpu_vendor() != CpuVendor_Intel)
        return STATUS_NOT_SUPPORTED;

    // Optional: do not hard fail on unsupported model in main() if users want
    // the module to load and then ioctl returns STATUS_NOT_SUPPORTED.
    return STATUS_SUCCESS;
}
```

Recommendation: let `main()` return success for Intel x64 and return `STATUS_NOT_SUPPORTED` in the IOCTL for unsupported models. This makes diagnostics easier and avoids load failures being indistinguishable from module errors.

12. Build with the repo's existing CI/build workflow. If the command is not obvious, inspect `.github/workflows` after cloning. Do not guess in the PR; document the exact command once known.

## 10. Minimal Pawn Pseudocode

This is only a shape, not copy/paste-ready.

```pawn
#include <pawnio.inc>

#define IMC_CLOCK_ABI_VERSION 1
#define IMC_CLOCK_SOURCE_MEMSS_PMA 1
#define IMC_REF_BCLK_DIV_3 1
#define IMC_GEAR_2 2
#define IMC_GEAR_4 4
#define IMC_FLAG_STATIC_LOCKED 0x1
#define IMC_FLAG_EXPERIMENTAL 0x8

#define PCI_MCHBAR_LO 0x48
#define PCI_MCHBAR_HI 0x4C
#define MEMSS_PMA_CR_BIOS_DATA 0x13D10

stock bool:is_supported_model(model)
{
    switch (model)
    {
        case 0xCC:
            return true;
    }
    return false;
}

stock NTSTATUS:read_mchbar_base(&base)
{
    new lo = 0;
    new hi = 0;
    new NTSTATUS:status = pci_config_read_dword(0, 0, 0, PCI_MCHBAR_LO, lo);
    if (!NT_SUCCESS(status))
        return status;

    status = pci_config_read_dword(0, 0, 0, PCI_MCHBAR_HI, hi);
    if (!NT_SUCCESS(status))
        return status;

    if ((lo & 1) == 0)
        return STATUS_NOT_SUPPORTED;

    base = ((hi << 32) | lo) & 0x000003FFFFFE0000;
    return STATUS_SUCCESS;
}

DEFINE_IOCTL_SIZED(ioctl_read_imc_clock, 0, 7)
{
    // 1. CPUID family/model check
    // 2. MCHBAR base
    // 3. read MEMSS_PMA_CR_BIOS_DATA
    // 4. fill output fields
}
```

Open questions before final code:

- Confirm Pawn cell width and 64-bit shifts/constants syntax.
- Confirm exact CPUID helper syntax in PawnIO modules.
- Confirm `virtual_read_dword(address, value)` argument order from `SmbusIntelSkylakeIMC.p`.
- Confirm NTSTATUS constants available in `pawnio.inc`.

## 11. Security Review Checklist

Must pass before opening PR:

- No user-controlled physical address.
- No user-controlled PCI bus/device/function/register.
- No writes to PCI config, MMIO, MSR, IO ports, or MCHBAR enable.
- No dynamic arrays.
- No loops over unbounded hardware ranges.
- Unsupported CPU/platform returns `STATUS_NOT_SUPPORTED`.
- Invalid/unexpected ratio `0` returns `STATUS_NOT_SUPPORTED` or equivalent failure, not success.
- Doxygen comments describe every output word.
- Module remains read-only and does not replace a first-party driver if one exists.
- PR description explicitly says this is not a generic WinRing0 replacement and intentionally exposes only high-level fixed-register reads.

## 12. Validation Plan

### 12.1 Static/build validation

- Build all modules in PawnIO.Modules with CI-equivalent command.
- Confirm no warnings.
- Confirm generated `IntelClientImcClock.bin` exports only expected IOCTL string(s).
- Optional binary string check:

```powershell
$bytes = [IO.File]::ReadAllBytes(".\IntelClientImcClock.bin")
$text = [Text.Encoding]::ASCII.GetString($bytes)
[regex]::Matches($text, "ioctl_[A-Za-z0-9_]+") | % Value | Sort-Object -Unique
```

Expected:

```text
ioctl_read_imc_clock
```

### 12.2 Negative runtime validation

Run on unsupported systems:

- AMD CPU: module load or IOCTL returns `STATUS_NOT_SUPPORTED`.
- Older Intel client: IOCTL returns `STATUS_NOT_SUPPORTED` unless explicitly allowlisted.
- Intel server with `Family 6 Model 0xCF`: must not be accepted as Panther Lake.

### 12.3 Panther Lake runtime validation

On real PTL hardware, collect:

```text
CPU CPUID family/model/stepping
Host bridge device ID
MCHBAR low/high raw values
MCHBAR base
MEMSS_PMA_CR_BIOS_DATA raw value
ratio
gear
refMode
computed qclkMHz using BCLK
BIOS configured memory data rate
Windows Task Manager memory speed
HWiNFO/CPU-Z memory clock or data rate
CapFrameX/LHM bus clock
```

Test cases:

- Idle.
- CPU memory stress.
- iGPU/display load if present.
- AC vs battery if laptop.
- Different memory types if available: DDR5 SO-DIMM, LPDDR5/LPDDR5X.

Pass criteria for static/configured clock:

- Ratio-derived value matches configured memory clock/data-rate convention within expected BCLK tolerance.
- Gear field matches platform memory mode if independently visible.

Pass criteria for live/current clock:

- Source changes if the platform dynamically changes memory workpoints under SAGV and a reference tool shows it.
- If it does not change, document source as static/configured, not live/current.

## 13. PR Content Plan

Suggested title:

```text
Add Intel client IMC clock ratio module
```

Suggested summary:

```text
Adds a read-only Intel client IMC/QCLK clock-ratio module for tools that need
memory-clock calculation on platforms where uncore ratio no longer matches IMC
ratio. The module exposes only a high-level fixed-register IOCTL and does not
provide generic PCI/MMIO access.
```

Suggested PR bullets:

- Adds `IntelClientImcClock.p`.
- Adds `ioctl_read_imc_clock`.
- Reads allowlisted Intel client MCHBAR register(s) only.
- Starts with strict Panther Lake model gating.
- Returns ratio/reference/gear/raw data for consumers to calculate memory clock with their measured BCLK.
- Avoids WinRing0-style generic read/write primitives.

PR references to include:

- PawnIO.Modules contribution guidelines: `https://github-wiki-see.page/m/namazso/PawnIO.Modules/wiki/Contribution-guidelines`
- PawnIO module usage: `https://github-wiki-see.page/m/namazso/PawnIO.Modules/wiki/Using-PawnIO-Modules`
- PawnIO.Modules repo: `https://github.com/namazso/PawnIO.Modules`
- Intel Core Ultra 200H/200U `MEMSS_PMA_CR_BIOS_DATA`: `https://edc.intel.com/content/www/vn/vi/design/publications/core-ultra-200h-and-200u-series-processors-cfg-and-mem-registers/memss-pma-bios-data-register-memss-pma-cr-bios-data-offset-13d10/`
- Intel Core Ultra H/U `SA_PERF_STATUS` warning about QCLK ratio: `https://edc.intel.com/content/www/br/pt/design/publications/14th-generation-core-processors-cfg-and-mem-registers/system-agent-performance-status-sa-perf-status-0-0-0-mchbar-pcu-offset-5918/https%3A%2525252F%2525252Fedc.intel.com%2525252Fcontent%2525252Fwww%2525252Fbr%2525252Fpt%2525252Fdesign%2525252Fpublications%2525252F14th-generation-core-processors-cfg-and-mem-registers%2525252Fsystem-agent-performance-status-sa-perf-status-0-0-0-mchbar-pcu-offset-5918%2525252F/`
- Intel MCHBAR base docs for Core Ultra 200H/200U: `https://edc.intel.com/content/www/kr/ko/design/publications/core-ultra-200h-and-200u-series-processors-cfg-and-mem-registers/mchbar-base-address-register-mchbar-0-0-0-pci-offset-48/`
- Linux PTL uncore support: `https://www.spinics.net/lists/kernel/msg5759913.html`
- Linux PTL iMC freerunning support: `https://www.spinics.net/lists/kernel/msg5759911.html`
- Linux i915 QGV/DCLK code reference: `https://codebrowser.dev/linux/linux/drivers/gpu/drm/i915/display/intel_bw.c.html`
- Linux i915 MTL QGV register definitions: `https://codebrowser.dev/linux/linux/drivers/gpu/drm/i915/display/intel_display_regs.h.html`

## 14. Later CapFrameX Integration Plan

After upstream PawnIO.Modules PR or local test binary:

1. Add embedded resource:

```xml
<EmbeddedResource Include="Resources\PawnIo\IntelClientImcClock.bin" />
```

2. Add wrapper:

```text
source/LibreHardwareMonitorLib/PawnIo/IntelClientImcClock.cs
```

3. Wrapper loads module from resource like `IntelMsr.cs`.

4. Constructor calls `ioctl_read_imc_clock` once. If unsupported, do not create sensor.

5. `IntelCpu.cs` keeps old `SupportsUncoreMemoryClock` unchanged for old platforms.

6. Add a second memory-clock source path for new IMC ratio module:

```text
old path: MSR_UNCORE_PERF_STATUS for SNB through CML client
new path: PawnIO IntelClientImcClock ratio/ref/gear for PTL+ validated platforms
```

7. Use the same `newBusClock` already calculated in `IntelCpu.Update`.

8. If source is static/locked, sensor can still update with bus clock, but ratio does not need to be reread every cycle. If source is live/current, reread periodically.

9. Update Panther Lake CPUID handling before enabling the new sensor:

```text
add/verify: Family 6 Model 0xD5 -> Panther Lake
re-check: 0xCF should not be treated as Panther Lake if Intel perfmon continues mapping it to Emerald Rapids
```

## 15. Open Risks / Decisions

- `MEMSS_PMA_CR_BIOS_DATA` may be a static MRC-locked ratio, not live current SAGV ratio.
- Public Panther Lake CFG/MEM register docs were not found during this research; the closest public docs are Core Ultra 200H/200U and Core Ultra H/U.
- `SA_PERF_STATUS.QCLK_RATIO` must not be used for Panther Lake unless validated, because Intel documents it as not properly defined on Core Ultra H/U.
- PMT/PMBAR offsets look promising but need exact bitfield documentation before implementation.
- CPUID model mapping must stay strict. Allow `0xCC` and `0xD5` for initial PTL coverage based on local Intel perfmon V1.05, but do not allow `0xCF` as Panther Lake without confirmation.
- Need real Panther Lake hardware to validate ratio/ref/gear conversion and UI semantics.

## 16. Handoff Prompt For Next Codex Instance

Use this prompt when switching instances:

```text
We are implementing an upstream PR for https://github.com/namazso/PawnIO.Modules.
Goal: add a safe read-only Intel client IMC/QCLK clock-ratio module for Panther Lake memory-clock calculation, without WinRing0-style generic access.

Read local plan first:
D:\Code\CapFrameX\dev-plans\PantherLake_IMC_Clock_PawnIO_PR_Plan.md

Key constraints:
- Do not expose generic PCI/MMIO/MSR read/write IOCTLs.
- Add a dedicated module, likely IntelClientImcClock.p.
- Export only ioctl_read_imc_clock with fixed output fields.
- Strictly allowlist Intel Family 6 Model 0xCC and 0xD5 initially.
- Read MCHBAR from PCI B0:D0:F0 offsets 0x48/0x4C, require enable bit, never write it.
- First candidate register: MCHBAR + 0x13D10 MEMSS_PMA_CR_BIOS_DATA, bits 7:0 ratio, bit 8 gear, ref BCLK/3.
- Treat source as experimental until validated on real PTL hardware.
- Do not enable SA_PERF_STATUS for PTL unless validated; Intel docs warn QCLK_RATIO is not defined properly on Core Ultra H/U.
- Include Doxygen-style docs and no dynamic arrays.

Start by cloning PawnIO.Modules, inspecting IntelMSR.p and SmbusIntelSkylakeIMC.p, then implement the new module and run the repo's build workflow.
```

## 17. Immediate Next Actions

1. Clone `PawnIO.Modules` in a separate workspace.
2. Inspect `include/pawnio.inc` and existing modules for exact helper signatures.
3. Confirm Pawn cell width / 64-bit constant handling.
4. Implement a minimal `IntelClientImcClock.p` with one IOCTL and MEMSS-PMA source only.
5. Build and fix warnings.
6. Run negative tests on non-PTL hardware.
7. Get PTL hardware dump to validate raw register and formula.
8. Open PR with explicit security rationale and datasheet/source references.
