#include "ipc_client.h"
#include "swapchain.h"
#include "../daemon/common.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdarg.h>
#include <unistd.h>
#include <errno.h>
#include <pthread.h>
#include <time.h>
#include <sys/socket.h>
#include <sys/un.h>

static int sock_fd = -1;
static bool connected = false;
static pthread_t receiver_thread;
static volatile bool receiver_running = false;
static pthread_mutex_t ipc_mutex = PTHREAD_MUTEX_INITIALIZER;

// Verbose debug mode - set CAPFRAMEX_DEBUG=1 to enable
static int verbose_mode = -1;  // -1 = not initialized

bool ipc_is_verbose(void) {
    if (verbose_mode < 0) {
        const char* env = getenv("CAPFRAMEX_DEBUG");
        verbose_mode = (env && (env[0] == '1' || env[0] == 'y' || env[0] == 'Y')) ? 1 : 0;
    }
    return verbose_mode == 1;
}

void ipc_debug_log(const char* fmt, ...) {
    if (!ipc_is_verbose()) return;

    FILE* dbg = fopen("/tmp/capframex_layer_debug.log", "a");
    if (dbg) {
        va_list args;
        va_start(args, fmt);
        fprintf(dbg, "[Layer PID=%d] ", getpid());
        vfprintf(dbg, fmt, args);
        fprintf(dbg, "\n");
        va_end(args);
        fclose(dbg);
    }
}

// Cached process info for frame data
static pid_t cached_pid = 0;
static char cached_process_name[256] = {0};
static char cached_gpu_name[256] = {0};

// Reconnection tracking
static struct timespec last_connect_attempt = {0, 0};
#define RECONNECT_INTERVAL_MS 100  // 0.1 seconds between reconnect attempts

static const char* get_socket_path(void) {
    static char path[256];
#if CAPFRAMEX_SOCKET_USE_HOME
    // Use ~/.config/capframex for socket - Proton containers share /home but isolate /tmp
    const char* home = getenv("HOME");
    if (home) {
        snprintf(path, sizeof(path), "%s/.config/capframex/%s", home, CAPFRAMEX_SOCKET_NAME);
    } else {
        snprintf(path, sizeof(path), "/tmp/%s-%d", CAPFRAMEX_SOCKET_NAME, getuid());
    }
#else
    const char* runtime_dir = getenv("XDG_RUNTIME_DIR");
    if (runtime_dir) {
        snprintf(path, sizeof(path), "%s/%s", runtime_dir, CAPFRAMEX_SOCKET_NAME);
    } else {
        snprintf(path, sizeof(path), "/tmp/%s-%d", CAPFRAMEX_SOCKET_NAME, getuid());
    }
#endif
    return path;
}

