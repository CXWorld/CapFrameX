#ifndef CAPFRAMEX_IPC_CLIENT_H
#define CAPFRAMEX_IPC_CLIENT_H

#include "timing.h"
#include <stdbool.h>
#include <stdint.h>

// Initialize IPC client
void ipc_client_init(void);

// Cleanup IPC client
void ipc_client_cleanup(void);

// Connect to daemon
bool ipc_client_connect(void);

// Check if connected to daemon
bool ipc_client_is_connected(void);

// Check if capture is active (requested by app)
bool ipc_client_is_capture_active(void);

// Send frame data to app
void ipc_client_send_frame_data(const FrameTimingData* frame);

// Notify about swapchain creation
void ipc_client_notify_swapchain_created(uint32_t width, uint32_t height);

// Start capture (called by daemon/app via IPC)
void ipc_client_start_capture(const char* game_name, const char* gpu_name,
                               uint32_t width, uint32_t height);

// Stop capture
void ipc_client_stop_capture(void);

#endif // CAPFRAMEX_IPC_CLIENT_H
