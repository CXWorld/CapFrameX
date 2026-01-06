#include "ipc_client.h"
#include "../daemon/common.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>
#include <errno.h>
#include <pthread.h>
#include <sys/socket.h>
#include <sys/un.h>

static int sock_fd = -1;
static bool connected = false;
static pthread_t receiver_thread;
static volatile bool receiver_running = false;
static pthread_mutex_t ipc_mutex = PTHREAD_MUTEX_INITIALIZER;

// Cached process info for frame data
static pid_t cached_pid = 0;
static char cached_process_name[256] = {0};
static char cached_gpu_name[256] = {0};

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
    if (sock_fd < 0 || !connected) return -1;

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
        case MSG_PING:
            send_message(MSG_PONG, NULL, 0);
            break;

        case MSG_CONFIG_UPDATE:
            // Handle config updates if needed
            break;

        default:
            // Layer ignores most messages - it just streams data
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

static void get_process_name(char* buffer, size_t size) {
    char path[64];
    snprintf(path, sizeof(path), "/proc/%d/comm", getpid());
    FILE* f = fopen(path, "r");
    if (f) {
        if (fgets(buffer, size, f)) {
            size_t len = strlen(buffer);
            if (len > 0 && buffer[len-1] == '\n') {
                buffer[len-1] = '\0';
            }
        }
        fclose(f);
    } else {
        strncpy(buffer, "Unknown", size - 1);
        buffer[size - 1] = '\0';
    }
}

void ipc_client_init(void) {
    pthread_mutex_lock(&ipc_mutex);

    // Cache process info
    cached_pid = getpid();
    get_process_name(cached_process_name, sizeof(cached_process_name));

    pthread_mutex_unlock(&ipc_mutex);
}

void ipc_client_cleanup(void) {
    pthread_mutex_lock(&ipc_mutex);

    if (receiver_running) {
        receiver_running = false;
        shutdown(sock_fd, SHUT_RDWR);
        pthread_join(receiver_thread, NULL);
    }

    if (sock_fd >= 0) {
        close(sock_fd);
        sock_fd = -1;
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

    // Send hello message to announce ourselves
    ipc_client_send_hello(cached_gpu_name);

    return true;
}

bool ipc_client_is_connected(void) {
    return connected;
}

void ipc_client_set_gpu_name(const char* gpu_name) {
    pthread_mutex_lock(&ipc_mutex);
    if (gpu_name) {
        strncpy(cached_gpu_name, gpu_name, sizeof(cached_gpu_name) - 1);
        cached_gpu_name[sizeof(cached_gpu_name) - 1] = '\0';
    }
    pthread_mutex_unlock(&ipc_mutex);
}

void ipc_client_send_hello(const char* gpu_name) {
    if (!connected) return;

    LayerHelloPayload payload = {0};
    payload.pid = cached_pid;
    strncpy(payload.process_name, cached_process_name, sizeof(payload.process_name) - 1);
    if (gpu_name && strlen(gpu_name) > 0) {
        strncpy(payload.gpu_name, gpu_name, sizeof(payload.gpu_name) - 1);
    } else {
        strncpy(payload.gpu_name, cached_gpu_name, sizeof(payload.gpu_name) - 1);
    }

    send_message(MSG_LAYER_HELLO, &payload, sizeof(payload));

    fprintf(stderr, "[CapFrameX Layer] Sent hello: PID=%d, process=%s, GPU=%s\n",
            payload.pid, payload.process_name, payload.gpu_name);
}

void ipc_client_send_swapchain_created(uint32_t width, uint32_t height,
                                        uint32_t format, uint32_t image_count) {
    if (!connected) return;

    SwapchainInfoPayload payload = {
        .pid = cached_pid,
        .width = width,
        .height = height,
        .format = format,
        .image_count = image_count
    };

    send_message(MSG_SWAPCHAIN_CREATED, &payload, sizeof(payload));

    fprintf(stderr, "[CapFrameX Layer] Sent swapchain info: %ux%u\n", width, height);
}

void ipc_client_send_swapchain_destroyed(void) {
    if (!connected) return;

    SwapchainInfoPayload payload = {
        .pid = cached_pid,
        .width = 0,
        .height = 0,
        .format = 0,
        .image_count = 0
    };

    send_message(MSG_SWAPCHAIN_DESTROYED, &payload, sizeof(payload));
}

void ipc_client_send_frame_data(const FrameTimingData* frame) {
    // Always send if connected - continuous streaming model
    if (!connected) return;

    FrameDataPoint point = {
        .frame_number = frame->frame_number,
        .timestamp_ns = frame->timestamp_ns,
        .frametime_ms = frame->frametime_ms,
        .fps = (frame->frametime_ms > 0) ? 1000.0f / frame->frametime_ms : 0,
        .pid = cached_pid
    };

    send_message(MSG_FRAMETIME_DATA, &point, sizeof(point));
}
