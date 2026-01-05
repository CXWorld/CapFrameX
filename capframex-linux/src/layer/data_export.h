#ifndef CAPFRAMEX_DATA_EXPORT_H
#define CAPFRAMEX_DATA_EXPORT_H

#include "timing.h"
#include <stdbool.h>

// Capture session data
typedef struct {
    char game_name[256];
    char gpu_name[256];
    uint64_t start_time;
    uint64_t end_time;
    uint32_t resolution_width;
    uint32_t resolution_height;
} CaptureSessionInfo;

// Start a new capture session
void data_export_start_session(const CaptureSessionInfo* info);

// End the current capture session
void data_export_end_session(void);

// Check if a capture is in progress
bool data_export_is_capturing(void);

// Add a frame to the current capture
void data_export_add_frame(const FrameTimingData* frame);

// Export captured data to CSV file
// Returns 0 on success, -1 on failure
int data_export_to_csv(const char* filepath);

// Export captured data to JSON file (for metadata)
int data_export_to_json(const char* filepath);

// Get the default export directory
const char* data_export_get_default_dir(void);

// Generate a filename based on game name and timestamp
void data_export_generate_filename(char* buffer, size_t buffer_size,
                                   const char* game_name, const char* extension);

#endif // CAPFRAMEX_DATA_EXPORT_H
