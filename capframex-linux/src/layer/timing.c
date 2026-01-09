#include "timing.h"
#include "ipc_client.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>
#include <pthread.h>

static FrameTimingData frame_buffer[FRAME_BUFFER_SIZE];
static uint32_t buffer_head = 0;
static uint32_t buffer_count = 0;
static uint64_t last_frame_time = 0;
static pthread_mutex_t timing_mutex = PTHREAD_MUTEX_INITIALIZER;

void timing_init(void) {
    pthread_mutex_lock(&timing_mutex);
    memset(frame_buffer, 0, sizeof(frame_buffer));
    buffer_head = 0;
    buffer_count = 0;
    last_frame_time = 0;
    pthread_mutex_unlock(&timing_mutex);
}

void timing_cleanup(void) {
    timing_clear_buffer();
}

uint64_t timing_get_timestamp(void) {
    struct timespec ts;
    clock_gettime(CLOCK_MONOTONIC, &ts);
    return (uint64_t)ts.tv_sec * 1000000000ULL + (uint64_t)ts.tv_nsec;
}

void timing_record_frame(uint64_t frame_number, uint64_t pre_present_ns, uint64_t post_present_ns) {
    pthread_mutex_lock(&timing_mutex);

    FrameTimingData* frame = &frame_buffer[buffer_head];

    frame->frame_number = frame_number;
    frame->timestamp_ns = pre_present_ns;

    // Calculate frametime (time since last frame)
    if (last_frame_time > 0) {
        frame->frametime_ms = (float)(pre_present_ns - last_frame_time) / 1000000.0f;
    } else {
        frame->frametime_ms = 0.0f;
    }

    // Time spent in the present call itself
    frame->present_time_ms = (float)(post_present_ns - pre_present_ns) / 1000000.0f;

    last_frame_time = pre_present_ns;

    // Advance ring buffer
    buffer_head = (buffer_head + 1) % FRAME_BUFFER_SIZE;
    if (buffer_count < FRAME_BUFFER_SIZE) {
        buffer_count++;
    }

    pthread_mutex_unlock(&timing_mutex);

    // Always send frame data to daemon (continuous streaming)
    ipc_client_send_frame_data(frame);
}

const FrameTimingData* timing_get_frame_buffer(void) {
    return frame_buffer;
}

uint32_t timing_get_frame_count(void) {
    pthread_mutex_lock(&timing_mutex);
    uint32_t count = buffer_count;
    pthread_mutex_unlock(&timing_mutex);
    return count;
}

bool timing_get_latest_frame(FrameTimingData* out) {
    pthread_mutex_lock(&timing_mutex);

    if (buffer_count == 0) {
        pthread_mutex_unlock(&timing_mutex);
        return false;
    }

    uint32_t latest_idx = (buffer_head == 0) ? FRAME_BUFFER_SIZE - 1 : buffer_head - 1;
    *out = frame_buffer[latest_idx];

    pthread_mutex_unlock(&timing_mutex);
    return true;
}

uint32_t timing_get_frames_since(uint64_t since_frame, FrameTimingData* out, uint32_t max_frames) {
    pthread_mutex_lock(&timing_mutex);

    uint32_t copied = 0;
    uint32_t start_idx;

    if (buffer_count < FRAME_BUFFER_SIZE) {
        start_idx = 0;
    } else {
        start_idx = buffer_head;  // Oldest frame in a full buffer
    }

    for (uint32_t i = 0; i < buffer_count && copied < max_frames; i++) {
        uint32_t idx = (start_idx + i) % FRAME_BUFFER_SIZE;
        if (frame_buffer[idx].frame_number > since_frame) {
            out[copied++] = frame_buffer[idx];
        }
    }

    pthread_mutex_unlock(&timing_mutex);
    return copied;
}

void timing_clear_buffer(void) {
    pthread_mutex_lock(&timing_mutex);
    memset(frame_buffer, 0, sizeof(frame_buffer));
    buffer_head = 0;
    buffer_count = 0;
    last_frame_time = 0;
    pthread_mutex_unlock(&timing_mutex);
}

float timing_get_average_frametime(uint32_t num_frames) {
    pthread_mutex_lock(&timing_mutex);

    if (buffer_count == 0) {
        pthread_mutex_unlock(&timing_mutex);
        return 0.0f;
    }

    uint32_t count = (num_frames < buffer_count) ? num_frames : buffer_count;
    float sum = 0.0f;
    uint32_t valid_count = 0;

    // Start from the most recent frame
    for (uint32_t i = 0; i < count; i++) {
        uint32_t idx = (buffer_head - 1 - i + FRAME_BUFFER_SIZE) % FRAME_BUFFER_SIZE;
        if (frame_buffer[idx].frametime_ms > 0.0f) {
            sum += frame_buffer[idx].frametime_ms;
            valid_count++;
        }
    }

    pthread_mutex_unlock(&timing_mutex);

    return (valid_count > 0) ? sum / valid_count : 0.0f;
}

float timing_get_current_fps(void) {
    float avg_frametime = timing_get_average_frametime(60);  // Average over ~1 second at 60fps
    if (avg_frametime <= 0.0f) {
        return 0.0f;
    }
    return 1000.0f / avg_frametime;
}
