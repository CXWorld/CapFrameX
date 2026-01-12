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

- **.NET 9.0 Desktop Runtime (x64)** - [Download](https://dotnet.microsoft.com/download/dotnet/9.0)
- **Visual C++ 2015-2022 Redistributable (x64)** - [Download](https://aka.ms/vs/17/release/vc_redist.x64.exe)

The application will check for these dependencies on startup and display a message if any are missing.

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
| Logs | `%AppData%\CapFrameX\Logs` | `./Portable/Logs` |
| UI state (window size, column widths) | `%LocalAppData%\Jot` | `./Portable/Config` |
| Window title | "CapFrameX" | "CapFrameX Portable" |
| Config migration | Migrates old settings | Skipped |
| Start with Windows | Available | Disabled |

## Notes

- Settings configured in portable mode are stored in the portable config folder and won't affect or be affected by an installed version
- The `portable.json` file must be valid JSON; if parsing fails, the application falls back to installed mode
- Directories are automatically created if they don't exist
