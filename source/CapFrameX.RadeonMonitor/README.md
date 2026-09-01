# CapFrameX Radeon SMU Monitor

This is a standalone x64 WPF test application for Radeon monitoring. It reads
the public SMU metrics table through PawnIO when the table address is available
and automatically falls back to the AMD driver's ADL PMLog interface on RDNA3.
It is independent from the CapFrameX ADLX monitoring path and does not install
or modify CapFrameX configuration.

The parser supports these table families:

- RDNA2 / SMU11: `SmuMetrics_t`, `SmuMetrics_V2_t`, `SmuMetrics_V3_t`, and
  `SmuMetrics_V4_t` from `smu11_driver_if_sienna_cichlid.h`.
- RDNA3 / SMU13: the 244-byte SMU 13.0.0/13.0.10 layout used by Navi 31/32
  and the 240-byte SMU 13.0.7 layout used by Navi 33.
- RDNA4 / SMU14: the 260-byte `SmuMetrics_t` from
  `smu14_driver_if_v14_0.h`.

When the firmware exposes a readable table address, the application shows
clocks, activity, voltage/current planes, board and socket power,
the extra temperature channels, fan data, per-cause throttling percentages,
PCIe state/busy data, energy and D3Hot counters, and SmartShift fields. Every
decoded value is accompanied by its raw value and the complete DWORD table can
be inspected in the UI.

On RDNA3 systems where C2PMSG_80/81 do not expose the table address, or where
the raw read reports `STATUS_DEVICE_NOT_READY`, the
[AMD Display Library](https://github.com/GPUOpen-LibrariesAndSDKs/display-library)
PMLog fallback returns the sensor set supported by the installed Radeon driver.
The application matches the ADL adapter to the PawnIO-selected GPU by PCI bus,
device, and function. Supported values are displayed even when their current
value is zero, and the raw ADL sensor IDs and integer values can be inspected
in the UI. This path commonly provides clocks, utilization, voltages,
temperatures, fan data, PCIe state, board power, and per-cause throttling
percentages. Fields that exist only in `SmuMetrics_t`, such as its counters and
serial fields, remain unavailable without the raw table address.

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

## Module ABI 2

The module source is based on
[PawnIO.Modules PR #110](https://github.com/namazso/PawnIO.Modules/pull/110)
and preserves its original `ioctl_read_smn`, `ioctl_write_smn`,
`ioctl_read_metrics`, and `ioctl_get_bounds` entry points. ABI 2 adds:

- `ioctl_get_device_info`: selected PCI identity, BAR bounds, current table
  translation, and supported table sizes.
- `ioctl_get_metrics_address`: diagnostic view of the validated GPU-address to
  BAR translation.
- `ioctl_read_metrics_rdna2`: fixed 164-byte maximum SMU11 core table.
- `ioctl_read_metrics_rdna3_0`: fixed 244-byte SMU13.0.0/13.0.10 core table.
- `ioctl_read_metrics_rdna3_7`: fixed 240-byte SMU13.0.7 core table.
- `ioctl_read_metrics_rdna4`: fixed 260-byte SMU14 core table.

The new read calls accept no address. The module reads C2PMSG_80/81 itself,
requires a stable pair, translates from the Radeon discrete-GPU VRAM MC base,
and verifies the complete read against the selected BAR0 aperture. The test
application checks that translation before issuing a metrics read and calls
only the ABI 2 device-info and metrics-read functions; it never calls the
generic SMN write function.

The C2PMSG_80/81 address-discovery mechanism comes from the original PawnIO
module and was validated there on Navi 44. It is not a general RDNA protocol.
On a tested Navi 31 system both registers remain zero even while AMD telemetry
is active. SMU13 normally receives the driver's buffer address as
the parameter of the one-time `SetDriverDramAddrHigh` and
`SetDriverDramAddrLow` initialization messages; the later metrics transfer
does not make that address recoverable. In this state the application skips
the PawnIO read IOCTL and uses the read-only ADL PMLog fallback instead.

The module deliberately does not scan VRAM, accept a caller-selected physical
address, replace the driver's buffer, or send firmware commands. Each of those
alternatives could read unrelated allocations or race the AMD display driver.

## Selection and overrides

The module selects the AMD VGA device with the largest probed BAR0 aperture,
matching the PR behavior. The application displays the exact PCI BDF and IDs
before monitoring starts.

Generation auto-detection is deliberately conservative. Known Navi 2x, 3x,
and 4x PCI-ID ranges are recognized; an unknown ID requires a manual RDNA
selection. For RDNA2, Auto chooses V3 for current Navi 21 firmware and V2 for
Navi 22/23/24. Base, V2, V3, and V4 remain manually selectable so older
firmware can be tested without changing code. For RDNA3, Auto chooses the
SMU13.0.7 table for known Navi 33 IDs and the SMU13.0.0/13.0.10 table for
Navi 31/32; both remain available as explicit overrides.

The field layouts follow AMD's public Linux headers:

- [SMU11 Sienna Cichlid](https://github.com/torvalds/linux/blob/master/drivers/gpu/drm/amd/pm/swsmu/inc/pmfw_if/smu11_driver_if_sienna_cichlid.h)
- [SMU13 v13.0.0](https://github.com/torvalds/linux/blob/master/drivers/gpu/drm/amd/pm/swsmu/inc/pmfw_if/smu13_driver_if_v13_0_0.h)
- [SMU13 v13.0.7](https://github.com/torvalds/linux/blob/master/drivers/gpu/drm/amd/pm/swsmu/inc/pmfw_if/smu13_driver_if_v13_0_7.h)
- [SMU14 v14.0](https://github.com/torvalds/linux/blob/master/drivers/gpu/drm/amd/pm/swsmu/inc/pmfw_if/smu14_driver_if_v14_0.h)

This remains experimental low-level code. Validate the raw table and selected
layout on each GPU/firmware combination before integrating any field into the
main CapFrameX monitoring pipeline.
