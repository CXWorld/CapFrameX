#define _GNU_SOURCE
#include "launcher_detect.h"
#include "process_monitor.h"
#include "ignore_list.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <strings.h>
#include <fnmatch.h>

// Known launchers and their executable patterns
static const LauncherInfo KNOWN_LAUNCHERS[] = {
    { LAUNCHER_STEAM,     "Steam",     "steam" },
    { LAUNCHER_STEAM,     "Steam",     "steamwebhelper" },
    { LAUNCHER_LUTRIS,    "Lutris",    "lutris" },
    { LAUNCHER_HEROIC,    "Heroic",    "heroic" },
    { LAUNCHER_HEROIC,    "Heroic",    "legendary" },
    { LAUNCHER_BOTTLES,   "Bottles",   "bottles" },
    { LAUNCHER_GAMESCOPE, "Gamescope", "gamescope" },
    { LAUNCHER_WINE,      "Wine",      "wine*" },
    { LAUNCHER_WINE,      "Wine",      "wineserver" },
    { LAUNCHER_PROTON,    "Proton",    "proton" },
    { LAUNCHER_UNKNOWN,   NULL,        NULL }
};

// Common game directories
static const char* GAME_DIRECTORIES[] = {
    "/.steam/steam/steamapps/common/",
    "/.local/share/Steam/steamapps/common/",
    "/.local/share/lutris/",
    "/.local/share/bottles/",
    "/Games/",
    NULL
};

// Blacklisted processes (known non-games)
static const char* DEFAULT_BLACKLIST[] = {
    "steam",
    "steamwebhelper",
    "lutris",
    "heroic",
    "bottles",
    "wine",
    "wineserver",
    "winedevice.exe",
    "services.exe",
    "plugplay.exe",
    "explorer.exe",
    "rpcss.exe",
    "tabtip.exe",
    "conhost.exe",
    "start.exe",
    "cmd.exe",
    "bash",
    "sh",
    "python",
    "python3",
    "pressure-vessel",
    "pressure-vessel-wrap",
    "pv-bwrap",
    "srt-bwrap",
    "pv-adverb",
    "steam-runtime-*",
    "reaper",
    "_v2-entry-point",
    "proton",
    "steam-runtime-launcher-*",
    "*-inspect-library",
    "*-capsule-capture-libs",
    "*-detect-platform",
    "*-detect-lib",
    "wine64",
    "wine64-preloade",
    "wineboot.exe",
    "rundll32.exe",
    "regsvr32.exe",
    "ntlm_auth",
    "gst-plugin-scanner",
    "ld-linux*",
    NULL
};

#define MAX_CUSTOM_LAUNCHERS 32
#define MAX_WHITELIST 256
#define MAX_BLACKLIST 256

static char* custom_whitelist[MAX_WHITELIST] = {0};
static char* custom_blacklist[MAX_BLACKLIST] = {0};
static int whitelist_count = 0;
static int blacklist_count = 0;

void launcher_detect_init(void) {
    // Initialize with default blacklist
    for (int i = 0; DEFAULT_BLACKLIST[i] != NULL && blacklist_count < MAX_BLACKLIST; i++) {
        custom_blacklist[blacklist_count++] = strdup(DEFAULT_BLACKLIST[i]);
    }
}

LauncherType launcher_detect_type(const ProcessInfo* info) {
    if (!info || !info->exe_name[0]) {
        return LAUNCHER_UNKNOWN;
    }

    for (int i = 0; KNOWN_LAUNCHERS[i].name != NULL; i++) {
        if (fnmatch(KNOWN_LAUNCHERS[i].exe_pattern, info->exe_name, FNM_CASEFOLD) == 0) {
            return KNOWN_LAUNCHERS[i].type;
        }
    }

    return LAUNCHER_UNKNOWN;
}

const char* launcher_get_name(LauncherType type) {
    switch (type) {
        case LAUNCHER_STEAM:     return "Steam";
        case LAUNCHER_LUTRIS:    return "Lutris";
        case LAUNCHER_HEROIC:    return "Heroic";
        case LAUNCHER_BOTTLES:   return "Bottles";
        case LAUNCHER_GAMESCOPE: return "Gamescope";
        case LAUNCHER_WINE:      return "Wine";
        case LAUNCHER_PROTON:    return "Proton";
        default:                 return "Unknown";
    }
}

