# CapFrameX 2.0 Linux Telemetry - Validation Plan

Last updated: 2026-09-20

> **Question:** the Linux service needs a telemetry source (CPU/GPU load, clocks, temperatures,
> power, memory, fans, throttling). Is the **Linux kernel** suitable as that source?
>
> **Short answer: yes - as the primary source, not as the only one.** The kernel's stable
> user-space interfaces cover CPU, memory, AMD and Intel GPUs and mainboard sensors without any
> extra driver of our own, without root for most values, and at negligible cost. Three gaps are
> structural and need a second mechanism each:
> 1. **NVIDIA proprietary driver** - exposes practically no telemetry through the kernel
>    (no hwmon, no DRM sysfs metrics). **NVML** (`libnvidia-ml.so.1`, user space, no root) is mandatory.
> 2. **CPU package power** - `powercap`/RAPL `energy_uj` has been readable by root only since the
>    PLATYPUS fix (CVE-2020-8694, kernel 5.10 and stable backports). Needs a narrow privileged helper
>    or an explicit opt-in permission change.
> 3. **Mainboard sensors** (fans, voltages, VRM/chipset temperatures) - depend on Super-I/O/EC
>    drivers being present (`nct6775`, `it87`, `asus-ec-sensors`, ...; newer chips often
>    out-of-tree) and come as unlabelled channels (`in0`, `fan2`).
>
> Nothing comparable to PawnIO is needed for the baseline: where Windows needs a signed kernel
> driver to read MSRs/SMU/Super-I/O, Linux already ships those drivers and publishes the results as
> files. This plan turns that assessment into measured facts before `Linux.Telemetry` is written.

## 1. Candidate sources

### 1.1 Kernel interfaces (primary)
| Interface | Delivers | Access | Notes to validate |
|---|---|---|---|
| **hwmon** `/sys/class/hwmon/hwmon*/` | temperatures, fans, voltages, power, energy; per driver: `k10temp`/`zenpower` (AMD CPU Tctl/Tccd), `coretemp` (Intel per-core), `amdgpu`, `i915`/`xe`, `nvme`, `spd5118`/`jc42` (DIMM temperature), Super-I/O, `asus-ec-sensors`, `asus_wmi_sensors`, `dell-smm-hwmon`, ... | world-readable | `hwmonN` numbering changes across boots - identify by `name` + device path; labels via `*_label` where the driver provides them |
| **cpufreq** `/sys/devices/system/cpu/cpu*/cpufreq/scaling_cur_freq` | per-core frequency | world-readable | on `amd-pstate`/`intel_pstate` this is the *requested/estimated* frequency; effective clock needs APERF/MPERF (MSR) - compare against `turbostat` |
| **`/proc/stat`**, `/proc/meminfo`, `/proc/pressure/*` | per-core load, memory, PSI stall info | world-readable | trivial, exact |
| CPU topology `/sys/devices/system/cpu/cpu*/topology/`, `cache/` | cores, SMT siblings, P/E-core type (`cpu_capacity`, `/sys/devices/cpu_core`, `cpu_atom`), CCD mapping via L3 sharing | world-readable | needed for the per-core overlay module grouping |
| **powercap / RAPL** `/sys/class/powercap/intel-rapl:*/energy_uj` | CPU package/core/uncore/DRAM energy -> power; works on Intel and on AMD Zen (same driver) | **root only** | see T5 |
| **amdgpu sysfs** `/sys/class/drm/card*/device/` | `gpu_busy_percent`, `mem_busy_percent`, `mem_info_vram_used/total`, `mem_info_gtt_*`, `pp_dpm_sclk/mclk`, `current_link_speed/width`, hwmon `power1_average`/`power1_input` (PPT), `temp1..3` (edge/junction/mem), `fan1_input`, `freq1_input`; binary **`gpu_metrics`** (one read = consistent snapshot incl. throttle status, per-rail power on some ASICs, average vs. current activity) | world-readable | `gpu_metrics` layout is versioned (v1.x dGPU, v2.x/v3.x APU) - parser per version; read cost and side effects (T3) |
| **Intel xe / i915** | hwmon `power1_*`/`energy1_input`, `temp*`, `fan*` (Arc); frequency files under `gt/gt*/` (`rps_act_freq_mhz` / xe `freq0/act_freq`); engine busyness only via fdinfo or PMU | mostly world-readable; PMU needs `perf_event_paranoid` <= 0 or `CAP_PERFMON` | xe vs. i915 differences; Arc Battlemage on xe |
| **DRM fdinfo** `/proc/<pid>/fdinfo/<fd>` (`drm-engine-*`, `drm-cycles-*`, `drm-memory-*`, `drm-total-*`) | **per-process** GPU engine time and memory - amdgpu, i915, xe, msm, panfrost, (nouveau/NVK partially) | same uid as the target process | the closest Linux equivalent to PresentMon's per-process GPU busy; key for frame-metric gap #2 (Linux plan 3.3) |
| **MSR** `/dev/cpu/*/msr` | anything the CPU has (effective clock, per-core power on AMD, throttle reasons, voltage/VID) | root + `msr` module; **blocked by kernel lockdown** (Secure Boot on many distros) | only if a must-have metric has no other source |
| DMI `/sys/class/dmi/id/*`, `/proc/cpuinfo`, PCI IDs | system info strings | world-readable (serials excluded) | for `ISystemInfoProvider` |
| Tracepoints / eBPF (`drm`, `gpu_scheduler`, `dma_fence`, `power:cpu_frequency`) | event-level GPU scheduling, frequency changes | root / `CAP_BPF` + `CAP_PERFMON` | research only - not for 2.0 |

