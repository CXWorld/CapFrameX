# CapFrameX Linux In-Game Overlay Development Plan

## Executive Summary

This document outlines the implementation plan for a modern in-game overlay for CapFrameX Linux. The overlay will display real-time frametime graphs, FPS metrics, and hardware telemetry directly within games. The design will draw inspiration from MangoHud's functionality while introducing a fresh, modern aesthetic that sets CapFrameX apart.

## Research Findings Summary

### MangoHud Architecture Analysis
- Uses Vulkan implicit layer mechanism via `VK_LAYER_MANGOHUD_overlay`
- Hooks `vkQueuePresentKHR` to inject rendering before frame presentation
- Uses Dear ImGui for all UI rendering
- Collects telemetry from `/proc`, `/sys/class/hwmon`, and vendor-specific APIs
- Configuration via environment variables and config files

### Existing CapFrameX Layer
- Already has Vulkan layer infrastructure (`src/layer/`)
- Hooks swapchain creation and `vkQueuePresentKHR`
- Has IPC mechanism to communicate with daemon
- Currently only captures frametimes, no overlay rendering

### Key Technical Insights
- ImGui is the de-facto standard for game overlays (minimal overhead, GPU-accelerated)
- Vulkan synchronization is critical - must not interfere with game's command buffers
- Hardware telemetry must be non-blocking (separate polling thread)
- Modern UI trends: glassmorphism, subtle animations, adaptive layouts

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                         Game Process                             │
├─────────────────────────────────────────────────────────────────┤
│  ┌─────────────────┐    ┌─────────────────┐    ┌─────────────┐ │
│  │  Vulkan Layer   │───▶│  Overlay Core   │───▶│   ImGui     │ │
│  │  (hooks)        │    │  (rendering)    │    │   Backend   │ │
│  └────────┬────────┘    └────────┬────────┘    └─────────────┘ │
│           │                      │                              │
│  ┌────────▼────────┐    ┌────────▼────────┐                    │
│  │  Frametime      │    │   Telemetry     │                    │
│  │  Collector      │    │   Collector     │                    │
│  └────────┬────────┘    └─────────────────┘                    │
│           │                                                     │
└───────────┼─────────────────────────────────────────────────────┘
            │ IPC (Unix Socket)
┌───────────▼─────────────────────────────────────────────────────┐
│                      CapFrameX Daemon                            │
│  ┌─────────────────┐    ┌─────────────────┐                     │
│  │  Session        │    │  Config         │                     │
│  │  Recording      │    │  Manager        │                     │
│  └─────────────────┘    └─────────────────┘                     │
└─────────────────────────────────────────────────────────────────┘
```

---

## Phase 1: ImGui Integration (Foundation)

### 1.1 Add ImGui to Layer Build

**Files to Create/Modify:**
- `src/layer/CMakeLists.txt` - Add ImGui sources
- `src/layer/imgui/` - ImGui library files (copy from upstream)
- `src/layer/imgui_impl_vulkan.cpp` - Vulkan backend
- `src/layer/imgui_impl_vulkan.h` - Vulkan backend header

**ImGui Configuration:**
```cpp
// imgui_config.h
#define IMGUI_DISABLE_DEMO_WINDOWS
#define IMGUI_DISABLE_DEBUG_TOOLS
#define IMGUI_USE_WCHAR32  // Better Unicode support
```

**Required ImGui Files:**
- imgui.cpp, imgui.h
- imgui_draw.cpp
- imgui_tables.cpp
- imgui_widgets.cpp
- imgui_impl_vulkan.cpp (from imgui/backends)

### 1.2 Create Overlay Renderer

**File:** `src/layer/overlay_renderer.h`
```c
#ifndef OVERLAY_RENDERER_H
#define OVERLAY_RENDERER_H

#include <vulkan/vulkan.h>
#include <stdbool.h>

typedef struct OverlayRenderer OverlayRenderer;

// Lifecycle
OverlayRenderer* overlay_renderer_create(VkDevice device, VkPhysicalDevice physDevice,
                                          uint32_t graphicsQueueFamily, VkQueue queue);
void overlay_renderer_destroy(OverlayRenderer* renderer);

