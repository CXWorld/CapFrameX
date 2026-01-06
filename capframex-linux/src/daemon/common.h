#ifndef CAPFRAMEX_COMMON_H
#define CAPFRAMEX_COMMON_H

#include <stdint.h>
#include <stdbool.h>
#include <limits.h>
#include <sys/types.h>

#define CAPFRAMEX_VERSION "1.0.0"
#define CAPFRAMEX_SOCKET_NAME "capframex.sock"
#define CAPFRAMEX_SHM_NAME "/capframex_pids"
#define CAPFRAMEX_SHM_DATA_NAME "/capframex_framedata"

#define MAX_TRACKED_PROCESSES 256
#define MAX_GAME_NAME_LENGTH 256
#define MAX_PATH_LENGTH PATH_MAX

// IPC Message Types
typedef enum {
    MSG_GAME_STARTED = 1,      // Daemon -> App: game process detected
    MSG_GAME_STOPPED = 2,      // Daemon -> App: game process exited
    MSG_START_CAPTURE = 3,     // App -> Daemon: subscribe to frame stream for PID
    MSG_STOP_CAPTURE = 4,      // App -> Daemon: unsubscribe from frame stream
    MSG_FRAMETIME_DATA = 5,    // Layer -> Daemon -> App: continuous frame data
    MSG_PING = 6,              // Keepalive
    MSG_PONG = 7,              // Keepalive response
    MSG_CONFIG_UPDATE = 8,     // App -> Daemon/Layer
    MSG_STATUS_REQUEST = 9,    // App -> Daemon
    MSG_STATUS_RESPONSE = 10,  // Daemon -> App
    MSG_LAYER_HELLO = 11,      // Layer -> Daemon: layer announces itself with PID/process info
    MSG_SWAPCHAIN_CREATED = 12,// Layer -> Daemon: swapchain info (resolution, format)
    MSG_SWAPCHAIN_DESTROYED = 13, // Layer -> Daemon: swapchain destroyed
} MessageType;

// Process information structure
typedef struct {
    pid_t pid;
    char exe_path[MAX_PATH_LENGTH];
    char exe_name[MAX_GAME_NAME_LENGTH];
    pid_t parent_pid;
    char parent_name[MAX_GAME_NAME_LENGTH];
    uint64_t start_time;
    bool is_game;
    bool is_capturing;
} ProcessInfo;

// IPC Message header
typedef struct {
    uint32_t type;
    uint32_t payload_size;
    uint64_t timestamp;
} MessageHeader;

// Game detected message payload
typedef struct {
    pid_t pid;
    char game_name[MAX_GAME_NAME_LENGTH];
    char exe_path[MAX_PATH_LENGTH];
    char launcher[MAX_GAME_NAME_LENGTH];
} GameDetectedPayload;

// Frame data for IPC
typedef struct {
    uint64_t frame_number;
    uint64_t timestamp_ns;
    float frametime_ms;
    float fps;
    pid_t pid;  // Source process ID (for daemon to route to correct subscriber)
} FrameDataPoint;

// Layer hello message - layer announces itself to daemon
typedef struct {
    pid_t pid;
    char process_name[MAX_GAME_NAME_LENGTH];
    char gpu_name[MAX_GAME_NAME_LENGTH];
} LayerHelloPayload;

// Swapchain info message
typedef struct {
    pid_t pid;
    uint32_t width;
    uint32_t height;
    uint32_t format;
    uint32_t image_count;
} SwapchainInfoPayload;

// Shared memory structure for active PIDs
typedef struct {
    uint32_t count;
    uint32_t version;
    pid_t pids[MAX_TRACKED_PROCESSES];
} SharedPidList;

// Utility macros
#define LOG_INFO(fmt, ...) fprintf(stdout, "[INFO] " fmt "\n", ##__VA_ARGS__)
#define LOG_WARN(fmt, ...) fprintf(stderr, "[WARN] " fmt "\n", ##__VA_ARGS__)
#define LOG_ERROR(fmt, ...) fprintf(stderr, "[ERROR] " fmt "\n", ##__VA_ARGS__)

#ifdef DEBUG
#define LOG_DEBUG(fmt, ...) fprintf(stdout, "[DEBUG] " fmt "\n", ##__VA_ARGS__)
#else
#define LOG_DEBUG(fmt, ...)
#endif

#endif // CAPFRAMEX_COMMON_H
