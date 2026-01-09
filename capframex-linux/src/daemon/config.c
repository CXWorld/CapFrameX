#include "config.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/stat.h>
#include <errno.h>

static DaemonConfig config;
static bool initialized = false;

static void ensure_directory(const char* path) {
    struct stat st;
    if (stat(path, &st) == -1) {
        if (mkdir(path, 0755) == -1 && errno != EEXIST) {
            LOG_WARN("Failed to create directory %s: %s", path, strerror(errno));
        }
    }
}

void config_set_defaults(void) {
    memset(&config, 0, sizeof(config));

    config.auto_detect_games = true;
    config.scan_interval_ms = 1000;
    config.log_level = 2;  // Info

    // Set default paths
    const char* home = getenv("HOME");
    const char* xdg_config = getenv("XDG_CONFIG_HOME");
    const char* xdg_data = getenv("XDG_DATA_HOME");

    if (xdg_config) {
        snprintf(config.config_dir, sizeof(config.config_dir),
                 "%s/capframex", xdg_config);
    } else if (home) {
        snprintf(config.config_dir, sizeof(config.config_dir),
                 "%s/.config/capframex", home);
    } else {
        strncpy(config.config_dir, "/tmp/capframex", sizeof(config.config_dir) - 1);
    }

    if (xdg_data) {
        snprintf(config.data_dir, sizeof(config.data_dir),
                 "%s/capframex", xdg_data);
    } else if (home) {
        snprintf(config.data_dir, sizeof(config.data_dir),
                 "%s/.local/share/capframex", home);
    } else {
        strncpy(config.data_dir, "/tmp/capframex/data", sizeof(config.data_dir) - 1);
    }

    snprintf(config.log_file, sizeof(config.log_file),
             "%s/daemon.log", config.data_dir);

    initialized = true;
}

DaemonConfig* config_get(void) {
    if (!initialized) {
        config_set_defaults();
    }
    return &config;
}

const char* config_get_dir(void) {
    if (!initialized) {
        config_set_defaults();
    }
    return config.config_dir;
}

const char* config_get_data_dir(void) {
    if (!initialized) {
        config_set_defaults();
    }
    return config.data_dir;
}

int config_load(const char* path) {
    const char* config_path = path;
    char default_path[MAX_PATH_LENGTH];

    if (!config_path) {
        snprintf(default_path, sizeof(default_path),
                 "%s/daemon.conf", config_get_dir());
        config_path = default_path;
    }

    FILE* f = fopen(config_path, "r");
    if (!f) {
        LOG_INFO("Config file not found, using defaults: %s", config_path);
        return 0;  // Not an error, use defaults
    }

    char line[1024];
    while (fgets(line, sizeof(line), f)) {
        // Skip comments and empty lines
        if (line[0] == '#' || line[0] == '\n') continue;

        char key[256], value[768];
        if (sscanf(line, "%255[^=]=%767[^\n]", key, value) == 2) {
            // Trim whitespace
            char* k = key;
            char* v = value;
            while (*k == ' ') k++;
            while (*v == ' ') v++;

            if (strcmp(k, "auto_detect_games") == 0) {
                config.auto_detect_games = (strcmp(v, "true") == 0 || strcmp(v, "1") == 0);
            } else if (strcmp(k, "scan_interval_ms") == 0) {
                config.scan_interval_ms = atoi(v);
            } else if (strcmp(k, "log_level") == 0) {
                config.log_level = atoi(v);
            } else if (strcmp(k, "log_file") == 0) {
                strncpy(config.log_file, v, sizeof(config.log_file) - 1);
            }
        }
    }

    fclose(f);
    LOG_INFO("Configuration loaded from %s", config_path);
    return 0;
}

int config_save(const char* path) {
    const char* config_path = path;
    char default_path[MAX_PATH_LENGTH];

    if (!config_path) {
        ensure_directory(config.config_dir);
        snprintf(default_path, sizeof(default_path),
                 "%s/daemon.conf", config.config_dir);
        config_path = default_path;
    }

    FILE* f = fopen(config_path, "w");
    if (!f) {
        LOG_ERROR("Failed to save config to %s: %s", config_path, strerror(errno));
        return -1;
    }

    fprintf(f, "# CapFrameX Daemon Configuration\n\n");
    fprintf(f, "auto_detect_games=%s\n", config.auto_detect_games ? "true" : "false");
    fprintf(f, "scan_interval_ms=%d\n", config.scan_interval_ms);
    fprintf(f, "log_level=%d\n", config.log_level);
    fprintf(f, "log_file=%s\n", config.log_file);

    fclose(f);
    LOG_INFO("Configuration saved to %s", config_path);
    return 0;
}
