#include "ipc_client.h"
#include "data_export.h"
#include "../daemon/common.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>
#include <errno.h>
#include <pthread.h>
#include <sys/socket.h>
#include <sys/un.h>
#include <sys/mman.h>
#include <fcntl.h>

static int sock_fd = -1;
static int shm_fd = -1;
static SharedPidList* shm_pids = NULL;
static bool connected = false;
static bool capture_active = false;
static pthread_t receiver_thread;
static volatile bool receiver_running = false;
static pthread_mutex_t ipc_mutex = PTHREAD_MUTEX_INITIALIZER;

static CaptureSessionInfo current_capture_info;

static const char* get_socket_path(void) {
    static char path[256];
    const char* runtime_dir = getenv("XDG_RUNTIME_DIR");
    if (runtime_dir) {
        snprintf(path, sizeof(path), "%s/%s", runtime_dir, CAPFRAMEX_SOCKET_NAME);
    } else {
        snprintf(path, sizeof(path), "/tmp/%s-%d", CAPFRAMEX_SOCKET_NAME, getuid());
    }
    return path;
}

static int send_message(MessageType type, void* payload, uint32_t payload_size) {
    if (sock_fd < 0) return -1;

    size_t total_size = sizeof(MessageHeader) + payload_size;
    char* buffer = malloc(total_size);
    if (!buffer) return -1;

    MessageHeader* header = (MessageHeader*)buffer;
    header->type = type;
    header->payload_size = payload_size;

    struct timespec ts;
    clock_gettime(CLOCK_MONOTONIC, &ts);
    header->timestamp = (uint64_t)ts.tv_sec * 1000000000ULL + (uint64_t)ts.tv_nsec;

    if (payload && payload_size > 0) {
        memcpy(buffer + sizeof(MessageHeader), payload, payload_size);
    }

    ssize_t sent = send(sock_fd, buffer, total_size, MSG_NOSIGNAL);
    free(buffer);

    return (sent == (ssize_t)total_size) ? 0 : -1;
}

static void handle_message(MessageHeader* header, void* payload) {
    switch (header->type) {
        case MSG_START_CAPTURE:
            fprintf(stderr, "[CapFrameX Layer] Received start capture command\n");
            ipc_client_start_capture(
                current_capture_info.game_name,
                current_capture_info.gpu_name,
                current_capture_info.resolution_width,
                current_capture_info.resolution_height);
            break;

        case MSG_STOP_CAPTURE:
            fprintf(stderr, "[CapFrameX Layer] Received stop capture command\n");
            ipc_client_stop_capture();
            break;

        case MSG_PING:
            send_message(MSG_PONG, NULL, 0);
            break;

        case MSG_CONFIG_UPDATE:
            // Handle config updates if needed
            break;

        default:
            break;
    }
    (void)payload;
}

static void* receiver_thread_func(void* arg) {
    (void)arg;
    char buffer[4096];

    while (receiver_running) {
        ssize_t len = recv(sock_fd, buffer, sizeof(buffer), 0);

        if (len <= 0) {
            if (errno == EINTR) continue;
            fprintf(stderr, "[CapFrameX Layer] Disconnected from daemon\n");
            connected = false;
            break;
        }

        if (len >= (ssize_t)sizeof(MessageHeader)) {
            MessageHeader* header = (MessageHeader*)buffer;
            void* payload = (len > (ssize_t)sizeof(MessageHeader)) ?
                            buffer + sizeof(MessageHeader) : NULL;
            handle_message(header, payload);
        }
    }

    return NULL;
}

static bool open_shared_memory(void) {
    shm_fd = shm_open(CAPFRAMEX_SHM_NAME, O_RDONLY, 0);
    if (shm_fd == -1) {
        return false;
    }

    shm_pids = mmap(NULL, sizeof(SharedPidList), PROT_READ, MAP_SHARED, shm_fd, 0);
    if (shm_pids == MAP_FAILED) {
        close(shm_fd);
        shm_fd = -1;
        shm_pids = NULL;
        return false;
    }

    return true;
}

static bool is_pid_active(void) {
    if (!shm_pids) return false;

    pid_t my_pid = getpid();
    for (uint32_t i = 0; i < shm_pids->count; i++) {
        if (shm_pids->pids[i] == my_pid) {
            return true;
        }
    }
    return false;
}

