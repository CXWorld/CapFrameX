# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

CapFrameX Linux is a frametime capture and analysis tool for Linux. It captures real-time GPU frametime metrics from Vulkan applications and provides statistical analysis through a modern GUI.

## Architecture

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│     Daemon      │◄───►│  Vulkan Layer   │◄───►│   GUI App       │
│       (C)       │     │      (C)        │     │ (.NET/Avalonia) │
└─────────────────┘     └─────────────────┘     └─────────────────┘
        │                       │                       │
        └───────────────────────┴───────────────────────┘
                              IPC
                  (Unix Socket at ~/.config/capframex/)
```

**Three-tier design:**
- **Daemon** (`src/daemon/`): Game detection, process monitoring, IPC server, message routing
- **Vulkan Layer** (`src/layer/`): In-game frametime capture via VK_LAYER_capframex_capture
- **GUI Application** (`src/app/`): Avalonia UI with MVVM pattern, statistics, charting

**IPC Protocol:** Binary messages over Unix socket with MessageType enum defining 19+ message types (MSG_GAME_STARTED, MSG_FRAMETIME_DATA, MSG_START_CAPTURE, etc.) defined in `src/app/CapFrameX.Shared/IPC/Messages.cs` and `src/daemon/common.h`.

## Build Commands

### Prerequisites

```bash
# Debian/Ubuntu
sudo apt install build-essential cmake libvulkan-dev

# .NET 8 SDK required - see https://dotnet.microsoft.com
```

### Build All Components

```bash
./scripts/build.sh
```

### Build Individual Components

```bash
# Native only (daemon + layer)
mkdir -p build && cd build
cmake .. -DCMAKE_BUILD_TYPE=Release
make -j$(nproc)

# .NET only
cd src/app
dotnet restore
dotnet build -c Release

# Publish self-contained
./scripts/build.sh --publish
```

### Build Output Locations

- Daemon: `build/bin/capframex-daemon`
- Layer: `build/lib/libcapframex_layer.so`
- App: `src/app/CapFrameX.App/bin/Release/net8.0/`

## Running and Testing

### Run Without Installing

```bash
# Terminal 1: Start daemon
./build/bin/capframex-daemon -d    # -d for debug output

# Terminal 2: Run the app
dotnet run --project src/app/CapFrameX.App

# Terminal 3: Test with Vulkan app
export VK_LAYER_PATH=$PWD/build/lib
export VK_INSTANCE_LAYERS=VK_LAYER_capframex_capture
vkcube
```

### Run Integration Test

```bash
# Daemon must be running first
dotnet run --project src/app/FrameReceptionTest
```

The FrameReceptionTest connects to the daemon, auto-subscribes to detected games, and prints frame statistics.

## Key Files

**Daemon:**
- `src/daemon/main.c` - Entry point, game tracking loop
- `src/daemon/ipc.c` - Unix socket server, message routing
- `src/daemon/launcher_detect.c` - Steam, Lutris, Heroic, Bottles detection
- `src/daemon/common.h` - Shared structs and message definitions

**Layer:**
- `src/layer/layer.c` - Vulkan layer entry points
- `src/layer/swapchain.c` - vkCreateSwapchainKHR hooking
- `src/layer/timing.c` - vkQueuePresentKHR hooking, frametime measurement
- `src/layer/ipc_client.c` - Client connection to daemon

**App:**
- `src/app/CapFrameX.Shared/IPC/Messages.cs` - IPC protocol definitions (must match common.h)
- `src/app/CapFrameX.Core/Capture/DaemonClient.cs` - IPC client wrapper
- `src/app/CapFrameX.App/ViewModels/` - MVVM view models

## Configuration

- User settings: `~/.config/capframex/AppSettings.json`
- Sessions: `~/.local/share/capframex/sessions/`
- IPC socket: `~/.config/capframex/capframex.sock`

## Custom Agents

Two custom agents are defined in `.claude/agents/`:

- **code-reviewer**: Reviews code for correctness, style, security, and best practices. Uses Sonnet model.
- **web-researcher**: Conducts web research and summarizes findings with sources. Uses Sonnet model.

## Key Dependencies

**Native:** Vulkan SDK, POSIX threads, CMake 3.16+

**C#/.NET:**
- Avalonia 11.0.6 (UI framework)
- CommunityToolkit.Mvvm 8.2.2 (MVVM)
- LiveChartsCore 2.0.0-rc2 (Charting)
- System.Reactive 6.0.0 (Observables)
