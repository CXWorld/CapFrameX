using CapFrameX.Overlay;
using LibreHardwareMonitor.PawnIo;

namespace CapFrameX.ViewModel.SubModels
{
    /// <summary>
    /// Presentation model for one software component the measurements depend on - the sensor
    /// driver, the capture backend, the overlays: its version, a short state, a status color
    /// and a tooltip explaining what the state means for CapFrameX.
    /// </summary>
    public class SoftwareComponentStatus
    {
        internal const string NoValue = "–";

        private const string StatusGreen = "#4CAF50";
        private const string StatusOrange = "#FF9800";
        private const string StatusRed = "#F44336";
        private const string StatusGray = "#757575";

        public string Version { get; }

        public string State { get; }

        public string StatusColor { get; }

        public string ToolTip { get; }

        public SoftwareComponentStatus(string version, string state, string statusColor, string toolTip)
        {
            Version = string.IsNullOrWhiteSpace(version) ? NoValue : version;
            State = state;
            StatusColor = statusColor;
            ToolTip = toolTip;
        }

        public static SoftwareComponentStatus Detecting { get; } =
            new SoftwareComponentStatus(null, "Detecting...", StatusGray, null);

        public static SoftwareComponentStatus FromPawnIo(PawnIoDriverStatus status)
        {
            switch (status?.State)
            {
                case PawnIoDriverState.Running:
                    return new SoftwareComponentStatus(status.Version, "Running", StatusGreen,
                        "Kernel driver for the CPU, memory and mainboard sensors.");
                case PawnIoDriverState.Stopped:
                    return new SoftwareComponentStatus(status.Version, "Stopped", StatusOrange,
                        "The PawnIO driver is installed but not loaded. CPU, memory and mainboard sensors are unavailable.");
                case PawnIoDriverState.Blocked:
                    return new SoftwareComponentStatus(status.Version, "Blocked by Windows", StatusRed,
                        "Windows refuses to load the registered PawnIO driver (code integrity, error 577). " +
                        "CPU, memory and mainboard sensors are unavailable. The CapFrameX log names the driver package to remove.");
                case PawnIoDriverState.NotInstalled:
                    return new SoftwareComponentStatus(null, "Not installed", StatusGray,
                        "PawnIO is not installed. CPU, memory and mainboard sensors are unavailable.");
                default:
                    return new SoftwareComponentStatus(status?.Version, "Unknown", StatusGray,
                        "The PawnIO service could not be queried.");
            }
        }

        public static SoftwareComponentStatus ForPresentMon(string version, bool isPresent)
        {
            return isPresent
                ? new SoftwareComponentStatus(version, "Bundled", StatusGreen,
                    "Capture backend that records the frame times.")
                : new SoftwareComponentStatus(version, "Missing", StatusRed,
                    "The bundled PresentMon executable is missing, so captures cannot start. Reinstall CapFrameX.");
        }

        public static SoftwareComponentStatus ForRtss(string version)
        {
            return version != null
                ? new SoftwareComponentStatus(version, "Installed", StatusGreen,
                    "RivaTuner Statistics Server, used by the RTSS overlay.")
                : new SoftwareComponentStatus(null, "Not installed", StatusGray,
                    "RTSS is not installed, so the RTSS overlay is unavailable. The in-game and hook-free overlays do not need it.");
        }

        public static SoftwareComponentStatus FromVulkanLayer(VulkanLayerRegistrationStatus status)
        {
            string version = status?.Version != null ? "v" + status.Version : null;
            string detail = string.IsNullOrWhiteSpace(status?.Detail) ? string.Empty : "\n\n" + status.Detail;

            switch (status?.State)
            {
                case VulkanLayerRegistrationState.Registered:
                    return new SoftwareComponentStatus(version, "Registered", StatusGreen,
                        "Implicit Vulkan layer that draws the in-game overlay in Vulkan games." + detail);
                case VulkanLayerRegistrationState.Incomplete:
                    return new SoftwareComponentStatus(version, "Incomplete", StatusOrange,
                        "The layer is registered for one bitness only. Vulkan games of the other bitness get the hook-free " +
                        "overlay instead of the in-game one. Reinstalling CapFrameX registers both." + detail);
                case VulkanLayerRegistrationState.Conflicting:
                    return new SoftwareComponentStatus(version, "Conflicting registration", StatusRed,
                        "A registration the Vulkan loader cannot use shadows the working one and disables the layer for that " +
                        "bitness; the affected games fall back to the hook-free overlay. Reinstalling CapFrameX removes stray " +
                        "registrations." + detail);
                case VulkanLayerRegistrationState.NotRegistered:
                    return new SoftwareComponentStatus(null, "Not registered", StatusGray,
                        "The Vulkan layer is not registered, so Vulkan games get the hook-free overlay instead of the in-game one. " +
                        "Reinstalling CapFrameX registers it." + detail);
                default:
                    return new SoftwareComponentStatus(version, "Unknown", StatusGray,
                        "The Vulkan layer registration could not be read." + detail);
            }
        }
    }
}
