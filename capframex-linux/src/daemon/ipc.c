#include "ipc.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>
#include <errno.h>
#include <pthread.h>
#include <poll.h>
#include <sys/socket.h>
#include <sys/un.h>
#include <sys/mman.h>
#include <sys/stat.h>
#include <fcntl.h>
#include <time.h>

#define MAX_CLIENTS 16
#define MAX_LAYERS 64
#define MAX_APP_SUBSCRIPTIONS 16
#define RECV_BUFFER_SIZE 4096

static int server_socket = -1;
static int shm_fd = -1;
static SharedPidList* shm_pids = NULL;
static char socket_path[256];
static pthread_t server_thread;
static volatile bool running = false;
static ipc_message_callback message_callback = NULL;

// Generic client tracking
typedef struct {
    int fd;
    ClientType type;
} ClientInfo;

static ClientInfo clients[MAX_CLIENTS];
static int client_count = 0;
static pthread_mutex_t clients_mutex = PTHREAD_MUTEX_INITIALIZER;

// Layer clients (frame producers)
static LayerClient layer_clients[MAX_LAYERS];
static int layer_count = 0;
static pthread_mutex_t layers_mutex = PTHREAD_MUTEX_INITIALIZER;

// App subscriptions (frame consumers)
static AppSubscription app_subscriptions[MAX_APP_SUBSCRIPTIONS];
static int subscription_count = 0;
static pthread_mutex_t subscriptions_mutex = PTHREAD_MUTEX_INITIALIZER;

static uint64_t get_timestamp_ns(void) {
    struct timespec ts;
    clock_gettime(CLOCK_MONOTONIC, &ts);
    return (uint64_t)ts.tv_sec * 1000000000ULL + (uint64_t)ts.tv_nsec;
}

static int create_socket(void) {
    const char* runtime_dir = getenv("XDG_RUNTIME_DIR");
    if (runtime_dir) {
        snprintf(socket_path, sizeof(socket_path), "%s/%s",
                 runtime_dir, CAPFRAMEX_SOCKET_NAME);
    } else {
        snprintf(socket_path, sizeof(socket_path), "/tmp/%s-%d",
                 CAPFRAMEX_SOCKET_NAME, getuid());
    }

    unlink(socket_path);

    server_socket = socket(AF_UNIX, SOCK_STREAM, 0);
    if (server_socket == -1) {
        LOG_ERROR("Failed to create socket: %s", strerror(errno));
        return -1;
    }

    struct sockaddr_un addr = {0};
    addr.sun_family = AF_UNIX;
    strncpy(addr.sun_path, socket_path, sizeof(addr.sun_path) - 1);

    if (bind(server_socket, (struct sockaddr*)&addr, sizeof(addr)) == -1) {
        LOG_ERROR("Failed to bind socket: %s", strerror(errno));
        close(server_socket);
        server_socket = -1;
        return -1;
    }

    chmod(socket_path, 0666);

    if (listen(server_socket, MAX_CLIENTS) == -1) {
        LOG_ERROR("Failed to listen on socket: %s", strerror(errno));
        close(server_socket);
        server_socket = -1;
        return -1;
    }

    LOG_INFO("IPC socket created: %s", socket_path);
    return 0;
}

static int create_shared_memory(void) {
    shm_fd = shm_open(CAPFRAMEX_SHM_NAME, O_CREAT | O_RDWR, 0666);
    if (shm_fd == -1) {
        LOG_ERROR("Failed to create shared memory: %s", strerror(errno));
        return -1;
    }

    if (ftruncate(shm_fd, sizeof(SharedPidList)) == -1) {
        LOG_ERROR("Failed to size shared memory: %s", strerror(errno));
        close(shm_fd);
        shm_fd = -1;
        return -1;
    }

    shm_pids = mmap(NULL, sizeof(SharedPidList), PROT_READ | PROT_WRITE,
                    MAP_SHARED, shm_fd, 0);
    if (shm_pids == MAP_FAILED) {
        LOG_ERROR("Failed to map shared memory: %s", strerror(errno));
        close(shm_fd);
        shm_fd = -1;
        shm_pids = NULL;
        return -1;
    }

    memset(shm_pids, 0, sizeof(SharedPidList));
    LOG_INFO("Shared memory created: %s", CAPFRAMEX_SHM_NAME);
    return 0;
}

