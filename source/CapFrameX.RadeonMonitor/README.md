# CapFrameX Radeon SMU Monitor

This is a standalone x64 WPF test application for Radeon monitoring. It reads
the public SMU metrics table through PawnIO when the table address is available
and automatically falls back to the AMD driver's ADL PMLog interface on RDNA2,
RDNA3, and RDNA4. RDNA4 refreshes the driver-owned public buffer through a
lightweight ADL query before PawnIO copies it; no WDDM/D3D polling is used.
On Navi 21, RDNA3, and RDNA4, module ABI 5 can query, refresh, and
read the private 8-KiB monitoring-tool table through a fixed SMU mailbox
protocol. The
exact Navi 21 `0x003A0010` layout is decoded into 38 sensors and the Navi 31
`0x004E000C` layout into 66 sensors. RDNA4 tool-table versions
`0x00660001..0x00660006` supply effective GFX and FCLK values; ADL and the
public table supply complementary driver values.
Direct Navi 21 SMUIO SVI telemetry remains available as a fallback for an
unknown private-table layout, without using the GPU I2C bus.
It is independent from the CapFrameX ADLX monitoring path and does not install
or modify CapFrameX configuration.

The parser supports these table families:

- RDNA2 / SMU11: `SmuMetrics_t`, `SmuMetrics_V2_t`, `SmuMetrics_V3_t`, and
  `SmuMetrics_V4_t` from `smu11_driver_if_sienna_cichlid.h`.
- RDNA3 / SMU13: the 244-byte SMU 13.0.0 table layout used here for
  Navi 31/32 and the 240-byte SMU 13.0.7 layout used by Navi 33.
- RDNA4 / SMU14: the 260-byte `SmuMetrics_t` from
  `smu14_driver_if_v14_0.h`.

When the firmware exposes a readable table address, the application shows
clocks, activity, voltage/current planes, board and socket power,
the extra temperature channels, fan data, per-cause throttling percentages,
PCIe state/busy data, energy and D3Hot counters, and SmartShift fields. Every
decoded value is accompanied by its raw value and the complete DWORD table can
be inspected in the UI.

For numeric metrics, the grid tracks the current, minimum, maximum, and
arithmetic mean values across the active monitoring session. Starting polling,
loading another module, changing the selected generation or table layout, and
switching between raw PawnIO and ADL PMLog data start a new statistics session.
Successive **Read once** operations accumulate until one of those resets.
Identifiers and bit fields that have no meaningful numeric average show only
their current value. **Reset stats** starts a new statistics interval from the
currently displayed values without stopping active polling.
Redundant firmware average-clock and moving-average rows are omitted from the
sensor list. Unique voltage, current, power, activity, temperature, and fan
fields remain available as current samples without an `Average` label; the
application calculates their session statistics itself.

