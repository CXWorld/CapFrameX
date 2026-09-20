namespace CapFrameX.OSD.Integration
{
    /// <summary>
    /// Public answer to "is this process presenting through Vulkan right now?", for consumers
    /// outside this assembly that must not take a dependency on the internal probes.
    ///
    /// The signal is the renderer-state mapping the CapFrameX Vulkan layer publishes, so it is
    /// only conclusive in the positive: a true answer means Vulkan presented within the layer's
    /// priority window. A false answer means "no evidence of Vulkan" — it also covers a Vulkan
    /// title the layer was never loaded into, which PresentMon reports as DXGI as well.
    /// </summary>
    public static class VulkanPresentation
    {
        public static bool IsActive(int processId)
        {
            return VulkanActivityProbe.TryHasRecentPresent(
                processId, out bool recent, out _, out _, out _) && recent;
        }
    }
}
