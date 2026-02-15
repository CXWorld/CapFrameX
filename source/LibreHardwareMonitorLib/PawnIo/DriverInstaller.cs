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
/// Production-hardened kernel driver service management for PawnIO.
/// 
/// Policy:
/// - If the device is already usable (possibly installed/started by a 3rd party): do nothing.
/// - Otherwise, if the service exists: attempt to start it.
/// - If start fails OR device still not usable: stop (best-effort), delete, recreate, start, verify.
/// </summary>
public static class DriverInstaller
{
    /// <summary>
    /// Default Windows service name for the PawnIO driver.
    /// </summary>
    public const string PAWNIO_SERVICE_NAME = "PawnIO";
    private const string PAWNIO_DEVICE_PATH = @"\\.\PawnIO";

    // SCM access
    private const uint SC_MANAGER_CONNECT = 0x0001;
    private const uint SC_MANAGER_CREATE_SERVICE = 0x0002;

    // Service access
    private const uint SERVICE_QUERY_STATUS = 0x0004;
    private const uint SERVICE_START = 0x0010;
    private const uint SERVICE_STOP = 0x0020;
    private const uint DELETE = 0x00010000;

    private const uint SERVICE_ALL_ACCESS = 0xF01FF;

    // Service config
    private const uint SERVICE_KERNEL_DRIVER = 0x00000001;
    private const uint SERVICE_DEMAND_START = 0x00000003;
    private const uint SERVICE_ERROR_NORMAL = 0x00000001;

    // Control codes
    private const uint SERVICE_CONTROL_STOP = 0x00000001;

    // Service states
    private const uint SERVICE_STOPPED = 0x00000001;
    private const uint SERVICE_START_PENDING = 0x00000002;
    private const uint SERVICE_STOP_PENDING = 0x00000003;
    private const uint SERVICE_RUNNING = 0x00000004;

    // Common Win32 errors
    private const int ERROR_SERVICE_ALREADY_RUNNING = 1056;
    private const int ERROR_SERVICE_DOES_NOT_EXIST = 1060;
    private const int ERROR_SERVICE_EXISTS = 1073;
    private const int ERROR_MARKED_FOR_DELETE = 1072;

    // P/Invokes
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeServiceHandle OpenSCManager(string? machineName, string? databaseName, uint dwAccess);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeServiceHandle OpenService(SafeHandle hSCManager, string lpServiceName, uint dwDesiredAccess);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeServiceHandle CreateService(
        SafeHandle hSCManager,
        string lpServiceName,
        string lpDisplayName,
        uint dwDesiredAccess,
        uint dwServiceType,
        uint dwStartType,
        uint dwErrorControl,
        string lpBinaryPathName,
        string lpLoadOrderGroup,
        IntPtr lpdwTagId,
        string lpDependencies,
        string lpServiceStartName,
        string lpPassword);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool StartService(SafeHandle hService, int dwNumServiceArgs, IntPtr lpServiceArgVectors);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool ControlService(SafeHandle hService, uint dwControl, out SERVICE_STATUS lpServiceStatus);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool DeleteService(SafeHandle hService);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool QueryServiceStatus(SafeHandle hService, out SERVICE_STATUS lpServiceStatus);

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

    /// <summary>
    /// Ensures PawnIO driver is usable. Uses an already-running 3rd party installation if present.
    /// </summary>
    /// <param name="serviceName">Windows service name for the driver (typically "PawnIO").</param>
    /// <param name="sysFilePath">Full path to the PawnIO.sys file (used only if install/reinstall is required).</param>
    /// <param name="timeout">Overall timeout for stop/start transitions.</param>
    /// <returns>True if the device is usable at the end; false otherwise.</returns>
    public static bool EnsureDriverReady(string serviceName, string sysFilePath, TimeSpan? timeout = null)
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

            if (string.IsNullOrWhiteSpace(sysFilePath))
                throw new ArgumentException("sysFilePath must be provided.", nameof(sysFilePath));

            if (!File.Exists(sysFilePath))
                throw new FileNotFoundException("Driver file not found.", sysFilePath);

            using var scm = OpenSCManager(null, null, SC_MANAGER_CONNECT | SC_MANAGER_CREATE_SERVICE);
            if (scm.IsInvalid)
                ThrowLastWin32("OpenSCManager failed. (Are you running elevated?)");

            // Try existing service path first.
            if (TryOpenService(scm, serviceName, out var existingService))
            {
                using (existingService)
                {
                    Log.Information("Driver service '{ServiceName}' exists; attempting to start.", serviceName);

                    // Case 2: Installed but not started: start it.
                    if (TryStartServiceAndWait(existingService, opTimeout))
                    {
                        if (IsDriverDeviceAvailable())
                        {
                            Log.Information("Driver service started and device is available.");
                            return true;
                        }

                        Log.Warning("Service reported started, but device is still not available; proceeding to reinstall.");
                    }
                    else
                    {
                        Log.Warning("Failed to start existing service '{ServiceName}'; proceeding to reinstall.", serviceName);
                    }

                    // Case 3: Installed but cannot be started or device not present => reinstall.
                    BestEffortStop(existingService, opTimeout);
                    BestEffortDelete(existingService, serviceName, opTimeout);
                }
            }
            else
            {
                Log.Information("Driver service '{ServiceName}' not present; proceeding to install.", serviceName);
            }