### 1.2 Vendor user-space libraries (complementary)
| Library | Role |
|---|---|
| **NVML** (`libnvidia-ml.so.1`) | the NVIDIA source: utilisation, clocks, temperature, power, VRAM, fan, PCIe, **throttle reasons**, per-process utilisation/memory. Ships with the driver; load with `dlopen`, never link. |
| Vulkan (`VK_EXT_memory_budget`, device properties) | vendor-neutral VRAM budget/usage fallback and GPU identification (the salvaged `VulkanGpuEnumerator`) |
| `libsensors` (lm-sensors) | not needed - it is a wrapper over hwmon. Its per-board **label configuration database** is worth evaluating as input for naming Super-I/O channels (licence check: LGPL library, configs are separate files) |

### 1.3 Rejected as sources
- **LibreHardwareMonitor on Linux**: its Linux support is rudimentary and its model (ring-0 port/MSR
  access) needs root for everything the kernel already exports unprivileged.
- **Parsing CLI tools** (`sensors`, `nvidia-smi`, `radeontop`, `intel_gpu_top`): process spawn per
  sample, unstable text formats. They are *reference instruments* for this validation, not sources.
- **Own kernel module**: out-of-tree module = DKMS, Secure Boot signing, distro matrix. The reason
  PawnIO exists on Windows does not exist here.

## 2. What has to be proven

