#ifndef CAPFRAMEX_SWAPCHAIN_H
#define CAPFRAMEX_SWAPCHAIN_H

#include "layer.h"

// Maximum number of tracked swapchains per device
#define MAX_SWAPCHAINS 8

// Ring buffer size for tracking present timestamps (for matching timing data)
#define PRESENT_HISTORY_SIZE 16

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

    // Ring buffer for tracking present timestamps by presentID
    uint64_t present_timestamps[PRESENT_HISTORY_SIZE];
    uint32_t first_present_id;     // First presentID from driver (for normalization)
    bool present_id_initialized;   // Whether first_present_id has been set
} SwapchainData;

// Initialize swapchain tracking for a device
void swapchain_init_device(DeviceData* device_data);

// Cleanup swapchain tracking for a device
void swapchain_cleanup_device(DeviceData* device_data);

// Get swapchain data
SwapchainData* swapchain_get_data(VkSwapchainKHR swapchain);

// Get active swapchain info for IPC reporting
// Returns true if an active swapchain exists, fills in the parameters
bool swapchain_get_active_info(uint32_t* width, uint32_t* height,
                                uint32_t* format, uint32_t* image_count);

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
