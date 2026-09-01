# CapFrameX Radeon SMU Monitor

This is a standalone x64 WPF test application for reading the public Radeon
SMU metrics table through PawnIO. It is intentionally independent from the
CapFrameX ADLX monitoring path and does not install or modify CapFrameX
configuration.

The application supports these table families:

- RDNA2 / SMU11: `SmuMetrics_t`, `SmuMetrics_V2_t`, `SmuMetrics_V3_t`, and
  `SmuMetrics_V4_t` from `smu11_driver_if_sienna_cichlid.h`.
- RDNA3 / SMU13: the 244-byte SMU 13.0.0/13.0.10 layout used by Navi 31/32
  and the 240-byte SMU 13.0.7 layout used by Navi 33.
- RDNA4 / SMU14: the 260-byte `SmuMetrics_t` from
  `smu14_driver_if_v14_0.h`.

It exposes clocks, activity, voltage/current planes, board and socket power,
the extra temperature channels, fan data, per-cause throttling percentages,
PCIe state/busy data, energy and D3Hot counters, and SmartShift fields. Every
decoded value is accompanied by its raw value and the complete DWORD table can
be inspected in the UI.

## Build the test application

From the repository root:

```powershell
dotnet build source\CapFrameX.RadeonMonitor\CapFrameX.RadeonMonitor.csproj -c Debug -p:Platform=x64
```

`PawnIOLib.dll` and the signed PawnIO 2.x driver package are copied from the
existing `LibreHardwareMonitorLib\PawnIo` directory into the application output.
The application does not install the driver. Install the signed package on the
test system first, for example from an elevated terminal in the output folder:

```powershell
pnputil /add-driver PawnIO\PawnIO.inf /install
```

## Compile the PawnIO module on the target build system

[`PawnIO/RadeonSMU.p`](PawnIO/RadeonSMU.p) is source-only in this repository.
No compiled module is checked in or produced by the C# project.
It retains the upstream LGPL-2.1-or-later license; the corresponding license
text is already stored at
[`../LibreHardwareMonitorLib/Resources/PawnIO/COPYING`](../LibreHardwareMonitorLib/Resources/PawnIO/COPYING).

Use the Pawn compiler and `include` directory from `PawnIO.Modules`. From a
PowerShell prompt where those files are available, a matching command is:

```powershell
pawncc.exe RadeonSMU.p -iinclude --% -C64 -;+ -(+ -p
```

The compiler normally produces `RadeonSMU.amx`. The application accepts both
`.amx` and `.bin`, either through the file picker or as its first command-line
argument. Renaming the output to `RadeonSMU.bin` and placing it next to the EXE
makes it the default module:

```powershell
Copy-Item RadeonSMU.amx <app-output>\RadeonSMU.bin
```

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
application calls only the ABI 2 device-info and metrics-read functions; it
never calls the generic SMN write function.

The module does not send a firmware command to refresh the table, avoiding
mailbox races with the AMD display driver. A read can therefore report
`STATUS_DEVICE_NOT_READY` until the driver has published its table address.

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
