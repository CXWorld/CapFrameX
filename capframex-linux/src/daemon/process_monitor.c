#include "process_monitor.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>
#include <dirent.h>
#include <fcntl.h>
#include <errno.h>
#include <signal.h>
#include <pthread.h>
#include <sys/socket.h>
#include <sys/stat.h>
#include <linux/netlink.h>
#include <linux/connector.h>
#include <linux/cn_proc.h>

static int nl_socket = -1;
static pthread_t monitor_thread;
static volatile bool monitoring = false;
static process_event_callback event_callback = NULL;

int process_get_exe_path(pid_t pid, char* buffer, size_t buffer_size) {
    char proc_path[64];
    snprintf(proc_path, sizeof(proc_path), "/proc/%d/exe", pid);

    ssize_t len = readlink(proc_path, buffer, buffer_size - 1);
    if (len == -1) {
        return -1;
    }
    buffer[len] = '\0';
    return 0;
}

int process_get_cmdline(pid_t pid, char* buffer, size_t buffer_size) {
    char proc_path[64];
    snprintf(proc_path, sizeof(proc_path), "/proc/%d/cmdline", pid);

    int fd = open(proc_path, O_RDONLY);
    if (fd == -1) {
        return -1;
    }

    ssize_t len = read(fd, buffer, buffer_size - 1);
    close(fd);

    if (len <= 0) {
        return -1;
    }

    // Replace null bytes with spaces (except the last one)
    for (ssize_t i = 0; i < len - 1; i++) {
        if (buffer[i] == '\0') {
            buffer[i] = ' ';
        }
    }
    buffer[len] = '\0';

    return 0;
}

static int get_parent_pid(pid_t pid) {
    char proc_path[64];
    snprintf(proc_path, sizeof(proc_path), "/proc/%d/stat", pid);

    FILE* f = fopen(proc_path, "r");
    if (!f) {
        return -1;
    }

    pid_t ppid = -1;
    // Format: pid (comm) state ppid ...
    // Need to handle comm which can contain spaces and parentheses
    char line[1024];
    if (fgets(line, sizeof(line), f)) {
        char* start = strrchr(line, ')');
        if (start) {
            sscanf(start + 2, "%*c %d", &ppid);
        }
    }
    fclose(f);

    return ppid;
}

static void get_process_name(pid_t pid, char* buffer, size_t buffer_size) {
    char proc_path[64];
    snprintf(proc_path, sizeof(proc_path), "/proc/%d/comm", pid);

    FILE* f = fopen(proc_path, "r");
    if (f) {
        if (fgets(buffer, buffer_size, f)) {
            // Remove trailing newline
            size_t len = strlen(buffer);
            if (len > 0 && buffer[len - 1] == '\n') {
                buffer[len - 1] = '\0';
            }
        }
        fclose(f);
    } else {
        buffer[0] = '\0';
    }
}

int process_get_info(pid_t pid, ProcessInfo* info) {
    memset(info, 0, sizeof(ProcessInfo));
    info->pid = pid;

    // Get executable path
    if (process_get_exe_path(pid, info->exe_path, sizeof(info->exe_path)) != 0) {
        return -1;
    }

    // Extract executable name from path
    const char* name = strrchr(info->exe_path, '/');
    if (name) {
        strncpy(info->exe_name, name + 1, sizeof(info->exe_name) - 1);
    } else {
        strncpy(info->exe_name, info->exe_path, sizeof(info->exe_name) - 1);
    }

    // Get parent PID and name
    info->parent_pid = get_parent_pid(pid);
    if (info->parent_pid > 0) {
        get_process_name(info->parent_pid, info->parent_name, sizeof(info->parent_name));
    }

    // Get start time (for uniqueness)
    char stat_path[64];
    snprintf(stat_path, sizeof(stat_path), "/proc/%d/stat", pid);
    FILE* f = fopen(stat_path, "r");
    if (f) {
        char line[1024];
        if (fgets(line, sizeof(line), f)) {
            char* p = strrchr(line, ')');
            if (p) {
                unsigned long long starttime;
                // Skip to field 22 (starttime)
                int fields_to_skip = 19; // Already past pid and comm
                for (int i = 0; i < fields_to_skip && p; i++) {
                    p = strchr(p + 1, ' ');
                }
                if (p && sscanf(p, "%llu", &starttime) == 1) {
                    info->start_time = starttime;
                }
            }
        }
        fclose(f);
    }

    return 0;
}

bool process_is_running(pid_t pid) {
    char proc_path[64];
    snprintf(proc_path, sizeof(proc_path), "/proc/%d", pid);

    struct stat st;
    return stat(proc_path, &st) == 0;
}

