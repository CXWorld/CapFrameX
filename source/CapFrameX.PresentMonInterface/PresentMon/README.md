# Bundled PresentMon

CapFrameX bundles Intel's signed x64 console executable from
[PresentMon v2.6.0](https://github.com/GameTechDev/PresentMon/releases/tag/v2.6.0),
released on September 21, 2026.

- Asset: [PresentMon-2.6.0-x64.exe](https://github.com/GameTechDev/PresentMon/releases/download/v2.6.0/PresentMon-2.6.0-x64.exe)
- SHA-256: `b2a706bc6ad475749e3b7e3409263aa1e6906d45bdcf993f6dbc0f660188f1af`
- License: [MIT](license.txt), copied from the release's source tree.

## Capture compatibility

The arguments in `PresentMonServiceConfiguration` retain the existing CSV layout:
29 columns with PC latency tracking, or 28 without it. Frame-type tracking, app
timing, QPC timestamps in milliseconds, and configurable circular buffers remain
supported. No parser or metric-index changes are required for this update.

Version 2.6.0 adds the optional `--write_display_metadata` argument, which inserts
`VidPnSourceId` and `LayerIndex` and appends `PresentId`. CapFrameX leaves this
argument disabled because its capture parser expects the layout above.

## Changes from 2.5.1

The upstream release adds tracking of newer OS DDI flip events, display metadata,
and D3D12 pipeline-state compilation metrics. ETL playback no longer prunes old
swap-chain data between batches, making replay more deterministic. The present
overflow counter is now atomic. The new PSO metrics are not exposed by CapFrameX's
current CSV configuration.

The release's telemetry-provider, Intel overlay UI, and Windows service changes
apply to the separate Intel PresentMon components; CapFrameX bundles only the
console executable. See the release notes linked above for the full change list.

## Update verification

- Intel Authenticode signature and GitHub release SHA-256 verified.
- Release x64 application, test project, and WiX 3.14.1 installer built
  successfully, including MSI validation.
- 72 existing capture-configuration, capture-manager, frame-filter, feed-diagnostic,
  and record-manager tests passed on .NET 10.
- All six upstream `Tests/Gold` ETL fixtures replayed with both 2.5.1 and 2.6.0,
  with PC latency enabled and disabled: 24 successful runs, identical CSV output
  for every old/new pair, and every row matched CapFrameX's expected column count.
- Circular buffer sizes 2048, 4096, and 8192 accepted by the new executable.

These checks cover recorded traces and application tests, not a live game session.