// Per-swapchain setup
bool overlay_renderer_setup_swapchain(OverlayRenderer* renderer, VkSwapchainKHR swapchain,
                                       VkFormat format, uint32_t width, uint32_t height,
                                       uint32_t imageCount, VkImage* images);
void overlay_renderer_cleanup_swapchain(OverlayRenderer* renderer, VkSwapchainKHR swapchain);

// Render overlay onto swapchain image
void overlay_renderer_draw(OverlayRenderer* renderer, VkSwapchainKHR swapchain,
                           uint32_t imageIndex, VkSemaphore waitSemaphore,
                           VkSemaphore signalSemaphore);

#endif
```

**File:** `src/layer/overlay_renderer.c`
```c
#include "overlay_renderer.h"
#include "imgui/imgui.h"
#include "imgui/imgui_impl_vulkan.h"

struct OverlayRenderer {
    VkDevice device;
    VkQueue queue;
    VkCommandPool commandPool;
    VkDescriptorPool descriptorPool;
    VkRenderPass renderPass;

    // Per-swapchain data
    struct SwapchainOverlayData {
        VkSwapchainKHR swapchain;
        VkFramebuffer* framebuffers;
        VkCommandBuffer* commandBuffers;
        uint32_t imageCount;
        uint32_t width, height;
    } swapchainData[8];  // Support up to 8 swapchains
    uint32_t swapchainCount;
};

// Implementation follows...
```

### 1.3 Hook Integration Points

**Modify:** `src/layer/layer.c`

```c
// In layer_CreateDevice - after device creation
static OverlayRenderer* g_overlayRenderer = NULL;

// After successful device creation:
g_overlayRenderer = overlay_renderer_create(device, physicalDevice,
                                             queueFamilyIndex, queue);

// In layer_DestroyDevice - before device destruction
overlay_renderer_destroy(g_overlayRenderer);
g_overlayRenderer = NULL;
```

**Modify:** `src/layer/swapchain.c`

```c
// In layer_CreateSwapchainKHR - after swapchain creation
if (g_overlayRenderer) {
    VkImage images[16];
    uint32_t imageCount = 16;
    vkGetSwapchainImagesKHR(device, *pSwapchain, &imageCount, images);

    overlay_renderer_setup_swapchain(g_overlayRenderer, *pSwapchain,
                                      pCreateInfo->imageFormat,
                                      pCreateInfo->imageExtent.width,
                                      pCreateInfo->imageExtent.height,
                                      imageCount, images);
}

// In layer_QueuePresentKHR - before presenting
if (g_overlayRenderer && overlay_enabled()) {
    overlay_renderer_draw(g_overlayRenderer, swapchain, imageIndex,
                          waitSemaphore, signalSemaphore);
}
```

---

## Phase 2: Telemetry Collection

### 2.1 CPU Telemetry

**File:** `src/layer/telemetry/cpu_telemetry.h`
```c
#ifndef CPU_TELEMETRY_H
#define CPU_TELEMETRY_H

typedef struct {
    float usage_percent;        // Overall CPU usage
    float per_core_usage[128];  // Per-core usage (max 128 cores)
    int core_count;
    float frequency_mhz;        // Current frequency
    float temperature_c;        // Package temperature (if available)
    float power_w;              // Package power (if available via RAPL)
} CpuTelemetry;

void cpu_telemetry_init(void);
void cpu_telemetry_update(void);
CpuTelemetry cpu_telemetry_get(void);
void cpu_telemetry_shutdown(void);

