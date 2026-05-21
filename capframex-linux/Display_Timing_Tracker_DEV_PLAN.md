# DRM Display Timing Tracker - Development Plan

## Goal
Track time between display changes (page flips) on Linux, comparable to ETW's MsBetweenDisplayChange metric on Windows.

## Overview
Use the Linux DRM subsystem to capture page flip completion events with vblank timestamps, then compute deltas between consecutive flips.

---

## Phase 1: Basic DRM Event Capture

### Tasks
1. Open the DRM device (`/dev/dri/card0` or appropriate device)
2. Set up vblank event handling via `drmWaitVBlank()` or async `drmHandleEvent()`
3. Parse `struct drm_event_vblank` for timestamps
4. Store timestamps and compute deltas

### Key APIs
```c
#include <xf86drm.h>
#include <xf86drmMode.h>

int fd = open("/dev/dri/card0", O_RDWR);
drmEventContext evctx = {
    .version = DRM_EVENT_CONTEXT_VERSION,
    .page_flip_handler = flip_handler,
    .vblank_handler = vblank_handler,
};
```

### Acceptance Criteria
- [ ] Successfully open DRM device
- [ ] Receive vblank/page flip events
- [ ] Print timestamp deltas to stdout

---

## Phase 2: Integration with Your App

### Tasks
1. Create a separate monitoring thread (non-blocking poll on DRM fd)
2. Build a ring buffer for recent frame timing data
3. Expose API to query timing stats (min/max/avg ms between flips, missed frames)

### Suggested Interface
```c
typedef struct {
    double ms_between_display_change;
    uint64_t vblank_sequence;
    uint64_t timestamp_us;
    bool frame_missed;  // sequence gap > 1
} display_timing_sample_t;

// API
int display_timing_init(const char* drm_device);
int display_timing_get_latest(display_timing_sample_t* out);
void display_timing_get_stats(double* avg_ms, double* min_ms, double* max_ms);
void display_timing_shutdown(void);
```

### Acceptance Criteria
- [ ] Non-blocking integration with main app loop
- [ ] No measurable performance impact
- [ ] Detect missed/dropped frames via sequence gaps

---

## Phase 3: Multi-CRTC / Multi-Monitor Support

### Tasks
1. Enumerate CRTCs via `drmModeGetResources()`
2. Track timing per-CRTC (each monitor has its own flip cadence)
3. Allow filtering by specific output

### Acceptance Criteria
- [ ] Correctly track independent refresh rates (e.g., 60Hz + 144Hz setup)
- [ ] Label data by CRTC/connector

---

## Phase 4: Correlation with App Frames (Optional)

### Tasks
1. Instrument your app's frame submission (GL/Vulkan timestamp queries)
2. Match submitted frames to display flip events
3. Calculate full latency: submit → flip → scanout

### Notes
- Vulkan: use `VK_GOOGLE_display_timing` or `VK_EXT_present_timing` if available
- OpenGL: `glXGetSyncValuesOML()` / `glXGetMscRateOML()` on GLX
- Wayland: `wp_presentation_feedback` protocol

---

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `libdrm` | DRM userspace API |
| `libudev` (optional) | Device enumeration |
| `pthreads` | Async monitoring thread |

Install on Debian/Ubuntu:
```bash
sudo apt install libdrm-dev libudev-dev
```

---

## Files to Create

```
src/
├── display_timing.h      # Public API
├── display_timing.c      # Core implementation
├── drm_events.c          # Low-level DRM event handling
└── main.c                # Test harness / example usage
```

---

## Quick Start Commands for Claude Code

```bash
# Phase 1: Scaffold the project
claude "Create a C project with libdrm that opens /dev/dri/card0, polls for page flip events, and prints the ms delta between consecutive vblank timestamps"

# Phase 2: Add threading
claude "Add a monitoring thread with a ring buffer that stores the last 120 display timing samples, expose a function to get average ms between flips"

# Phase 3: Multi-monitor
claude "Extend to enumerate all CRTCs and track timing per-CRTC separately"
```

---

## References

- [DRM Developer Guide](https://dri.freedesktop.org/docs/drm/)
- [libdrm source](https://gitlab.freedesktop.org/mesa/drm)
- `drm_event_vblank` struct in `<drm/drm.h>`
- Example: `modetest` utility in libdrm (`tests/modetest/`)
