using CapFrameX.Contracts.Localization;
using CapFrameX.Overlay;
using LibreHardwareMonitor.PawnIo;

namespace CapFrameX.ViewModel.SubModels
{
    /// <summary>
    /// Presentation model for one software component the measurements depend on - the sensor
    /// driver, the capture backend, the overlays: its version, a short state, a status color
    /// and a tooltip explaining what the state means for CapFrameX. State and tooltip are catalog
    /// texts resolved on every read, so a view that reads them again after a language switch
    /// shows the new language.
    /// </summary>
    public class SoftwareComponentStatus
    {
        internal const string NoValue = "–";

        private const string StatusGreen = "#4CAF50";
        private const string StatusOrange = "#FF9800";
        private const string StatusRed = "#F44336";
        private const string StatusGray = "#757575";

        private readonly LocalizedText _state;
        private readonly LocalizedText _toolTip;
        private readonly string _detail;

        public string Version { get; }

        public string State => _state.Resolve();

        public string StatusColor { get; }

        public string ToolTip => _toolTip == null ? null : _toolTip.Resolve() + _detail;

        /// <param name="detail">Data appended to the tooltip below the explanation, e.g. registry paths.</param>
        public SoftwareComponentStatus(string version, LocalizedText state, string statusColor, LocalizedText toolTip,
            string detail = null)
        {
            Version = string.IsNullOrWhiteSpace(version) ? NoValue : version;
            _state = state;
            StatusColor = statusColor;
            _toolTip = toolTip;
            _detail = string.IsNullOrWhiteSpace(detail) ? string.Empty : "\n\n" + detail;
        }

        public static SoftwareComponentStatus Detecting { get; } =
            new SoftwareComponentStatus(null, new LocalizedText("InfoViewModel_Detecting"), StatusGray, null);

        public static SoftwareComponentStatus FromPawnIo(PawnIoDriverStatus status)
        {
            switch (status?.State)
            {
                case PawnIoDriverState.Running:
                    return new SoftwareComponentStatus(status.Version, new LocalizedText("SoftwareComponentStatus_Running"),
                        StatusGreen, new LocalizedText("SoftwareComponentStatus_PawnIoRunningToolTip"));
                case PawnIoDriverState.Stopped:
                    return new SoftwareComponentStatus(status.Version, new LocalizedText("SoftwareComponentStatus_Stopped"),
                        StatusOrange, new LocalizedText("SoftwareComponentStatus_PawnIoStoppedToolTip"));
                case PawnIoDriverState.Blocked:
                    return new SoftwareComponentStatus(status.Version, new LocalizedText("SoftwareComponentStatus_BlockedByWindows"),
                        StatusRed, new LocalizedText("SoftwareComponentStatus_PawnIoBlockedToolTip"));
                case PawnIoDriverState.NotInstalled:
                    return new SoftwareComponentStatus(null, new LocalizedText("SoftwareComponentStatus_NotInstalled"),
                        StatusGray, new LocalizedText("SoftwareComponentStatus_PawnIoNotInstalledToolTip"));
                default:
                    return new SoftwareComponentStatus(status?.Version, new LocalizedText("SoftwareComponentStatus_Unknown"),
                        StatusGray, new LocalizedText("SoftwareComponentStatus_PawnIoUnknownToolTip"));
            }
        }

        public static SoftwareComponentStatus ForPresentMon(string version, bool isPresent)
        {
            return isPresent
                ? new SoftwareComponentStatus(version, new LocalizedText("SoftwareComponentStatus_Bundled"),
                    StatusGreen, new LocalizedText("SoftwareComponentStatus_PresentMonBundledToolTip"))
                : new SoftwareComponentStatus(version, new LocalizedText("SoftwareComponentStatus_Missing"),
                    StatusRed, new LocalizedText("SoftwareComponentStatus_PresentMonMissingToolTip"));
        }

        public static SoftwareComponentStatus ForRtss(string version)
        {
            return version != null
                ? new SoftwareComponentStatus(version, new LocalizedText("SoftwareComponentStatus_Installed"),
                    StatusGreen, new LocalizedText("SoftwareComponentStatus_RtssInstalledToolTip"))
                : new SoftwareComponentStatus(null, new LocalizedText("SoftwareComponentStatus_NotInstalled"),
                    StatusGray, new LocalizedText("SoftwareComponentStatus_RtssNotInstalledToolTip"));
        }

        public static SoftwareComponentStatus FromVulkanLayer(VulkanLayerRegistrationStatus status)
        {
            string version = status?.Version != null ? CxLang.Format("SoftwareComponentStatus_Version0", status.Version) : null;
            string detail = status?.Detail;

            switch (status?.State)
            {
                case VulkanLayerRegistrationState.Registered:
                    return new SoftwareComponentStatus(version, new LocalizedText("SoftwareComponentStatus_Registered"),
                        StatusGreen, new LocalizedText("SoftwareComponentStatus_VulkanLayerRegisteredToolTip"), detail);
                case VulkanLayerRegistrationState.Incomplete:
                    return new SoftwareComponentStatus(version, new LocalizedText("SoftwareComponentStatus_Incomplete"),
                        StatusOrange, new LocalizedText("SoftwareComponentStatus_VulkanLayerIncompleteToolTip"), detail);
                case VulkanLayerRegistrationState.Conflicting:
                    return new SoftwareComponentStatus(version, new LocalizedText("SoftwareComponentStatus_ConflictingRegistration"),
                        StatusRed, new LocalizedText("SoftwareComponentStatus_VulkanLayerConflictingToolTip"), detail);
                case VulkanLayerRegistrationState.NotRegistered:
                    return new SoftwareComponentStatus(null, new LocalizedText("SoftwareComponentStatus_NotRegistered"),
                        StatusGray, new LocalizedText("SoftwareComponentStatus_VulkanLayerNotRegisteredToolTip"), detail);
                default:
                    return new SoftwareComponentStatus(version, new LocalizedText("SoftwareComponentStatus_Unknown"),
                        StatusGray, new LocalizedText("SoftwareComponentStatus_VulkanLayerUnknownToolTip"), detail);
            }
        }
    }
}
