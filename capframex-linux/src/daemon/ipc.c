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
#define RECV_BUFFER_SIZE 4096

static int server_socket = -1;
static int shm_fd = -1;
static SharedPidList* shm_pids = NULL;
static char socket_path[256];
static pthread_t server_thread;
static volatile bool running = false;
static ipc_message_callback message_callback = NULL;

static int client_fds[MAX_CLIENTS];
static int client_count = 0;
static pthread_mutex_t clients_mutex = PTHREAD_MUTEX_INITIALIZER;

static uint64_t get_timestamp_ns(void) {
    struct timespec ts;
    clock_gettime(CLOCK_MONOTONIC, &ts);
    return (uint64_t)ts.tv_sec * 1000000000ULL + (uint64_t)ts.tv_nsec;
}

static int create_socket(void) {
    // Create socket path in XDG_RUNTIME_DIR or /tmp
    const char* runtime_dir = getenv("XDG_RUNTIME_DIR");
    if (runtime_dir) {
        snprintf(socket_path, sizeof(socket_path), "%s/%s",
                 runtime_dir, CAPFRAMEX_SOCKET_NAME);
    } else {
        snprintf(socket_path, sizeof(socket_path), "/tmp/%s-%d",
                 CAPFRAMEX_SOCKET_NAME, getuid());
    }

    // Remove existing socket
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

    // Set permissions so other users can connect
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
        client_fds[client_count++] = fd;
        LOG_INFO("Client connected (fd=%d, total=%d)", fd, client_count);
    } else {
        LOG_WARN("Max clients reached, rejecting connection");
        close(fd);
    }
    pthread_mutex_unlock(&clients_mutex);
}

static void remove_client(int fd) {
    pthread_mutex_lock(&clients_mutex);
    for (int i = 0; i < client_count; i++) {
        if (client_fds[i] == fd) {
            close(fd);
            // Shift remaining clients
            for (int j = i; j < client_count - 1; j++) {
                client_fds[j] = client_fds[j + 1];
            }
            client_count--;
            LOG_INFO("Client disconnected (fd=%d, remaining=%d)", fd, client_count);
            break;
        }
    }
    pthread_mutex_unlock(&clients_mutex);
}

static void handle_client_message(int client_fd, char* buffer, ssize_t len) {
    if (len < (ssize_t)sizeof(MessageHeader)) {
        LOG_WARN("Received incomplete message from client %d", client_fd);
        return;
    }

    MessageHeader* header = (MessageHeader*)buffer;
    void* payload = (len > (ssize_t)sizeof(MessageHeader)) ? buffer + sizeof(MessageHeader) : NULL;

    LOG_DEBUG("Received message type %d from client %d", header->type, client_fd);

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
            fds[nfds].fd = client_fds[i];
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
    memset(client_fds, 0, sizeof(client_fds));

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
        close(client_fds[i]);
    }
    client_count = 0;
    pthread_mutex_unlock(&clients_mutex);

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
        if (ipc_send(client_fds[i], type, payload, payload_size) == 0) {
            success_count++;
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
