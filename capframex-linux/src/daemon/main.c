#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <signal.h>
#include <unistd.h>
#include <getopt.h>

#include "common.h"
#include "config.h"
#include "process_monitor.h"
#include "launcher_detect.h"
#include "ipc.h"

static volatile bool running = true;

// Tracked games
#define MAX_GAMES 64
static ProcessInfo tracked_games[MAX_GAMES];
static int tracked_game_count = 0;

static void signal_handler(int sig) {
    (void)sig;
    LOG_INFO("Received signal %d, shutting down...", sig);
    running = false;
}

static bool is_wine_preloader(const char* exe_path) {
    return exe_path && (strstr(exe_path, "wine64-preloader") != NULL ||
                        strstr(exe_path, "wine-preloader") != NULL);
}

static void get_game_name(ProcessInfo* info, char* buffer, size_t buffer_size) {
    // For Wine/Proton processes, use comm name instead of exe name
    if (is_wine_preloader(info->exe_path)) {
        char proc_path[64];
        snprintf(proc_path, sizeof(proc_path), "/proc/%d/comm", info->pid);
        FILE* f = fopen(proc_path, "r");
        if (f) {
            if (fgets(buffer, buffer_size, f)) {
                size_t len = strlen(buffer);
                if (len > 0 && buffer[len - 1] == '\n') {
                    buffer[len - 1] = '\0';
                }
            }
            fclose(f);
            return;
        }
    }
    // Default: use exe name
    strncpy(buffer, info->exe_name, buffer_size - 1);
    buffer[buffer_size - 1] = '\0';
}

static void add_tracked_game(ProcessInfo* info) {
    if (tracked_game_count >= MAX_GAMES) {
        LOG_WARN("Max tracked games reached, ignoring %s", info->exe_name);
        return;
    }

    // Check if already tracked
    for (int i = 0; i < tracked_game_count; i++) {
        if (tracked_games[i].pid == info->pid) {
            return;
        }
    }

    // Get proper game name (handles Wine processes)
    char game_name[256];
    get_game_name(info, game_name, sizeof(game_name));

    memcpy(&tracked_games[tracked_game_count], info, sizeof(ProcessInfo));
    // Override exe_name with proper game name for Wine processes
    strncpy(tracked_games[tracked_game_count].exe_name, game_name,
            sizeof(tracked_games[tracked_game_count].exe_name) - 1);
    tracked_games[tracked_game_count].is_game = true;
    tracked_game_count++;

    LOG_INFO("Game detected: %s (PID %d)", game_name, info->pid);

    // Notify clients
    GameDetectedPayload payload = {0};
    payload.pid = info->pid;
    strncpy(payload.game_name, game_name, sizeof(payload.game_name) - 1);
    strncpy(payload.exe_path, info->exe_path, sizeof(payload.exe_path) - 1);

    LauncherType launcher_type;
    if (launcher_is_launcher_child(info->pid, &launcher_type)) {
        strncpy(payload.launcher, launcher_get_name(launcher_type),
                sizeof(payload.launcher) - 1);
    }

    ipc_broadcast(MSG_GAME_STARTED, &payload, sizeof(payload));

    // Update shared memory
    pid_t pids[MAX_GAMES];
    for (int i = 0; i < tracked_game_count; i++) {
        pids[i] = tracked_games[i].pid;
    }
    ipc_update_active_pids(pids, tracked_game_count);
}

static void remove_tracked_game(pid_t pid) {
    for (int i = 0; i < tracked_game_count; i++) {
        if (tracked_games[i].pid == pid) {
            LOG_INFO("Game exited: %s (PID %d)", tracked_games[i].exe_name, pid);

            // Notify clients
            GameDetectedPayload payload = {0};
            payload.pid = pid;
            strncpy(payload.game_name, tracked_games[i].exe_name,
                    sizeof(payload.game_name) - 1);
            ipc_broadcast(MSG_GAME_STOPPED, &payload, sizeof(payload));

            // Remove from list
            for (int j = i; j < tracked_game_count - 1; j++) {
                tracked_games[j] = tracked_games[j + 1];
            }
            tracked_game_count--;

            // Update shared memory
            pid_t pids[MAX_GAMES];
            for (int k = 0; k < tracked_game_count; k++) {
                pids[k] = tracked_games[k].pid;
            }
            ipc_update_active_pids(pids, tracked_game_count);
            break;
        }
    }
}