| # | Question | Pass criterion |
|---|---|---|
| T1 | **Coverage**: for each well-known metric (architecture plan 3.2), which source delivers it on which hardware/driver/kernel? | coverage matrix filled for the hardware matrix (section 4); every gap has a named fallback or is accepted as "unavailable + reason" |
| T2 | **Correctness**: do values match reference instruments? | load/clock/temp/VRAM within tolerance of `turbostat`, `nvidia-smi`, `amdgpu_top`, `intel_gpu_top`, MangoHud on the same run; CPU/GPU power within 5 % of the reference (and against a PMD/Benchlab measurement on the Windows side of the same dual-boot machine where available) |
| T3 | **Cost and perturbation**: what does sampling cost, and does it disturb the game? | read latency p50/p99 per source over 10k reads; service CPU < 0.5 % of one core at 4 Hz full set; **no measurable frametime impact**: A/B capture with the CapFrameX layer itself (polling off / 1 Hz / 4 Hz / 10 Hz) - 1 % low and stutter count unchanged within run-to-run noise. Specifically check `amdgpu` `gpu_metrics`/`pp_dpm_*`/power reads (SMU round trip) and NVML power queries, which are known suspects for periodic hitches at high poll rates |
| T4 | **Update rate**: how often do values really change? | measured refresh interval per source -> minimum sensible poll interval per reader (no point polling a 1 Hz sensor at 10 Hz) |
| T5 | **Privileged metrics**: which must-have metrics need root, and how do we get them safely? | decision between: (a) socket-activated root helper with a file allowlist + polkit (Linux plan section 5) - **expected outcome**; (b) packaged udev/tmpfiles rule making `energy_uj` group-readable - simpler, but re-opens the PLATYPUS side channel for that group, must be an explicit user opt-in; (c) do without. Also: is anything beyond RAPL needed (effective clock via MSR, AMD per-core power)? If `scaling_cur_freq` is within tolerance of `turbostat` Bzy_MHz under gaming load, MSR access is dropped entirely |
| T6 | **Per-process GPU time** via fdinfo (and NVML on NVIDIA): is it precise and timely enough to derive a per-frame or per-interval "GPU busy" comparable to PresentMon's? | delta of `drm-engine-gfx` over a capture vs. GPU-bound/CPU-bound reference scenes behaves like PresentMon GPUBusy on the same machine under Windows (qualitatively: ~100 % of frametime when GPU-bound, clearly lower when CPU-bound); sampling cost acceptable; works for Proton titles (the game's DRM fd lives in the wine/Proton process tree - find the right pid) |
| T7 | **Identity and stability**: stable sensor ids across reboots, suspend/resume, GPU hot-unplug, driver reload, multi-GPU (iGPU + dGPU), which GPU renders the captured game | ids based on PCI address/hwmon device path + name; GPU of the captured process resolved from the layer's `VkPhysicalDeviceProperties`/PCI bus info |
| T8 | **Throttling signals** for the overlay's throttle module | amdgpu `gpu_metrics.throttle_status`, NVML `ClocksThrottleReasons`, Intel `thermal_throttle` counters in `/sys/devices/system/cpu/cpu*/thermal_throttle/`, AMD CPU: PROCHOT only via MSR -> likely unavailable; documented per vendor |
| T9 | **Environments**: containers and immutable systems | service runs on the host, so sysfs is fully visible; verify SteamOS/gamescope session, Bazzite, and that nothing is needed from inside the Proton/Flatpak sandbox |

## 3. Method

### 3.1 Probe tool - `cfx-telemetry-probe`
Small console tool, .NET `net10.0` (same runtime and file-access pattern the service will use, so
the latency numbers are representative), located at `CapFrameX.Service.Linux/tools/cfx-telemetry-probe`.
- Enumerates every candidate source of 1.1/1.2, records presence, permissions, driver name and
  version, kernel version, lockdown state (`/sys/kernel/security/lockdown`), session type.
- Reads each value N times: latency histogram, observed change interval, value range.
- File access pattern under test: keep the fd open and `pread` at offset 0 vs. open/read/close per
  sample - sysfs attributes differ in whether they tolerate re-reads on an open fd.
- Optional `--with-reference`: runs `turbostat`, `nvidia-smi --query-gpu`, `amdgpu_top -J`,
  `intel_gpu_top -J` in parallel (when installed / root available) and logs them time-aligned.
- Output: one JSON report + a short Markdown summary per machine. Reports are collected in
  `dev-plans/validation/linux-telemetry/`.
- The salvaged `SysfsReader`/`VulkanGpuEnumerator` code is the starting point; the probe is also the
  seed of the `Linux.Telemetry` reader tests (its JSON reports become fixtures: a directory snapshot
  of the relevant sysfs files per machine lets readers be unit-tested on any OS, including the
  Windows CI runner).

### 3.2 Perturbation test (T3)
Fixed, repeatable Vulkan workload (a benchmark scene with a locked camera path; one native, one
Proton title), five runs per polling configuration, captured with the CapFrameX layer. Compare
1 % low average, 0.1 % low, stutter count and the frametime spectrum (a periodic spike at the poll
interval shows up as a peak). The test doubles as the first real end-to-end use of L1 capture.

