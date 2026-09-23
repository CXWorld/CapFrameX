#ifndef CAPFRAMEX_PROCESS_MONITOR_H
#define CAPFRAMEX_PROCESS_MONITOR_H

#include "common.h"

// Callback function type for process events
typedef void (*process_event_callback)(ProcessInfo* info, bool is_new);

// Initialize the process monitor
// Returns 0 on success, -1 on failure
int process_monitor_init(void);

// Start monitoring for process events
// callback: Function called when a process event occurs
// Returns 0 on success, -1 on failure
int process_monitor_start(process_event_callback callback);

// Stop monitoring
void process_monitor_stop(void);

// Cleanup resources
void process_monitor_cleanup(void);

// Get information about a specific process
// Returns 0 on success, -1 if process not found
int process_get_info(pid_t pid, ProcessInfo* info);

// Check if a process is still running
bool process_is_running(pid_t pid);

// Get the executable path for a PID
// Returns 0 on success, -1 on failure
int process_get_exe_path(pid_t pid, char* buffer, size_t buffer_size);

// Get the command line for a PID
// Returns 0 on success, -1 on failure
int process_get_cmdline(pid_t pid, char* buffer, size_t buffer_size);

// Scan all currently running processes
// callback: Function called for each process found
void process_scan_all(process_event_callback callback);

#endif // CAPFRAMEX_PROCESS_MONITOR_H