static bool is_in_game_directory(const char* exe_path) {
    if (!exe_path) return false;

    const char* home = getenv("HOME");
    char full_path[MAX_PATH_LENGTH];

    for (int i = 0; GAME_DIRECTORIES[i] != NULL; i++) {
        if (GAME_DIRECTORIES[i][0] == '/') {
            // Relative to home
            if (home) {
                snprintf(full_path, sizeof(full_path), "%s%s", home, GAME_DIRECTORIES[i]);
                if (strstr(exe_path, full_path) != NULL) {
                    return true;
                }
            }
        } else {
            // Absolute path
            if (strstr(exe_path, GAME_DIRECTORIES[i]) != NULL) {
                return true;
            }
        }
    }

    return false;
}

bool launcher_is_blacklisted(const char* exe_name) {
    if (!exe_name) return false;

    // Check hardcoded/default blacklist
    for (int i = 0; i < blacklist_count; i++) {
        if (custom_blacklist[i] &&
            fnmatch(custom_blacklist[i], exe_name, FNM_CASEFOLD) == 0) {
            return true;
        }
    }

    // Check user ignore list
    if (ignore_list_contains(exe_name)) {
        return true;
    }

    return false;
}

bool launcher_is_whitelisted(const char* exe_name) {
    if (!exe_name) return false;

    for (int i = 0; i < whitelist_count; i++) {
        if (custom_whitelist[i] &&
            fnmatch(custom_whitelist[i], exe_name, FNM_CASEFOLD) == 0) {
            return true;
        }
    }

    return false;
}

bool launcher_is_launcher_child(pid_t pid, LauncherType* out_launcher_type) {
    // Walk up the process tree looking for a launcher
    pid_t current = pid;
    int depth = 0;
    const int max_depth = 20;  // Prevent infinite loops

    while (current > 1 && depth < max_depth) {
        ProcessInfo info;
        if (process_get_info(current, &info) != 0) {
            break;
        }

        LauncherType type = launcher_detect_type(&info);
        if (type != LAUNCHER_UNKNOWN) {
            if (out_launcher_type) {
                *out_launcher_type = type;
            }
            return true;
        }

        current = info.parent_pid;
        depth++;
    }

    return false;
}

int launcher_get_chain(pid_t pid, char* buffer, size_t buffer_size) {
    if (!buffer || buffer_size == 0) return 0;

    buffer[0] = '\0';

    // Collect launchers walking up the tree (will be in reverse order)
    LauncherType launchers[20];
    int launcher_count = 0;

    pid_t current = pid;
    int depth = 0;
    const int max_depth = 20;

    while (current > 1 && depth < max_depth && launcher_count < 20) {
        ProcessInfo info;
        if (process_get_info(current, &info) != 0) {
            break;
        }

        LauncherType type = launcher_detect_type(&info);
        if (type != LAUNCHER_UNKNOWN) {
            // Avoid duplicates (e.g., multiple wine* matches)
            if (launcher_count == 0 || launchers[launcher_count - 1] != type) {
                launchers[launcher_count++] = type;
            }
        }

        current = info.parent_pid;
        depth++;
    }

    if (launcher_count == 0) return 0;

    // Build the chain string in reverse order (root launcher first)
    size_t pos = 0;
    for (int i = launcher_count - 1; i >= 0; i--) {
        const char* name = launcher_get_name(launchers[i]);
        size_t name_len = strlen(name);

        // Check if we have space (name + " > " separator or null terminator)
        size_t needed = name_len + (i > 0 ? 3 : 1);
        if (pos + needed > buffer_size) break;

        memcpy(buffer + pos, name, name_len);
        pos += name_len;

        if (i > 0) {
            memcpy(buffer + pos, " > ", 3);
            pos += 3;
        }
    }
    buffer[pos] = '\0';

    return launcher_count;
}

static bool is_wine_preloader(const char* exe_path) {
    return exe_path && (strstr(exe_path, "wine64-preloader") != NULL ||
                        strstr(exe_path, "wine-preloader") != NULL);
}

static bool get_wine_game_name(pid_t pid, char* buffer, size_t buffer_size) {
    // For Wine processes, get the actual game name from /proc/[pid]/comm
    char proc_path[64];
    snprintf(proc_path, sizeof(proc_path), "/proc/%d/comm", pid);

    FILE* f = fopen(proc_path, "r");
    if (!f) return false;

    if (fgets(buffer, buffer_size, f)) {
        size_t len = strlen(buffer);
        if (len > 0 && buffer[len - 1] == '\n') {
            buffer[len - 1] = '\0';
        }
        fclose(f);
        return true;
    }
    fclose(f);
    return false;
}

