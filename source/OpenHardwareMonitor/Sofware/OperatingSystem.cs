using System;

namespace OpenHardwareMonitor.Sofware
{
    /// <summary>
    /// Contains basic information about the operating system.
    /// </summary>
    public static class OperatingSystem
    {
        /// <summary>
        /// Statically checks if the current system <see cref="Is64Bit"/> and <see cref="IsUnix"/>.
        /// </summary>
        static OperatingSystem()
        {
            // The operating system doesn't change during execution so let's query it just one time.
            PlatformID platform = Environment.OSVersion.Platform;
            IsUnix = platform == PlatformID.Unix || platform == PlatformID.MacOSX;

            if (Environment.Is64BitOperatingSystem)
                Is64Bit = true;
        }

        /// <summary>
        /// Gets information about whether the current system is 64 bit.
        /// </summary>
        public static bool Is64Bit { get; }

        /// <summary>
        /// Gets information about whether the current system is Unix based.
        /// </summary>
        public static bool IsUnix { get; }
    }
}