static void process_event_handler(ProcessInfo* info, bool is_new) {
    if (is_new) {
        // New process - check if it's a game
        if (launcher_is_game_process(info)) {
            add_tracked_game(info);
        }
    } else {
        // Process exited - check if it was tracked
        remove_tracked_game(info->pid);
    }
}

static void ipc_message_handler(MessageHeader* header, void* payload, int client_fd) {
    switch (header->type) {
        case MSG_STATUS_REQUEST: {
            // Send all tracked games to the requesting client
            LOG_INFO("Client %d requested status, sending %d games", client_fd, tracked_game_count);
            for (int i = 0; i < tracked_game_count; i++) {
                GameDetectedPayload game_payload = {0};
                game_payload.pid = tracked_games[i].pid;
                strncpy(game_payload.game_name, tracked_games[i].exe_name,
                        sizeof(game_payload.game_name) - 1);
                strncpy(game_payload.exe_path, tracked_games[i].exe_path,
                        sizeof(game_payload.exe_path) - 1);

                LauncherType launcher_type;
                if (launcher_is_launcher_child(tracked_games[i].pid, &launcher_type)) {
                    strncpy(game_payload.launcher, launcher_get_name(launcher_type),
                            sizeof(game_payload.launcher) - 1);
                }

                ipc_send(client_fd, MSG_GAME_STARTED, &game_payload, sizeof(game_payload));
            }

            // Also send info about connected layers
            int layer_count = 0;
            LayerClient* layers = ipc_get_layers(&layer_count);
            LOG_INFO("Sending %d layer(s) to client %d", layer_count, client_fd);
            for (int i = 0; i < layer_count; i++) {
                GameDetectedPayload layer_payload = {0};
                layer_payload.pid = layers[i].pid;
                strncpy(layer_payload.game_name, layers[i].process_name,
                        sizeof(layer_payload.game_name) - 1);
                // Include resolution in launcher field if available
                if (layers[i].has_swapchain) {
                    snprintf(layer_payload.launcher, sizeof(layer_payload.launcher),
                             "%ux%u", layers[i].swapchain_width, layers[i].swapchain_height);
                }
                ipc_send(client_fd, MSG_GAME_STARTED, &layer_payload, sizeof(layer_payload));
            }
            break;
        }

        case MSG_START_CAPTURE: {
            // App wants to subscribe to frame stream from a specific PID
            if (payload && header->payload_size >= sizeof(pid_t)) {
                pid_t target_pid = *(pid_t*)payload;
                LOG_INFO("Client %d subscribing to frame stream from PID %d", client_fd, target_pid);
                ipc_subscribe_app(client_fd, target_pid);
            }
            break;
        }

        case MSG_STOP_CAPTURE: {
            // App wants to unsubscribe from frame stream
            LOG_INFO("Client %d unsubscribing from frame stream", client_fd);
            ipc_unsubscribe_app(client_fd);
            break;
        }

        case MSG_LAYER_HELLO: {
            // Layer announced itself - notify all non-layer clients about the new "game"
            if (payload) {
                LayerHelloPayload* hello = (LayerHelloPayload*)payload;
                LOG_INFO("Layer hello from PID %d: %s on %s",
                         hello->pid, hello->process_name, hello->gpu_name);

                // Broadcast to all clients (excluding layers) that a new game is available
                GameDetectedPayload game_payload = {0};
                game_payload.pid = hello->pid;
                strncpy(game_payload.game_name, hello->process_name,
                        sizeof(game_payload.game_name) - 1);
                strncpy(game_payload.launcher, hello->gpu_name,
                        sizeof(game_payload.launcher) - 1);
                ipc_broadcast_to_non_layers(MSG_GAME_STARTED, &game_payload, sizeof(game_payload));
            }
            break;
        }

        case MSG_SWAPCHAIN_CREATED: {
            // Layer created swapchain - could notify apps about resolution
            if (payload) {
                SwapchainInfoPayload* info = (SwapchainInfoPayload*)payload;
                LOG_INFO("Swapchain created for PID %d: %ux%u",
                         info->pid, info->width, info->height);
            }
            break;
        }

        case MSG_SWAPCHAIN_DESTROYED: {
            // Layer destroyed swapchain
            if (payload) {
                SwapchainInfoPayload* info = (SwapchainInfoPayload*)payload;
                LOG_INFO("Swapchain destroyed for PID %d", info->pid);
            }
            break;
        }

        default:
            break;
    }
}