bool launcher_is_game_process(const ProcessInfo* info) {
    if (!info) return false;

    // Check blacklist first
    if (launcher_is_blacklisted(info->exe_name)) {
        return false;
    }

    // Check whitelist (always consider as game)
    if (launcher_is_whitelisted(info->exe_name)) {
        return true;
    }

    // Special handling for Wine/Proton games - MUST check BEFORE launcher type check
    // because wine64-preloader matches "wine*" pattern but hosts actual game processes
    if (is_wine_preloader(info->exe_path)) {
        char comm_name[256];
        if (get_wine_game_name(info->pid, comm_name, sizeof(comm_name))) {
            // Check if the comm name is blacklisted
            if (launcher_is_blacklisted(comm_name)) {
                return false;
            }
            // Wine game processes have truncated names in comm, but they're real games
            // Exclude known system/helper processes
            if (strcasecmp(comm_name, "wineserver") == 0 ||
                strcasecmp(comm_name, "wine64-preloade") == 0 ||
                strcasecmp(comm_name, "wine-preloader") == 0 ||
                strcasecmp(comm_name, "wine64") == 0 ||
                strcasecmp(comm_name, "wine") == 0 ||
                strcasecmp(comm_name, "wineboot.exe") == 0 ||
                strcasecmp(comm_name, "services.exe") == 0 ||
                strcasecmp(comm_name, "winedevice.exe") == 0 ||
                strcasecmp(comm_name, "plugplay.exe") == 0 ||
                strcasecmp(comm_name, "explorer.exe") == 0 ||
                strcasecmp(comm_name, "rpcss.exe") == 0 ||
                strcasecmp(comm_name, "tabtip.exe") == 0 ||
                strcasecmp(comm_name, "conhost.exe") == 0 ||
                strcasecmp(comm_name, "steam.exe") == 0 ||
                strcasecmp(comm_name, "steamwebhelper.") == 0 ||
                strcasecmp(comm_name, "crashpad_handle") == 0 ||
                strncasecmp(comm_name, "crashpad", 8) == 0 ||
                strcasecmp(comm_name, "xalia.exe") == 0 ||
                strcasecmp(comm_name, "svchost.exe") == 0 ||
                strcasecmp(comm_name, "rundll32.exe") == 0 ||
                strcasecmp(comm_name, "regsvr32.exe") == 0 ||
                strcasecmp(comm_name, "start.exe") == 0 ||
                strcasecmp(comm_name, "cmd.exe") == 0 ||
                strcasecmp(comm_name, "wineconsole") == 0 ||
                strcasecmp(comm_name, "winedbg") == 0 ||
                strncasecmp(comm_name, "proton", 6) == 0 ||
                strncasecmp(comm_name, "pressure-", 9) == 0) {
                return false;
            }
            // Looks like a real game process
            return true;
        }
        // Wine preloader that didn't match - not a game
        return false;
    }

    // Check if it's a launcher itself (non-Wine)
    if (launcher_detect_type(info) != LAUNCHER_UNKNOWN) {
        return false;
    }

    // Check if it's in a known game directory (for native Linux games)
    if (is_in_game_directory(info->exe_path)) {
        return true;
    }

    // Check if it's a child of a launcher
    LauncherType launcher_type;
    if (launcher_is_launcher_child(info->pid, &launcher_type)) {
        // Additional heuristics for launcher children
        // Windows executables (.exe) are likely games when launched via Wine/Proton
        const char* ext = strrchr(info->exe_name, '.');
        if (ext && strcasecmp(ext, ".exe") == 0) {
            return true;
        }

        // Native Linux games typically don't have extensions
        // and are in game directories (already checked above)
        return false;
    }

    return false;
}

void launcher_whitelist_add(const char* exe_name) {
    if (!exe_name || whitelist_count >= MAX_WHITELIST) return;

    custom_whitelist[whitelist_count++] = strdup(exe_name);
    LOG_INFO("Added to whitelist: %s", exe_name);
}

void launcher_blacklist_add(const char* exe_name) {
    if (!exe_name || blacklist_count >= MAX_BLACKLIST) return;

    custom_blacklist[blacklist_count++] = strdup(exe_name);
    LOG_INFO("Added to blacklist: %s", exe_name);
}

void launcher_add_custom(const char* name, const char* exe_pattern) {
    // This would require dynamic launcher list - simplified for now
    LOG_INFO("Custom launcher added: %s (%s)", name, exe_pattern);
}