On systems where C2PMSG_80/81 do not expose a usable table address, or where
the raw read reports `STATUS_DEVICE_NOT_READY` or rejects a transient
out-of-aperture pointer, the
[AMD Display Library](https://github.com/GPUOpen-LibrariesAndSDKs/display-library)
PMLog fallback returns the sensor set supported by the installed Radeon driver.
These expected raw-read results use a non-throwing status path, so switching to
ADL does not raise a first-chance `PawnIoException` in the debugger.
The application matches the ADL adapter to the PawnIO-selected GPU by PCI bus,
device, and function. Supported values are displayed even when their current
value is zero, and the raw ADL sensor IDs and integer values can be inspected
in the UI. This path commonly provides clocks, utilization, voltages,
temperatures, fan data, PCIe state, board power, and per-cause throttling
percentages. Fields that exist only in `SmuMetrics_t`, such as its counters and
serial fields, remain unavailable without the raw table address.

### Private SMU tool table

The ABI 5 module uses the private monitoring-tool
mailbox: C2PMSG_72 is the command register, C2PMSG_96 the response, and
C2PMSG_98 the argument/result register. It sends
only the four compile-time services needed to query the version (`0x14`),
query the high and low table address (`0x07`/`0x08`), and refresh the table
(`0x09`, selector `4`). No message ID or address is supplied by the
application. These service IDs belong to AMD's private PMFW monitoring
protocol and are not declared by the public Linux interface. The module keeps
them in a fixed allowlist; the public C2PMSG register definitions and the live
validation records are linked under **Implementation references** below.

The returned GPU/MC address is accepted only for allowlisted Navi 21, RDNA3,
or RDNA4
PCI IDs, only for a known table family, and only when the complete 8-KiB range
fits both the hardware framebuffer interval and the selected BAR0 aperture.
Navi 21 normally uses framebuffer registers `0xE54C/0xE550`; RDNA3 and RDNA4
use `0xE4D4/0xE4D8`. Version, address query, refresh, validation, and copy occur
inside one IOCTL. Mailbox responses are polled with at most 8,096 immediate
MMIO reads, matching the existing `RyzenSMU.p` pattern. There is no short
`microsleep`: PawnIO implements that native with `KeDelayExecutionThread`, so
its actual delay is scheduler-granularity rather than a 100-microsecond polling
interval. If a cold-start refresh returns a uniform or otherwise
invalid table, the module repeats the fixed refresh-and-copy sequence up to
five times with a 10-ms delay. The module does not scan VRAM or replace the
driver's allocation. Its fixed 512-KiB BAR5 mapping covers all mailbox and
framebuffer-bound registers used by this path and fits the RDNA4 aperture.

Public metrics and the private tool table now use the same address translation:
the module reads the generation-specific `MC_VM_FB_LOCATION_BASE/TOP`, verifies
the complete GPU/MC range, subtracts that hardware framebuffer base, and adds
the resulting offset to BAR0. There is no separate hard-coded VRAM MC base for
the public path.

The application serializes each low-level PCI/SMN operation with the
cross-process `Global\Access_PCI` mutex also used by established hardware
monitoring tools. A five-second acquisition timeout turns contention into a
reported unavailable sample, allowing the ADL fallback to remain usable rather
than issuing overlapping SMU mailbox transactions.

The application has verified maps for the tested full versions `0x003A0010`
(private layout 6, Navi 21) and `0x004E000C` (private layout 7, Navi 31). The
Navi 21 map exposes 38 rows. The Navi 31 map exposes 66 rows: edge, hotspot,
memory-junction, VR, GCD, and six MCD temperatures; four voltage/current rails;
five rail powers plus TGP, TBP, peak power, and sustained PPT; front-end,
memory, SoC, FCLK, six shader, and six effective-shader clocks; fan speed; GPU
utilization; absolute PPT/TDC limits; and PPT/TDC/thermal-limit percentages.

The offsets were checked against live values on the installed RX 6800 XT and
RX 7900 XTX. Layout 7 has per-shader clocks but no aggregate GPU clock, so the
application retains the inexpensive ADL `GFX clock`. ADL also supplies fan
duty and memory activity when supported. No WDDM/D3D polling is added.

All mapped rows are kept in the grid on every sample; a non-finite or
out-of-range field displays an em dash and does not alter its running
statistics. Other recognized private table versions can still be copied into
the raw dump but are not decoded under a nearby layout's names. In that case
the UI retains ADL monitoring (and direct SVI on Navi 21) and reports the
unsupported full version explicitly instead of exposing guessed offsets.

### Direct Navi 21 SVI telemetry

For Navi 21 PCI IDs, module ABI 5 also reads the fixed SMUIO register range
`0x5A00C..0x5A018`. These four words contain the physical SVI0 plane 1,
SVI0 plane 0, SVI1 plane 0, and SVI1 plane 1 telemetry. Voltage uses AMD's
published SVI VID conversion, `1.55 - VID * 0.00625 V`.

The semantic rail mapping and IDD scale are board-specific fields from the
ATOM `SMC_DPM_Info` table. The first calibrated profile covers the tested
PowerColor subsystem `148C:2406` with VBIOS family
`020.001.000.043.000000`:

| Rail | Physical plane | Maximum current |
| --- | --- | ---: |
| VDDCR_GFX | SVI0 plane 0 | 714 A |
| VDDCR_SOC | SVI0 plane 1 | 99 A |
| VDDIO | SVI1 plane 0 | 128 A |
| VDDCI_MEM | SVI1 plane 1 | 64 A |

Current is decoded as `IDD * maximum current / 255 + offset`; all four offsets
are zero on this profile. Rail power is derived from the simultaneous voltage
and current values. The IDD field is only eight bits, so instantaneous current
and power are visibly quantized. The decoded values were validated on the
installed RX 6800 XT.

The SVI sensor rows remain stable across transient power-state samples. When a
plane briefly reports an invalid VID, its current is still decoded from IDD,
while voltage and derived power display `—` for that sample. Invalid samples
are not included in the statistics, and the existing minimum, maximum, and
average remain visible until another valid sample arrives or statistics are
reset.

An unprofiled Navi 21 board gets physical-plane voltage labels only. The SVI
fallback does not guess its rail mapping or current scale. It deliberately
avoids GPU I2C/PMBus access; VR temperatures shown for `0x003A0010` come from
the private SMU table rather than direct controller access.

## Build the test application

From the repository root:

```powershell
dotnet build source\CapFrameX.RadeonMonitor\CapFrameX.RadeonMonitor.csproj -c Debug -p:Platform=x64
```

`PawnIOLib.dll` is copied from the existing `LibreHardwareMonitorLib\PawnIo`
directory into the application output. The application does not install a
driver. Its bundled module has no PawnIO module signature, so the test system
must already run a development build of PawnIO 2.x compiled with
`PAWNIO_UNRESTRICTED=ON`. Windows test-signing requirements still apply to that
driver package. The normal signed driver under `LibreHardwareMonitorLib\PawnIo`
correctly rejects this development module and is therefore not copied into the
application output.

## Compile the PawnIO module

[`PawnIO/RadeonSMU.p`](PawnIO/RadeonSMU.p) retains the upstream
LGPL-2.1-or-later license; the corresponding license text is stored at
[`../LibreHardwareMonitorLib/Resources/PawnIO/COPYING`](../LibreHardwareMonitorLib/Resources/PawnIO/COPYING).

The compiled [`PawnIO/RadeonSMU.bin`](PawnIO/RadeonSMU.bin) is included in the
project and copied to the application output with the same name. That is the
module path selected by default; the file picker and first command-line
argument can still override it.

To regenerate the module, use the Pawn compiler and `include` directory from
`PawnIO.Modules`. From the `PawnIO` source directory, a matching command is:

```powershell
& 'C:\Program Files (x86)\Pawn\bin\pawncc.exe' RadeonSMU.p `
    -iE:\Code\CX.PawnIO.Modules\include -C64 -p
& 'C:\Program Files\PawnIO\PawnIOUtil.exe' sign RadeonSMU.amx RadeonSMU.bin
Remove-Item RadeonSMU.amx
```

The include path is only an example; point it at the local
`PawnIO.Modules\include` directory. Calling `PawnIOUtil sign` without a key
adds the zero-length signature header required by `pawnio_load`; loading that
module requires the unrestricted development driver. The application also
adds this header in memory when a raw `.amx` override is selected.

## Module ABI 5

The module source is based on
[PawnIO.Modules PR #110](https://github.com/namazso/PawnIO.Modules/pull/110)
and preserves its original `ioctl_read_smn`, `ioctl_write_smn`,
`ioctl_read_metrics`, and `ioctl_get_bounds` entry points. The current ABI
exposes these bounded helpers:

- `ioctl_get_device_info`: selected PCI identity, BAR bounds, current table
  translation, and supported table sizes.
- `ioctl_get_metrics_address`: diagnostic view of the validated GPU-address to
  BAR translation.
- `ioctl_read_metrics_rdna2`: fixed 164-byte maximum SMU11 core table.
- `ioctl_read_metrics_rdna3_0`: fixed 244-byte SMU13.0.0-layout core table.
- `ioctl_read_metrics_rdna3_7`: fixed 240-byte SMU13.0.7 core table.
- `ioctl_read_metrics_rdna4`: fixed 260-byte SMU14 core table.
- `ioctl_read_navi21_svi`: the four fixed, read-only Navi 21 SVI telemetry
  words at `0x5A00C..0x5A018`.
- `ioctl_read_navi21_tool_table`: fixed Navi 21 version/address queries,
  refresh, range validation, and an 8-KiB table copy with metadata; retained
  for ABI 4 consumers.
- `ioctl_read_rdna_tool_table`: the ABI 5 generic RDNA version and
  address queries, generation-specific framebuffer validation, refresh, and
  an 8-KiB table copy with metadata.

The fixed read calls accept no address argument. This is a convenience rather
than an additional security boundary: C2PMSG_80/81 are inside the writable SMN
allowlist, so a module caller can steer the address they contain, and the
legacy `ioctl_read_metrics` accepts a physical address directly. Every metrics
read still validates the complete range against the selected GPU's BAR0 VRAM
aperture. The Navi 21 SVI helper reads only its four compile-time SMUIO offsets.
The test application calls only the ABI 5 fixed device-info, metrics, SVI, and
generic RDNA tool-table functions; it never calls the generic SMN write or legacy
caller-addressed metrics function. The tool-table call does not let the
application choose a mailbox service or memory address.

The C2PMSG_80/81 address-discovery mechanism comes from the original PawnIO
module and was validated there on Navi 44. It is not a general RDNA protocol.
The module now requires two identical complete high/low/high snapshots before
using the pointer. It retries a torn, stale, or out-of-aperture candidate up to
five times with a 10-ms delay; every candidate must still pass the complete
BAR0 range checks before it can be mapped. A persistent failure falls back to
ADL PMLog in the application instead of losing all RDNA4 monitoring.

The public SMU14 path was tested live on an RX 9070 XT (`1002:7550`, revision
`C0`): its published address `0x83F6DC6000` resolved inside the 16-GiB BAR0
aperture, 1,000 consecutive reads completed successfully, and all 260 bytes
matched AMD's `SmuMetrics_t` layout. SMU14 `AvgCurrent` values are milliamperes
and are displayed as amperes with three decimal places. `AvgFanPwm` is already
a percentage; it is not incorrectly rescaled from a 0..255 value.
The same card returned private table version `0x00660006` at GPU/MC address
`0x83F6DAB000`. Effective GFX and FCLK values decoded from offsets `0x1F8` and
`0x1CC`; the bounded refresh and 8-KiB copy completed in about 56 ms.

On the tested Navi 21 RX 6800 XT and Navi 31 RX 7900 XTX, both registers remain
zero even while AMD telemetry is active. SMU11 and SMU13 normally receive the
driver's public-metrics buffer address as the parameter of the one-time
`SetDriverDramAddrHigh` and `SetDriverDramAddrLow` initialization messages; the
later transfer does not make that address recoverable. Navi 21 and RDNA3
therefore use the separate, bounded private-tool path described above. For an
unverified full table version, ADL PMLog supplies the displayed values while
the private table is retained in the raw dump.

The module deliberately does not scan VRAM or replace the driver's buffer. It
sends firmware commands only in the bounded RDNA tool-table helper, using the fixed
private services and complete-range checks described above.

## Selection and overrides

The module selects the AMD VGA device with the largest probed BAR0 aperture,
matching the PR behavior. The application displays the exact PCI BDF and IDs
before monitoring starts.

Generation auto-detection is deliberately conservative. Known Navi 2x, 3x,
and 4x PCI-ID ranges are recognized; an unknown ID requires a manual RDNA
selection. For RDNA2, Auto chooses V3 for current Navi 21 firmware and V2 for
Navi 22/23/24. Base, V2, V3, and V4 remain manually selectable so older
firmware can be tested without changing code. For RDNA3, Auto chooses the
SMU13.0.7 table for known Navi 33 IDs and the SMU13.0.0-layout table for
Navi 31/32; both remain available as explicit overrides.

The fixed read sizes were checked field-by-field against AMD's public Linux
headers: SMU11 Base/V2/V3/V4 are 136/156/164/160 bytes respectively, so its
read uses the 164-byte maximum; SMU13.0.0 is 244 bytes, SMU13.0.7 is 240 bytes,
and SMU14 is 260 bytes.

The field layouts follow these headers:

- [SMU11 Sienna Cichlid](https://github.com/torvalds/linux/blob/master/drivers/gpu/drm/amd/pm/swsmu/inc/pmfw_if/smu11_driver_if_sienna_cichlid.h)
- [SMU13 v13.0.0](https://github.com/torvalds/linux/blob/master/drivers/gpu/drm/amd/pm/swsmu/inc/pmfw_if/smu13_driver_if_v13_0_0.h)
- [SMU13 v13.0.7](https://github.com/torvalds/linux/blob/master/drivers/gpu/drm/amd/pm/swsmu/inc/pmfw_if/smu13_driver_if_v13_0_7.h)
- [SMU14 v14.0](https://github.com/torvalds/linux/blob/master/drivers/gpu/drm/amd/pm/swsmu/inc/pmfw_if/smu14_driver_if_v14_0.h)
- [SMUIO 11.0.0 register offsets](https://github.com/torvalds/linux/blob/master/drivers/gpu/drm/amd/include/asic_reg/smuio/smuio_11_0_0_offset.h)
- [ATOM firmware board tables](https://github.com/torvalds/linux/blob/master/drivers/gpu/drm/amd/include/atomfirmware.h)

### Implementation references

- [Linux MP 13.0.0 C2PMSG register definitions](https://github.com/torvalds/linux/blob/master/drivers/gpu/drm/amd/include/asic_reg/mp/mp_13_0_0_offset.h)
- [Linux MP 14.0.2 C2PMSG register definitions](https://github.com/torvalds/linux/blob/master/drivers/gpu/drm/amd/include/asic_reg/mp/mp_14_0_2_offset.h)
- [Linux MMHUB framebuffer-bound decoding](https://github.com/torvalds/linux/blob/master/drivers/gpu/drm/amd/amdgpu/mmhub_v1_7.c)
- [Linux MC-to-physical translation](https://github.com/torvalds/linux/blob/master/drivers/gpu/drm/amd/amdgpu/amdgpu_gmc.c)
- [PawnIO.Modules `RyzenSMU.p` bounded polling](https://github.com/namazso/PawnIO.Modules/blob/main/RyzenSMU.p)
- [PawnIO `microsleep` implementation](https://github.com/namazso/PawnIO/blob/master/PawnIO/src/natives_impl_windows.cpp)
- [Private-protocol versions and Navi 21/Navi 31/Navi 48 validation](https://github.com/miklebel/PawnIO.Modules/pull/1)
- [RDNA3 comparison against HWiNFO](https://github.com/namazso/PawnIO.Modules/pull/110#issuecomment-5528416120)
- [RDNA4 comparison against HWiNFO](https://github.com/namazso/PawnIO.Modules/pull/110#issuecomment-5529590343)

This remains experimental low-level code. Validate the raw table and selected
layout on each GPU/firmware combination before integrating any field into the
main CapFrameX monitoring pipeline.
