#include "swapchain.h"
#include "timing.h"
#include "ipc_client.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <pthread.h>
#include <unistd.h>

static SwapchainData swapchain_list[MAX_SWAPCHAINS];
static int swapchain_count = 0;
static pthread_mutex_t swapchain_mutex = PTHREAD_MUTEX_INITIALIZER;

void swapchain_init_device(DeviceData* device_data) {
    (void)device_data;
    // Per-device initialization if needed
}

void swapchain_cleanup_device(DeviceData* device_data) {
    pthread_mutex_lock(&swapchain_mutex);
    for (int i = swapchain_count - 1; i >= 0; i--) {
        if (swapchain_list[i].device == device_data->device) {
            for (int j = i; j < swapchain_count - 1; j++) {
                swapchain_list[j] = swapchain_list[j + 1];
            }
            swapchain_count--;
        }
    }
    pthread_mutex_unlock(&swapchain_mutex);
}

SwapchainData* swapchain_get_data(VkSwapchainKHR swapchain) {
    pthread_mutex_lock(&swapchain_mutex);
    for (int i = 0; i < swapchain_count; i++) {
        if (swapchain_list[i].swapchain == swapchain) {
            pthread_mutex_unlock(&swapchain_mutex);
            return &swapchain_list[i];
        }
    }
    pthread_mutex_unlock(&swapchain_mutex);
    return NULL;
}

bool swapchain_get_active_info(uint32_t* width, uint32_t* height,
                                uint32_t* format, uint32_t* image_count) {
    pthread_mutex_lock(&swapchain_mutex);
    fprintf(stderr, "[CapFrameX Layer] DEBUG: swapchain_get_active_info called, swapchain_count=%d\n", swapchain_count);
    for (int i = 0; i < swapchain_count; i++) {
        fprintf(stderr, "[CapFrameX Layer] DEBUG: swapchain[%d]: active=%d, width=%u, height=%u\n",
                i, swapchain_list[i].active, swapchain_list[i].width, swapchain_list[i].height);
        if (swapchain_list[i].active && swapchain_list[i].width > 0) {
            *width = swapchain_list[i].width;
            *height = swapchain_list[i].height;
            *format = (uint32_t)swapchain_list[i].format;
            *image_count = swapchain_list[i].image_count;
            fprintf(stderr, "[CapFrameX Layer] DEBUG: Found active swapchain: %ux%u\n", *width, *height);
            pthread_mutex_unlock(&swapchain_mutex);
            return true;
        }
    }
    fprintf(stderr, "[CapFrameX Layer] DEBUG: No active swapchain found\n");
    pthread_mutex_unlock(&swapchain_mutex);
    return false;
}

static SwapchainData* add_swapchain(VkDevice device, VkSwapchainKHR swapchain,
                                     const VkSwapchainCreateInfoKHR* info) {
    pthread_mutex_lock(&swapchain_mutex);

    if (swapchain_count >= MAX_SWAPCHAINS) {
        pthread_mutex_unlock(&swapchain_mutex);
        fprintf(stderr, "[CapFrameX Layer] Max swapchains reached\n");
        return NULL;
    }

    SwapchainData* data = &swapchain_list[swapchain_count];
    memset(data, 0, sizeof(SwapchainData));

    data->swapchain = swapchain;
    data->device = device;
    data->width = info->imageExtent.width;
    data->height = info->imageExtent.height;
    data->format = info->imageFormat;
    data->image_count = info->minImageCount;
    data->frame_count = 0;
    data->active = true;

    swapchain_count++;

    pthread_mutex_unlock(&swapchain_mutex);

    fprintf(stderr, "[CapFrameX Layer] Swapchain created: %ux%u\n",
            data->width, data->height);

    return data;
}

static void remove_swapchain(VkSwapchainKHR swapchain) {
    pthread_mutex_lock(&swapchain_mutex);

    for (int i = 0; i < swapchain_count; i++) {
        if (swapchain_list[i].swapchain == swapchain) {
            fprintf(stderr, "[CapFrameX Layer] Swapchain destroyed after %lu frames\n",
                    (unsigned long)swapchain_list[i].frame_count);

            for (int j = i; j < swapchain_count - 1; j++) {
                swapchain_list[j] = swapchain_list[j + 1];
            }
            swapchain_count--;
            break;
        }
    }

    pthread_mutex_unlock(&swapchain_mutex);
}