#endif
```

**Data Sources:**
- `/proc/stat` - CPU usage calculation
- `/proc/cpuinfo` - Frequency (if not available via sysfs)
- `/sys/devices/system/cpu/cpu*/cpufreq/scaling_cur_freq` - Per-core frequency
- `/sys/class/hwmon/hwmon*/temp*_input` - Temperature
- `/sys/class/powercap/intel-rapl/*/energy_uj` - Power consumption (Intel)

### 2.2 GPU Telemetry

**File:** `src/layer/telemetry/gpu_telemetry.h`
```c
#ifndef GPU_TELEMETRY_H
#define GPU_TELEMETRY_H

typedef enum {
    GPU_VENDOR_UNKNOWN,
    GPU_VENDOR_AMD,
    GPU_VENDOR_NVIDIA,
    GPU_VENDOR_INTEL
} GpuVendor;

typedef struct {
    GpuVendor vendor;
    float usage_percent;
    float vram_used_mb;
    float vram_total_mb;
    float core_clock_mhz;
    float memory_clock_mhz;
    float temperature_c;
    float power_w;
    float fan_rpm;
} GpuTelemetry;

void gpu_telemetry_init(void);
void gpu_telemetry_update(void);
GpuTelemetry gpu_telemetry_get(void);
void gpu_telemetry_shutdown(void);

#endif
```

**AMD Data Sources:**
- `/sys/class/drm/card*/device/gpu_busy_percent` - GPU utilization
- `/sys/class/drm/card*/device/mem_info_vram_used` - VRAM usage
- `/sys/class/drm/card*/device/mem_info_vram_total` - VRAM total
- `/sys/class/drm/card*/device/hwmon/hwmon*/temp1_input` - Temperature
- `/sys/class/drm/card*/device/hwmon/hwmon*/power1_average` - Power
- `/sys/class/drm/card*/device/pp_dpm_sclk` - Core clock states
- `/sys/class/drm/card*/device/pp_dpm_mclk` - Memory clock states

**NVIDIA Data Sources (via NVML):**
- Dynamic loading of `libnvidia-ml.so`
- `nvmlDeviceGetUtilizationRates()` - GPU/memory utilization
- `nvmlDeviceGetMemoryInfo()` - VRAM usage
- `nvmlDeviceGetClockInfo()` - Clock speeds
- `nvmlDeviceGetTemperature()` - Temperature
- `nvmlDeviceGetPowerUsage()` - Power draw

**Intel Data Sources:**
- `/sys/class/drm/card*/gt/gt*/rps_cur_freq_mhz` - Current frequency
- `intel_gpu_top` style parsing (requires i915 perf)

### 2.3 Memory Telemetry

**File:** `src/layer/telemetry/memory_telemetry.h`
```c
#ifndef MEMORY_TELEMETRY_H
#define MEMORY_TELEMETRY_H

typedef struct {
    uint64_t total_mb;
    uint64_t used_mb;
    uint64_t available_mb;
    float usage_percent;
    uint64_t swap_total_mb;
    uint64_t swap_used_mb;
} MemoryTelemetry;

void memory_telemetry_update(void);
MemoryTelemetry memory_telemetry_get(void);

#endif
```

**Data Source:** `/proc/meminfo`

### 2.4 Telemetry Manager

**File:** `src/layer/telemetry/telemetry_manager.h`
```c
#ifndef TELEMETRY_MANAGER_H
#define TELEMETRY_MANAGER_H

#include "cpu_telemetry.h"
#include "gpu_telemetry.h"
#include "memory_telemetry.h"

typedef struct {
    CpuTelemetry cpu;
    GpuTelemetry gpu;
    MemoryTelemetry memory;
    uint64_t timestamp_ns;
} SystemTelemetry;

// Starts background polling thread
void telemetry_manager_init(void);

// Gets latest telemetry snapshot (thread-safe)
SystemTelemetry telemetry_manager_get(void);

// Stops background thread
void telemetry_manager_shutdown(void);

// Configuration
void telemetry_manager_set_poll_interval_ms(int interval);

#endif
```

**Implementation Notes:**
- Use separate thread for telemetry polling (100ms default interval)
- Use atomic operations or mutex for thread-safe access
- Cache expensive operations (file descriptor reuse)
- Gracefully handle missing sensors

---

## Phase 3: Frametime Graph & Metrics

### 3.1 Frametime Buffer

**File:** `src/layer/metrics/frametime_buffer.h`
```c
#ifndef FRAMETIME_BUFFER_H
#define FRAMETIME_BUFFER_H

#include <stdint.h>

#define FRAMETIME_BUFFER_SIZE 512

