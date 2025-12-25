# CapFrameX Test Renderer (DirectX 12)

A simple DirectX 12 test application for PresentMon capture testing. Renders a rotating triangle at configurable frame rates.

## Why DirectX 12 instead of OpenGL?

**PresentMon relies on ETW (Event Tracing for Windows) to capture frame timing data.** Specifically, it hooks into DXGI present calls that generate `Microsoft-Windows-DXGI` and `Microsoft-Windows-D3D12` ETW events.

### The Problem with OpenGL

OpenGL on Windows has inconsistent ETW event generation:
- OpenGL doesn't use DXGI directly in the same way DirectX applications do
- The ICD (Installable Client Driver) model means each GPU vendor implements OpenGL differently
- Some drivers may not generate the same ETW events that PresentMon expects
- Swap chain management is handled internally by the driver, not exposed to ETW

### Why DirectX 12 Works

DirectX 12 applications:
1. **Use DXGI swap chains** - The `IDXGISwapChain::Present()` call generates ETW events
2. **Generate standardized ETW events** - `Microsoft-Windows-DXGI` provider captures all present calls
3. **Explicit resource management** - Frame boundaries are clearly defined
4. **Direct Present calls** - `_swapChain.Present()` in the code directly triggers the events PresentMon captures

The key line in the code that PresentMon captures:
```csharp
_swapChain!.Present(0, PresentFlags.AllowTearing);
```

## Building

```bash
dotnet build -c Release
```

## Running

```bash
# Default 60 FPS
dotnet run

# Custom target FPS
dotnet run -- 144
```

## Controls

| Key | Action |
|-----|--------|
| 1 | 30 FPS |
| 2 | 60 FPS |
| 3 | 90 FPS |
| 4 | 120 FPS |
| 5 | 144 FPS |
| 6 | 165 FPS |
| 7 | 240 FPS |
| 8 | 300 FPS |
| 9 | Unlimited |
| Q/ESC | Quit |

## Dependencies

- .NET 8.0 (Windows)
- [Vortice.Windows](https://github.com/amerkoleci/Vortice.Windows) - DirectX 12 bindings for .NET

## Technical Notes

- Uses flip-model swap chain (`SwapEffect.FlipDiscard`) for best compatibility with modern Windows
- VSync is disabled; frame rate is controlled via spin-wait for precision
- `AllowTearing` flag enables variable refresh rate (VRR/G-Sync/FreeSync) support
- Window uses Per-Monitor DPI awareness

## Verifying PresentMon Detection

1. Start the test renderer
2. Open PresentMon or CapFrameX
3. You should see the process listed with frame timing data
4. Change FPS using keyboard shortcuts and verify the frame times update accordingly