void ipc_client_init(void) {
    pthread_mutex_lock(&ipc_mutex);

    // Try to open shared memory (non-blocking, just for PID checking)
    open_shared_memory();

    pthread_mutex_unlock(&ipc_mutex);
}

void ipc_client_cleanup(void) {
    pthread_mutex_lock(&ipc_mutex);

    if (capture_active) {
        pthread_mutex_unlock(&ipc_mutex);
        ipc_client_stop_capture();
        pthread_mutex_lock(&ipc_mutex);
    }

    if (receiver_running) {
        receiver_running = false;
        // Send a dummy message to unblock recv
        shutdown(sock_fd, SHUT_RDWR);
        pthread_join(receiver_thread, NULL);
    }

    if (sock_fd >= 0) {
        close(sock_fd);
        sock_fd = -1;
    }

    if (shm_pids) {
        munmap(shm_pids, sizeof(SharedPidList));
        shm_pids = NULL;
    }

    if (shm_fd >= 0) {
        close(shm_fd);
        shm_fd = -1;
    }

    connected = false;

    pthread_mutex_unlock(&ipc_mutex);
}

bool ipc_client_connect(void) {
    pthread_mutex_lock(&ipc_mutex);

    if (connected) {
        pthread_mutex_unlock(&ipc_mutex);
        return true;
    }

    sock_fd = socket(AF_UNIX, SOCK_STREAM, 0);
    if (sock_fd == -1) {
        pthread_mutex_unlock(&ipc_mutex);
        return false;
    }

    struct sockaddr_un addr = {0};
    addr.sun_family = AF_UNIX;
    strncpy(addr.sun_path, get_socket_path(), sizeof(addr.sun_path) - 1);

    if (connect(sock_fd, (struct sockaddr*)&addr, sizeof(addr)) == -1) {
        close(sock_fd);
        sock_fd = -1;
        pthread_mutex_unlock(&ipc_mutex);
        return false;
    }

    connected = true;

    // Start receiver thread
    receiver_running = true;
    pthread_create(&receiver_thread, NULL, receiver_thread_func, NULL);

    fprintf(stderr, "[CapFrameX Layer] Connected to daemon\n");

    pthread_mutex_unlock(&ipc_mutex);
    return true;
}

bool ipc_client_is_connected(void) {
    return connected;
}

bool ipc_client_is_capture_active(void) {
    // Check both IPC capture flag and shared memory
    if (capture_active) return true;
    return is_pid_active();
}

void ipc_client_send_frame_data(const FrameTimingData* frame) {
    if (!capture_active) return;

    // Add to local capture buffer
    data_export_add_frame(frame);

    // If connected, also send via IPC for live monitoring
    if (connected) {
        FrameDataPoint point = {
            .frame_number = frame->frame_number,
            .timestamp_ns = frame->timestamp_ns,
            .frametime_ms = frame->frametime_ms,
            .fps = (frame->frametime_ms > 0) ? 1000.0f / frame->frametime_ms : 0
        };
        send_message(MSG_FRAMETIME_DATA, &point, sizeof(point));
    }
}

void ipc_client_notify_swapchain_created(uint32_t width, uint32_t height) {
    current_capture_info.resolution_width = width;
    current_capture_info.resolution_height = height;
}

void ipc_client_start_capture(const char* game_name, const char* gpu_name,
                               uint32_t width, uint32_t height) {
    pthread_mutex_lock(&ipc_mutex);

    if (capture_active) {
        pthread_mutex_unlock(&ipc_mutex);
        return;
    }

    strncpy(current_capture_info.game_name, game_name ? game_name : "Unknown",
            sizeof(current_capture_info.game_name) - 1);
    strncpy(current_capture_info.gpu_name, gpu_name ? gpu_name : "Unknown",
            sizeof(current_capture_info.gpu_name) - 1);
    current_capture_info.resolution_width = width;
    current_capture_info.resolution_height = height;

    capture_active = true;

    pthread_mutex_unlock(&ipc_mutex);

    data_export_start_session(&current_capture_info);
}

void ipc_client_stop_capture(void) {
    pthread_mutex_lock(&ipc_mutex);

    if (!capture_active) {
        pthread_mutex_unlock(&ipc_mutex);
        return;
    }

    capture_active = false;

    pthread_mutex_unlock(&ipc_mutex);

    data_export_end_session();
}