static void add_client(int fd) {
    pthread_mutex_lock(&clients_mutex);
    if (client_count < MAX_CLIENTS) {
        clients[client_count].fd = fd;
        clients[client_count].type = CLIENT_TYPE_UNKNOWN;
        client_count++;
        LOG_INFO("Client connected (fd=%d, total=%d)", fd, client_count);
    } else {
        LOG_WARN("Max clients reached, rejecting connection");
        close(fd);
    }
    pthread_mutex_unlock(&clients_mutex);
}

static void remove_client(int fd) {
    // First, unregister from layer/app tracking
    ipc_unregister_layer(fd);
    ipc_unregister_app(fd);

    pthread_mutex_lock(&clients_mutex);
    for (int i = 0; i < client_count; i++) {
        if (clients[i].fd == fd) {
            close(fd);
            for (int j = i; j < client_count - 1; j++) {
                clients[j] = clients[j + 1];
            }
            client_count--;
            LOG_INFO("Client disconnected (fd=%d, remaining=%d)", fd, client_count);
            break;
        }
    }
    pthread_mutex_unlock(&clients_mutex);
}

static void set_client_type(int fd, ClientType type) {
    pthread_mutex_lock(&clients_mutex);
    for (int i = 0; i < client_count; i++) {
        if (clients[i].fd == fd) {
            clients[i].type = type;
            break;
        }
    }
    pthread_mutex_unlock(&clients_mutex);
}

ClientType ipc_get_client_type(int fd) {
    ClientType type = CLIENT_TYPE_UNKNOWN;
    pthread_mutex_lock(&clients_mutex);
    for (int i = 0; i < client_count; i++) {
        if (clients[i].fd == fd) {
            type = clients[i].type;
            break;
        }
    }
    pthread_mutex_unlock(&clients_mutex);
    return type;
}

// Layer client management
void ipc_register_layer(int client_fd, const LayerHelloPayload* hello) {
    pthread_mutex_lock(&layers_mutex);

    // Check if already registered (update existing)
    for (int i = 0; i < layer_count; i++) {
        if (layer_clients[i].fd == client_fd || layer_clients[i].pid == hello->pid) {
            layer_clients[i].fd = client_fd;
            layer_clients[i].pid = hello->pid;
            strncpy(layer_clients[i].process_name, hello->process_name,
                    sizeof(layer_clients[i].process_name) - 1);
            strncpy(layer_clients[i].gpu_name, hello->gpu_name,
                    sizeof(layer_clients[i].gpu_name) - 1);
            LOG_INFO("Layer updated: PID=%d, process=%s, GPU=%s",
                     hello->pid, hello->process_name, hello->gpu_name);
            pthread_mutex_unlock(&layers_mutex);
            set_client_type(client_fd, CLIENT_TYPE_LAYER);
            return;
        }
    }

    // Add new layer
    if (layer_count < MAX_LAYERS) {
        LayerClient* layer = &layer_clients[layer_count];
        layer->fd = client_fd;
        layer->pid = hello->pid;
        strncpy(layer->process_name, hello->process_name,
                sizeof(layer->process_name) - 1);
        strncpy(layer->gpu_name, hello->gpu_name,
                sizeof(layer->gpu_name) - 1);
        layer->has_swapchain = false;
        layer->swapchain_width = 0;
        layer->swapchain_height = 0;
        layer->swapchain_format = 0;
        layer_count++;

        LOG_INFO("Layer registered: PID=%d, process=%s, GPU=%s (total=%d)",
                 hello->pid, hello->process_name, hello->gpu_name, layer_count);
    } else {
        LOG_WARN("Max layers reached, cannot register PID=%d", hello->pid);
    }

    pthread_mutex_unlock(&layers_mutex);
    set_client_type(client_fd, CLIENT_TYPE_LAYER);
}

void ipc_update_layer_swapchain(int client_fd, const SwapchainInfoPayload* info) {
    pthread_mutex_lock(&layers_mutex);

    for (int i = 0; i < layer_count; i++) {
        if (layer_clients[i].fd == client_fd || layer_clients[i].pid == info->pid) {
            layer_clients[i].swapchain_width = info->width;
            layer_clients[i].swapchain_height = info->height;
            layer_clients[i].swapchain_format = info->format;
            layer_clients[i].has_swapchain = (info->width > 0 && info->height > 0);

            LOG_INFO("Layer swapchain updated: PID=%d, %ux%u",
                     info->pid, info->width, info->height);
            break;
        }
    }

    pthread_mutex_unlock(&layers_mutex);
}

