using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using Serilog;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Windows.Win32.Storage.FileSystem;
using PInvoke = Windows.Win32.PInvoke;

/// <summary>
/// Represents a safe handle for a Windows service object.
/// </summary>
public sealed class SafeServiceHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    private SafeServiceHandle() : base(true) { }

    /// <summary>
    /// Releases the service handle.
    /// </summary>
    /// <returns></returns>
    protected override bool ReleaseHandle() => CloseServiceHandle(handle);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool CloseServiceHandle(IntPtr hSCObject);
}

/// <summary>
/// Production-hardened installation of the PawnIO kernel driver.
///
/// PawnIO 2.x is a PnP driver: its INF declares a root-enumerated device (<c>Root\PawnIO</c>,
/// class SoftwareDevice) and attaches the service to it via <c>AddService</c>. The device object
/// <c>\Device\PawnIO</c> is therefore only created once that device node exists - registering the
/// .sys as a plain SCM kernel service loads the image but leaves the driver without a device, and
/// the device ACL from the INF's <c>.HW</c> section is never applied either. Installation
/// consequently goes through SetupAPI, mirroring what <c>devcon install</c> does.
///
/// Policy:
/// - If the device is already usable (possibly installed/started by a 3rd party): do nothing.
/// - If a driver package is installed but its service is stopped: start it.
/// - Otherwise: remove a stale legacy service if present, create the device node if missing and
///   install the INF onto it.
/// </summary>
public static class DriverInstaller
{
    /// <summary>
    /// Default Windows service name for the PawnIO driver.
    /// </summary>
    public const string PAWNIO_SERVICE_NAME = "PawnIO";

    private const string PAWNIO_DEVICE_PATH = @"\\?\GLOBALROOT\Device\PawnIO";
    private const string PAWNIO_HARDWARE_ID = @"Root\PawnIO";
    private const string SOFTWARE_DEVICE_CLASS_NAME = "SoftwareDevice";

    // Class SoftwareDevice, as declared by PawnIO.inf.
    private static Guid _softwareDeviceClassGuid = new("62f9c741-b25a-46ce-b54c-9bccce08b6f2");

    // SCM access
    private const uint SC_MANAGER_CONNECT = 0x0001;

    // Service access
    private const uint SERVICE_QUERY_STATUS = 0x0004;
    private const uint SERVICE_START = 0x0010;
    private const uint SERVICE_STOP = 0x0020;
    private const uint DELETE = 0x00010000;

    // Service states
    private const uint SERVICE_STOPPED = 0x00000001;
    private const uint SERVICE_STOP_PENDING = 0x00000003;
    private const uint SERVICE_RUNNING = 0x00000004;

    // Control codes
    private const uint SERVICE_CONTROL_STOP = 0x00000001;

    // Common Win32 errors
    private const int ERROR_SERVICE_ALREADY_RUNNING = 1056;
    private const int ERROR_MARKED_FOR_DELETE = 1072;
    private const int ERROR_NO_MORE_ITEMS = 259;
    private const int ERROR_INSUFFICIENT_BUFFER = 122;

    // SetupAPI
    private const uint DIGCF_PRESENT = 0x00000002;
    private const uint DICD_GENERATE_ID = 0x00000001;
    private const uint SPDRP_HARDWAREID = 0x00000001;
    private const uint DIF_REGISTERDEVICE = 0x00000019;
    private const uint DIF_REMOVE = 0x00000005;

    // newdev
    private const uint INSTALLFLAG_FORCE = 0x00000001;
    private const uint INSTALLFLAG_NONINTERACTIVE = 0x00000004;

    // P/Invokes - service control
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeServiceHandle OpenSCManager(string? machineName, string? databaseName, uint dwAccess);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeServiceHandle OpenService(SafeHandle hSCManager, string lpServiceName, uint dwDesiredAccess);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool StartService(SafeHandle hService, int dwNumServiceArgs, IntPtr lpServiceArgVectors);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool ControlService(SafeHandle hService, uint dwControl, out SERVICE_STATUS lpServiceStatus);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool DeleteService(SafeHandle hService);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool QueryServiceStatus(SafeHandle hService, out SERVICE_STATUS lpServiceStatus);

