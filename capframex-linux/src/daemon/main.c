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

    memcpy(&tracked_games[tracked_game_count], info, sizeof(ProcessInfo));
    tracked_games[tracked_game_count].is_game = true;
    tracked_game_count++;

    LOG_INFO("Game detected: %s (PID %d)", info->exe_name, info->pid);

    // Notify clients
    GameDetectedPayload payload = {0};
    payload.pid = info->pid;
    strncpy(payload.game_name, info->exe_name, sizeof(payload.game_name) - 1);
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
    (void)payload;
    (void)client_fd;

    switch (header->type) {
        case MSG_STATUS_REQUEST: {
            // Send list of tracked games
            // For simplicity, send count first then each game
            uint32_t count = tracked_game_count;
            ipc_send(client_fd, MSG_STATUS_RESPONSE, &count, sizeof(count));
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