void ipc_unregister_layer(int client_fd) {
    pthread_mutex_lock(&layers_mutex);

    for (int i = 0; i < layer_count; i++) {
        if (layer_clients[i].fd == client_fd) {
            LOG_INFO("Layer unregistered: PID=%d, process=%s",
                     layer_clients[i].pid, layer_clients[i].process_name);

            // Shift remaining layers
            for (int j = i; j < layer_count - 1; j++) {
                layer_clients[j] = layer_clients[j + 1];
            }
            layer_count--;
            break;
        }
    }

    pthread_mutex_unlock(&layers_mutex);
}

LayerClient* ipc_get_layer_by_pid(pid_t pid) {
    pthread_mutex_lock(&layers_mutex);
    for (int i = 0; i < layer_count; i++) {
        if (layer_clients[i].pid == pid) {
            pthread_mutex_unlock(&layers_mutex);
            return &layer_clients[i];
        }
    }
    pthread_mutex_unlock(&layers_mutex);
    return NULL;
}

LayerClient* ipc_get_layer_by_fd(int fd) {
    pthread_mutex_lock(&layers_mutex);
    for (int i = 0; i < layer_count; i++) {
        if (layer_clients[i].fd == fd) {
            pthread_mutex_unlock(&layers_mutex);
            return &layer_clients[i];
        }
    }
    pthread_mutex_unlock(&layers_mutex);
    return NULL;
}

int ipc_get_layer_count(void) {
    pthread_mutex_lock(&layers_mutex);
    int count = layer_count;
    pthread_mutex_unlock(&layers_mutex);
    return count;
}

LayerClient* ipc_get_layers(int* count) {
    pthread_mutex_lock(&layers_mutex);
    *count = layer_count;
    pthread_mutex_unlock(&layers_mutex);
    return layer_clients;  // Note: caller should hold lock or copy data
}

// App subscription management
void ipc_subscribe_app(int client_fd, pid_t target_pid) {
    pthread_mutex_lock(&subscriptions_mutex);

    // Check if already subscribed (update)
    for (int i = 0; i < subscription_count; i++) {
        if (app_subscriptions[i].fd == client_fd) {
            app_subscriptions[i].subscribed_pid = target_pid;
            LOG_INFO("App subscription updated: fd=%d -> PID=%d", client_fd, target_pid);
            pthread_mutex_unlock(&subscriptions_mutex);
            set_client_type(client_fd, CLIENT_TYPE_APP);
            return;
        }
    }

    // Add new subscription
    if (subscription_count < MAX_APP_SUBSCRIPTIONS) {
        app_subscriptions[subscription_count].fd = client_fd;
        app_subscriptions[subscription_count].subscribed_pid = target_pid;
        subscription_count++;
        LOG_INFO("App subscribed: fd=%d -> PID=%d (total=%d)",
                 client_fd, target_pid, subscription_count);
    } else {
        LOG_WARN("Max subscriptions reached");
    }

    pthread_mutex_unlock(&subscriptions_mutex);
    set_client_type(client_fd, CLIENT_TYPE_APP);
}

void ipc_unsubscribe_app(int client_fd) {
    pthread_mutex_lock(&subscriptions_mutex);

    for (int i = 0; i < subscription_count; i++) {
        if (app_subscriptions[i].fd == client_fd) {
            app_subscriptions[i].subscribed_pid = 0;
            LOG_INFO("App unsubscribed: fd=%d", client_fd);
            break;
        }
    }

    pthread_mutex_unlock(&subscriptions_mutex);
}

void ipc_unregister_app(int client_fd) {
    pthread_mutex_lock(&subscriptions_mutex);

    for (int i = 0; i < subscription_count; i++) {
        if (app_subscriptions[i].fd == client_fd) {
            for (int j = i; j < subscription_count - 1; j++) {
                app_subscriptions[j] = app_subscriptions[j + 1];
            }
            subscription_count--;
            LOG_INFO("App unregistered: fd=%d", client_fd);
            break;
        }
    }

    pthread_mutex_unlock(&subscriptions_mutex);
}

