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
#include "ignore_list.h"

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

            // Also send info about connected layers (skip blacklisted processes)
            int layer_count = 0;
            LayerClient* layers = ipc_get_layers(&layer_count);
            int sent_count = 0;
            for (int i = 0; i < layer_count; i++) {
                // Skip blacklisted processes
                if (ipc_is_blacklisted_process(layers[i].process_name)) {
                    continue;
                }
                GameDetectedPayload layer_payload = {0};
                layer_payload.pid = layers[i].pid;
                strncpy(layer_payload.game_name, layers[i].process_name,
                        sizeof(layer_payload.game_name) - 1);
                // Include GPU name in launcher field, with resolution if available
                if (layers[i].has_swapchain && strlen(layers[i].gpu_name) > 0) {
                    snprintf(layer_payload.launcher, sizeof(layer_payload.launcher),
                             "%s (%ux%u)", layers[i].gpu_name,
                             layers[i].swapchain_width, layers[i].swapchain_height);
                } else if (strlen(layers[i].gpu_name) > 0) {
                    strncpy(layer_payload.launcher, layers[i].gpu_name,
                            sizeof(layer_payload.launcher) - 1);
                } else if (layers[i].has_swapchain) {
                    snprintf(layer_payload.launcher, sizeof(layer_payload.launcher),
                             "%ux%u", layers[i].swapchain_width, layers[i].swapchain_height);
                }
                ipc_send(client_fd, MSG_GAME_STARTED, &layer_payload, sizeof(layer_payload));
                sent_count++;
            }
            LOG_INFO("Sent %d layer(s) to client %d (filtered from %d)", sent_count, client_fd, layer_count);
            break;
        }

        case MSG_START_CAPTURE: {
            // App wants to subscribe to frame stream from a specific PID
            if (payload && header->payload_size >= sizeof(pid_t)) {
                pid_t target_pid = *(pid_t*)payload;
                LOG_INFO(">>> Client %d subscribing to frame stream from PID %d <<<", client_fd, target_pid);

                // Check if there's a matching layer
                LayerClient* layer = ipc_get_layer_by_pid(target_pid);
                if (layer) {
                    LOG_INFO("  Found matching layer: process=%s, has_swapchain=%d",
                             layer->process_name, layer->has_swapchain);
                } else {
                    LOG_WARN("  WARNING: No layer found for PID %d - frames may not arrive!", target_pid);
                    // List available layers
                    int count = 0;
                    LayerClient* layers = ipc_get_layers(&count);
                    LOG_INFO("  Available layers (%d):", count);
                    for (int i = 0; i < count; i++) {
                        LOG_INFO("    - PID %d: %s", layers[i].pid, layers[i].process_name);
                    }
                }

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
            // Layer announced itself
            if (payload) {
                LayerHelloPayload* hello = (LayerHelloPayload*)payload;
                LOG_INFO("Layer hello from PID %d: %s on %s",
                         hello->pid, hello->process_name, hello->gpu_name);

                // Register the layer - returns true if this is a new layer (not duplicate or blacklisted)
                bool is_new = ipc_register_layer(client_fd, hello);

                // Only broadcast to app clients if this is a genuinely new game
                if (is_new) {
                    GameDetectedPayload game_payload = {0};
                    game_payload.pid = hello->pid;
                    strncpy(game_payload.game_name, hello->process_name,
                            sizeof(game_payload.game_name) - 1);
                    strncpy(game_payload.launcher, hello->gpu_name,
                            sizeof(game_payload.launcher) - 1);
                    ipc_broadcast_to_non_layers(MSG_GAME_STARTED, &game_payload, sizeof(game_payload));
                }
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

        case MSG_IGNORE_LIST_ADD: {
            // Add process to ignore list
            if (payload && header->payload_size >= sizeof(IgnoreListEntry)) {
                IgnoreListEntry* entry = (IgnoreListEntry*)payload;
                if (ignore_list_add(entry->process_name) == 0) {
                    LOG_INFO("Added to ignore list: %s (requested by client %d)",
                             entry->process_name, client_fd);
                    // Broadcast update to all app clients
                    ipc_broadcast_to_non_layers(MSG_IGNORE_LIST_UPDATED, NULL, 0);
                }
            }
            break;
        }

        case MSG_IGNORE_LIST_REMOVE: {
            // Remove process from ignore list
            if (payload && header->payload_size >= sizeof(IgnoreListEntry)) {
                IgnoreListEntry* entry = (IgnoreListEntry*)payload;
                if (ignore_list_remove(entry->process_name) == 0) {
                    LOG_INFO("Removed from ignore list: %s (requested by client %d)",
                             entry->process_name, client_fd);
                    // Broadcast update to all app clients
                    ipc_broadcast_to_non_layers(MSG_IGNORE_LIST_UPDATED, NULL, 0);
                }
            }
            break;
        }

        case MSG_IGNORE_LIST_GET: {
            // Send all ignore list entries to requesting client
            int count = ignore_list_count();
            LOG_INFO("Client %d requested ignore list, sending %d entries", client_fd, count);

            // Build and send response with all entries
            // Format: count (4 bytes) + concatenated null-terminated strings
            size_t buffer_size = sizeof(uint32_t);
            for (int i = 0; i < count; i++) {
                const char* name = ignore_list_get(i);
                if (name) {
                    buffer_size += strlen(name) + 1;  // +1 for null terminator
                }
            }

            char* buffer = malloc(buffer_size);
            if (buffer) {
                uint32_t* count_ptr = (uint32_t*)buffer;
                *count_ptr = (uint32_t)count;

                char* pos = buffer + sizeof(uint32_t);
                for (int i = 0; i < count; i++) {
                    const char* name = ignore_list_get(i);
                    if (name) {
                        size_t len = strlen(name) + 1;
                        memcpy(pos, name, len);
                        pos += len;
                    }
                }

                ipc_send(client_fd, MSG_IGNORE_LIST_RESPONSE, buffer, buffer_size);
                free(buffer);
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
    // Make stdout line-buffered for proper logging when output is redirected
    setvbuf(stdout, NULL, _IOLBF, 0);
    setvbuf(stderr, NULL, _IOLBF, 0);

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

    if (ignore_list_init() != 0) {
        LOG_WARN("Failed to initialize ignore list, continuing without it");
    }

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
    ignore_list_cleanup();

    LOG_INFO("Daemon stopped");
    return 0;
}