typedef struct {
    float frametimes_ms[FRAMETIME_BUFFER_SIZE];
    uint32_t head;
    uint32_t count;

    // Calculated metrics
    float fps_current;
    float fps_avg;
    float fps_1_percent_low;
    float fps_0_1_percent_low;
    float frametime_avg;
    float frametime_max;
    float frametime_min;
} FrametimeBuffer;

void frametime_buffer_init(FrametimeBuffer* buf);
void frametime_buffer_add(FrametimeBuffer* buf, float frametime_ms);
void frametime_buffer_calculate_metrics(FrametimeBuffer* buf);

// For graph rendering
const float* frametime_buffer_get_data(FrametimeBuffer* buf, uint32_t* count);

#endif
```

### 3.2 Graph Renderer

**File:** `src/layer/ui/graph_renderer.h`
```c
#ifndef GRAPH_RENDERER_H
#define GRAPH_RENDERER_H

#include "imgui/imgui.h"

typedef struct {
    ImVec4 line_color;
    ImVec4 fill_color;
    ImVec4 grid_color;
    float line_thickness;
    bool show_grid;
    bool show_min_max;
    float target_frametime_ms;  // e.g., 16.67ms for 60fps target line
} GraphStyle;

void graph_render_frametime(const float* data, int count,
                            float width, float height,
                            const GraphStyle* style);

void graph_render_fps(const float* data, int count,
                      float width, float height,
                      const GraphStyle* style);

#endif
```

**Graph Features:**
- Smooth line rendering with anti-aliasing
- Optional gradient fill under line
- Horizontal grid lines at key thresholds (16.67ms, 33.33ms)
- Target frametime indicator line
- Min/max range indicators
- Adaptive Y-axis scaling

---

## Phase 4: Modern UI Design

### 4.1 Design Language: "CrystalHUD"

**Core Principles:**
1. **Glassmorphism** - Frosted glass effect with blur
2. **Minimal Chrome** - Thin borders, subtle shadows
3. **Accent Colors** - CapFrameX blue (#4AA3DF) as primary accent
4. **Information Hierarchy** - Large metrics, small labels
5. **Adaptive Density** - Compact mode for smaller screens

### 4.2 Color Palette

```c
// Primary colors
#define COLOR_BG_PRIMARY     ImVec4(0.08f, 0.12f, 0.16f, 0.85f)  // Dark blue-gray, translucent
#define COLOR_BG_SECONDARY   ImVec4(0.10f, 0.15f, 0.20f, 0.90f)  // Slightly lighter
#define COLOR_ACCENT         ImVec4(0.29f, 0.64f, 0.87f, 1.0f)   // CapFrameX blue #4AA3DF
#define COLOR_ACCENT_DIM     ImVec4(0.29f, 0.64f, 0.87f, 0.5f)   // Dimmed accent

// Metric colors
#define COLOR_FPS_GOOD       ImVec4(0.30f, 0.85f, 0.45f, 1.0f)   // Green - above target
#define COLOR_FPS_OK         ImVec4(0.95f, 0.75f, 0.20f, 1.0f)   // Yellow - near target
#define COLOR_FPS_BAD        ImVec4(0.95f, 0.30f, 0.25f, 1.0f)   // Red - below target

// Text colors
#define COLOR_TEXT_PRIMARY   ImVec4(1.0f, 1.0f, 1.0f, 1.0f)      // White
#define COLOR_TEXT_SECONDARY ImVec4(0.7f, 0.7f, 0.7f, 1.0f)      // Gray
#define COLOR_TEXT_DIM       ImVec4(0.5f, 0.5f, 0.5f, 1.0f)      // Dim gray

// Graph colors
#define COLOR_GRAPH_LINE     ImVec4(0.29f, 0.64f, 0.87f, 1.0f)   // Accent
#define COLOR_GRAPH_FILL     ImVec4(0.29f, 0.64f, 0.87f, 0.15f)  // Accent with low alpha
#define COLOR_GRAPH_GRID     ImVec4(1.0f, 1.0f, 1.0f, 0.1f)      // Subtle white
```

### 4.3 Layout System

**File:** `src/layer/ui/layout.h`
```c
#ifndef OVERLAY_LAYOUT_H
#define OVERLAY_LAYOUT_H

