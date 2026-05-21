#ifndef CAPFRAMEX_IGNORE_LIST_H
#define CAPFRAMEX_IGNORE_LIST_H

#include "common.h"

#define MAX_IGNORE_LIST 512

// Initialize ignore list (loads from file)
int ignore_list_init(void);

// Check if a process name is in the ignore list
bool ignore_list_contains(const char* process_name);

// Add a process to the ignore list (persists to file)
int ignore_list_add(const char* process_name);

// Remove a process from the ignore list (persists to file)
int ignore_list_remove(const char* process_name);

// Get count of entries
int ignore_list_count(void);

// Get entry by index (returns NULL if out of range)
const char* ignore_list_get(int index);

// Reload from file
int ignore_list_reload(void);

// Get file path
const char* ignore_list_get_path(void);

// Cleanup
void ignore_list_cleanup(void);

#endif // CAPFRAMEX_IGNORE_LIST_H
