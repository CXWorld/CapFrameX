# IGCL telemetry probe

Shows which power telemetry items the installed Intel graphics driver reports through the three
ways CapFrameX could ask for them, and compares the activity and power rates they produce over the
same intervals:

| Label   | Call                                                     |
|---------|----------------------------------------------------------|
| `V1 v0` | `ctlPowerTelemetryGet`, structure version 0              |
| `V1 v1` | `ctlPowerTelemetryGet`, structure version 1 (CapFrameX)  |
| `V2`    | `ctlPowerTelemetryGetV2`, structure version 1            |

## Why

`CapFrameX.IGCL` uses `ctlPowerTelemetryGet` with structure version 1. `ctlPowerTelemetryGetV2`
documents the same items, but its `globalActivityCounter` averages over all engines instead of
counting the time any engine is busy. CapFrameX derives the **GPU Core** load from that counter,
and V2 would drop it by an order of magnitude. On an Arc B390 (driver 32.0.101.8993) with vkcube
running uncapped, V1 reads ~80 % and V2 ~5.5 %, while render/compute activity, energy and clock
match.

The open question is whether V2 reports items on discrete Arc cards that V1 leaves unsupported.
If it does, `CapFrameX.IGCL` should keep V1 and fill only those gaps from V2.

## Build

```bat
tools\IgclTelemetryProbe\build.cmd
```

Needs Visual Studio with the x64 C++ tools. It compiles against `source\CapFrameX.IGCL`
(`igcl_api.h`, `cApiWrapper.cpp`), so the probe always uses the SDK version CapFrameX ships. The C
runtime is linked statically: `bin\IgclTelemetryProbe.exe` depends on nothing but `KERNEL32.dll`
and the driver's `ControlLib.dll`, so it can be copied to any machine with an Intel GPU.

## Run

```bat
IgclTelemetryProbe.exe [seconds] > probe.txt
```

`seconds` (default 10) sets how long the rate comparison at the end runs. Start a game or
benchmark on the Intel GPU first, so the activity and power columns show real load.

## Reading the output

- **Calls**: result per variant. `not initialized` for V2 means the driver does not export
  `ctlPowerTelemetryGetV2` yet (32.0.101.8724 did not, 32.0.101.8993 does).
- **Supported items**: `bSupported` per item and variant, with unit and value.
- **Summary**: the decision line is `Only reported by V2 (vs V1 v1)`. Anything listed there is
  missing from the sensors CapFrameX creates today.
- **Throttle flags** have no `bSupported` flag, so `0` can also mean "not reported".
- **Activity and power**: rates from both calls over the same one-second intervals. `V1 glob%` is
  what CapFrameX shows as GPU load.

The B390 reference run reports identical items for all three variants; the only difference is the
global activity rate.