static int setup_netlink_socket(void) {
    nl_socket = socket(PF_NETLINK, SOCK_DGRAM, NETLINK_CONNECTOR);
    if (nl_socket == -1) {
        LOG_ERROR("Failed to create netlink socket: %s", strerror(errno));
        return -1;
    }

    struct sockaddr_nl addr = {
        .nl_family = AF_NETLINK,
        .nl_pid = getpid(),
        .nl_groups = CN_IDX_PROC,
    };

    if (bind(nl_socket, (struct sockaddr*)&addr, sizeof(addr)) == -1) {
        LOG_ERROR("Failed to bind netlink socket: %s", strerror(errno));
        close(nl_socket);
        nl_socket = -1;
        return -1;
    }

    // Subscribe to process events
    struct {
        struct nlmsghdr nl_hdr;
        struct cn_msg cn_msg;
        enum proc_cn_mcast_op cn_mcast;
    } msg = {
        .nl_hdr = {
            .nlmsg_len = sizeof(msg),
            .nlmsg_type = NLMSG_DONE,
            .nlmsg_pid = getpid(),
        },
        .cn_msg = {
            .id = { CN_IDX_PROC, CN_VAL_PROC },
            .len = sizeof(enum proc_cn_mcast_op),
        },
        .cn_mcast = PROC_CN_MCAST_LISTEN,
    };

    if (send(nl_socket, &msg, sizeof(msg), 0) == -1) {
        LOG_ERROR("Failed to subscribe to process events: %s", strerror(errno));
        close(nl_socket);
        nl_socket = -1;
        return -1;
    }

    return 0;
}

static void* monitor_thread_func(void* arg) {
    (void)arg;

    char buf[4096];
    struct sockaddr_nl addr;

    while (monitoring) {
        socklen_t addr_len = sizeof(addr);
        ssize_t len = recvfrom(nl_socket, buf, sizeof(buf), 0,
                               (struct sockaddr*)&addr, &addr_len);

        if (len <= 0) {
            if (errno == EINTR) continue;
            break;
        }

        // Verify sender is kernel
        if (addr.nl_pid != 0) continue;

        struct nlmsghdr* nl_hdr = (struct nlmsghdr*)buf;
        if (!NLMSG_OK(nl_hdr, (size_t)len)) continue;

        struct cn_msg* cn_msg = NLMSG_DATA(nl_hdr);
        struct proc_event* ev = (struct proc_event*)cn_msg->data;

        ProcessInfo info;

        switch (ev->what) {
            case PROC_EVENT_EXEC:
                if (process_get_info(ev->event_data.exec.process_pid, &info) == 0) {
                    if (event_callback) {
                        event_callback(&info, true);
                    }
                }
                break;

            case PROC_EVENT_EXIT:
                memset(&info, 0, sizeof(info));
                info.pid = ev->event_data.exit.process_pid;
                if (event_callback) {
                    event_callback(&info, false);
                }
                break;

            default:
                break;
        }
    }

    return NULL;
}

int process_monitor_init(void) {
    return setup_netlink_socket();
}

int process_monitor_start(process_event_callback callback) {
    if (nl_socket == -1) {
        LOG_ERROR("Process monitor not initialized");
        return -1;
    }

    event_callback = callback;
    monitoring = true;

    if (pthread_create(&monitor_thread, NULL, monitor_thread_func, NULL) != 0) {
        LOG_ERROR("Failed to create monitor thread: %s", strerror(errno));
        monitoring = false;
        return -1;
    }

    LOG_INFO("Process monitor started");
    return 0;
}

void process_monitor_stop(void) {
    if (!monitoring) return;

    monitoring = false;

    // Unsubscribe from process events
    if (nl_socket != -1) {
        struct {
            struct nlmsghdr nl_hdr;
            struct cn_msg cn_msg;
            enum proc_cn_mcast_op cn_mcast;
        } msg = {
            .nl_hdr = {
                .nlmsg_len = sizeof(msg),
                .nlmsg_type = NLMSG_DONE,
                .nlmsg_pid = getpid(),
            },
            .cn_msg = {
                .id = { CN_IDX_PROC, CN_VAL_PROC },
                .len = sizeof(enum proc_cn_mcast_op),
            },
            .cn_mcast = PROC_CN_MCAST_IGNORE,
        };
        send(nl_socket, &msg, sizeof(msg), 0);
    }

    pthread_join(monitor_thread, NULL);
    LOG_INFO("Process monitor stopped");
}

void process_monitor_cleanup(void) {
    process_monitor_stop();

    if (nl_socket != -1) {
        close(nl_socket);
        nl_socket = -1;
    }
}

void process_scan_all(process_event_callback callback) {
    DIR* proc_dir = opendir("/proc");
    if (!proc_dir) {
        LOG_ERROR("Failed to open /proc: %s", strerror(errno));
        return;
    }

    struct dirent* entry;
    while ((entry = readdir(proc_dir)) != NULL) {
        // Check if directory name is a number (PID)
        char* endptr;
        long pid = strtol(entry->d_name, &endptr, 10);
        if (*endptr != '\0' || pid <= 0) continue;

        ProcessInfo info;
        if (process_get_info((pid_t)pid, &info) == 0) {
            callback(&info, true);
        }
    }

    closedir(proc_dir);
}
