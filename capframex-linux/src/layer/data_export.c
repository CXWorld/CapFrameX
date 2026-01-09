#include "data_export.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>
#include <sys/stat.h>
#include <pthread.h>
#include <errno.h>

#define MAX_CAPTURED_FRAMES 1000000  // ~4.6 hours at 60fps

static CaptureSessionInfo current_session;
static FrameTimingData* captured_frames = NULL;
static uint32_t captured_frame_count = 0;
static bool capturing = false;
static pthread_mutex_t export_mutex = PTHREAD_MUTEX_INITIALIZER;

static void ensure_directory(const char* path) {
    struct stat st;
    if (stat(path, &st) == -1) {
        mkdir(path, 0755);
    }
}

const char* data_export_get_default_dir(void) {
    static char path[512];
    static bool initialized = false;

    if (!initialized) {
        const char* home = getenv("HOME");
        const char* xdg_data = getenv("XDG_DATA_HOME");

        if (xdg_data) {
            snprintf(path, sizeof(path), "%s/capframex/sessions", xdg_data);
        } else if (home) {
            snprintf(path, sizeof(path), "%s/.local/share/capframex/sessions", home);
        } else {
            snprintf(path, sizeof(path), "/tmp/capframex/sessions");
        }

        initialized = true;
    }

    return path;
}

void data_export_generate_filename(char* buffer, size_t buffer_size,
                                   const char* game_name, const char* extension) {
    time_t now = time(NULL);
    struct tm* tm_info = localtime(&now);

    char timestamp[64];
    strftime(timestamp, sizeof(timestamp), "%Y%m%d_%H%M%S", tm_info);

    // Sanitize game name (replace special chars with underscore)
    char safe_name[256];
    size_t j = 0;
    for (size_t i = 0; game_name[i] && j < sizeof(safe_name) - 1; i++) {
        char c = game_name[i];
        if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
            (c >= '0' && c <= '9') || c == '-' || c == '_') {
            safe_name[j++] = c;
        } else if (c == ' ') {
            safe_name[j++] = '_';
        }
    }
    safe_name[j] = '\0';

    snprintf(buffer, buffer_size, "%s_%s.%s", safe_name, timestamp, extension);
}

void data_export_start_session(const CaptureSessionInfo* info) {
    pthread_mutex_lock(&export_mutex);

    if (capturing) {
        pthread_mutex_unlock(&export_mutex);
        return;
    }

    memcpy(&current_session, info, sizeof(CaptureSessionInfo));
    current_session.start_time = time(NULL);

    // Allocate frame buffer
    captured_frames = calloc(MAX_CAPTURED_FRAMES, sizeof(FrameTimingData));
    if (!captured_frames) {
        fprintf(stderr, "[CapFrameX Layer] Failed to allocate capture buffer\n");
        pthread_mutex_unlock(&export_mutex);
        return;
    }

    captured_frame_count = 0;
    capturing = true;

    fprintf(stderr, "[CapFrameX Layer] Capture started: %s\n", info->game_name);

    pthread_mutex_unlock(&export_mutex);
}

void data_export_end_session(void) {
    pthread_mutex_lock(&export_mutex);

    if (!capturing) {
        pthread_mutex_unlock(&export_mutex);
        return;
    }

    current_session.end_time = time(NULL);
    capturing = false;

    fprintf(stderr, "[CapFrameX Layer] Capture ended: %u frames captured\n",
            captured_frame_count);

    // Auto-export to default location
    const char* export_dir = data_export_get_default_dir();
    ensure_directory(export_dir);

    char filename[512];
    char filepath[1024];

    data_export_generate_filename(filename, sizeof(filename),
                                  current_session.game_name, "csv");
    snprintf(filepath, sizeof(filepath), "%s/%s", export_dir, filename);

    pthread_mutex_unlock(&export_mutex);

    data_export_to_csv(filepath);

    // Export JSON metadata
    pthread_mutex_lock(&export_mutex);
    data_export_generate_filename(filename, sizeof(filename),
                                  current_session.game_name, "json");
    snprintf(filepath, sizeof(filepath), "%s/%s", export_dir, filename);
    pthread_mutex_unlock(&export_mutex);

    data_export_to_json(filepath);

    // Free the buffer
    pthread_mutex_lock(&export_mutex);
    if (captured_frames) {
        free(captured_frames);
        captured_frames = NULL;
    }
    captured_frame_count = 0;
    pthread_mutex_unlock(&export_mutex);
}

