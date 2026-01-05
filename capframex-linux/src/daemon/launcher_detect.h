#ifndef CAPFRAMEX_LAUNCHER_DETECT_H
#define CAPFRAMEX_LAUNCHER_DETECT_H

#include "common.h"

// Known launcher types
typedef enum {
    LAUNCHER_UNKNOWN = 0,
    LAUNCHER_STEAM,
    LAUNCHER_LUTRIS,
    LAUNCHER_HEROIC,
    LAUNCHER_BOTTLES,
    LAUNCHER_GAMESCOPE,
    LAUNCHER_WINE,
    LAUNCHER_PROTON,
} LauncherType;

// Launcher information
typedef struct {
    LauncherType type;
    const char* name;
    const char* exe_pattern;
} LauncherInfo;

// Initialize launcher detection
void launcher_detect_init(void);

// Check if a process is a known launcher
// Returns the launcher type, or LAUNCHER_UNKNOWN if not a launcher
LauncherType launcher_detect_type(const ProcessInfo* info);

// Get launcher name from type
const char* launcher_get_name(LauncherType type);

// Check if a process is likely a game (child of a launcher or in known locations)
bool launcher_is_game_process(const ProcessInfo* info);

// Check if a PID is a descendant of a launcher
bool launcher_is_launcher_child(pid_t pid, LauncherType* out_launcher_type);

// Add a custom launcher pattern
void launcher_add_custom(const char* name, const char* exe_pattern);

// Add a game to the whitelist
void launcher_whitelist_add(const char* exe_name);

// Add a process to the blacklist (ignored for game detection)
void launcher_blacklist_add(const char* exe_name);

// Check if a process is blacklisted
bool launcher_is_blacklisted(const char* exe_name);

// Check if a process is whitelisted
bool launcher_is_whitelisted(const char* exe_name);

#endif // CAPFRAMEX_LAUNCHER_DETECT_H
