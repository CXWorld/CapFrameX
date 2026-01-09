#include "layer.h"
#include "swapchain.h"
#include "timing.h"
#include "ipc_client.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <pthread.h>
#include <unistd.h>

// Forward declarations for layer entry points
VK_LAYER_EXPORT VKAPI_ATTR PFN_vkVoidFunction VKAPI_CALL layer_GetInstanceProcAddr(
    VkInstance instance, const char* pName);
VK_LAYER_EXPORT VKAPI_ATTR PFN_vkVoidFunction VKAPI_CALL layer_GetDeviceProcAddr(
    VkDevice device, const char* pName);

// Simple hash map for instance/device data
#define MAX_INSTANCES 16
#define MAX_DEVICES 16

static InstanceData* instance_map[MAX_INSTANCES];
static VkInstance instance_keys[MAX_INSTANCES];
static int instance_count = 0;

static DeviceData* device_map[MAX_DEVICES];
static VkDevice device_keys[MAX_DEVICES];
static int device_count = 0;

static pthread_mutex_t map_mutex = PTHREAD_MUTEX_INITIALIZER;
static bool layer_initialized = false;

void layer_init(void) {
    if (layer_initialized) return;

    timing_init();
    ipc_client_init();

    // Try to connect to daemon (will stream frames when connected)
    fprintf(stderr, "[CapFrameX Layer] Attempting daemon connection...\n");
    fflush(stderr);
    bool conn_result = ipc_client_connect();
    fprintf(stderr, "[CapFrameX Layer] Connection result: %d\n", conn_result);
    fflush(stderr);

    if (conn_result) {
        fprintf(stderr, "[CapFrameX Layer] Connected to daemon - streaming enabled\n");
    } else {
        fprintf(stderr, "[CapFrameX Layer] Daemon not available - running standalone\n");
    }
    fflush(stderr);

    layer_initialized = true;
    fprintf(stderr, "[CapFrameX Layer] Initialized for PID %d\n", getpid());
}

void layer_cleanup(void) {
    if (!layer_initialized) return;

    ipc_client_cleanup();
    timing_cleanup();

    pthread_mutex_lock(&map_mutex);
    for (int i = 0; i < instance_count; i++) {
        free(instance_map[i]);
    }
    for (int i = 0; i < device_count; i++) {
        free(device_map[i]);
    }
    instance_count = 0;
    device_count = 0;
    pthread_mutex_unlock(&map_mutex);

    layer_initialized = false;
}

InstanceData* layer_get_instance_data(VkInstance instance) {
    pthread_mutex_lock(&map_mutex);
    for (int i = 0; i < instance_count; i++) {
        if (instance_keys[i] == instance) {
            pthread_mutex_unlock(&map_mutex);
            return instance_map[i];
        }
    }
    pthread_mutex_unlock(&map_mutex);
    return NULL;
}

DeviceData* layer_get_device_data(VkDevice device) {
    pthread_mutex_lock(&map_mutex);
    for (int i = 0; i < device_count; i++) {
        if (device_keys[i] == device) {
            pthread_mutex_unlock(&map_mutex);
            return device_map[i];
        }
    }
    pthread_mutex_unlock(&map_mutex);
    return NULL;
}

void layer_store_instance_data(VkInstance instance, InstanceData* data) {
    pthread_mutex_lock(&map_mutex);
    if (instance_count < MAX_INSTANCES) {
        instance_keys[instance_count] = instance;
        instance_map[instance_count] = data;
        instance_count++;
    }
    pthread_mutex_unlock(&map_mutex);
}

void layer_store_device_data(VkDevice device, DeviceData* data) {
    pthread_mutex_lock(&map_mutex);
    if (device_count < MAX_DEVICES) {
        device_keys[device_count] = device;
        device_map[device_count] = data;
        device_count++;
    }
    pthread_mutex_unlock(&map_mutex);
}

