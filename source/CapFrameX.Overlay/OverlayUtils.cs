using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using System.Collections.Generic;

namespace CapFrameX.Overlay
{
    public static class OverlayUtils
    {
        public static List<OverlayEntryWrapper> GetOverlayEntryDefaults(IAppConfiguration appConfiguration)
        {
            return new List<OverlayEntryWrapper>
                {
					// CX 
					// CaptureServiceStatus
					new OverlayEntryWrapper("CaptureServiceStatus")
                    {
                        OverlayEntryType = EOverlayEntryType.CX,
                        ShowOnOverlay = true,
                        ShowOnOverlayIsEnabled = true,
                        Description = "Capture service status",
                        GroupName = "Status:",
                        Value = "Capture service ready...",
                        ValueFormat = default,
                        ShowGraph = false,
                        ShowGraphIsEnabled = false,
                        Color = string.Empty,
                        IsEntryEnabled = true
                    },

					// CaptureTimer
					new OverlayEntryWrapper("CaptureTimer")
                    {
                        OverlayEntryType = EOverlayEntryType.CX,
                        ShowOnOverlay = true,
                        ShowOnOverlayIsEnabled = true,
                        Description = "Capture timer",
                        GroupName = "Status:",
                        Value = "0",
                        ValueFormat = default,
                        ShowGraph = false,
                        ShowGraphIsEnabled = false,
                        Color = string.Empty,
                        IsEntryEnabled = true
                    },

					// System time
                    new OverlayEntryWrapper("SystemTime")
                    {
                        OverlayEntryType = EOverlayEntryType.CX,
                        ShowOnOverlay = false,
                        ShowOnOverlayIsEnabled = true,
                        Description = "System time",
                        GroupName = "Time",
                        Value = "Time",
                        ValueFormat = default,
                        ShowGraph = false,
                        ShowGraphIsEnabled = false,
                        Color = string.Empty,
                        IsEntryEnabled = true
                    },

					// RunHistory
					new OverlayEntryWrapper("RunHistory")
                    {
                        OverlayEntryType = EOverlayEntryType.CX,
                        ShowOnOverlay = false,
                        ShowOnOverlayIsEnabled = true,
                        Description = "Run history",
                        GroupName = string.Empty,
                        Value = default,
                        ValueFormat = default,
                        ShowGraph = false,
                        ShowGraphIsEnabled = false,
                        Color = string.Empty,
                        IsEntryEnabled = true
                    },

                    // CX CPU usage
					new OverlayEntryWrapper("CxAppCpuUsage")
                    {
                        OverlayEntryType = EOverlayEntryType.CX,
                        ShowOnOverlay = false,
                        ShowOnOverlayIsEnabled = true,
                        Description = "CapFrameX CPU Usage (%)",
                        GroupName = "CX CPU",
                        Value = "0",
                        ValueFormat = default,
                        ShowGraph = false,
                        ShowGraphIsEnabled = false,
                        Color = string.Empty,
                        IsEntryEnabled = true
                    },

					// RTSS
					// Framerate
					new OverlayEntryWrapper("Framerate")
                    {
                        OverlayEntryType = EOverlayEntryType.CX,
                        ShowOnOverlay = true,
                        ShowOnOverlayIsEnabled = true,
                        Description = "Framerate",
                        GroupName = "<APP>",
                        Value = 0d,
                        ValueFormat = default,
                        ShowGraph = true,
                        ShowGraphIsEnabled = true,
                        Color = string.Empty,
                        IsEntryEnabled = true
                    },

					// Frametime
					new OverlayEntryWrapper("Frametime")
                    {
                        OverlayEntryType = EOverlayEntryType.CX,
                        ShowOnOverlay = true,
                        ShowOnOverlayIsEnabled = true,
                        Description = "Frametime",
                        GroupName = "<APP>",
                        Value = 0d,
                        ValueFormat = default,
                        ShowGraph = true,
                        ShowGraphIsEnabled = true,
                        Color = string.Empty,
                        IsEntryEnabled = true
                    },

                    // Custom CPU
					new OverlayEntryWrapper("CustomCPU")
                    {
                        OverlayEntryType = EOverlayEntryType.CX,
                        ShowOnOverlay = false,
                        ShowOnOverlayIsEnabled = true,
                        Description = "Custom CPU Name",
                        GroupName = "CPU Info",
                        Value = "CPU",
                        ValueFormat = default,
                        ShowGraph = false,
                        ShowGraphIsEnabled = false,
                        Color = string.Empty,
                        IsEntryEnabled = true
                    },

                    // Custom GPU
					new OverlayEntryWrapper("CustomGPU")
                    {
                        OverlayEntryType = EOverlayEntryType.CX,
                        ShowOnOverlay = false,
                        ShowOnOverlayIsEnabled = true,
                        Description = "Custom GPU Name",
                        GroupName = "GPU Info",
                        Value = "GPU",
                        ValueFormat = default,
                        ShowGraph = false,
                        ShowGraphIsEnabled = false,
                        Color = string.Empty,
                        IsEntryEnabled = true
                    },

                    // Custom Mainboard
					new OverlayEntryWrapper("Mainboard")
                    {
                        OverlayEntryType = EOverlayEntryType.CX,
                        ShowOnOverlay = false,
                        ShowOnOverlayIsEnabled = true,
                        Description = "Mainboard Name",
                        GroupName = "MB Info",
                        Value = "Mainboard",
                        ValueFormat = default,
                        ShowGraph = false,
                        ShowGraphIsEnabled = false,
                        Color = string.Empty,
                        IsEntryEnabled = true
                    },

                    // Custom RAM
					new OverlayEntryWrapper("CustomRAM")
                    {
                        OverlayEntryType = EOverlayEntryType.CX,
                        ShowOnOverlay = false,
                        ShowOnOverlayIsEnabled = true,
                        Description = "Custom RAM Description",
                        GroupName = "RAM Info",
                        Value = "RAM",
                        ValueFormat = default,
                        ShowGraph = false,
                        ShowGraphIsEnabled = false,
                        Color = string.Empty,
                        IsEntryEnabled = true
                    },

                    // OS
					new OverlayEntryWrapper("OS")
                    {
                        OverlayEntryType = EOverlayEntryType.CX,
                        ShowOnOverlay = false,
                        ShowOnOverlayIsEnabled = true,
                        Description = "OS Version",
                        GroupName = "OS",
                        Value = "OS",
                        ValueFormat = default,
                        ShowGraph = false,
                        ShowGraphIsEnabled = false,
                        Color = string.Empty,
                        IsEntryEnabled = true
                    },

                    new OverlayEntryWrapper("GPUDriver")
                    {
                        OverlayEntryType = EOverlayEntryType.CX,
                        ShowOnOverlay = false,
                        ShowOnOverlayIsEnabled = true,
                        Description = "GPU Software Version",
                        GroupName = "GPU Driver",
                        Value = "Not available",
                        ValueFormat = default,
                        ShowGraph = false,
                        ShowGraphIsEnabled = false,
                        Color = string.Empty,
                        IsEntryEnabled = true
                    },

                    // PC Latency
                    new OverlayEntryWrapper("OnlinePcLatency")
                    {
                        OverlayEntryType = EOverlayEntryType.OnlineMetric,
                        ShowOnOverlay = false,
                        ShowOnOverlayIsEnabled = true,
                        Description = "PC Latency (ms)",
                        GroupName = "PC Latency",
                        Value = "0",
                        ValueFormat = default,
                        ShowGraph = false,
                        ShowGraphIsEnabled = false,
                        Color = string.Empty,
                        IsEntryEnabled = appConfiguration.UsePcLatency,
                        SortKey = "1_1"
                    },

                    // Animation Error
                    new OverlayEntryWrapper("OnlineAnimationError")
                    {
                        OverlayEntryType = EOverlayEntryType.OnlineMetric,
                        ShowOnOverlay = false,
                        ShowOnOverlayIsEnabled = true,
                        Description = "Animation Error (ms)",
                        GroupName = "Animation Error",
                        Value = "0",
                        ValueFormat = default,
                        ShowGraph = false,
                        ShowGraphIsEnabled = false,
                        Color = string.Empty,
                        IsEntryEnabled = true,
                        SortKey = "1_2"
                    },

                    // Online metrics
                    // Average
                    new OverlayEntryWrapper("OnlineAverage")
                    {
                        OverlayEntryType = EOverlayEntryType.OnlineMetric,
                        ShowOnOverlay = true,
                        ShowOnOverlayIsEnabled = true,
                        Description = "Real-time average FPS",
                        GroupName = "Average",
                        Value = "0",
                        ValueFormat = default,
                        ShowGraph = false,
                        ShowGraphIsEnabled = false,
                        Color = string.Empty,
                        IsEntryEnabled = true,
                        SortKey = "1_3"
                    },

                    // P1
                    new OverlayEntryWrapper("OnlineP1")
                    {
                        OverlayEntryType = EOverlayEntryType.OnlineMetric,
                        ShowOnOverlay = false,
                        ShowOnOverlayIsEnabled = true,
                        Description = "Real-time P1 FPS",
                        GroupName = "P1%",
                        Value = "0",
                        ValueFormat = default,
                        ShowGraph = false,
                        ShowGraphIsEnabled = false,
                        Color = string.Empty,
                        IsEntryEnabled = true,
                        SortKey = "1_4"
                    },

                    // P0.1
                    new OverlayEntryWrapper("OnlineP0dot1")
                    {
                        OverlayEntryType = EOverlayEntryType.OnlineMetric,
                        ShowOnOverlay = false,
                        ShowOnOverlayIsEnabled = true,
                        Description = "Real-time P0.1 FPS",
                        GroupName = "P0.1%",
                        Value = "0",
                        ValueFormat = default,
                        ShowGraph = false,
                        ShowGraphIsEnabled = false,
                        Color = string.Empty,
                        IsEntryEnabled = true,
                        SortKey = "1_5"
                    },

                    // P0.2
                    new OverlayEntryWrapper("OnlineP0dot2")
                    {
                        OverlayEntryType = EOverlayEntryType.OnlineMetric,
                        ShowOnOverlay = false,
                        ShowOnOverlayIsEnabled = true,
                        Description = "Real-time P0.2 FPS",
                        GroupName = "P0.2%",
                        Value = "0",
                        ValueFormat = default,
                        ShowGraph = false,
                        ShowGraphIsEnabled = false,
                        Color = string.Empty,
                        IsEntryEnabled = true,
                        SortKey = "1_6"
                    },

                    // 1% Low
                    new OverlayEntryWrapper("Online1PercentLow")
                    {
                        OverlayEntryType = EOverlayEntryType.OnlineMetric,
                        ShowOnOverlay = true,
                        ShowOnOverlayIsEnabled = true,
                        Description = "Real-time 1% Low FPS",
                        GroupName = "1% Low",
                        Value = "0",
                        ValueFormat = default,
                        ShowGraph = false,
                        ShowGraphIsEnabled = false,
                        Color = string.Empty,
                        IsEntryEnabled = true,
                        SortKey = "1_7"
                    },

                     // 0.1% Low
                    new OverlayEntryWrapper("Online0dot1PercentLow")
                    {
                        OverlayEntryType = EOverlayEntryType.OnlineMetric,
                        ShowOnOverlay = false,
                        ShowOnOverlayIsEnabled = true,
                        Description = "Real-time 0.1% Low FPS",
                        GroupName = "0.1% Low",
                        Value = "0",
                        ValueFormat = default,
                        ShowGraph = false,
                        ShowGraphIsEnabled = false,
                        Color = string.Empty,
                        IsEntryEnabled = true,
                        SortKey = "1_8"
                    },

                    // 0.2% Low
                    new OverlayEntryWrapper("Online0dot2PercentLow")
                    {
                        OverlayEntryType = EOverlayEntryType.OnlineMetric,
                        ShowOnOverlay = false,
                        ShowOnOverlayIsEnabled = true,
                        Description = "Real-time 0.2% Low FPS",
                        GroupName = "0.2% Low",
                        Value = "0",
                        ValueFormat = default,
                        ShowGraph = false,
                        ShowGraphIsEnabled = false,
                        Color = string.Empty,
                        IsEntryEnabled = true,
                        SortKey = "1_9"
                    },

                    // GPU Active Time Average
                    new OverlayEntryWrapper("OnlineGpuActiveTimeAverage")
                    {
                        OverlayEntryType = EOverlayEntryType.OnlineMetric,
                        ShowOnOverlay = false,
                        ShowOnOverlayIsEnabled = true,
                        Description = "GPU Active Time Average (ms)",
                        GroupName = "GPUBusy Avg",
                        Value = "0",
                        ValueFormat = default,
                        ShowGraph = false,
                        ShowGraphIsEnabled = false,
                        Color = string.Empty,
                        IsEntryEnabled = true,
                        SortKey = "1_10"
                    },

                    // CPU Active Time Average
                    new OverlayEntryWrapper("OnlineCpuActiveTimeAverage")
                    {
                        OverlayEntryType = EOverlayEntryType.OnlineMetric,
                        ShowOnOverlay = false,
                        ShowOnOverlayIsEnabled = true,
                        Description = "CPU Active Time Average (ms)",
                        GroupName = "CPUBusy Avg",
                        Value = "0",
                        ValueFormat = default,
                        ShowGraph = false,
                        ShowGraphIsEnabled = false,
                        Color = string.Empty,
                        IsEntryEnabled = true,
                        SortKey = "1_11"
                    },

                    // Frame Time Average
                    new OverlayEntryWrapper("OnlineFrameTimeAverage")
                    {
                        OverlayEntryType = EOverlayEntryType.OnlineMetric,
                        ShowOnOverlay = false,
                        ShowOnOverlayIsEnabled = true,
                        Description = "Frame Time Average (ms)",
                        GroupName = "Frametime Avg",
                        Value = "0",
                        ValueFormat = default,
                        ShowGraph = false,
                        ShowGraphIsEnabled = false,
                        Color = string.Empty,
                        IsEntryEnabled = true,
                        SortKey = "1_12"
                    },

                    // GPU Active Time Deviation
                    new OverlayEntryWrapper("OnlineGpuActiveTimePercentageDeviation")
                    {
                        OverlayEntryType = EOverlayEntryType.OnlineMetric,
                        ShowOnOverlay = false,
                        ShowOnOverlayIsEnabled = true,
                        Description = "GPU Active Time Deviation (%)",
                        GroupName = "GPUBusy Deviation",
                        Value = "0",
                        ValueFormat = default,
                        ShowGraph = false,
                        ShowGraphIsEnabled = false,
                        Color = string.Empty,
                        IsEntryEnabled = true,
                        SortKey = "1_13"
                    },

                    // Stuttering percentage
                    new OverlayEntryWrapper("OnlineStutteringPercentage")
                    {
                        OverlayEntryType = EOverlayEntryType.OnlineMetric,
                        ShowOnOverlay = false,
                        ShowOnOverlayIsEnabled = true,
                        Description = "Stuttering Time (%)",
                        GroupName = "Stuttering",
                        Value = "0",
                        ValueFormat = default,
                        ShowGraph = false,
                        ShowGraphIsEnabled = false,
                        Color = string.Empty,
                        IsEntryEnabled = true,
                        SortKey = "1_14"
                    },

                    // PMD
                    new OverlayEntryWrapper("PmdGpuPowerCurrent")
                    {
                        OverlayEntryType = EOverlayEntryType.OnlineMetric,
                        ShowOnOverlay = false,
                        ShowOnOverlayIsEnabled = true,
                        Description = "PMD GPU Power (W)",
                        GroupName = "PMD GPU Power",
                        Value = "0",
                        ValueFormat = default,
                        ShowGraph = false,
                        ShowGraphIsEnabled = false,
                        Color = string.Empty,
                        IsEntryEnabled = true,
                        SortKey = "1_15"
                    },
                    new OverlayEntryWrapper("PmdCpuPowerCurrent")
                    {
                        OverlayEntryType = EOverlayEntryType.OnlineMetric,
                        ShowOnOverlay = false,
                        ShowOnOverlayIsEnabled = true,
                        Description = "PMD CPU Power (W)",
                        GroupName = "PMD CPU Power",
                        Value = "0",
                        ValueFormat = default,
                        ShowGraph = false,
                        ShowGraphIsEnabled = false,
                        Color = string.Empty,
                        IsEntryEnabled = true,
                        SortKey = "1_16"
                    },
                    new OverlayEntryWrapper("PmdSystemPowerCurrent")
                    {
                        OverlayEntryType = EOverlayEntryType.OnlineMetric,
                        ShowOnOverlay = false,
                        ShowOnOverlayIsEnabled = true,
                        Description = "PMD System Power (W)",
                        GroupName = "PMD Sys Power",
                        Value = "0",
                        ValueFormat = default,
                        ShowGraph = false,
                        ShowGraphIsEnabled = false,
                        Color = string.Empty,
                        IsEntryEnabled = true,
                        SortKey = "1_17"
                    },
                     new OverlayEntryWrapper("BatteryLifePercent")
                    {
                        OverlayEntryType = EOverlayEntryType.CX,
                        ShowOnOverlay = false,
                        ShowOnOverlayIsEnabled = true,
                        Description = "Battery Life (%)",
                        GroupName = "Battery Life",
                        Value = "0",
                        ValueFormat = default,
                        ShowGraph = false,
                        ShowGraphIsEnabled = false,
                        Color = string.Empty,
                        IsEntryEnabled = true
                    },
                    new OverlayEntryWrapper("BatteryLifeRemaining")
                    {
                        OverlayEntryType = EOverlayEntryType.CX,
                        ShowOnOverlay = false,
                        ShowOnOverlayIsEnabled = true,
                        Description = "Battery Life Remaining (min)",
                        GroupName = "Battery Life",
                        Value = "0",
                        ValueFormat = default,
                        ShowGraph = false,
                        ShowGraphIsEnabled = false,
                        Color = string.Empty,
                        IsEntryEnabled = true
                    },
                    new OverlayEntryWrapper("Ping")
                    {
                        OverlayEntryType = EOverlayEntryType.CX,
                        ShowOnOverlay = false,
                        ShowOnOverlayIsEnabled = true,
                        Description = "Network Ping",
                        GroupName = "Ping",
                        Value = "0",
                        ValueFormat = default,
                        ShowGraph = false,
                        ShowGraphIsEnabled = false,
                        Color = string.Empty,
                        IsEntryEnabled = true
                    },
					//new OverlayEntryWrapper("ThreadAffinityState")
					//{
					//	OverlayEntryType = EOverlayEntryType.CX,
					//	ShowOnOverlay = false,
					//	ShowOnOverlayIsEnabled = true,
					//	Description = "Thread Affinity State",
					//	GroupName = "Thread Affinity",
					//	Value = "Default",
					//	ValueFormat = default,
					//	ShowGraph = false,
					//	ShowGraphIsEnabled = false,
					//	Color = string.Empty
                    //  IsEntryEnabled = true
					//}
            };
        }
    }
}