    // P/Invokes - device installation
    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr SetupDiGetClassDevsW(ref Guid classGuid, string? enumerator, IntPtr hwndParent, uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern IntPtr SetupDiCreateDeviceInfoList(ref Guid classGuid, IntPtr hwndParent);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool SetupDiCreateDeviceInfoW(IntPtr deviceInfoSet, string deviceName, ref Guid classGuid,
        string? deviceDescription, IntPtr hwndParent, uint creationFlags, ref SP_DEVINFO_DATA deviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInfo(IntPtr deviceInfoSet, uint memberIndex, ref SP_DEVINFO_DATA deviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool SetupDiGetDeviceRegistryPropertyW(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData,
        uint property, out uint propertyRegDataType, byte[]? propertyBuffer, uint propertyBufferSize, out uint requiredSize);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool SetupDiSetDeviceRegistryPropertyW(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData,
        uint property, byte[] propertyBuffer, uint propertyBufferSize);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiCallClassInstaller(uint installFunction, IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    [DllImport("newdev.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool UpdateDriverForPlugAndPlayDevicesW(IntPtr hwndParent, string hardwareId,
        string fullInfPath, uint installFlags, out bool rebootRequired);

    [StructLayout(LayoutKind.Sequential)]
    private struct SERVICE_STATUS
    {
        public uint dwServiceType;
        public uint dwCurrentState;
        public uint dwControlsAccepted;
        public uint dwWin32ExitCode;
        public uint dwServiceSpecificExitCode;
        public uint dwCheckPoint;
        public uint dwWaitHint;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVINFO_DATA
    {
        public uint cbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public IntPtr Reserved;
    }

    /// <summary>
    /// Ensures the PawnIO driver is usable. Uses an already-running 3rd party installation if present.
    /// </summary>
    /// <param name="serviceName">Windows service name for the driver (typically "PawnIO").</param>
    /// <param name="infFilePath">Full path to PawnIO.inf. The .sys and .cat it references must sit next to it.</param>
    /// <param name="timeout">Overall timeout for the device to show up after installation.</param>
    /// <returns>True if the device is usable at the end; false otherwise.</returns>
    public static bool EnsureDriverReady(string serviceName, string infFilePath, TimeSpan? timeout = null)
    {
        var opTimeout = timeout ?? TimeSpan.FromSeconds(15);

        try
        {
            // Case 1: Device works (3rd party tool may have installed & started it).
            if (IsDriverDeviceAvailable())
            {
                Log.Information("Driver device is available; using existing installation.");
                return true;
            }

            if (string.IsNullOrWhiteSpace(infFilePath))
                throw new ArgumentException("infFilePath must be provided.", nameof(infFilePath));

            if (!File.Exists(infFilePath))
                throw new FileNotFoundException("Driver INF not found.", infFilePath);

            // The INF pulls both of these in by name; a missing one only surfaces as a late,
            // opaque SetupAPI failure, so check up front.
            string packageDir = Path.GetDirectoryName(Path.GetFullPath(infFilePath))!;
            foreach (string companion in new[] { "PawnIO.sys", "PawnIO.cat" })
            {
                string path = Path.Combine(packageDir, companion);
                if (!File.Exists(path))
                    throw new FileNotFoundException("Driver package is incomplete.", path);
            }

            // Case 2: A previous CapFrameX version registered the driver as a plain SCM service.
            // That registration cannot produce a device, and leaving it behind lets it fight the
            // PnP one over the service entry, so drop it before installing.
            RemoveStaleLegacyService(serviceName, opTimeout);

            // Case 3: Install the driver package onto a Root\PawnIO device node.
            bool rebootRequired = InstallDriverPackage(Path.GetFullPath(infFilePath));

            if (WaitForDriverDevice(opTimeout))
            {
                Log.Information("Driver installed successfully; device is available.");
                return true;
            }

            // PnP normally starts a demand-start driver as part of the install. If it did not,
            // nudge the service before giving up.
            if (TryStartGlobalPawnIOService() && WaitForDriverDevice(opTimeout))
            {
                Log.Information("Driver installed and service started manually; device is available.");
                return true;
            }

            if (rebootRequired)
            {
                Log.Warning("Driver package installed but the device is not available yet; Windows reported that a reboot is required.");
                return false;
            }

            throw new InvalidOperationException("Driver package installed, but device could not be opened.");
        }
        catch (Exception ex)
        {
            // Keep the top-level behavior consistent with production: log and return false.
            Log.Fatal(ex, "EnsureDriverReady failed for PawnIO driver.");
            return false;
        }
    }

    /// <summary>
    /// Installs the driver package onto a <c>Root\PawnIO</c> device node, creating that node first
    /// if it does not exist yet.
    /// </summary>
    /// <returns>True if Windows signalled that a reboot is required to finish the installation.</returns>
    private static bool InstallDriverPackage(string infFilePath)
    {
        if (DeviceNodeExists())
        {
            Log.Information("Device node '{HardwareId}' already present; updating its driver.", PAWNIO_HARDWARE_ID);
        }
        else
        {
            Log.Information("Device node '{HardwareId}' not present; creating it.", PAWNIO_HARDWARE_ID);
            CreateDeviceNode();
        }

        if (!UpdateDriverForPlugAndPlayDevicesW(IntPtr.Zero, PAWNIO_HARDWARE_ID, infFilePath,
                INSTALLFLAG_FORCE | INSTALLFLAG_NONINTERACTIVE, out bool rebootRequired))
        {
            int err = Marshal.GetLastWin32Error();

            // Upgrading from a version that registered the driver through the SCM: the old service
            // was still loaded, so deleting it only marked it for deletion and the INF cannot
            // recreate it until that clears. Nothing to do but wait for the reboot.
            if (err == ERROR_MARKED_FOR_DELETE)
            {
                Log.Warning("The previous '{ServiceName}' service is still marked for delete; the driver package can only be installed after a reboot.", PAWNIO_SERVICE_NAME);
                return true;
            }

            ThrowWin32(err, $"UpdateDriverForPlugAndPlayDevices failed for '{infFilePath}'. (Are you running elevated?)");
        }

        Log.Information("Driver package '{Inf}' installed. RebootRequired={RebootRequired}", infFilePath, rebootRequired);
        return rebootRequired;
    }

    /// <summary>
    /// Checks whether a device node carrying the PawnIO hardware ID is already registered.
    /// </summary>
    private static bool DeviceNodeExists()
    {
        IntPtr devInfo = SetupDiGetClassDevsW(ref _softwareDeviceClassGuid, null, IntPtr.Zero, DIGCF_PRESENT);
        if (devInfo == new IntPtr(-1))
            return false;

        try
        {
            var data = new SP_DEVINFO_DATA { cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>() };

            for (uint index = 0; SetupDiEnumDeviceInfo(devInfo, index, ref data); index++)
            {
                foreach (string id in GetHardwareIds(devInfo, ref data))
                {
                    if (string.Equals(id, PAWNIO_HARDWARE_ID, StringComparison.OrdinalIgnoreCase))
                        return true;
                }

                data = new SP_DEVINFO_DATA { cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>() };
            }

            int err = Marshal.GetLastWin32Error();
            if (err != ERROR_NO_MORE_ITEMS)
                Log.Debug("Enumerating SoftwareDevice class stopped with Win32Error={Win32Error}.", err);

            return false;
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(devInfo);
        }
    }

    /// <summary>
    /// Reads the REG_MULTI_SZ hardware ID list of a device.
    /// </summary>
    private static string[] GetHardwareIds(IntPtr devInfo, ref SP_DEVINFO_DATA data)
    {
        if (!SetupDiGetDeviceRegistryPropertyW(devInfo, ref data, SPDRP_HARDWAREID, out _, null, 0, out uint required))
        {
            int err = Marshal.GetLastWin32Error();
            if (err != ERROR_INSUFFICIENT_BUFFER || required == 0)
                return Array.Empty<string>();
        }

        byte[] buffer = new byte[required];
        if (!SetupDiGetDeviceRegistryPropertyW(devInfo, ref data, SPDRP_HARDWAREID, out _, buffer, required, out _))
            return Array.Empty<string>();

        // char-overload of Split with options is not available on net472/netstandard2.0.
        return System.Text.Encoding.Unicode.GetString(buffer)
                     .Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// Creates the root-enumerated device node the driver attaches to.
    /// </summary>
    private static void CreateDeviceNode()
    {
        IntPtr devInfo = SetupDiCreateDeviceInfoList(ref _softwareDeviceClassGuid, IntPtr.Zero);
        if (devInfo == new IntPtr(-1))
            ThrowLastWin32("SetupDiCreateDeviceInfoList failed.");

        var data = new SP_DEVINFO_DATA { cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>() };
        bool registered = false;

        try
        {
            if (!SetupDiCreateDeviceInfoW(devInfo, SOFTWARE_DEVICE_CLASS_NAME, ref _softwareDeviceClassGuid,
                    null, IntPtr.Zero, DICD_GENERATE_ID, ref data))
            {
                ThrowLastWin32("SetupDiCreateDeviceInfo failed.");
            }

            // SPDRP_HARDWAREID is REG_MULTI_SZ, so the list needs its own terminating null on top
            // of the one Encoding.Unicode puts after the string.
            byte[] hardwareId = System.Text.Encoding.Unicode.GetBytes(PAWNIO_HARDWARE_ID + "\0\0");

            if (!SetupDiSetDeviceRegistryPropertyW(devInfo, ref data, SPDRP_HARDWAREID, hardwareId, (uint)hardwareId.Length))
                ThrowLastWin32("SetupDiSetDeviceRegistryProperty(SPDRP_HARDWAREID) failed.");

            if (!SetupDiCallClassInstaller(DIF_REGISTERDEVICE, devInfo, ref data))
                ThrowLastWin32("SetupDiCallClassInstaller(DIF_REGISTERDEVICE) failed. (Are you running elevated?)");

            registered = true;
            Log.Information("Created device node for '{HardwareId}'.", PAWNIO_HARDWARE_ID);
        }
        catch
        {
            // Do not leave a half-registered node behind - it would shadow the next attempt.
            if (!registered)
            {
                try { SetupDiCallClassInstaller(DIF_REMOVE, devInfo, ref data); }
                catch (Exception ex) { Log.Debug(ex, "Rolling back the device node failed."); }
            }

            throw;
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(devInfo);
        }
    }

    /// <summary>
    /// Deletes a PawnIO service that was registered directly against a .sys file rather than through
    /// the driver package. Versions up to 1.9.0 installed the driver that way.
    /// </summary>
    private static void RemoveStaleLegacyService(string serviceName, TimeSpan timeout)
    {
        try
        {
            if (!IsLegacyServiceRegistration(serviceName))
                return;

            using var scm = OpenSCManager(null, null, SC_MANAGER_CONNECT);
            if (scm.IsInvalid)
                return;

            using var service = OpenService(scm, serviceName, SERVICE_QUERY_STATUS | SERVICE_STOP | DELETE);
            if (service.IsInvalid)
                return;

            Log.Warning("Found a leftover '{ServiceName}' service without a usable device; removing it before installing the driver package.", serviceName);

            BestEffortStop(service, timeout);

            if (!DeleteService(service))
            {
                int err = Marshal.GetLastWin32Error();
                if (err == ERROR_MARKED_FOR_DELETE)
                    Log.Information("Service '{ServiceName}' is already marked for delete; it disappears on the next reboot.", serviceName);
                else
                    Log.Warning("DeleteService failed for '{ServiceName}' with Win32Error={Win32Error}.", serviceName, err);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Removing the leftover '{ServiceName}' service failed.", serviceName);
        }
    }

    /// <summary>
    /// Distinguishes a driver-store backed registration - written by the INF's AddService and owned
    /// by the driver package - from one that points straight at a loose .sys file. Only the latter
    /// is ours to delete; removing the former would tear down a healthy PawnIO installation.
    /// </summary>
    private static bool IsLegacyServiceRegistration(string serviceName)
    {
        try
        {
            using RegistryKey key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{serviceName}");
            if (key?.GetValue("ImagePath") is not string imagePath || string.IsNullOrWhiteSpace(imagePath))
                return false;

            bool inDriverStore = imagePath.IndexOf(@"\DriverStore\", StringComparison.OrdinalIgnoreCase) >= 0;

            if (inDriverStore)
                Log.Information("Existing '{ServiceName}' service is backed by a driver package; leaving it alone.", serviceName);

            return !inDriverStore;
        }
        catch (Exception ex)
        {
            // Without a reliable answer, do not delete anything.
            Log.Debug(ex, "Could not read the ImagePath of the '{ServiceName}' service.", serviceName);
            return false;
        }
    }

    /// <summary>
    /// Polls until the driver device can be opened or the timeout elapses. PnP starts the driver
    /// asynchronously, so the device does not exist the instant the install call returns.
    /// </summary>
    private static bool WaitForDriverDevice(TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();

        while (true)
        {
            if (TryOpenDriverDevice())
                return true;

            if (sw.Elapsed >= timeout)
                return false;

            Thread.Sleep(200);
        }
    }

    /// <summary>
    /// Checks if the driver device is available.
    /// Prefers global driver installation via PawnIO installer.
    /// </summary>
    private static bool IsDriverDeviceAvailable()
    {
        // First, check for global PawnIO service installation (preferred)
        // This ensures we use the globally installed driver if available
        if (TryStartGlobalPawnIOService())
        {
            // Global service is running, verify device is accessible
            if (TryOpenDriverDevice())
            {
                return true;
            }
        }

        // Fallback: check if the device is accessible from any other source
        // (e.g., another application started it, or a local installation)
        return TryOpenDriverDevice();
    }

    /// <summary>
    /// Attempts to open the driver device handle.
    /// </summary>
    private static bool TryOpenDriverDevice()
    {
        using SafeFileHandle handle = PInvoke.CreateFile(
            PAWNIO_DEVICE_PATH,
            (uint)FileAccess.ReadWrite,
            FILE_SHARE_MODE.FILE_SHARE_READ | FILE_SHARE_MODE.FILE_SHARE_WRITE,
            null,
            FILE_CREATION_DISPOSITION.OPEN_EXISTING,
            FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_NORMAL,
            null);

        return !handle.IsInvalid;
    }

    /// <summary>
    /// Checks for a global PawnIO driver installation and attempts to start it.
    /// Global installation is typically done via the PawnIO installer and registers the driver
    /// package in the driver store with a persistent service registration.
    /// </summary>
    private static bool TryStartGlobalPawnIOService()
    {
        try
        {
            using var scm = OpenSCManager(null, null, SC_MANAGER_CONNECT);
            if (scm.IsInvalid)
            {
                return false;
            }

            using var service = OpenService(scm, PAWNIO_SERVICE_NAME, SERVICE_QUERY_STATUS | SERVICE_START);
            if (service.IsInvalid)
            {
                // Service does not exist - no global installation
                return false;
            }

            // Service exists - check if it's a global installation by verifying it's running
            // or can be started
            if (!QueryServiceStatus(service, out var status))
            {
                return false;
            }

            if (status.dwCurrentState == SERVICE_RUNNING)
            {
                Log.Information("Global PawnIO driver service is already running.");
                return true;
            }

            if (status.dwCurrentState == SERVICE_STOPPED)
            {
                Log.Information("Found global PawnIO driver installation. Attempting to start service.");

                if (StartService(service, 0, IntPtr.Zero))
                {
                    // Wait for service to reach running state
                    if (WaitForState(service, SERVICE_RUNNING, TimeSpan.FromSeconds(10)))
                    {
                        Log.Information("Global PawnIO driver service started successfully.");
                        return true;
                    }
                }
                else
                {
                    int err = Marshal.GetLastWin32Error();
                    if (err == ERROR_SERVICE_ALREADY_RUNNING)
                    {
                        return true;
                    }
                    Log.Warning("Failed to start global PawnIO service. Win32Error={Win32Error}", err);
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Error checking for global PawnIO installation.");
            return false;
        }
    }

    private static void BestEffortStop(SafeServiceHandle service, TimeSpan timeout)
    {
        try
        {
            uint state = QueryCurrentState(service, out _);

            if (state == SERVICE_STOPPED)
                return;

            if (state == SERVICE_STOP_PENDING)
            {
                WaitForState(service, SERVICE_STOPPED, timeout);
                return;
            }

            if (!ControlService(service, SERVICE_CONTROL_STOP, out _))
            {
                int err = Marshal.GetLastWin32Error();
                Log.Warning("ControlService(STOP) failed with Win32Error={Win32Error}.", err);
                return;
            }

            WaitForState(service, SERVICE_STOPPED, timeout);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Best-effort stop failed.");
        }
    }

    private static uint QueryCurrentState(SafeServiceHandle service, out uint win32ExitCode)
    {
        if (!QueryServiceStatus(service, out var status))
        {
            int err = Marshal.GetLastWin32Error();
            ThrowWin32(err, "QueryServiceStatus failed.");
        }

        win32ExitCode = status.dwWin32ExitCode;
        return status.dwCurrentState;
    }

    private static bool WaitForState(SafeServiceHandle service, uint desiredState, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        uint lastState = 0;

        while (sw.Elapsed < timeout)
        {
            uint state = QueryCurrentState(service, out var exit);
            if (state == desiredState)
                return true;

            // If it hard-failed, stop waiting early.
            if (desiredState == SERVICE_RUNNING && state == SERVICE_STOPPED && exit != 0)
            {
                Log.Warning("Service stopped with non-zero Win32ExitCode={ExitCode} while waiting for RUNNING.", exit);
                return false;
            }

            // Backoff: poll reasonably without busy-waiting.
            // Use service's wait hint as a signal, but clamp to sane bounds.
            uint delayMs = 200;
            if (QueryServiceStatus(service, out var status))
            {
                // dwWaitHint is milliseconds.
                delayMs = Clamp(status.dwWaitHint / 10, 100, 500);
            }

            if (state != lastState)
            {
                Log.Debug("Service state transition: {State} (target {Target})", state, desiredState);
                lastState = state;
            }

            Thread.Sleep((int)delayMs);
        }

        Log.Warning("Timed out waiting for service state {TargetState}.", desiredState);
        return false;
    }

    private static uint Clamp(uint value, uint min, uint max) => value < min ? min : (value > max ? max : value);

    private static void ThrowLastWin32(string message)
    {
        int err = Marshal.GetLastWin32Error();
        ThrowWin32(err, message);
    }

    private static void ThrowWin32(int error, string message)
    {
        throw new Win32Exception(error, message);
    }

    /// <summary>
    /// Gets the file path of the PawnIO driver binary.
    /// </summary>
    public static string GetPawnIODriverPath()
    {
        return Path.Combine(GetPawnIOPackageDirectory(), $"{PAWNIO_SERVICE_NAME}.sys");
    }

    /// <summary>
    /// Gets the file path of the PawnIO driver INF, which is what the installation is driven from.
    /// </summary>
    public static string GetPawnIOInfPath()
    {
        return Path.Combine(GetPawnIOPackageDirectory(), $"{PAWNIO_SERVICE_NAME}.inf");
    }

    private static string GetPawnIOPackageDirectory()
    {
        return Path.Combine(AppContext.BaseDirectory, "PawnIo");
    }
}
