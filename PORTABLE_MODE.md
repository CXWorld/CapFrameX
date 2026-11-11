# CapFrameX Portable Mode

## Overview

CapFrameX now supports a portable mode that allows the application to run entirely from a single folder without writing to system directories. This is useful for running from USB drives, network shares, or keeping multiple isolated installations.

## How It Works

Portable mode is activated by placing a `portable.json` file in the same directory as the CapFrameX executable. When the application starts, it checks for this file and redirects all data storage to paths relative to the application directory.

### Key Features

- **Self-contained**: All configuration, captures, screenshots, and logs are stored relative to the application folder
- **No registry modifications**: The application doesn't write to the Windows registry in portable mode
- **No system folder usage**: Bypasses `%AppData%` and `Documents` folders entirely
- **Configurable paths**: All storage paths can be customized via `portable.json`

## portable.json Configuration

Create a `portable.json` file in the CapFrameX application directory with the following structure:

```json
{
  "portable": true,
  "paths": {
    "config": "./Portable/Config",
    "captures": "./Portable/Captures",
    "screenshots": "./Portable/Screenshots",
    "logs": "./Portable/Logs",
    "cloud": "./Portable/Captures/Cloud"
  }
}
```

### Configuration Options

| Property | Description | Default |
|----------|-------------|---------|
| `portable` | Enables portable mode when `true` | `true` |
| `paths.config` | Configuration files (AppSettings.json, overlay configs, sensor config, UI state) | `./Portable/Config` |
| `paths.captures` | Capture recordings | `./Portable/Captures` |
| `paths.screenshots` | Screenshot storage | `./Portable/Screenshots` |
| `paths.logs` | Log files | `./Portable/Logs` |
| `paths.cloud` | Cloud download directory | `./Portable/Captures/Cloud` |

All paths are relative to the application directory. You can use `./` or `.\` prefix, or just the folder name.

## Requirements

When running in portable mode, the following dependencies must be installed on the system:

- **.NET 10.0 Desktop Runtime (x64)** - [Download](https://dotnet.microsoft.com/download/dotnet/10.0)
- **Visual C++ 2015-2022 Redistributable (x64)** - [Download](https://aka.ms/vs/17/release/vc_redist.x64.exe)

The native .NET application host checks the Desktop Runtime before managed startup. Once CapFrameX
starts, its dependency checker validates the expected .NET 10 Desktop Runtime and Visual C++ runtime.

## Vulkan layer registration

Vulkan games show the in-game overlay through the CapFrameX Vulkan layer, which the Vulkan loader only
finds through the registry. Extracting the portable package writes no registry values, so at startup
the portable version compares the registration with the layers in its own `vulkan` folder (`vulkan` for
x64, `vulkan\x86` for x86). When no CapFrameX layer is registered, when one bitness has none, or when
the registered layer uses other files than this folder, it asks whether to register the layers of this
folder. Layers of an installed CapFrameX with identical files count as a match.

**Yes** runs `vulkan\Register-CapFrameXVulkanLayer.ps1` with the rights of CapFrameX (it always runs
elevated). The script:

- registers the x64 manifest in the 64-bit view and the x86 manifest in the 32-bit view (`WOW6432Node`)
  of `HKLM\SOFTWARE\Khronos\Vulkan\ImplicitLayers`,
- disables every other CapFrameX layer registration there (value `1`, not deleted), because the loader
  identifies the layer by its name and would otherwise pick one of them,
- removes CapFrameX layer registrations from HKCU, which elevated games ignore and which break the
  layer for the other bitness.

An installed CapFrameX then uses the portable layers too, until you repair its installation. Restart
Vulkan games that were running. **No** is remembered: CapFrameX asks again only when the layers of the
folder or their registration change, for example after extracting a newer version or moving the folder.
The Info tab shows the current registration.

You can also run the script yourself from an elevated PowerShell; `-ValidateOnly` only shows the planned
changes:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\vulkan\Register-CapFrameXVulkanLayer.ps1
```

Deleting the portable folder leaves its registration behind without a manifest; the Vulkan loader skips
it. Repair an installed CapFrameX to enable its own registration again.

## 1.9.2 Beta

Installer and portable builds of **1.9.2.0-beta** include the in-game overlay again: keep the `hook` and `vulkan` folders, including their x86 subfolders, alongside the application. The `benchlab-service` folder contains the bundled service, which CapFrameX starts when needed; compatible separately installed services are also supported.

The current installer defaults to `C:\Program Files\CapFrameX`, with the x64 Vulkan layer and its manifest in `vulkan` and the x86 pair in `vulkan\x86` beneath that folder. It registers them in their respective HKLM registry views. Earlier installers used `C:\Program Files (x86)\CapFrameX`. Portable extraction does not register the layers; Vulkan integration requires the matching layers to be registered separately. Later builds offer this at startup, see [Vulkan layer registration](#vulkan-layer-registration).

The beta bundles **CX-PresentMon 2.6.1**, the CapFrameX-maintained build based on Intel PresentMon 2.6.0, with fixes for accumulating stale timing data and stalled output after frame-generation changes. All shipped Windows binaries must pass the [release signature checks](scripts/RELEASE_SIGNING.md).

**PC Latency correction:** Disabling PC Latency in the settings does not fix the PresentMon tracking problem, contrary to our [earlier post on X](https://x.com/CapFrameX/status/2105998858019078230). Application-timing tracking remains active independently of this setting; the fix is included in the bundled CX-PresentMon 2.6.1.

**Reviewers:** Do not use the hook-free overlay for benchmark measurements. Hiding it can cause frametime spikes, and some AMD and Intel drivers can have DWM problems. Check the renderer actually in use.

## Updating to the revised v1.9.0 package

The revised package contains application version **1.9.0.8**. Extract it into a new folder, then copy your existing `Portable` data folder and any customized `portable.json` into that folder. Overwriting an older application folder can leave removed in-game overlay and BENCHLAB service binaries behind.

In the stable v1.9.0 package, the in-game overlay is unavailable until our code-signing certificate is available. Hook-free and RTSS rendering remain supported. BENCHLAB monitoring requires a separately installed compatible service; v1.9.0 does not include or launch a bundled service executable.

## Creating a Portable Distribution

1. Copy the CapFrameX application files to a folder
2. Create a `portable.json` file with the configuration above
3. (Optional) Pre-create the `Portable` subdirectory with: `Config`, `Captures`, `Screenshots`, `Logs`
4. The application is now portable and can be moved to any location

## Behavior Differences

| Feature | Installed Mode | Portable Mode |
|---------|---------------|---------------|
| Config storage | `%AppData%\CapFrameX\Configuration` | `./Portable/Config` |
| Captures storage | `Documents\CapFrameX\Captures` | `./Portable/Captures` |
| Screenshots | `Documents\CapFrameX\Screenshots` | `./Portable/Screenshots` |
| Application logs | `%AppData%\CapFrameX\Logs` | `./Portable/Logs` |
| Native OSD logs | `%TEMP%\cfx-osd-logs` | `%TEMP%\cfx-osd-logs` |
| UI state (window size, column widths) | `%LocalAppData%\Jot` | `./Portable/Config` |
| Window title | "CapFrameX" | "CapFrameX Portable" |
| Config migration | Migrates old settings | Skipped |
| Start with Windows | Available | Disabled |

## Notes

- Settings configured in portable mode are stored in the portable config folder and won't affect or be affected by an installed version
- The `portable.json` file must be valid JSON; if parsing fails, the application falls back to installed mode
- Directories are automatically created if they don't exist
