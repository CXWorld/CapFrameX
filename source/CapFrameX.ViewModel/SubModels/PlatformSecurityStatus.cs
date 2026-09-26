using CapFrameX.Contracts.Data;

namespace CapFrameX.ViewModel.SubModels
{
    /// <summary>
    /// Presentation model for one boot or code integrity setting on the Info tab. Green means the
    /// setting is of no concern for measurements and drivers; orange marks a state worth knowing
    /// when runs are compared or a driver does not load. The set of states is finite, so the
    /// factories hand out shared instances and the periodic refresh raises no change notifications.
    /// </summary>
    public class PlatformSecurityStatus
    {
        private const string StatusGreen = "#4CAF50";
        private const string StatusOrange = "#FF9800";
        private const string StatusGray = "#757575";

        private static readonly PlatformSecurityStatus SecureBootOn = new PlatformSecurityStatus("On", StatusGreen,
            "UEFI Secure Boot is on. Some anti-cheat systems require it, and Windows ignores the test signing option while it is on.");
        private static readonly PlatformSecurityStatus SecureBootOff = new PlatformSecurityStatus("Off", StatusOrange,
            "UEFI Secure Boot is off. Some anti-cheat systems refuse to run without it.");
        private static readonly PlatformSecurityStatus TestSigningOn = new PlatformSecurityStatus("On", StatusOrange,
            "Windows loads test-signed drivers. A driver that only loads this way stops working once Secure Boot is turned on.");
        private static readonly PlatformSecurityStatus TestSigningOff = new PlatformSecurityStatus("Off", StatusGreen,
            "Windows only loads drivers with a production signature.");
        private static readonly PlatformSecurityStatus VbsRunning = new PlatformSecurityStatus("Running", StatusOrange,
            "Virtualization-based security is running. It costs some performance, so compare runs only with the same setting.");
        private static readonly PlatformSecurityStatus VbsOff = new PlatformSecurityStatus("Off", StatusGreen,
            "Virtualization-based security is not running.");
        private static readonly PlatformSecurityStatus MemoryIntegrityOn = new PlatformSecurityStatus("On", StatusOrange,
            "Memory integrity (hypervisor-enforced code integrity) is on. It costs some performance and blocks drivers on Microsoft's vulnerable-driver list.");
        private static readonly PlatformSecurityStatus MemoryIntegrityOff = new PlatformSecurityStatus("Off", StatusGreen,
            "Memory integrity (hypervisor-enforced code integrity) is off.");

        public string State { get; }

        public string StatusColor { get; }

        public string ToolTip { get; }

        private PlatformSecurityStatus(string state, string statusColor, string toolTip)
        {
            State = state;
            StatusColor = statusColor;
            ToolTip = toolTip;
        }

        public static PlatformSecurityStatus Unknown { get; } = new PlatformSecurityStatus("Unknown", StatusGray, null);

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