typedef enum {
    OVERLAY_POSITION_TOP_LEFT,
    OVERLAY_POSITION_TOP_RIGHT,
    OVERLAY_POSITION_BOTTOM_LEFT,
    OVERLAY_POSITION_BOTTOM_RIGHT,
    OVERLAY_POSITION_TOP_CENTER,
    OVERLAY_POSITION_BOTTOM_CENTER
} OverlayPosition;

typedef enum {
    OVERLAY_PRESET_MINIMAL,      // FPS only
    OVERLAY_PRESET_COMPACT,      // FPS + frametime graph
    OVERLAY_PRESET_STANDARD,     // FPS + graph + CPU/GPU usage
    OVERLAY_PRESET_DETAILED,     // All metrics
    OVERLAY_PRESET_CUSTOM        // User-defined
} OverlayPreset;

typedef struct {
    OverlayPosition position;
    OverlayPreset preset;
    float scale;                 // 0.5 - 2.0
    float opacity;               // 0.0 - 1.0
    int margin_x;
    int margin_y;

    // Component visibility
    bool show_fps;
    bool show_frametime;
    bool show_frametime_graph;
    bool show_1_percent_low;
    bool show_0_1_percent_low;
    bool show_cpu_usage;
    bool show_cpu_temp;
    bool show_cpu_power;
    bool show_gpu_usage;
    bool show_gpu_temp;
    bool show_gpu_power;
    bool show_gpu_clock;
    bool show_vram;
    bool show_ram;

    // Graph settings
    int graph_width;
    int graph_height;
    int graph_history_seconds;   // How many seconds of data to show
} OverlayConfig;

#endif
```

### 4.4 Component Designs

#### FPS Counter (Large, Prominent)
```
┌─────────────────┐
│   142.7         │  <- Large font, color-coded
│   FPS           │  <- Small label
└─────────────────┘
```

#### Frametime Graph (Signature Element)
```
┌─────────────────────────────────────┐
│ ▄▄  ▄▄█▄   ▄▄▄ ▄   ▄▄▄▄▄▄  ▄▄▄▄▄ │  <- Smooth line with gradient fill
│▄████████▄▄████▄█▄▄██████████████████│
│ 16.67ms ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─│  <- Target line (60fps)
│ 6.2ms avg                     8.1ms │  <- Current/avg labels
└─────────────────────────────────────┘
```

#### Metrics Row (Compact, Information-Dense)
```
┌─────────────────────────────────────────────┐
│ CPU 45% │ 65°C │ GPU 78% │ 72°C │ VRAM 8.2G│
└─────────────────────────────────────────────┘
```

#### Full Layout Example (Standard Preset)
```
┌─────────────────────────────────────┐
│                          CapFrameX  │  <- Subtle branding
├─────────────────────────────────────┤
│  ┌───────┐  ┌───────┐  ┌───────┐   │
│  │ 142.7 │  │  87.2 │  │  72.1 │   │
│  │  FPS  │  │ 1% Low│  │0.1%Low│   │
│  └───────┘  └───────┘  └───────┘   │
├─────────────────────────────────────┤
│  Frametime Graph                    │
│  ▄▄▄▄█▄▄▄▄▄▄▄▄▄▄▄▄▄▄█▄▄▄▄▄▄▄▄▄▄▄▄ │
│  ████████████████████████████████████│
│  7.0ms                        6.8ms │
├─────────────────────────────────────┤
│  CPU  42% ████░░░░░░   65°C   45W  │
│  GPU  78% ████████░░   72°C  185W  │
│  VRAM 8.2/12 GB ██████░░░░         │
│  RAM  12.4/32 GB ████░░░░░░        │
└─────────────────────────────────────┘
```

### 4.5 Font Loading

**Recommended Font:** Roboto Mono or JetBrains Mono (for metrics)
**Alternative:** Inter or Source Sans Pro (for labels)

```c
// Font loading in overlay_renderer_create()
ImGuiIO& io = ImGui::GetIO();

// Load from embedded data or system fonts
// Path: /usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf (fallback)
io.Fonts->AddFontFromFileTTF("/path/to/RobotoMono-Regular.ttf", 14.0f);

