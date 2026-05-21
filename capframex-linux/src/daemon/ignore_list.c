#include "ignore_list.h"
#include "config.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <strings.h>
#include <unistd.h>
#include <sys/stat.h>
#include <errno.h>
#include <time.h>
#include <pthread.h>

// Ignore list entry
typedef struct {
    char name[MAX_GAME_NAME_LENGTH];
    char added_at[32];  // ISO 8601 timestamp
} IgnoreEntry;

static IgnoreEntry ignore_list[MAX_IGNORE_LIST];
static int ignore_count = 0;
static char ignore_list_path[MAX_PATH_LENGTH];
static pthread_mutex_t ignore_mutex = PTHREAD_MUTEX_INITIALIZER;
static bool initialized = false;

// Forward declarations
static int save_to_file(void);
static int load_from_file(void);
static void get_iso_timestamp(char* buf, size_t size);

static void ensure_directory(const char* path) {
    struct stat st;
    if (stat(path, &st) == -1) {
        if (mkdir(path, 0755) == -1 && errno != EEXIST) {
            LOG_WARN("Failed to create directory %s: %s", path, strerror(errno));
        }
    }
}

int ignore_list_init(void) {
    pthread_mutex_lock(&ignore_mutex);

    if (initialized) {
        pthread_mutex_unlock(&ignore_mutex);
        return 0;
    }

    // Build file path
    const char* config_dir = config_get_dir();
    snprintf(ignore_list_path, sizeof(ignore_list_path),
             "%s/ignore_list.json", config_dir);

    // Ensure config directory exists
    ensure_directory(config_dir);

    // Clear the list
    memset(ignore_list, 0, sizeof(ignore_list));
    ignore_count = 0;

    // Load existing file
    int result = load_from_file();

    initialized = true;
    pthread_mutex_unlock(&ignore_mutex);

    LOG_INFO("Ignore list initialized with %d entries from %s", ignore_count, ignore_list_path);
    return result;
}

bool ignore_list_contains(const char* process_name) {
    if (!process_name || !initialized) return false;

    pthread_mutex_lock(&ignore_mutex);

    for (int i = 0; i < ignore_count; i++) {
        if (strcasecmp(ignore_list[i].name, process_name) == 0) {
            pthread_mutex_unlock(&ignore_mutex);
            return true;
        }
    }

    pthread_mutex_unlock(&ignore_mutex);
    return false;
}

int ignore_list_add(const char* process_name) {
    if (!process_name || !initialized) return -1;

    pthread_mutex_lock(&ignore_mutex);

    // Check if already exists
    for (int i = 0; i < ignore_count; i++) {
        if (strcasecmp(ignore_list[i].name, process_name) == 0) {
            pthread_mutex_unlock(&ignore_mutex);
            return 0;  // Already in list
        }
    }

    // Check capacity
    if (ignore_count >= MAX_IGNORE_LIST) {
        LOG_WARN("Ignore list is full, cannot add: %s", process_name);
        pthread_mutex_unlock(&ignore_mutex);
        return -1;
    }

    // Add new entry
    strncpy(ignore_list[ignore_count].name, process_name, MAX_GAME_NAME_LENGTH - 1);
    ignore_list[ignore_count].name[MAX_GAME_NAME_LENGTH - 1] = '\0';
    get_iso_timestamp(ignore_list[ignore_count].added_at, sizeof(ignore_list[ignore_count].added_at));
    ignore_count++;

    // Persist to file
    int result = save_to_file();

    pthread_mutex_unlock(&ignore_mutex);

    LOG_INFO("Added to ignore list: %s", process_name);
    return result;
}

int ignore_list_remove(const char* process_name) {
    if (!process_name || !initialized) return -1;

    pthread_mutex_lock(&ignore_mutex);

    // Find and remove
    for (int i = 0; i < ignore_count; i++) {
        if (strcasecmp(ignore_list[i].name, process_name) == 0) {
            // Shift remaining entries
            for (int j = i; j < ignore_count - 1; j++) {
                ignore_list[j] = ignore_list[j + 1];
            }
            ignore_count--;

            // Persist to file
            int result = save_to_file();

            pthread_mutex_unlock(&ignore_mutex);

            LOG_INFO("Removed from ignore list: %s", process_name);
            return result;
        }
    }

    pthread_mutex_unlock(&ignore_mutex);
    return -1;  // Not found
}

int ignore_list_count(void) {
    pthread_mutex_lock(&ignore_mutex);
    int count = ignore_count;
    pthread_mutex_unlock(&ignore_mutex);
    return count;
}

const char* ignore_list_get(int index) {
    pthread_mutex_lock(&ignore_mutex);

    if (index < 0 || index >= ignore_count) {
        pthread_mutex_unlock(&ignore_mutex);
        return NULL;
    }

    const char* name = ignore_list[index].name;
    pthread_mutex_unlock(&ignore_mutex);
    return name;
}

int ignore_list_reload(void) {
    pthread_mutex_lock(&ignore_mutex);

    memset(ignore_list, 0, sizeof(ignore_list));
    ignore_count = 0;

    int result = load_from_file();

    pthread_mutex_unlock(&ignore_mutex);

    LOG_INFO("Ignore list reloaded with %d entries", ignore_count);
    return result;
}

