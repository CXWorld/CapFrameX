# CapFrameX v1.9.2 Beta

This beta prioritizes critical PresentMon capture and tracking fixes after several users reported increasing CPU usage, frametime spikes, and stalled capture output. It also includes localization, overlay, and installer improvements since 1.9.1.

> **Correction to our earlier X post:** Disabling **PC Latency** in the CapFrameX settings **does not resolve this PresentMon problem**. Our [earlier post on X](https://x.com/CapFrameX/status/2105998858019078230) incorrectly suggested it as a workaround. Application-timing tracking remains active independently of the PC Latency setting, so stale tracking data can still accumulate. The fix is included in CX-PresentMon 2.6.1, bundled with this beta.

> **Important information for reviewers:** Do not use the **CapFrameX hook-free overlay** for benchmark measurements. Hiding this overlay can cause frametime spikes, and some AMD and Intel driver versions can have problems with Windows Desktop Window Manager (DWM). Use a suitable in-game or RTSS overlay and check which renderer is actually active before measuring, including any automatic fallback to hook-free. These overlay limitations are separate from the PresentMon fixes below.

## Downloads and requirements

| Package | Use |
| --- | --- |
| [Installer](https://github.com/CXWorld/CapFrameX/releases/download/v1.9.2_beta/CapFrameX_1.9.2.0_Beta_Installer.zip) | Extract the ZIP and run `CapFrameXBootstrapper.exe`. |
| [Portable](https://github.com/CXWorld/CapFrameX/releases/download/v1.9.2_beta/CapFrameX_1.9.2.0_Beta_Portable.zip) | Extract into a new folder and run `CapFrameX.exe`. Keep `portable.json` beside the executable. |
| [SHA-256 checksums](https://github.com/CXWorld/CapFrameX/releases/download/v1.9.2_beta/SHA256SUMS.txt) | Verify both ZIP downloads. |

- **Application version:** 1.9.2.0-beta. This is a prerelease; [v1.9.1](https://github.com/CXWorld/CapFrameX/releases/tag/v1.9.1) remains the stable release.
- **Runtime:** [.NET 10 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/10.0) is required for both packages. The plain .NET Runtime is not sufficient.
- Portable installations also require the Microsoft Visual C++ 2015–2022 Redistributables for the native components. Setup handles the redistributables for installer users.
- The packages include signed application and dependency binaries, the x64/x86 in-game overlay components, and BENCHLAB Service 2.4.0.

## PresentMon: critical stability fixes

CapFrameX now bundles **CX-PresentMon 2.6.1**, our maintained build based on **Intel PresentMon 2.6.0**. This is a CapFrameX build, not an official Intel PresentMon 2.6.1 release. The CSV layout remains compatible with Intel PresentMon 2.6.0.

- **Bound stale application-timing tracking.** In affected games, timing events without matching present events could accumulate for the lifetime of the process. Repeatedly scanning these entries could increase CPU usage and memory use and introduce frametime spikes. Stale entries are now removed.
- **Recover capture output after frame-generation changes.** A present waiting for a frame ID could block subsequent output when frame-type events stopped, for example after disabling or replacing frame generation. The stale-present check now also releases this wait instead of leaving output blocked until the completed-present buffer fills.
- **Harden GPU queue and presentation tracking.** Additional changes bound retained HAGS hardware-queue tracking, clean up stopped queues, prevent duplicate DWM present queuing, and improve swap-chain/process tracking. These are additional robustness changes; they are not presented as the confirmed cause of every reported issue.

See [Intel PresentMon issue #695](https://github.com/GameTechDev/PresentMon/issues/695) for the reported failures and [the CX-PresentMon source](https://github.com/DevTechProfile/CXPresentMon/tree/cfx/bugfixes) for the fixes. The beta ships source revision `8e2d21ef90fae171d840c9c9f4136340f7288547`.

## Overlay and interface

- The CapFrameX in-game overlay is now the default for new configurations. Existing renderer selections are preserved.
- Removed the experimental label and availability switch for the in-game renderer.
- Improved overlay-item column sizing, capture status wrapping, tab alignment, and dialog button sizing.
- Fixed record-list column behavior while scrolling and disabled formatting actions when no overlay entry is selected.

## Localization

- Added German, Russian, and Spanish translations.
- Improved language switching across the Info tab, charts, dropdowns, and sensor views.
- Corrected translated overlay labels while preserving game names, units, and internal API identifiers.

## Installer and packaging

- Migrated the installer to WiX Toolset 6.0.2 and refreshed its layout and import hint.
- Preserve autostart configuration during repair and upgrades; remove it during a full uninstall.
- Avoid unnecessary Visual C++ redistributable repairs when repairing CapFrameX.
- Retain the complete signing checks introduced for 1.9.1.5, including managed dependencies, resource assemblies, and installer custom-action dependencies.
- Register x64 and x86 Vulkan layers in their respective HKLM registry views. Portable packages do not register Vulkan layers automatically.

## Beta feedback

Please report remaining capture interruptions, growing background CPU usage, or frame-generation transition problems with the game, GPU, driver version, active overlay renderer, and relevant logs. Hardware- and driver-specific behavior still needs feedback from a wider range of systems.

**Full changelog:** [v1.9.1.5...v1.9.2_beta](https://github.com/CXWorld/CapFrameX/compare/v1.9.1.5...v1.9.2_beta)