            // Case 4: Not installed OR removed above => create and start.
            using var created = CreateOrOpenAfterRaces(scm, serviceName, sysFilePath, opTimeout);

            if (!TryStartServiceAndWait(created, opTimeout))
                throw new InvalidOperationException($"StartService failed for '{serviceName}' after install/reinstall.");

            if (!IsDriverDeviceAvailable())
                throw new InvalidOperationException("Driver service started, but device could not be opened.");

            Log.Information("Driver installed/reinstalled successfully; device is available.");
            return true;
        }
        catch (Exception ex)
        {
            // Keep the top-level behavior consistent with production: log and return false.
            Log.Fatal(ex, "EnsureDriverReady failed for PawnIO driver.");
            return false;
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
    /// Global installation is typically done via the PawnIO installer and places
    /// the driver in System32\drivers with a persistent service registration.
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

    private static bool TryOpenService(SafeHandle scm, string serviceName, out SafeServiceHandle service)
    {
        service = OpenService(scm, serviceName, SERVICE_QUERY_STATUS | SERVICE_START | SERVICE_STOP | DELETE);
        if (!service.IsInvalid)
            return true;

        int err = Marshal.GetLastWin32Error();
        service.Dispose();

        if (err == ERROR_SERVICE_DOES_NOT_EXIST)
            return false;

        // Any other error is actionable.
        ThrowWin32(err, $"OpenService failed for '{serviceName}'.");
        return false; // unreachable
    }

    private static SafeServiceHandle CreateOrOpenAfterRaces(SafeHandle scm, string serviceName, string sysFilePath, TimeSpan timeout)
    {
        // CreateService can race if another process creates it after we decided it didn't exist.
        // Also, service can be marked-for-delete briefly after deletion.
        var sw = Stopwatch.StartNew();

        // Convert to NT path format for kernel drivers (e.g., \??\C:\path\to\driver.sys)
        string ntPath = sysFilePath;
        if (!sysFilePath.StartsWith(@"\??\", StringComparison.OrdinalIgnoreCase))
        {
            ntPath = @"\??\" + sysFilePath;
        }

        while (true)
        {
            var svc = CreateService(
                scm,
                serviceName,
                serviceName,
                SERVICE_ALL_ACCESS,
                SERVICE_KERNEL_DRIVER,
                SERVICE_DEMAND_START,
                SERVICE_ERROR_NORMAL,
                ntPath,
                null,
                IntPtr.Zero,
                null,
                null,
                null);

            if (!svc.IsInvalid)
                return svc;

            int err = Marshal.GetLastWin32Error();
            svc.Dispose();

            if (err == ERROR_SERVICE_EXISTS)
            {
                // Someone else created it; open it with what we need.
                if (TryOpenService(scm, serviceName, out var existing))
                    return existing;

                // If TryOpenService throws, we won't reach here.
            }

            if (err == ERROR_MARKED_FOR_DELETE)
            {
                if (sw.Elapsed > timeout)
                    ThrowWin32(err, $"Service '{serviceName}' is marked for delete and did not clear in time.");
                Thread.Sleep(200);
                continue;
            }

            ThrowWin32(err, $"CreateService failed for '{serviceName}'.");
        }
    }

    private static bool TryStartServiceAndWait(SafeServiceHandle service, TimeSpan timeout)
    {
        // If already running, treat as success.
        uint state = QueryCurrentState(service, out var win32Exit);
        if (state == SERVICE_RUNNING)
            return true;

        // If in stop-pending, wait it out before starting.
        if (state == SERVICE_STOP_PENDING)
        {
            if (!WaitForState(service, SERVICE_STOPPED, timeout))
                return false;
        }

        if (StartService(service, 0, IntPtr.Zero))
            return WaitForState(service, SERVICE_RUNNING, timeout);

        int err = Marshal.GetLastWin32Error();
        if (err == ERROR_SERVICE_ALREADY_RUNNING)
            return true;

        // Some failures are transient; however, for a kernel driver the correct action is typically reinstall.
        Log.Warning("StartService failed with Win32Error={Win32Error} (Exit={ExitCode}).", err, win32Exit);
        return false;
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

    private static void BestEffortDelete(SafeServiceHandle service, string serviceName, TimeSpan timeout)
    {
        try
        {
            if (!DeleteService(service))
            {
                int err = Marshal.GetLastWin32Error();
                if (err == ERROR_MARKED_FOR_DELETE)
                {
                    // Already being deleted.
                    Log.Information("Service '{ServiceName}' already marked for delete.", serviceName);
                }
                else
                {
                    Log.Warning("DeleteService failed for '{ServiceName}' with Win32Error={Win32Error}.", serviceName, err);
                }
            }

            // Optional: wait until it truly disappears to reduce CreateService races.
            // We cannot re-open SCM here safely without the caller; so the caller handles ERROR_MARKED_FOR_DELETE.
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Best-effort delete failed for '{ServiceName}'.", serviceName);
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
    /// Gets the file path of the PawnIO driver.
    /// </summary>
    public static string GetPawnIODriverPath()
    {
        string baseDir = AppContext.BaseDirectory;
        string driverDir = Path.Combine(baseDir, "PawnIo");
        return Path.Combine(driverDir, $"{PAWNIO_SERVICE_NAME}.sys");
    }
}