// Forward frame data to subscribed apps
void ipc_forward_frame_data(const FrameDataPoint* frame) {
    pthread_mutex_lock(&subscriptions_mutex);

    for (int i = 0; i < subscription_count; i++) {
        if (app_subscriptions[i].subscribed_pid == frame->pid) {
            // This app is subscribed to this layer's frames
            ipc_send(app_subscriptions[i].fd, MSG_FRAMETIME_DATA,
                     (void*)frame, sizeof(FrameDataPoint));
        }
    }

    pthread_mutex_unlock(&subscriptions_mutex);
}

static void handle_client_message(int client_fd, char* buffer, ssize_t len) {
    if (len < (ssize_t)sizeof(MessageHeader)) {
        LOG_WARN("Received incomplete message from client %d", client_fd);
        return;
    }

    MessageHeader* header = (MessageHeader*)buffer;
    void* payload = (len > (ssize_t)sizeof(MessageHeader)) ? buffer + sizeof(MessageHeader) : NULL;

    LOG_DEBUG("Received message type %d from client %d", header->type, client_fd);

    // Handle frame data forwarding (high priority, before callback)
    if (header->type == MSG_FRAMETIME_DATA && payload) {
        FrameDataPoint* frame = (FrameDataPoint*)payload;
        ipc_forward_frame_data(frame);
        return;  // Don't pass to callback
    }

    // Handle layer hello
    if (header->type == MSG_LAYER_HELLO && payload) {
        LayerHelloPayload* hello = (LayerHelloPayload*)payload;
        ipc_register_layer(client_fd, hello);
    }

    // Handle swapchain messages
    if (header->type == MSG_SWAPCHAIN_CREATED && payload) {
        SwapchainInfoPayload* info = (SwapchainInfoPayload*)payload;
        ipc_update_layer_swapchain(client_fd, info);
    }

    if (header->type == MSG_SWAPCHAIN_DESTROYED && payload) {
        SwapchainInfoPayload* info = (SwapchainInfoPayload*)payload;
        info->width = 0;
        info->height = 0;
        ipc_update_layer_swapchain(client_fd, info);
    }

    // Pass to main callback for additional handling
    if (message_callback) {
        message_callback(header, payload, client_fd);
    }

    // Handle built-in message types
    switch (header->type) {
        case MSG_PING:
            ipc_send(client_fd, MSG_PONG, NULL, 0);
            break;
        default:
            break;
    }
}

static void* server_thread_func(void* arg) {
    (void)arg;

    struct pollfd* fds = malloc((MAX_CLIENTS + 1) * sizeof(struct pollfd));
    if (!fds) {
        LOG_ERROR("Failed to allocate poll fds");
        return NULL;
    }

    while (running) {
        int nfds = 0;

        // Add server socket
        fds[nfds].fd = server_socket;
        fds[nfds].events = POLLIN;
        nfds++;

        // Add client sockets
        pthread_mutex_lock(&clients_mutex);
        for (int i = 0; i < client_count; i++) {
            fds[nfds].fd = clients[i].fd;
            fds[nfds].events = POLLIN;
            nfds++;
        }
        pthread_mutex_unlock(&clients_mutex);

        int ret = poll(fds, nfds, 1000);  // 1 second timeout
        if (ret < 0) {
            if (errno == EINTR) continue;
            LOG_ERROR("Poll error: %s", strerror(errno));
            break;
        }

        if (ret == 0) continue;  // Timeout

        // Check server socket for new connections
        if (fds[0].revents & POLLIN) {
            int client_fd = accept(server_socket, NULL, NULL);
            if (client_fd != -1) {
                add_client(client_fd);
            }
        }

        // Check client sockets for data
        char buffer[RECV_BUFFER_SIZE];
        for (int i = 1; i < nfds; i++) {
            if (fds[i].revents & POLLIN) {
                ssize_t len = recv(fds[i].fd, buffer, sizeof(buffer), 0);
                if (len <= 0) {
                    remove_client(fds[i].fd);
                } else {
                    handle_client_message(fds[i].fd, buffer, len);
                }
            } else if (fds[i].revents & (POLLHUP | POLLERR)) {
                remove_client(fds[i].fd);
            }
        }
    }

    free(fds);
    return NULL;
}

int ipc_init(void) {
    memset(clients, 0, sizeof(clients));
    memset(layer_clients, 0, sizeof(layer_clients));
    memset(app_subscriptions, 0, sizeof(app_subscriptions));

    if (create_socket() != 0) {
        return -1;
    }

    if (create_shared_memory() != 0) {
        close(server_socket);
        server_socket = -1;
        return -1;
    }

    return 0;
}

