#ifndef CAPFRAMEX_TIMING_H
#define CAPFRAMEX_TIMING_H

#include <stdint.h>
#include <stdbool.h>

// Frame data structure
typedef struct {
    uint64_t frame_number;
    uint64_t timestamp_ns;
    float frametime_ms;           // CPU sampled frametime (CLOCK_MONOTONIC_RAW delta)
    float present_time_ms;        // Time spent in present call
    uint64_t actual_present_time_ns;  // From VK_EXT_present_timing (0 if not available)
    float ms_until_render_complete;   // Time until render complete (0 if not available)
    float ms_until_displayed;         // Time until displayed (0 if not available)
    float actual_frametime_ms;    // Frametime from actual present timing (0 if not available)
} FrameTimingData;

// Ring buffer size (~60 seconds at 144fps)
#define FRAME_BUFFER_SIZE 8640

// Initialize timing system
void timing_init(void);

// Cleanup timing system
void timing_cleanup(void);

// Get current timestamp in nanoseconds
uint64_t timing_get_timestamp(void);

// Record a frame timing (with optional actual present time from extension)
void timing_record_frame(uint64_t frame_number, uint64_t pre_present_ns, uint64_t post_present_ns,
                         uint64_t actual_present_time_ns, float ms_until_render_complete,
                         float ms_until_displayed);

// Get the frame buffer for reading (returns pointer to ring buffer)
const FrameTimingData* timing_get_frame_buffer(void);

// Get number of frames currently in buffer
uint32_t timing_get_frame_count(void);

// Get the most recent frame data
bool timing_get_latest_frame(FrameTimingData* out);

// Get frames since a given frame number
// Returns number of frames copied, up to max_frames
uint32_t timing_get_frames_since(uint64_t since_frame, FrameTimingData* out, uint32_t max_frames);

// Clear the frame buffer
void timing_clear_buffer(void);

// Get average frametime over the last N frames
float timing_get_average_frametime(uint32_t num_frames);

// Get current FPS (based on recent frames)
float timing_get_current_fps(void);

#endif // CAPFRAMEX_TIMING_H
