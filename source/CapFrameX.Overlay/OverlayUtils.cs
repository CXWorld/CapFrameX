using CapFrameX.Contracts.Overlay;
using System.Collections.Generic;

namespace CapFrameX.Overlay
{
    public static class OverlayUtils
    {
        public static List<OverlayEntryWrapper> GetOverlayEntryDefaults()
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
                        Color = string.Empty
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
                        Color = string.Empty
                    },

					// RunHistory
					new OverlayEntryWrapper("RunHistory")
                    {
                        OverlayEntryType = EOverlayEntryType.CX,
                        ShowOnOverlay = false,
                        ShowOnOverlayIsEnabled = false,
                        Description = "Run history",
                        GroupName = string.Empty,
                        Value = default,
                        ValueFormat = default,
                        ShowGraph = false,
                        ShowGraphIsEnabled = false,
                        Color = string.Empty
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
                        ShowGraph = false,
                        ShowGraphIsEnabled = true,
                        Color = string.Empty
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
                        ShowGraph = false,
                        ShowGraphIsEnabled = true,
                        Color = string.Empty
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
                        Color = string.Empty
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
                        Color = string.Empty
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
                        Color = string.Empty
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
                        Color = string.Empty
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
                        Color = string.Empty
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
                        Color = string.Empty
                    },

                    // Online metrics
                    // Average
                    new OverlayEntryWrapper("OnlineAverage")
                    {
                        OverlayEntryType = EOverlayEntryType.OnlineMetric,
                        ShowOnOverlay = false,
                        ShowOnOverlayIsEnabled = true,
                        Description = "Real-time average FPS",
                        GroupName = "Average",
                        Value = "0",
                        ValueFormat = default,
                        ShowGraph = false,
                        ShowGraphIsEnabled = false,
                        Color = string.Empty
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
                        Color = string.Empty
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
                        Color = string.Empty
                    },

                    // Render lag
                    new OverlayEntryWrapper("OnlineApplicationLatency")
                    {
                        OverlayEntryType = EOverlayEntryType.OnlineMetric,
                        ShowOnOverlay = false,
                        ShowOnOverlayIsEnabled = true,
                        Description = "Application Latency (ms)",
                        GroupName = "App Latency",
                        Value = "0",
                        ValueFormat = default,
                        ShowGraph = false,
                        ShowGraphIsEnabled = false,
                        Color = string.Empty
                    }
            };
        }
    }
}