bool data_export_is_capturing(void) {
    pthread_mutex_lock(&export_mutex);
    bool result = capturing;
    pthread_mutex_unlock(&export_mutex);
    return result;
}

void data_export_add_frame(const FrameTimingData* frame) {
    pthread_mutex_lock(&export_mutex);

    if (!capturing || !captured_frames) {
        pthread_mutex_unlock(&export_mutex);
        return;
    }

    if (captured_frame_count < MAX_CAPTURED_FRAMES) {
        captured_frames[captured_frame_count++] = *frame;
    }

    pthread_mutex_unlock(&export_mutex);
}

int data_export_to_csv(const char* filepath) {
    pthread_mutex_lock(&export_mutex);

    if (!captured_frames || captured_frame_count == 0) {
        pthread_mutex_unlock(&export_mutex);
        return -1;
    }

    FILE* f = fopen(filepath, "w");
    if (!f) {
        fprintf(stderr, "[CapFrameX Layer] Failed to open %s: %s\n",
                filepath, strerror(errno));
        pthread_mutex_unlock(&export_mutex);
        return -1;
    }

    // Write CSV header (compatible with Windows CapFrameX format)
    fprintf(f, "MsBetweenPresents,MsUntilRenderComplete,MsUntilDisplayed\n");

    // Write frame data
    for (uint32_t i = 0; i < captured_frame_count; i++) {
        FrameTimingData* frame = &captured_frames[i];
        // MsBetweenPresents = frametime
        // MsUntilRenderComplete ≈ present_time (approximation)
        // MsUntilDisplayed = frametime (no GPU timestamp available)
        fprintf(f, "%.2f,%.2f,%.2f\n",
                frame->frametime_ms,
                frame->present_time_ms,
                frame->frametime_ms);
    }

    fclose(f);
    fprintf(stderr, "[CapFrameX Layer] Exported %u frames to %s\n",
            captured_frame_count, filepath);

    pthread_mutex_unlock(&export_mutex);
    return 0;
}

int data_export_to_json(const char* filepath) {
    pthread_mutex_lock(&export_mutex);

    FILE* f = fopen(filepath, "w");
    if (!f) {
        pthread_mutex_unlock(&export_mutex);
        return -1;
    }

    // Calculate basic statistics
    float min_ft = 1000000.0f, max_ft = 0.0f, sum_ft = 0.0f;
    for (uint32_t i = 0; i < captured_frame_count; i++) {
        float ft = captured_frames[i].frametime_ms;
        if (ft > 0) {
            if (ft < min_ft) min_ft = ft;
            if (ft > max_ft) max_ft = ft;
            sum_ft += ft;
        }
    }
    float avg_ft = (captured_frame_count > 0) ? sum_ft / captured_frame_count : 0;
    float avg_fps = (avg_ft > 0) ? 1000.0f / avg_ft : 0;

    fprintf(f, "{\n");
    fprintf(f, "  \"game\": \"%s\",\n", current_session.game_name);
    fprintf(f, "  \"gpu\": \"%s\",\n", current_session.gpu_name);
    fprintf(f, "  \"resolution\": \"%ux%u\",\n",
            current_session.resolution_width, current_session.resolution_height);
    fprintf(f, "  \"start_time\": %lu,\n", (unsigned long)current_session.start_time);
    fprintf(f, "  \"end_time\": %lu,\n", (unsigned long)current_session.end_time);
    fprintf(f, "  \"duration_seconds\": %lu,\n",
            (unsigned long)(current_session.end_time - current_session.start_time));
    fprintf(f, "  \"frame_count\": %u,\n", captured_frame_count);
    fprintf(f, "  \"statistics\": {\n");
    fprintf(f, "    \"average_fps\": %.2f,\n", avg_fps);
    fprintf(f, "    \"average_frametime_ms\": %.2f,\n", avg_ft);
    fprintf(f, "    \"min_frametime_ms\": %.2f,\n", min_ft);
    fprintf(f, "    \"max_frametime_ms\": %.2f\n", max_ft);
    fprintf(f, "  }\n");
    fprintf(f, "}\n");

    fclose(f);

    pthread_mutex_unlock(&export_mutex);
    return 0;
}
