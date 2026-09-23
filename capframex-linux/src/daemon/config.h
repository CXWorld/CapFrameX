#ifndef CAPFRAMEX_CONFIG_H
#define CAPFRAMEX_CONFIG_H

#include "common.h"

// Configuration structure
typedef struct {
    // Process detection
    bool auto_detect_games;
    int scan_interval_ms;

    // Logging
    int log_level;  // 0=error, 1=warn, 2=info, 3=debug
    char log_file[MAX_PATH_LENGTH];

    // Paths
    char config_dir[MAX_PATH_LENGTH];
    char data_dir[MAX_PATH_LENGTH];
} DaemonConfig;

// Get the singleton config instance
DaemonConfig* config_get(void);

// Load configuration from file
int config_load(const char* path);

// Save configuration to file
int config_save(const char* path);

// Apply default configuration
void config_set_defaults(void);

// Get standard config directory path
const char* config_get_dir(void);

// Get standard data directory path
const char* config_get_data_dir(void);

#endif // CAPFRAMEX_CONFIG_H
