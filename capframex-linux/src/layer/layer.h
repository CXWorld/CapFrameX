#ifndef CAPFRAMEX_LAYER_H
#define CAPFRAMEX_LAYER_H

#include <vulkan/vulkan.h>
#include <vulkan/vk_layer.h>
#include <stdbool.h>
#include <stdint.h>

#define LAYER_NAME "VK_LAYER_capframex_capture"
#define LAYER_DESCRIPTION "CapFrameX Frametime Capture Layer"
#define LAYER_VERSION 1

// Export macro for layer entry points
#ifndef VK_LAYER_EXPORT
#define VK_LAYER_EXPORT __attribute__((visibility("default")))
#endif

// Instance dispatch table
typedef struct {
    PFN_vkGetInstanceProcAddr GetInstanceProcAddr;
    PFN_vkDestroyInstance DestroyInstance;
    PFN_vkEnumeratePhysicalDevices EnumeratePhysicalDevices;
    PFN_vkGetPhysicalDeviceProperties GetPhysicalDeviceProperties;
    PFN_vkEnumerateDeviceExtensionProperties EnumerateDeviceExtensionProperties;
    PFN_vkGetPhysicalDeviceQueueFamilyProperties GetPhysicalDeviceQueueFamilyProperties;
} InstanceDispatch;

// Device dispatch table
typedef struct {
    PFN_vkGetDeviceProcAddr GetDeviceProcAddr;
    PFN_vkDestroyDevice DestroyDevice;
    PFN_vkGetDeviceQueue GetDeviceQueue;
    PFN_vkCreateSwapchainKHR CreateSwapchainKHR;
    PFN_vkDestroySwapchainKHR DestroySwapchainKHR;
    PFN_vkQueuePresentKHR QueuePresentKHR;
    PFN_vkAcquireNextImageKHR AcquireNextImageKHR;
} DeviceDispatch;

// Per-instance data
typedef struct {
    VkInstance instance;
    InstanceDispatch dispatch;
    VkPhysicalDevice physical_device;
    char gpu_name[256];
} InstanceData;

// Per-device data
typedef struct {
    VkDevice device;
    DeviceDispatch dispatch;
    InstanceData* instance_data;
    VkQueue present_queue;
    uint32_t present_queue_family;
} DeviceData;

// Layer initialization/cleanup
void layer_init(void);
void layer_cleanup(void);

// Get dispatch tables
InstanceData* layer_get_instance_data(VkInstance instance);
DeviceData* layer_get_device_data(VkDevice device);

// Store dispatch tables
void layer_store_instance_data(VkInstance instance, InstanceData* data);
void layer_store_device_data(VkDevice device, DeviceData* data);

// Remove dispatch tables
void layer_remove_instance_data(VkInstance instance);
void layer_remove_device_data(VkDevice device);

#endif // CAPFRAMEX_LAYER_H