// Large font for FPS display
ImFontConfig config;
config.SizePixels = 32.0f;
io.Fonts->AddFontFromFileTTF("/path/to/RobotoMono-Bold.ttf", 32.0f, &config);
```

### 4.6 Animation & Polish

**Subtle Animations:**
- FPS number color transitions (smooth color lerp when threshold crossed)
- Graph line drawing animation on overlay show
- Fade in/out when toggling overlay

**Implementation:**
```c
// Color lerp for FPS
ImVec4 get_fps_color(float fps, float target_fps) {
    float ratio = fps / target_fps;
    if (ratio >= 1.0f) return COLOR_FPS_GOOD;
    if (ratio >= 0.75f) {
        float t = (ratio - 0.75f) / 0.25f;
        return ImLerp(COLOR_FPS_OK, COLOR_FPS_GOOD, t);
    }
    float t = ratio / 0.75f;
    return ImLerp(COLOR_FPS_BAD, COLOR_FPS_OK, t);
}
```

---

## Phase 5: Configuration System

### 5.1 Config File Format

**Path:** `~/.config/capframex/overlay.conf`

```ini
[General]
enabled=true
position=top_left
preset=standard
scale=1.0
opacity=0.85
toggle_key=F12

[Visibility]
fps=true
frametime=true
frametime_graph=true
1_percent_low=true
0_1_percent_low=true
cpu_usage=true
cpu_temp=true
gpu_usage=true
gpu_temp=true
gpu_power=true
vram=true
ram=false

[Graph]
width=300
height=80
history_seconds=10
show_grid=true
target_fps=60

[Colors]
accent=#4AA3DF
background_opacity=0.85
```

### 5.2 Runtime Configuration

**Environment Variables:**
- `CAPFRAMEX_OVERLAY_ENABLED=1` - Enable/disable overlay
- `CAPFRAMEX_OVERLAY_POSITION=top_left` - Position override
- `CAPFRAMEX_OVERLAY_PRESET=minimal` - Preset override
- `CAPFRAMEX_OVERLAY_SCALE=1.5` - Scale override

### 5.3 In-Game Toggle

**Hotkey Support:**
- Default: F12 (configurable)
- Cycle presets: Shift+F12
- Hide overlay: Ctrl+F12

**Implementation:**
```c
// Hook X11/Wayland input for hotkey detection
// Alternative: Read from /dev/input/event* with appropriate permissions
```

---

## Phase 6: Integration with CapFrameX App

### 6.1 IPC Protocol Extension

**New Messages:**
```c
// From App to Layer (via daemon)
typedef struct {
    uint8_t type;  // MSG_OVERLAY_CONFIG
    OverlayConfig config;
} OverlayConfigMessage;

typedef struct {
    uint8_t type;  // MSG_OVERLAY_TOGGLE
    bool enabled;
} OverlayToggleMessage;

