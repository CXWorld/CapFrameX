# CapFrameX Linux

A Linux-native solution for frametime capture and analysis, conceptually based on CapFrameX for Windows.

## Architecture

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│     Daemon      │◄───►│  Vulkan Layer   │◄───►│   GUI (C#)      │
│     ( C )       │     │    (C / C++)    │     │   Avalonia UI   │
└─────────────────┘     └─────────────────┘     └─────────────────┘
        │                       │                       │
        └───────────────────────┴───────────────────────┘
                              IPC
                    (Unix Socket / Shared Memory)
```

## Features

- **Game Detection**: Automatic detection of games launched via Steam, Lutris, Heroic, Bottles, Gamescope
- **Frametime Capture**: Vulkan layer for precise frametime measurement
- **Live Monitoring**: Real-time frametime graphs and statistics
- **Analysis**: Comprehensive statistics including percentiles, averages, and standard deviation
- **Session Comparison**: Compare multiple capture sessions
- **Export**: CSV export compatible with Windows CapFrameX

## Requirements

### Build Dependencies

```bash
# Daemon and Layer (Debian/Ubuntu)
sudo apt install build-essential cmake libvulkan-dev

# .NET 8 SDK
# See https://docs.microsoft.com/dotnet/core/install/linux
```

### Runtime Dependencies

- Vulkan-capable GPU with appropriate drivers
- .NET 8 Runtime

## Building

### Quick Build (All Components)

```bash
./scripts/build.sh
```

### Individual Components

```bash
# Daemon and Layer
mkdir build && cd build
cmake ..
make

# Avalonia App
cd src/app
dotnet build
```

## Installation

```bash
./scripts/install.sh
```

This will:
1. Install the daemon to `/usr/bin/capframex-daemon`
2. Install the Vulkan layer to `/usr/lib/libcapframex_layer.so`
3. Register the layer manifest
4. Install the systemd user service

## Usage

1. Start the daemon (or enable the systemd service):
   ```bash
   systemctl --user enable --now capframex-daemon
   ```

2. Launch the application:
   ```bash
   capframex
   ```

3. Start a game - it will be automatically detected
4. Press the capture hotkey (default: F11) to start/stop recording
5. View and analyze your captures in the Analysis tab

## Configuration

Configuration is stored in `~/.config/capframex/`

Sessions are stored in `~/.local/share/capframex/sessions/`

## Capture File Format

Capture files are stored as CSV with an accompanying JSON metadata file.

### CSV Columns

| Column | Description |
|--------|-------------|
| `MsBetweenPresents` | CPU sampled frametime (CLOCK_MONOTONIC) |
| `MsUntilRenderComplete` | Reserved for future use |
| `MsUntilDisplayed` | Reserved for future use |
| `MsActualPresent` | Actual frametime from VK_EXT_present_timing (0.00 if not available) |

### Timing Modes

The Vulkan layer detects whether `VK_EXT_present_timing` or `VK_GOOGLE_display_timing` extensions are available:

- **Present Timing**: Actual GPU/display presentation timestamps from driver
- **Layer Timing**: CPU sampled timestamps (fallback when extensions unavailable)

Both timing sources are captured and stored when available, allowing users to choose which metric to use for analysis.

## License

MIT