void layer_remove_instance_data(VkInstance instance) {
    pthread_mutex_lock(&map_mutex);
    for (int i = 0; i < instance_count; i++) {
        if (instance_keys[i] == instance) {
            free(instance_map[i]);
            for (int j = i; j < instance_count - 1; j++) {
                instance_keys[j] = instance_keys[j + 1];
                instance_map[j] = instance_map[j + 1];
            }
            instance_count--;
            break;
        }
    }
    pthread_mutex_unlock(&map_mutex);
}

void layer_remove_device_data(VkDevice device) {
    pthread_mutex_lock(&map_mutex);
    for (int i = 0; i < device_count; i++) {
        if (device_keys[i] == device) {
            free(device_map[i]);
            for (int j = i; j < device_count - 1; j++) {
                device_keys[j] = device_keys[j + 1];
                device_map[j] = device_map[j + 1];
            }
            device_count--;
            break;
        }
    }
    pthread_mutex_unlock(&map_mutex);
}

// Hooked Vulkan functions

static VKAPI_ATTR VkResult VKAPI_CALL layer_CreateInstance(
    const VkInstanceCreateInfo* pCreateInfo,
    const VkAllocationCallbacks* pAllocator,
    VkInstance* pInstance)
{
    layer_init();

    // Get the chain info from pNext
    VkLayerInstanceCreateInfo* chain_info = (VkLayerInstanceCreateInfo*)pCreateInfo->pNext;
    while (chain_info &&
           !(chain_info->sType == VK_STRUCTURE_TYPE_LOADER_INSTANCE_CREATE_INFO &&
             chain_info->function == VK_LAYER_LINK_INFO)) {
        chain_info = (VkLayerInstanceCreateInfo*)chain_info->pNext;
    }

    if (!chain_info) {
        return VK_ERROR_INITIALIZATION_FAILED;
    }

    PFN_vkGetInstanceProcAddr fpGetInstanceProcAddr = chain_info->u.pLayerInfo->pfnNextGetInstanceProcAddr;
    PFN_vkCreateInstance fpCreateInstance = (PFN_vkCreateInstance)fpGetInstanceProcAddr(NULL, "vkCreateInstance");

    // Advance the link info for the next layer
    chain_info->u.pLayerInfo = chain_info->u.pLayerInfo->pNext;

    VkResult result = fpCreateInstance(pCreateInfo, pAllocator, pInstance);
    if (result != VK_SUCCESS) {
        return result;
    }

    // Create and store instance data
    InstanceData* data = calloc(1, sizeof(InstanceData));
    if (!data) {
        return VK_ERROR_OUT_OF_HOST_MEMORY;
    }

    data->instance = *pInstance;
    data->dispatch.GetInstanceProcAddr = fpGetInstanceProcAddr;

#define LOAD_INSTANCE_PROC(name) \
    data->dispatch.name = (PFN_vk##name)fpGetInstanceProcAddr(*pInstance, "vk" #name)

    LOAD_INSTANCE_PROC(DestroyInstance);
    LOAD_INSTANCE_PROC(EnumeratePhysicalDevices);
    LOAD_INSTANCE_PROC(GetPhysicalDeviceProperties);
    LOAD_INSTANCE_PROC(EnumerateDeviceExtensionProperties);
    LOAD_INSTANCE_PROC(GetPhysicalDeviceQueueFamilyProperties);

#undef LOAD_INSTANCE_PROC

    layer_store_instance_data(*pInstance, data);

    return VK_SUCCESS;
}

static VKAPI_ATTR void VKAPI_CALL layer_DestroyInstance(
    VkInstance instance,
    const VkAllocationCallbacks* pAllocator)
{
    InstanceData* data = layer_get_instance_data(instance);
    if (data) {
        data->dispatch.DestroyInstance(instance, pAllocator);
        layer_remove_instance_data(instance);
    }
}

static VKAPI_ATTR VkResult VKAPI_CALL layer_CreateDevice(
    VkPhysicalDevice physicalDevice,
    const VkDeviceCreateInfo* pCreateInfo,
    const VkAllocationCallbacks* pAllocator,
    VkDevice* pDevice)
{
    // Get instance data from physical device (need to find the instance)
    // For simplicity, we iterate through instances
    InstanceData* inst_data = NULL;
    pthread_mutex_lock(&map_mutex);
    if (instance_count > 0) {
        inst_data = instance_map[0];  // Simplified: use first instance
    }
    pthread_mutex_unlock(&map_mutex);

    if (!inst_data) {
        return VK_ERROR_INITIALIZATION_FAILED;
    }

    // Get the chain info
    VkLayerDeviceCreateInfo* chain_info = (VkLayerDeviceCreateInfo*)pCreateInfo->pNext;
    while (chain_info &&
           !(chain_info->sType == VK_STRUCTURE_TYPE_LOADER_DEVICE_CREATE_INFO &&
             chain_info->function == VK_LAYER_LINK_INFO)) {
        chain_info = (VkLayerDeviceCreateInfo*)chain_info->pNext;
    }

    if (!chain_info) {
        return VK_ERROR_INITIALIZATION_FAILED;
    }

    PFN_vkGetInstanceProcAddr fpGetInstanceProcAddr = chain_info->u.pLayerInfo->pfnNextGetInstanceProcAddr;
    PFN_vkGetDeviceProcAddr fpGetDeviceProcAddr = chain_info->u.pLayerInfo->pfnNextGetDeviceProcAddr;
    PFN_vkCreateDevice fpCreateDevice = (PFN_vkCreateDevice)fpGetInstanceProcAddr(inst_data->instance, "vkCreateDevice");

    chain_info->u.pLayerInfo = chain_info->u.pLayerInfo->pNext;

    VkResult result = fpCreateDevice(physicalDevice, pCreateInfo, pAllocator, pDevice);
    if (result != VK_SUCCESS) {
        return result;
    }

    // Create device data
    DeviceData* data = calloc(1, sizeof(DeviceData));
    if (!data) {
        return VK_ERROR_OUT_OF_HOST_MEMORY;
    }

    data->device = *pDevice;
    data->instance_data = inst_data;
    data->dispatch.GetDeviceProcAddr = fpGetDeviceProcAddr;

#define LOAD_DEVICE_PROC(name) \
    data->dispatch.name = (PFN_vk##name)fpGetDeviceProcAddr(*pDevice, "vk" #name)

    LOAD_DEVICE_PROC(DestroyDevice);
    LOAD_DEVICE_PROC(GetDeviceQueue);
    LOAD_DEVICE_PROC(CreateSwapchainKHR);
    LOAD_DEVICE_PROC(DestroySwapchainKHR);
    LOAD_DEVICE_PROC(QueuePresentKHR);
    LOAD_DEVICE_PROC(AcquireNextImageKHR);

#undef LOAD_DEVICE_PROC

    // Store GPU name and notify daemon
    VkPhysicalDeviceProperties props;
    inst_data->dispatch.GetPhysicalDeviceProperties(physicalDevice, &props);
    strncpy(inst_data->gpu_name, props.deviceName, sizeof(inst_data->gpu_name) - 1);
    inst_data->physical_device = physicalDevice;

    // Update IPC with GPU info and send updated hello
    ipc_client_set_gpu_name(inst_data->gpu_name);
    ipc_client_send_hello(inst_data->gpu_name);

    layer_store_device_data(*pDevice, data);

    // Initialize swapchain tracking for this device
    swapchain_init_device(data);

    fprintf(stderr, "[CapFrameX Layer] Device created: %s\n", inst_data->gpu_name);

    return VK_SUCCESS;
}

static VKAPI_ATTR void VKAPI_CALL layer_DestroyDevice(
    VkDevice device,
    const VkAllocationCallbacks* pAllocator)
{
    DeviceData* data = layer_get_device_data(device);
    if (data) {
        swapchain_cleanup_device(data);
        data->dispatch.DestroyDevice(device, pAllocator);
        layer_remove_device_data(device);
    }
}

// Layer entry points

VK_LAYER_EXPORT VKAPI_ATTR VkResult VKAPI_CALL vkNegotiateLoaderLayerInterfaceVersion(
    VkNegotiateLayerInterface* pVersionStruct)
{
    if (pVersionStruct->sType != LAYER_NEGOTIATE_INTERFACE_STRUCT) {
        return VK_ERROR_INITIALIZATION_FAILED;
    }

    if (pVersionStruct->loaderLayerInterfaceVersion >= 2) {
        pVersionStruct->pfnGetInstanceProcAddr = layer_GetInstanceProcAddr;
        pVersionStruct->pfnGetDeviceProcAddr = layer_GetDeviceProcAddr;
        pVersionStruct->pfnGetPhysicalDeviceProcAddr = NULL;
    }

    if (pVersionStruct->loaderLayerInterfaceVersion > 2) {
        pVersionStruct->loaderLayerInterfaceVersion = 2;
    }

    return VK_SUCCESS;
}

VK_LAYER_EXPORT VKAPI_ATTR PFN_vkVoidFunction VKAPI_CALL layer_GetInstanceProcAddr(
    VkInstance instance,
    const char* pName)
{
    // Core functions
    if (strcmp(pName, "vkGetInstanceProcAddr") == 0)
        return (PFN_vkVoidFunction)layer_GetInstanceProcAddr;
    if (strcmp(pName, "vkCreateInstance") == 0)
        return (PFN_vkVoidFunction)layer_CreateInstance;
    if (strcmp(pName, "vkDestroyInstance") == 0)
        return (PFN_vkVoidFunction)layer_DestroyInstance;
    if (strcmp(pName, "vkCreateDevice") == 0)
        return (PFN_vkVoidFunction)layer_CreateDevice;

    // Device functions
    if (strcmp(pName, "vkGetDeviceProcAddr") == 0)
        return (PFN_vkVoidFunction)layer_GetDeviceProcAddr;
    if (strcmp(pName, "vkDestroyDevice") == 0)
        return (PFN_vkVoidFunction)layer_DestroyDevice;
    if (strcmp(pName, "vkCreateSwapchainKHR") == 0)
        return (PFN_vkVoidFunction)layer_CreateSwapchainKHR;
    if (strcmp(pName, "vkDestroySwapchainKHR") == 0)
        return (PFN_vkVoidFunction)layer_DestroySwapchainKHR;
    if (strcmp(pName, "vkQueuePresentKHR") == 0)
        return (PFN_vkVoidFunction)layer_QueuePresentKHR;

    // Pass through to next layer
    InstanceData* data = layer_get_instance_data(instance);
    if (data) {
        return data->dispatch.GetInstanceProcAddr(instance, pName);
    }

    return NULL;
}

VK_LAYER_EXPORT VKAPI_ATTR PFN_vkVoidFunction VKAPI_CALL layer_GetDeviceProcAddr(
    VkDevice device,
    const char* pName)
{
    if (strcmp(pName, "vkGetDeviceProcAddr") == 0)
        return (PFN_vkVoidFunction)layer_GetDeviceProcAddr;
    if (strcmp(pName, "vkDestroyDevice") == 0)
        return (PFN_vkVoidFunction)layer_DestroyDevice;
    if (strcmp(pName, "vkCreateSwapchainKHR") == 0)
        return (PFN_vkVoidFunction)layer_CreateSwapchainKHR;
    if (strcmp(pName, "vkDestroySwapchainKHR") == 0)
        return (PFN_vkVoidFunction)layer_DestroySwapchainKHR;
    if (strcmp(pName, "vkQueuePresentKHR") == 0)
        return (PFN_vkVoidFunction)layer_QueuePresentKHR;

    DeviceData* data = layer_get_device_data(device);
    if (data) {
        return data->dispatch.GetDeviceProcAddr(device, pName);
    }

    return NULL;
}