const char* ignore_list_get_path(void) {
    return ignore_list_path;
}

void ignore_list_cleanup(void) {
    pthread_mutex_lock(&ignore_mutex);
    memset(ignore_list, 0, sizeof(ignore_list));
    ignore_count = 0;
    initialized = false;
    pthread_mutex_unlock(&ignore_mutex);
}

// --- Internal functions ---

static void get_iso_timestamp(char* buf, size_t size) {
    time_t now = time(NULL);
    struct tm* tm_info = gmtime(&now);
    strftime(buf, size, "%Y-%m-%dT%H:%M:%SZ", tm_info);
}

// Simple JSON string extraction helper
// Finds "key": "value" and extracts value
static bool extract_json_string(const char* json, const char* key, char* value, size_t value_size) {
    char pattern[128];
    snprintf(pattern, sizeof(pattern), "\"%s\"", key);

    const char* key_pos = strstr(json, pattern);
    if (!key_pos) return false;

    // Find the colon after the key
    const char* colon = strchr(key_pos + strlen(pattern), ':');
    if (!colon) return false;

    // Find the opening quote of the value
    const char* quote_start = strchr(colon, '"');
    if (!quote_start) return false;
    quote_start++;  // Skip the quote

    // Find the closing quote
    const char* quote_end = strchr(quote_start, '"');
    if (!quote_end) return false;

    // Copy the value
    size_t len = quote_end - quote_start;
    if (len >= value_size) len = value_size - 1;

    strncpy(value, quote_start, len);
    value[len] = '\0';

    return true;
}

static int load_from_file(void) {
    FILE* f = fopen(ignore_list_path, "r");
    if (!f) {
        // File doesn't exist, create empty one
        return save_to_file();
    }

    // Read entire file
    fseek(f, 0, SEEK_END);
    long size = ftell(f);
    fseek(f, 0, SEEK_SET);

    if (size <= 0 || size > 1024 * 1024) {  // Max 1MB
        fclose(f);
        return -1;
    }

    char* content = malloc(size + 1);
    if (!content) {
        fclose(f);
        return -1;
    }

    size_t read_size = fread(content, 1, size, f);
    fclose(f);
    content[read_size] = '\0';

    // Find the processes array
    const char* processes_start = strstr(content, "\"processes\"");
    if (!processes_start) {
        free(content);
        return 0;  // Empty or invalid, start fresh
    }

    // Find the array start
    const char* array_start = strchr(processes_start, '[');
    if (!array_start) {
        free(content);
        return 0;
    }

    // Parse each object in the array
    const char* pos = array_start;
    while ((pos = strchr(pos, '{')) != NULL) {
        const char* obj_end = strchr(pos, '}');
        if (!obj_end) break;

        // Extract this object as a substring
        size_t obj_len = obj_end - pos + 1;
        char* obj = malloc(obj_len + 1);
        if (!obj) break;

        strncpy(obj, pos, obj_len);
        obj[obj_len] = '\0';

        // Extract name
        char name[MAX_GAME_NAME_LENGTH] = {0};
        char added_at[32] = {0};

        if (extract_json_string(obj, "name", name, sizeof(name))) {
            if (ignore_count < MAX_IGNORE_LIST) {
                strncpy(ignore_list[ignore_count].name, name, MAX_GAME_NAME_LENGTH - 1);

                if (extract_json_string(obj, "added_at", added_at, sizeof(added_at))) {
                    strncpy(ignore_list[ignore_count].added_at, added_at, sizeof(ignore_list[ignore_count].added_at) - 1);
                } else {
                    get_iso_timestamp(ignore_list[ignore_count].added_at, sizeof(ignore_list[ignore_count].added_at));
                }

                ignore_count++;
            }
        }

        free(obj);
        pos = obj_end + 1;
    }

    free(content);
    return 0;
}

static int save_to_file(void) {
    // Write to temp file first for atomic update
    char temp_path[MAX_PATH_LENGTH];
    snprintf(temp_path, sizeof(temp_path), "%s.tmp", ignore_list_path);

    FILE* f = fopen(temp_path, "w");
    if (!f) {
        LOG_ERROR("Failed to save ignore list: %s", strerror(errno));
        return -1;
    }

    fprintf(f, "{\n");
    fprintf(f, "    \"version\": 1,\n");
    fprintf(f, "    \"processes\": [\n");

    for (int i = 0; i < ignore_count; i++) {
        fprintf(f, "        {\"name\": \"%s\", \"added_at\": \"%s\"}",
                ignore_list[i].name, ignore_list[i].added_at);

        if (i < ignore_count - 1) {
            fprintf(f, ",");
        }
        fprintf(f, "\n");
    }

    fprintf(f, "    ]\n");
    fprintf(f, "}\n");

    fclose(f);

    // Atomic rename
    if (rename(temp_path, ignore_list_path) != 0) {
        LOG_ERROR("Failed to rename ignore list file: %s", strerror(errno));
        unlink(temp_path);
        return -1;
    }

    return 0;
}