### 3.3 Community data
The hardware matrix cannot be covered in-house. Publish the probe as a single-file download with a
"paste your report" issue template; reports contain no serial numbers or user names (strip DMI
serials, home paths, hostnames - verified by a test).

## 4. Matrix

| Axis | Values |
|---|---|
| CPU | AMD Zen 3 / Zen 4 / Zen 5 (incl. X3D, dual-CCD), Intel 12th-14th gen hybrid, Arrow Lake; one laptop each vendor |
| GPU | AMD RDNA2 / RDNA3 / RDNA4 (amdgpu), APU (Steam Deck or Phoenix/Strix - `gpu_metrics` v2/v3); Intel Arc Alchemist (i915) and Battlemage (xe); NVIDIA Turing-Blackwell on the proprietary driver **and** on the open kernel modules; one iGPU + dGPU system |
| Kernel | oldest supported LTS of the distro matrix, current stable |
| Distro / session | Ubuntu LTS (GNOME/Wayland), Fedora (GNOME/Wayland, Secure Boot + lockdown on), Arch (KDE/Wayland), one X11 session, SteamOS or Bazzite (gamescope) |
| Board sensors | one board each with `nct6775`-family, `it87`-family (incl. a chip needing the out-of-tree driver), `asus-ec-sensors` |

## 5. Expected design outcome (to be confirmed by the results)

- Reader set as listed in the Linux service plan, section 4, in
  `CapFrameX.Service.Linux/src/CapFrameX.Service.Linux.Telemetry`, implementing `ITelemetrySource`
  from `CapFrameX.Service.Shared`; each reader self-describes availability + reason; nothing fails
  start-up. The well-known metric vocabulary it maps onto is defined in the shared contracts.
- Default install = unprivileged: everything except CPU package power (and possibly effective
  clock). Optional helper package adds those.
- Poll scheduler with per-reader minimum intervals from T4 and demand-driven activation
  (architecture plan, section 8): a reader whose metrics nobody consumes is never read.
- Sensor naming: driver labels where present; a shipped, community-extendable mapping file for
  Super-I/O channels per board (DMI board name -> channel labels); raw names otherwise.
- Minimum supported kernel derived from T1 (expected: 6.1 LTS or newer; older kernels lose xe,
  parts of `gpu_metrics`, fdinfo keys).

## 6. Phases

| Phase | Scope | Exit |
|---|---|---|
| **V0** | probe tool (3.1), report format, anonymisation test | runs on the three in-house machines |
| **V1** | T1, T4, T7, T9 on in-house hardware; publish probe for community reports | coverage matrix v1 |
| **V2** | T2 + T3 (needs L1 capture from the Linux service plan) | correctness and perturbation report; minimum poll intervals fixed |
| **V3** | T5, T6, T8 | helper decision; fdinfo GPU-busy decision; throttle-signal table |
| **V4** | validation report in `dev-plans/validation/linux-telemetry/REPORT.md`; update Linux service plan section 4 and the capability list | report accepted -> phase L2 of the Linux service plan starts |

V0-V1 need nothing but a Linux machine and can start immediately, in parallel with everything else.

## 7. Risks

| Risk | Mitigation |
|---|---|
| `amdgpu` power/`gpu_metrics` polling causes periodic hitches on some ASIC/firmware combinations | T3 decides interval and which file to prefer; demand-driven polling means the cost exists only while the value is shown or logged |
| RAPL unavailable without root -> "CPU power" missing in the default install, users compare with Windows | helper package; clear reason text; do not estimate power from load |
| NVML changes behaviour across driver branches; open kernel modules differ | `dlopen` + per-function availability checks; matrix covers both module flavours |
| Super-I/O coverage is a moving target | treated as best-effort with community mapping file; not a release criterion |
| Kernel lockdown blocks MSR on Secure Boot systems | T5 aims to eliminate MSR needs altogether |
| Community reports leak identifying data | anonymisation is a tested feature of the probe, not a convention |