static void check_tracked_games(void) {
    // Periodically verify tracked games are still running
    for (int i = tracked_game_count - 1; i >= 0; i--) {
        if (!process_is_running(tracked_games[i].pid)) {
            remove_tracked_game(tracked_games[i].pid);
        }
    }
}

static void print_usage(const char* program) {
    printf("Usage: %s [options]\n", program);
    printf("\nOptions:\n");
    printf("  -c, --config FILE    Use specified config file\n");
    printf("  -d, --debug          Enable debug logging\n");
    printf("  -f, --foreground     Run in foreground (don't daemonize)\n");
    printf("  -h, --help           Show this help message\n");
    printf("  -v, --version        Show version information\n");
}

int main(int argc, char* argv[]) {
    const char* config_file = NULL;
    bool foreground = true;  // Default to foreground for systemd
    bool debug = false;

    static struct option long_options[] = {
        {"config",     required_argument, 0, 'c'},
        {"debug",      no_argument,       0, 'd'},
        {"foreground", no_argument,       0, 'f'},
        {"help",       no_argument,       0, 'h'},
        {"version",    no_argument,       0, 'v'},
        {0, 0, 0, 0}
    };

    int opt;
    while ((opt = getopt_long(argc, argv, "c:dfhv", long_options, NULL)) != -1) {
        switch (opt) {
            case 'c':
                config_file = optarg;
                break;
            case 'd':
                debug = true;
                break;
            case 'f':
                foreground = true;
                break;
            case 'h':
                print_usage(argv[0]);
                return 0;
            case 'v':
                printf("CapFrameX Daemon %s\n", CAPFRAMEX_VERSION);
                return 0;
            default:
                print_usage(argv[0]);
                return 1;
        }
    }

    // Load configuration
    config_set_defaults();
    config_load(config_file);

    DaemonConfig* cfg = config_get();
    if (debug) {
        cfg->log_level = 3;
    }

    LOG_INFO("CapFrameX Daemon %s starting...", CAPFRAMEX_VERSION);

    // Set up signal handlers
    signal(SIGINT, signal_handler);
    signal(SIGTERM, signal_handler);
    signal(SIGPIPE, SIG_IGN);

    // Initialize subsystems
    launcher_detect_init();

    if (process_monitor_init() != 0) {
        LOG_ERROR("Failed to initialize process monitor");
        return 1;
    }

    if (ipc_init() != 0) {
        LOG_ERROR("Failed to initialize IPC");
        process_monitor_cleanup();
        return 1;
    }

    // Start IPC server
    if (ipc_start(ipc_message_handler) != 0) {
        LOG_ERROR("Failed to start IPC server");
        ipc_cleanup();
        process_monitor_cleanup();
        return 1;
    }

    // Start process monitoring
    if (process_monitor_start(process_event_handler) != 0) {
        LOG_ERROR("Failed to start process monitor");
        ipc_cleanup();
        process_monitor_cleanup();
        return 1;
    }

    // Scan existing processes
    LOG_INFO("Scanning for running games...");
    process_scan_all(process_event_handler);
    LOG_INFO("Found %d games already running", tracked_game_count);

    LOG_INFO("Daemon ready, listening on %s", ipc_get_socket_path());

    // Main loop
    while (running) {
        sleep(cfg->scan_interval_ms / 1000);
        check_tracked_games();
    }

    // Cleanup
    LOG_INFO("Shutting down...");
    process_monitor_cleanup();
    ipc_cleanup();

    LOG_INFO("Daemon stopped");
    return 0;
}
