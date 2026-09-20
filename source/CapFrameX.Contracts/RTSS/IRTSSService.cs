using CapFrameX.Contracts.Overlay;
using CapFrameX.Monitoring.Contracts;
using System;
using System.Threading.Tasks;

namespace CapFrameX.Contracts.RTSS
{
    public interface IRTSSService : IProcessService
    {
        /// <summary>
        /// Answers whether a process is presenting through Vulkan. Set by the composition root —
        /// only it can see both the RTSS integration and the OSD's Vulkan probes. Used to decide
        /// whether RTSS may still be launched into a running game: RTSS' hook loader injects into
        /// live processes, which is its normal mode for DXGI titles, but a running Vulkan title
        /// can no longer pick up RTSS' implicit layer (the loader binds those at vkCreateInstance)
        /// so the injection could only destabilize it. A null probe means "unknown" and is treated
        /// as not-Vulkan, keeping the previous behaviour.
        /// </summary>
        Func<int, bool> VulkanPresentationProbe { get; set; }

        bool IsRTSSInstalled();
        string GetApiInfo(int processId);
        string GetResolution(int processId);
        Tuple<double, double> GetCurrentFramerate(int processId);
        float[] GetFrameTimesInterval(int processId, int milliseconds);
        Task CheckRTSSRunningAndRefresh();
        Task CheckRTSSRunning();
        void Refresh();
        void ClearOSD();
        void ReleaseOSD();
        void SetOverlayEntries(IOverlayEntry[] entries);
        void SetFormatVariables(string variables);
        void SetOverlayEntry(IOverlayEntry entry);
        void SetIsCaptureTimerActive(bool active);
        void SetRunHistoryOutlierFlags(bool[] flags);
        void SetRunHistory(string[] history);
        void SetRunHistoryAggregation(string aggregation);
        void OnOSDOn();
        void OnOSDOff();
        void OnOSDToggle();
        void SetShowRunHistory(bool showRunHistory);
        void SetOSDCustomPosition(bool active);
        void SetOverlayPosition(int x, int y);
    }
}