static int send_message(MessageType type, void* payload, uint32_t payload_size) {
    pthread_mutex_lock(&ipc_mutex);

    // Check connection state under lock
    if (sock_fd < 0 || !connected) {
        pthread_mutex_unlock(&ipc_mutex);
        return -1;
    }

    // Copy fd while holding lock to avoid race
    int fd = sock_fd;
    pthread_mutex_unlock(&ipc_mutex);

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

    ssize_t sent = send(fd, buffer, total_size, MSG_NOSIGNAL);
    int send_errno = errno;
    free(buffer);

    if (sent != (ssize_t)total_size) {
        // Always log send failures (errors, not debug)
        ipc_debug_log("send_message FAILED: type=%d, sent=%zd/%zu, errno=%d (%s)",
                  type, sent, total_size, send_errno, strerror(send_errno));

        // Mark as disconnected so we try to reconnect
        pthread_mutex_lock(&ipc_mutex);
        connected = false;
        pthread_mutex_unlock(&ipc_mutex);
        return -1;
    }

    return 0;
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

    // Clean up any old connection state before reconnecting
    if (sock_fd >= 0) {
        // Stop old receiver thread first
        receiver_running = false;
        shutdown(sock_fd, SHUT_RDWR);
        close(sock_fd);
        sock_fd = -1;
        // Give the old thread a moment to exit (don't join - may deadlock)
    }

    sock_fd = socket(AF_UNIX, SOCK_STREAM, 0);
    if (sock_fd == -1) {
        pthread_mutex_unlock(&ipc_mutex);
        return false;
    }

    struct sockaddr_un addr = {0};
    addr.sun_family = AF_UNIX;
    strncpy(addr.sun_path, get_socket_path(), sizeof(addr.sun_path) - 1);

    fprintf(stderr, "[CapFrameX Layer] Attempting to connect to: %s\n", addr.sun_path);

    if (connect(sock_fd, (struct sockaddr*)&addr, sizeof(addr)) == -1) {
        fprintf(stderr, "[CapFrameX Layer] Connection failed: %s\n", strerror(errno));
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
    // Note: Full GPU + swapchain info will be sent via pending_swapchain_send mechanism
    // in QueuePresentKHR on the next frame render, which has access to correct device data
    fprintf(stderr, "[CapFrameX Layer] DEBUG: Sending hello after connect, cached_gpu_name='%s'\n", cached_gpu_name);
    ipc_client_send_hello(cached_gpu_name);

    fprintf(stderr, "[CapFrameX Layer] Connected to daemon - streaming enabled\n");

    return true;
}

bool ipc_client_is_connected(void) {
    return connected;
}

bool ipc_client_try_reconnect(void) {
    if (connected) return true;

    struct timespec now;
    clock_gettime(CLOCK_MONOTONIC, &now);

    // Calculate elapsed time in milliseconds
    long elapsed_ms = (now.tv_sec - last_connect_attempt.tv_sec) * 1000 +
                      (now.tv_nsec - last_connect_attempt.tv_nsec) / 1000000;

    if (elapsed_ms < RECONNECT_INTERVAL_MS) {
        return false;  // Too soon to retry
    }
    last_connect_attempt = now;

    // Try to connect
    return ipc_client_connect();
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
    if (!connected) {
        fprintf(stderr, "[CapFrameX Layer] DEBUG: send_hello called but not connected, GPU=%s\n",
                gpu_name ? gpu_name : "(null)");
        return;
    }

    LayerHelloPayload payload = {0};
    payload.pid = cached_pid;
    strncpy(payload.process_name, cached_process_name, sizeof(payload.process_name) - 1);
    if (gpu_name && strlen(gpu_name) > 0) {
        strncpy(payload.gpu_name, gpu_name, sizeof(payload.gpu_name) - 1);
        fprintf(stderr, "[CapFrameX Layer] DEBUG: Using provided GPU name: '%s'\n", gpu_name);
    } else {
        strncpy(payload.gpu_name, cached_gpu_name, sizeof(payload.gpu_name) - 1);
        fprintf(stderr, "[CapFrameX Layer] DEBUG: Using cached GPU name: '%s' (provided was empty)\n", cached_gpu_name);
    }

    int result = send_message(MSG_LAYER_HELLO, &payload, sizeof(payload));

    fprintf(stderr, "[CapFrameX Layer] Sent hello: PID=%d, process=%s, GPU='%s', result=%d\n",
            payload.pid, payload.process_name, payload.gpu_name, result);
}

void ipc_client_send_swapchain_created(uint32_t width, uint32_t height,
                                        uint32_t format, uint32_t image_count) {
    if (!connected) {
        fprintf(stderr, "[CapFrameX Layer] DEBUG: send_swapchain_created called but not connected, res=%ux%u\n",
                width, height);
        return;
    }

    SwapchainInfoPayload payload = {
        .pid = cached_pid,
        .width = width,
        .height = height,
        .format = format,
        .image_count = image_count
    };

    int result = send_message(MSG_SWAPCHAIN_CREATED, &payload, sizeof(payload));

    fprintf(stderr, "[CapFrameX Layer] Sent swapchain info: %ux%u, format=%u, images=%u, result=%d\n",
            width, height, format, image_count, result);
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

// Frame count for debug logging
static uint64_t frames_sent = 0;
static uint64_t last_log_frame = 0;

void ipc_client_send_frame_data(const FrameTimingData* frame) {
    // Always send if connected - continuous streaming model
    if (!connected) {
        // Log periodically when not connected
        static uint64_t frames_dropped = 0;
        frames_dropped++;
        if (frames_dropped % 1000 == 0) {
            fprintf(stderr, "[CapFrameX Layer] Dropped %lu frames (not connected)\n",
                    (unsigned long)frames_dropped);
        }
        return;
    }

    FrameDataPoint point = {
        .frame_number = frame->frame_number,
        .timestamp_ns = frame->timestamp_ns,
        .frametime_ms = frame->frametime_ms,
        .fps = (frame->frametime_ms > 0) ? 1000.0f / frame->frametime_ms : 0,
        .pid = cached_pid
    };

    int result = send_message(MSG_FRAMETIME_DATA, &point, sizeof(point));

    frames_sent++;
    // Log every 1000 frames or every 10 seconds
    if (frames_sent - last_log_frame >= 1000) {
        fprintf(stderr, "[CapFrameX Layer] Sent %lu frames (PID=%d, last FT=%.2fms, result=%d)\n",
                (unsigned long)frames_sent, cached_pid, frame->frametime_ms, result);
        last_log_frame = frames_sent;
    }
}
