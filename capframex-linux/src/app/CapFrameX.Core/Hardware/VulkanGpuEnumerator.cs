using Silk.NET.Vulkan;

namespace CapFrameX.Core.Hardware;

/// <summary>
/// Enumerates GPUs using the Vulkan API for accurate device names and info
/// </summary>
public static class VulkanGpuEnumerator
{
    private const uint VK_VENDOR_AMD = 0x1002;
    private const uint VK_VENDOR_NVIDIA = 0x10DE;
    private const uint VK_VENDOR_INTEL = 0x8086;

    /// <summary>
    /// Enumerate all GPUs using Vulkan API
    /// </summary>
    public static List<GpuInfo> EnumerateGpus()
    {
        var gpus = new List<GpuInfo>();

        try
        {
            using var vk = Vk.GetApi();

            // Create a minimal Vulkan instance
            unsafe
            {
                var appInfo = new ApplicationInfo
                {
                    SType = StructureType.ApplicationInfo,
                    ApiVersion = Vk.Version12
                };

                var createInfo = new InstanceCreateInfo
                {
                    SType = StructureType.InstanceCreateInfo,
                    PApplicationInfo = &appInfo
                };

                Instance instance;
                var result = vk.CreateInstance(&createInfo, null, &instance);
                if (result != Result.Success)
                {
                    Console.WriteLine($"Failed to create Vulkan instance: {result}");
                    return gpus;
                }

                try
                {
                    // Enumerate physical devices
                    uint deviceCount = 0;
                    vk.EnumeratePhysicalDevices(instance, &deviceCount, null);

                    if (deviceCount == 0)
                    {
                        Console.WriteLine("No Vulkan-capable GPUs found");
                        return gpus;
                    }

                    var devices = new PhysicalDevice[deviceCount];
                    fixed (PhysicalDevice* pDevices = devices)
                    {
                        vk.EnumeratePhysicalDevices(instance, &deviceCount, pDevices);
                    }

                    foreach (var device in devices)
                    {
                        var properties = new PhysicalDeviceProperties();
                        vk.GetPhysicalDeviceProperties(device, &properties);

                        var memoryProperties = new PhysicalDeviceMemoryProperties();
                        vk.GetPhysicalDeviceMemoryProperties(device, &memoryProperties);

                        // Calculate total device-local memory
                        ulong totalMemory = 0;
                        for (int i = 0; i < memoryProperties.MemoryHeapCount; i++)
                        {
                            var heap = memoryProperties.MemoryHeaps[i];
                            if ((heap.Flags & MemoryHeapFlags.DeviceLocalBit) != 0)
                            {
                                totalMemory += heap.Size;
                            }
                        }

                        var gpu = new GpuInfo
                        {
                            Name = GetDeviceName(properties),
                            Vendor = GetVendor(properties.VendorID),
                            Type = GetGpuType(properties.DeviceType),
                            VendorId = properties.VendorID,
                            DeviceId = properties.DeviceID,
                            DriverVersion = FormatDriverVersion(properties.VendorID, properties.DriverVersion),
                            ApiVersion = FormatVulkanVersion(properties.ApiVersion),
                            MemorySize = totalMemory
                        };

                        gpus.Add(gpu);
                    }
                }
                finally
                {
                    vk.DestroyInstance(instance, null);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to enumerate GPUs via Vulkan: {ex.Message}");
        }

        return gpus;
    }

    private static unsafe string GetDeviceName(PhysicalDeviceProperties properties)
    {
        // DeviceName is a fixed-size byte array
        return new string((sbyte*)properties.DeviceName).TrimEnd('\0');
    }

    private static GpuVendor GetVendor(uint vendorId)
    {
        return vendorId switch
        {
            VK_VENDOR_AMD => GpuVendor.Amd,
            VK_VENDOR_NVIDIA => GpuVendor.Nvidia,
            VK_VENDOR_INTEL => GpuVendor.Intel,
            _ => GpuVendor.Unknown
        };
    }

    private static GpuType GetGpuType(PhysicalDeviceType deviceType)
    {
        return deviceType switch
        {
            PhysicalDeviceType.DiscreteGpu => GpuType.Discrete,
            PhysicalDeviceType.IntegratedGpu => GpuType.Integrated,
            PhysicalDeviceType.VirtualGpu => GpuType.Virtual,
            PhysicalDeviceType.Cpu => GpuType.Cpu,
            _ => GpuType.Unknown
        };
    }

    private static string FormatDriverVersion(uint vendorId, uint driverVersion)
    {
        // NVIDIA uses a different version encoding
        if (vendorId == VK_VENDOR_NVIDIA)
        {
            var major = (driverVersion >> 22) & 0x3FF;
            var minor = (driverVersion >> 14) & 0xFF;
            var patch = (driverVersion >> 6) & 0xFF;
            return $"{major}.{minor}.{patch}";
        }

        // Standard Vulkan version encoding
        var vkMajor = driverVersion >> 22;
        var vkMinor = (driverVersion >> 12) & 0x3FF;
        var vkPatch = driverVersion & 0xFFF;
        return $"{vkMajor}.{vkMinor}.{vkPatch}";
    }

    private static string FormatVulkanVersion(uint version)
    {
        var major = version >> 22;
        var minor = (version >> 12) & 0x3FF;
        var patch = version & 0xFFF;
        return $"{major}.{minor}.{patch}";
    }
}