int ipc_start(ipc_message_callback callback) {
    message_callback = callback;
    running = true;

    if (pthread_create(&server_thread, NULL, server_thread_func, NULL) != 0) {
        LOG_ERROR("Failed to create server thread: %s", strerror(errno));
        running = false;
        return -1;
    }

    LOG_INFO("IPC server started");
    return 0;
}

void ipc_stop(void) {
    if (!running) return;

    running = false;
    pthread_join(server_thread, NULL);

    // Close all client connections
    pthread_mutex_lock(&clients_mutex);
    for (int i = 0; i < client_count; i++) {
        close(clients[i].fd);
    }
    client_count = 0;
    pthread_mutex_unlock(&clients_mutex);

    // Clear layer and subscription tracking
    pthread_mutex_lock(&layers_mutex);
    layer_count = 0;
    pthread_mutex_unlock(&layers_mutex);

    pthread_mutex_lock(&subscriptions_mutex);
    subscription_count = 0;
    pthread_mutex_unlock(&subscriptions_mutex);

    LOG_INFO("IPC server stopped");
}

void ipc_cleanup(void) {
    ipc_stop();

    if (server_socket != -1) {
        close(server_socket);
        server_socket = -1;
        unlink(socket_path);
    }

    if (shm_pids) {
        munmap(shm_pids, sizeof(SharedPidList));
        shm_pids = NULL;
    }

    if (shm_fd != -1) {
        close(shm_fd);
        shm_unlink(CAPFRAMEX_SHM_NAME);
        shm_fd = -1;
    }
}

int ipc_send(int client_fd, MessageType type, void* payload, uint32_t payload_size) {
    size_t total_size = sizeof(MessageHeader) + payload_size;
    char* buffer = malloc(total_size);
    if (!buffer) return -1;

    MessageHeader* header = (MessageHeader*)buffer;
    header->type = type;
    header->payload_size = payload_size;
    header->timestamp = get_timestamp_ns();

    if (payload && payload_size > 0) {
        memcpy(buffer + sizeof(MessageHeader), payload, payload_size);
    }

    ssize_t sent = send(client_fd, buffer, total_size, MSG_NOSIGNAL);
    free(buffer);

    return (sent == (ssize_t)total_size) ? 0 : -1;
}

int ipc_broadcast(MessageType type, void* payload, uint32_t payload_size) {
    int success_count = 0;

    pthread_mutex_lock(&clients_mutex);
    for (int i = 0; i < client_count; i++) {
        if (ipc_send(clients[i].fd, type, payload, payload_size) == 0) {
            success_count++;
        }
    }
    pthread_mutex_unlock(&clients_mutex);

    return success_count;
}

int ipc_broadcast_to_apps(MessageType type, void* payload, uint32_t payload_size) {
    int success_count = 0;

    pthread_mutex_lock(&clients_mutex);
    for (int i = 0; i < client_count; i++) {
        if (clients[i].type == CLIENT_TYPE_APP) {
            if (ipc_send(clients[i].fd, type, payload, payload_size) == 0) {
                success_count++;
            }
        }
    }
    pthread_mutex_unlock(&clients_mutex);

    return success_count;
}

int ipc_broadcast_to_non_layers(MessageType type, void* payload, uint32_t payload_size) {
    int success_count = 0;

    pthread_mutex_lock(&clients_mutex);
    for (int i = 0; i < client_count; i++) {
        // Send to all clients that are NOT layers (apps and unknown clients)
        if (clients[i].type != CLIENT_TYPE_LAYER) {
            if (ipc_send(clients[i].fd, type, payload, payload_size) == 0) {
                success_count++;
            }
        }
    }
    pthread_mutex_unlock(&clients_mutex);

    return success_count;
}

int ipc_update_active_pids(pid_t* pids, uint32_t count) {
    if (!shm_pids) return -1;

    uint32_t copy_count = (count > MAX_TRACKED_PROCESSES) ? MAX_TRACKED_PROCESSES : count;

    shm_pids->version++;
    shm_pids->count = copy_count;
    memcpy(shm_pids->pids, pids, copy_count * sizeof(pid_t));

    return 0;
}

const char* ipc_get_socket_path(void) {
    return socket_path;
}

bool ipc_has_clients(void) {
    pthread_mutex_lock(&clients_mutex);
    bool has = client_count > 0;
    pthread_mutex_unlock(&clients_mutex);
    return has;
}
