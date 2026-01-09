#ifndef CAPFRAMEX_SWAPCHAIN_H
#define CAPFRAMEX_SWAPCHAIN_H

#include "layer.h"

// Maximum number of tracked swapchains per device
#define MAX_SWAPCHAINS 8

// Swapchain data
typedef struct {
    VkSwapchainKHR swapchain;
    VkDevice device;
    uint32_t width;
    uint32_t height;
    VkFormat format;
    uint32_t image_count;
    uint64_t frame_count;
    bool active;
} SwapchainData;

// Initialize swapchain tracking for a device
void swapchain_init_device(DeviceData* device_data);

// Cleanup swapchain tracking for a device
void swapchain_cleanup_device(DeviceData* device_data);

// Get swapchain data
SwapchainData* swapchain_get_data(VkSwapchainKHR swapchain);

// Hooked Vulkan functions
VKAPI_ATTR VkResult VKAPI_CALL layer_CreateSwapchainKHR(
    VkDevice device,
    const VkSwapchainCreateInfoKHR* pCreateInfo,
    const VkAllocationCallbacks* pAllocator,
    VkSwapchainKHR* pSwapchain);

VKAPI_ATTR void VKAPI_CALL layer_DestroySwapchainKHR(
    VkDevice device,
    VkSwapchainKHR swapchain,
    const VkAllocationCallbacks* pAllocator);

VKAPI_ATTR VkResult VKAPI_CALL layer_QueuePresentKHR(
    VkQueue queue,
    const VkPresentInfoKHR* pPresentInfo);

#endif // CAPFRAMEX_SWAPCHAIN_H