VKAPI_ATTR VkResult VKAPI_CALL layer_CreateSwapchainKHR(
    VkDevice device,
    const VkSwapchainCreateInfoKHR* pCreateInfo,
    const VkAllocationCallbacks* pAllocator,
    VkSwapchainKHR* pSwapchain)
{
    DeviceData* dev_data = layer_get_device_data(device);
    if (!dev_data || !dev_data->dispatch.CreateSwapchainKHR) {
        return VK_ERROR_INITIALIZATION_FAILED;
    }

    fprintf(stderr, "[CapFrameX Layer] CreateSwapchainKHR called: %ux%u\n",
            pCreateInfo->imageExtent.width, pCreateInfo->imageExtent.height);

    VkResult result = dev_data->dispatch.CreateSwapchainKHR(device, pCreateInfo,
                                                             pAllocator, pSwapchain);
    if (result == VK_SUCCESS) {
        fprintf(stderr, "[CapFrameX Layer] Swapchain created successfully\n");
        add_swapchain(device, *pSwapchain, pCreateInfo);

        // Notify daemon of new swapchain
        ipc_client_send_swapchain_created(
            pCreateInfo->imageExtent.width,
            pCreateInfo->imageExtent.height,
            (uint32_t)pCreateInfo->imageFormat,
            pCreateInfo->minImageCount);
    }

    return result;
}

VKAPI_ATTR void VKAPI_CALL layer_DestroySwapchainKHR(
    VkDevice device,
    VkSwapchainKHR swapchain,
    const VkAllocationCallbacks* pAllocator)
{
    DeviceData* dev_data = layer_get_device_data(device);
    if (!dev_data) return;

    remove_swapchain(swapchain);

    // Notify daemon of swapchain destruction
    ipc_client_send_swapchain_destroyed();

    if (dev_data->dispatch.DestroySwapchainKHR) {
        dev_data->dispatch.DestroySwapchainKHR(device, swapchain, pAllocator);
    }
}

// Internal version without locking (caller must hold mutex)
static SwapchainData* swapchain_get_data_unlocked(VkSwapchainKHR swapchain) {
    for (int i = 0; i < swapchain_count; i++) {
        if (swapchain_list[i].swapchain == swapchain) {
            return &swapchain_list[i];
        }
    }
    return NULL;
}

// Track if we need to send swapchain info
// Set when disconnected or reconnected, cleared after successful send
static bool pending_swapchain_send = false;
static pthread_mutex_t pending_mutex = PTHREAD_MUTEX_INITIALIZER;

