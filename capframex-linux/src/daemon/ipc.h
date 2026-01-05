#ifndef CAPFRAMEX_IPC_H
#define CAPFRAMEX_IPC_H

#include "common.h"

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

// Send a message to a specific client
int ipc_send(int client_fd, MessageType type, void* payload, uint32_t payload_size);

// Update shared memory with active game PIDs
int ipc_update_active_pids(pid_t* pids, uint32_t count);

// Get the socket path
const char* ipc_get_socket_path(void);

// Check if a client is connected
bool ipc_has_clients(void);

#endif // CAPFRAMEX_IPC_H
