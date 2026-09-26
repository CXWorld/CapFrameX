using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Localization;

namespace CapFrameX.ViewModel.SubModels
{
    /// <summary>
    /// Presentation model for one boot or code integrity setting on the Info tab. Green means the
    /// setting is of no concern for measurements and drivers; orange marks a state worth knowing
    /// when runs are compared or a driver does not load. The set of states is finite, so the
    /// factories hand out shared instances and the periodic refresh raises no change notifications.
    /// State and tooltip are catalog texts resolved on every read, which keeps the shared instances
    /// valid across language switches.
    /// </summary>
    public class PlatformSecurityStatus
    {
        private const string StatusGreen = "#4CAF50";
        private const string StatusOrange = "#FF9800";
        private const string StatusGray = "#757575";

        private static readonly PlatformSecurityStatus SecureBootOn = new PlatformSecurityStatus(
            new LocalizedText("PlatformSecurityStatus_On"), StatusGreen, new LocalizedText("PlatformSecurityStatus_SecureBootOnToolTip"));
        private static readonly PlatformSecurityStatus SecureBootOff = new PlatformSecurityStatus(
            new LocalizedText("PlatformSecurityStatus_Off"), StatusOrange, new LocalizedText("PlatformSecurityStatus_SecureBootOffToolTip"));
        private static readonly PlatformSecurityStatus TestSigningOn = new PlatformSecurityStatus(
            new LocalizedText("PlatformSecurityStatus_On"), StatusOrange, new LocalizedText("PlatformSecurityStatus_TestSigningOnToolTip"));
        private static readonly PlatformSecurityStatus TestSigningOff = new PlatformSecurityStatus(
            new LocalizedText("PlatformSecurityStatus_Off"), StatusGreen, new LocalizedText("PlatformSecurityStatus_TestSigningOffToolTip"));
        private static readonly PlatformSecurityStatus VbsRunning = new PlatformSecurityStatus(
            new LocalizedText("PlatformSecurityStatus_Running"), StatusOrange, new LocalizedText("PlatformSecurityStatus_VbsRunningToolTip"));
        private static readonly PlatformSecurityStatus VbsOff = new PlatformSecurityStatus(
            new LocalizedText("PlatformSecurityStatus_Off"), StatusGreen, new LocalizedText("PlatformSecurityStatus_VbsOffToolTip"));
        private static readonly PlatformSecurityStatus MemoryIntegrityOn = new PlatformSecurityStatus(
            new LocalizedText("PlatformSecurityStatus_On"), StatusOrange, new LocalizedText("PlatformSecurityStatus_MemoryIntegrityOnToolTip"));
        private static readonly PlatformSecurityStatus MemoryIntegrityOff = new PlatformSecurityStatus(
            new LocalizedText("PlatformSecurityStatus_Off"), StatusGreen, new LocalizedText("PlatformSecurityStatus_MemoryIntegrityOffToolTip"));

        private readonly LocalizedText _state;
        private readonly LocalizedText _toolTip;

        public string State => _state.Resolve();

        public string StatusColor { get; }

        public string ToolTip => _toolTip?.Resolve();

        private PlatformSecurityStatus(LocalizedText state, string statusColor, LocalizedText toolTip)
        {
            _state = state;
            StatusColor = statusColor;
            _toolTip = toolTip;
        }

        public static PlatformSecurityStatus Unknown { get; } =
            new PlatformSecurityStatus(new LocalizedText("PlatformSecurityStatus_Unknown"), StatusGray, null);

        public static PlatformSecurityStatus ForSecureBoot(ESystemInfoTertiaryStatus status)
            => Pick(status, SecureBootOn, SecureBootOff);

        public static PlatformSecurityStatus ForTestSigning(ESystemInfoTertiaryStatus status)
            => Pick(status, TestSigningOn, TestSigningOff);

        public static PlatformSecurityStatus ForVirtualizationBasedSecurity(ESystemInfoTertiaryStatus status)
            => Pick(status, VbsRunning, VbsOff);

        public static PlatformSecurityStatus ForMemoryIntegrity(ESystemInfoTertiaryStatus status)
            => Pick(status, MemoryIntegrityOn, MemoryIntegrityOff);

        private static PlatformSecurityStatus Pick(ESystemInfoTertiaryStatus status,
            PlatformSecurityStatus enabled, PlatformSecurityStatus disabled)
        {
            switch (status)
            {
                case ESystemInfoTertiaryStatus.Enabled:
                    return enabled;
                case ESystemInfoTertiaryStatus.Disabled:
                    return disabled;
                default:
                    return Unknown;
            }
        }
    }
}
