#ifndef CAPFRAMEX_IPC_H
#define CAPFRAMEX_IPC_H

#include "common.h"

// Client types
typedef enum {
    CLIENT_TYPE_UNKNOWN = 0,
    CLIENT_TYPE_APP,      // UI application (subscriber)
    CLIENT_TYPE_LAYER     // Vulkan layer (producer)
} ClientType;

// Layer client info (registered via MSG_LAYER_HELLO)
typedef struct {
    int fd;
    pid_t pid;
    char process_name[MAX_GAME_NAME_LENGTH];
    char gpu_name[MAX_GAME_NAME_LENGTH];
    uint32_t swapchain_width;
    uint32_t swapchain_height;
    uint32_t swapchain_format;
    bool has_swapchain;
    bool present_timing_supported;  // VK_EXT_present_timing available
} LayerClient;

// App subscription info
typedef struct {
    int fd;
    pid_t subscribed_pid;  // PID of the layer to receive frames from (0 = none)
} AppSubscription;

// Callback for received messages
typedef void (*ipc_message_callback)(MessageHeader* header, void* payload, int client_fd);

// Initialize IPC (creates socket and shared memory)
int ipc_init(void);

// Start listening for connections
int ipc_start(ipc_message_callback callback);

// Stop IPC server
void ipc_stop(void);

// Cleanup resources
void ipc_cleanup(void);

// Send a message to all connected clients
int ipc_broadcast(MessageType type, void* payload, uint32_t payload_size);

// Broadcast only to app clients
int ipc_broadcast_to_apps(MessageType type, void* payload, uint32_t payload_size);

// Broadcast to all non-layer clients (apps and unknown clients)
int ipc_broadcast_to_non_layers(MessageType type, void* payload, uint32_t payload_size);

// Send a message to a specific client
int ipc_send(int client_fd, MessageType type, void* payload, uint32_t payload_size);

// Update shared memory with active game PIDs
int ipc_update_active_pids(pid_t* pids, uint32_t count);

// Get the socket path
const char* ipc_get_socket_path(void);

// Check if any clients are connected
bool ipc_has_clients(void);

// Layer client management
// Returns true if this is a new layer (not already registered by PID), false if updated existing
bool ipc_register_layer(int client_fd, const LayerHelloPayload* hello);
void ipc_update_layer_swapchain(int client_fd, const SwapchainInfoPayload* info);
void ipc_unregister_layer(int client_fd);
LayerClient* ipc_get_layer_by_pid(pid_t pid);
LayerClient* ipc_get_layer_by_fd(int fd);
int ipc_get_layer_count(void);
LayerClient* ipc_get_layers(int* count);

// Thread-safe copy of layer data (caller provides buffer)
// Returns actual count copied. Buffer should be at least MAX_LAYERS sized.
int ipc_get_layers_copy(LayerClient* buffer, int max_count);

// Thread-safe copy of a single layer by PID
// Returns true if found and copied, false if not found
bool ipc_get_layer_by_pid_copy(pid_t pid, LayerClient* out);

// App subscription management
void ipc_subscribe_app(int client_fd, pid_t target_pid);
void ipc_unsubscribe_app(int client_fd);
void ipc_unregister_app(int client_fd);

// Forward frame data to subscribed apps
void ipc_forward_frame_data(const FrameDataPoint* frame);

// Get client type
ClientType ipc_get_client_type(int fd);

// Check if a process name is blacklisted (should not appear in game list)
bool ipc_is_blacklisted_process(const char* process_name);

#endif // CAPFRAMEX_IPC_H