// From Layer to App (via daemon)
typedef struct {
    uint8_t type;  // MSG_TELEMETRY_DATA
    SystemTelemetry telemetry;
    FrametimeMetrics frametime;
} TelemetryDataMessage;
```

### 6.2 App UI Integration

**Settings View Additions:**
- Overlay configuration panel
- Live preview of overlay layout
- Preset selector
- Individual metric toggles
- Color customization
- Hotkey configuration

**Capture View Additions:**
- Toggle overlay button
- Current overlay status indicator

---

## Implementation Timeline

### Phase 1: Foundation (Week 1-2)
- [ ] Add ImGui to layer build system
- [ ] Create basic overlay renderer
- [ ] Hook into swapchain presentation
- [ ] Render simple FPS counter

### Phase 2: Telemetry (Week 2-3)
- [ ] Implement CPU telemetry collection
- [ ] Implement GPU telemetry (AMD sysfs)
- [ ] Implement memory telemetry
- [ ] Create telemetry manager with background thread
- [ ] Add NVIDIA NVML support (optional)

### Phase 3: Metrics & Graphs (Week 3-4)
- [ ] Implement frametime buffer
- [ ] Create graph renderer
- [ ] Calculate FPS metrics (avg, 1% low, 0.1% low)
- [ ] Add target frametime indicator

### Phase 4: UI Polish (Week 4-5)
- [ ] Implement color palette
- [ ] Create layout system
- [ ] Design all component variants
- [ ] Add font loading
- [ ] Implement presets
- [ ] Add subtle animations

### Phase 5: Configuration (Week 5-6)
- [ ] Create config file parser
- [ ] Implement environment variable overrides
- [ ] Add hotkey support for toggle
- [ ] Create preset cycling

### Phase 6: App Integration (Week 6-7)
- [ ] Extend IPC protocol
- [ ] Add overlay settings to app UI
- [ ] Implement live telemetry display in app
- [ ] Testing and polish

---

## File Structure Summary

```
src/layer/
├── CMakeLists.txt              # Updated build config
├── layer.c                     # Updated with overlay integration
├── layer.h
├── swapchain.c                 # Updated with overlay rendering
├── swapchain.h
├── overlay_renderer.c          # NEW: Main overlay rendering
├── overlay_renderer.h
├── overlay_config.c            # NEW: Configuration loading
├── overlay_config.h
├── imgui/                      # NEW: ImGui library
│   ├── imgui.cpp
│   ├── imgui.h
│   ├── imgui_draw.cpp
│   ├── imgui_tables.cpp
│   ├── imgui_widgets.cpp
│   ├── imgui_impl_vulkan.cpp
│   ├── imgui_impl_vulkan.h
│   └── imgui_config.h
├── telemetry/                  # NEW: Hardware telemetry
│   ├── telemetry_manager.c
│   ├── telemetry_manager.h
│   ├── cpu_telemetry.c
│   ├── cpu_telemetry.h
│   ├── gpu_telemetry.c
│   ├── gpu_telemetry.h
│   ├── gpu_amd.c               # AMD-specific implementation
│   ├── gpu_nvidia.c            # NVIDIA NVML implementation
│   ├── memory_telemetry.c
│   └── memory_telemetry.h
├── metrics/                    # NEW: Frametime metrics
│   ├── frametime_buffer.c
│   ├── frametime_buffer.h
│   ├── statistics.c            # Percentile calculations
│   └── statistics.h
└── ui/                         # NEW: UI components
    ├── layout.c
    ├── layout.h
    ├── graph_renderer.c
    ├── graph_renderer.h
    ├── colors.h
    └── styles.h
```

---

## Risk Assessment & Mitigations

### Risk 1: Performance Impact
**Concern:** Overlay rendering adds latency
**Mitigation:**
- Render overlay on separate command buffer
- Use efficient ImGui drawing (batched)
- Profile and optimize hot paths
- Option to reduce update frequency

### Risk 2: Game Compatibility
**Concern:** Some games may conflict with layer
**Mitigation:**
- Extensive testing with popular titles
- Blacklist mechanism for problematic games
- Fallback to simpler rendering if issues detected

### Risk 3: Vulkan Version Compatibility
**Concern:** Different Vulkan versions/extensions
**Mitigation:**
- Target Vulkan 1.0 minimum
- Query capabilities before using features
- Graceful degradation

### Risk 4: Multi-GPU Systems
**Concern:** Systems with both discrete and integrated GPU
**Mitigation:**
- Detect active GPU from Vulkan device properties
- Show telemetry for GPU running the game
- Option to show both in detailed mode

---

## Success Metrics

1. **Performance:** Less than 0.5ms added frame latency
2. **Accuracy:** FPS/frametime within 1% of MangoHud
3. **Compatibility:** Works with 95%+ of Vulkan games tested
4. **Usability:** Positive feedback on visual design
5. **Stability:** No crashes during extended gaming sessions

---

## References

- [MangoHud Source Code](https://github.com/flightlessmango/MangoHud)
- [Dear ImGui](https://github.com/ocornut/imgui)
- [Vulkan Layer Specification](https://vulkan.lunarg.com/doc/view/latest/linux/layer_configuration.html)
- [AMD GPU sysfs Interface](https://docs.kernel.org/gpu/amdgpu/driver-misc.html)
- [NVIDIA NVML Documentation](https://developer.nvidia.com/nvidia-management-library-nvml)

---

*Document Version: 1.0*
*Created: January 2026*
*Author: CapFrameX Development Team*