VKAPI_ATTR VkResult VKAPI_CALL layer_QueuePresentKHR(
    VkQueue queue,
    const VkPresentInfoKHR* pPresentInfo)
{
    // Try to reconnect to daemon if not connected (handles game started before daemon)
    if (!ipc_client_is_connected()) {
        // Always mark pending when disconnected - we need to send info when we reconnect
        pthread_mutex_lock(&pending_mutex);
        pending_swapchain_send = true;
        pthread_mutex_unlock(&pending_mutex);

        ipc_client_try_reconnect();
    }

    // Check if we need to send swapchain info (with proper locking)
    pthread_mutex_lock(&pending_mutex);
    bool should_send = pending_swapchain_send;
    pthread_mutex_unlock(&pending_mutex);

    // Try to send swapchain info if pending and connected
    bool is_connected = ipc_client_is_connected();
    if (should_send) {
        ipc_debug_log("pending_send=1, connected=%d, swapchainCount=%u",
                      is_connected, pPresentInfo->swapchainCount);
    }

    if (should_send && is_connected && pPresentInfo->swapchainCount > 0) {
        pthread_mutex_lock(&swapchain_mutex);
        SwapchainData* sc = swapchain_get_data_unlocked(pPresentInfo->pSwapchains[0]);
        ipc_debug_log("Swapchain lookup: sc=%p, w=%u, h=%u",
                      (void*)sc, sc ? sc->width : 0, sc ? sc->height : 0);
        if (sc && sc->width > 0 && sc->height > 0) {
            DeviceData* tmp_dev = layer_get_device_data(sc->device);
            if (tmp_dev && tmp_dev->instance_data) {
                // Cache swapchain info before releasing lock
                uint32_t width = sc->width;
                uint32_t height = sc->height;
                uint32_t format = (uint32_t)sc->format;
                uint32_t image_count = sc->image_count;
                const char* gpu_name = tmp_dev->instance_data->gpu_name;

                bool present_timing = tmp_dev->present_timing_supported;
                pthread_mutex_unlock(&swapchain_mutex);

                // Send GPU info if not already sent
                if (gpu_name && gpu_name[0] != '\0') {
                    ipc_client_send_hello(gpu_name, present_timing);
                }
                // Send swapchain info
                ipc_client_send_swapchain_created(width, height, format, image_count);

                ipc_debug_log("SENT swapchain: %ux%u, clearing pending flag", width, height);

                // Clear pending flag with proper locking
                pthread_mutex_lock(&pending_mutex);
                pending_swapchain_send = false;
                pthread_mutex_unlock(&pending_mutex);

                pthread_mutex_lock(&swapchain_mutex);
            } else {
                ipc_debug_log("NO DEVICE DATA: tmp_dev=%p, instance=%p",
                              (void*)tmp_dev, tmp_dev ? (void*)tmp_dev->instance_data : NULL);
            }
        } else {
            ipc_debug_log("INVALID SC: sc=%p, w=%u, h=%u",
                          (void*)sc, sc ? sc->width : 0, sc ? sc->height : 0);
        }
        pthread_mutex_unlock(&swapchain_mutex);
    }

    // Find device data from queue (simplified - we check all devices)
    DeviceData* dev_data = NULL;
    pthread_mutex_lock(&swapchain_mutex);
    if (pPresentInfo->swapchainCount > 0) {
        SwapchainData* sc = swapchain_get_data_unlocked(pPresentInfo->pSwapchains[0]);
        if (sc) {
            dev_data = layer_get_device_data(sc->device);
        }
    }
    pthread_mutex_unlock(&swapchain_mutex);

    // Record frametime before present
    uint64_t pre_present_time = timing_get_timestamp();

    // Call the real QueuePresentKHR
    VkResult result = VK_SUCCESS;
    if (dev_data && dev_data->dispatch.QueuePresentKHR) {
        result = dev_data->dispatch.QueuePresentKHR(queue, pPresentInfo);
    }

    // Record frametime after present
    uint64_t post_present_time = timing_get_timestamp();

    // Update frame data for each presented swapchain
    pthread_mutex_lock(&swapchain_mutex);
    for (uint32_t i = 0; i < pPresentInfo->swapchainCount; i++) {
        SwapchainData* sc_data = swapchain_get_data_unlocked(pPresentInfo->pSwapchains[i]);
        if (sc_data) {
            // Store pre_present_time in ring buffer for future timing lookups
            // Use frame_count as our internal present ID
            sc_data->present_timestamps[sc_data->frame_count % PRESENT_HISTORY_SIZE] = pre_present_time;

            // Query actual present timing from extension if available
            uint64_t actual_present_time_ns = 0;
            float ms_until_render_complete = 0.0f;
            float ms_until_displayed = 0.0f;

            if (dev_data && dev_data->present_timing_supported) {
                if (dev_data->present_timing_type == PRESENT_TIMING_GOOGLE &&
                    dev_data->dispatch.GetPastPresentationTimingGOOGLE) {
                    // VK_GOOGLE_display_timing - returns timing for PAST frames
                    uint32_t count = 0;
                    VkResult timing_result = dev_data->dispatch.GetPastPresentationTimingGOOGLE(
                        dev_data->device, sc_data->swapchain, &count, NULL);

                    if (timing_result == VK_SUCCESS && count > 0) {
                        VkPastPresentationTimingGOOGLE* timings = malloc(count * sizeof(VkPastPresentationTimingGOOGLE));
                        if (timings) {
                            timing_result = dev_data->dispatch.GetPastPresentationTimingGOOGLE(
                                dev_data->device, sc_data->swapchain, &count, timings);

                            if (timing_result == VK_SUCCESS && count > 0) {
                                VkPastPresentationTimingGOOGLE* latest = &timings[count - 1];
                                actual_present_time_ns = latest->actualPresentTime;

                                // Initialize first_present_id on first timing data
                                if (!sc_data->present_id_initialized) {
                                    sc_data->first_present_id = latest->presentID;
                                    sc_data->present_id_initialized = true;
                                }

                                // Normalize presentID to index into our ring buffer
                                uint32_t normalized_id = latest->presentID - sc_data->first_present_id;
                                uint32_t history_idx = normalized_id % PRESENT_HISTORY_SIZE;
                                uint64_t frame_pre_present_time = sc_data->present_timestamps[history_idx];

                                if (frame_pre_present_time > 0) {
                                    if (latest->earliestPresentTime > 0) {
                                        ms_until_render_complete = (float)(latest->earliestPresentTime - frame_pre_present_time) / 1000000.0f;
                                    }
                                    if (latest->actualPresentTime > 0) {
                                        ms_until_displayed = (float)(latest->actualPresentTime - frame_pre_present_time) / 1000000.0f;
                                    }
                                }
                            }
                            free(timings);
                        }
                    }
                }
            }

            sc_data->frame_count++;
            timing_record_frame(sc_data->frame_count, pre_present_time, post_present_time,
                                actual_present_time_ns, ms_until_render_complete, ms_until_displayed);
        }
    }
    pthread_mutex_unlock(&swapchain_mutex);

    return result;
}
