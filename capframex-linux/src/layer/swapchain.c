#include "swapchain.h"
#include "timing.h"
#include "ipc_client.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <pthread.h>

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

    VkResult result = dev_data->dispatch.CreateSwapchainKHR(device, pCreateInfo,
                                                             pAllocator, pSwapchain);
    if (result == VK_SUCCESS) {
        add_swapchain(device, *pSwapchain, pCreateInfo);

        // Notify IPC client of new swapchain
        ipc_client_notify_swapchain_created(pCreateInfo->imageExtent.width,
                                            pCreateInfo->imageExtent.height);
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

    if (dev_data->dispatch.DestroySwapchainKHR) {
        dev_data->dispatch.DestroySwapchainKHR(device, swapchain, pAllocator);
    }
}

VKAPI_ATTR VkResult VKAPI_CALL layer_QueuePresentKHR(
    VkQueue queue,
    const VkPresentInfoKHR* pPresentInfo)
{
    // Find device data from queue (simplified - we check all devices)
    DeviceData* dev_data = NULL;
    pthread_mutex_lock(&swapchain_mutex);
    if (pPresentInfo->swapchainCount > 0) {
        SwapchainData* sc = swapchain_get_data(pPresentInfo->pSwapchains[0]);
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
    for (uint32_t i = 0; i < pPresentInfo->swapchainCount; i++) {
        SwapchainData* sc_data = swapchain_get_data(pPresentInfo->pSwapchains[i]);
        if (sc_data) {
            sc_data->frame_count++;

            // Record the frametime
            timing_record_frame(sc_data->frame_count, pre_present_time, post_present_time);
        }
    }

    return result;
}
