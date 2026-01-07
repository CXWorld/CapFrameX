#ifndef CAPFRAMEX_IPC_CLIENT_H
#define CAPFRAMEX_IPC_CLIENT_H

#include "timing.h"
#include <stdbool.h>
#include <stdint.h>

// Initialize IPC client (caches process info)
void ipc_client_init(void);

// Cleanup IPC client
void ipc_client_cleanup(void);

// Connect to daemon
bool ipc_client_connect(void);

// Check if connected to daemon
bool ipc_client_is_connected(void);

// Try to reconnect if not connected (rate-limited)
bool ipc_client_try_reconnect(void);

// Set GPU name (call when device is created)
void ipc_client_set_gpu_name(const char* gpu_name);

// Send hello message to daemon (announces this layer instance)
void ipc_client_send_hello(const char* gpu_name);

// Notify daemon about swapchain creation
void ipc_client_send_swapchain_created(uint32_t width, uint32_t height,
                                        uint32_t format, uint32_t image_count);

// Notify daemon about swapchain destruction
void ipc_client_send_swapchain_destroyed(void);

// Send frame data to daemon (always streams when connected)
void ipc_client_send_frame_data(const FrameTimingData* frame);

#endif // CAPFRAMEX_IPC_CLIENT_H
