using Microsoft.Win32.SafeHandles;
using Serilog;
using System;
using System.IO;
using System.Runtime.InteropServices;

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
    protected override bool ReleaseHandle()
    {
        return CloseServiceHandle(handle);
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool CloseServiceHandle(IntPtr hSCObject);
}

/// <summary>
/// Provides methods to install, start, stop, and remove a Windows kernel driver service.
/// </summary>
public static class DriverInstaller
{
    /// <summary>
    /// The name of the PawnIO driver service.
    /// </summary>
    public const string PAWNIO_SERVICE_NAME = "PawnIO";

    private const uint SERVICE_KERNEL_DRIVER = 0x00000001;
    private const uint SERVICE_DEMAND_START = 0x00000003;
    private const uint SERVICE_ERROR_NORMAL = 0x00000001;

    private const uint SC_MANAGER_CREATE_SERVICE = 0x0002;
    private const uint SERVICE_ALL_ACCESS = 0xF01FF;

    private const uint SERVICE_CONTROL_STOP = 0x00000001;
    private const uint SERVICE_STOP = 0x0020;
    private const uint SERVICE_QUERY_STATUS = 0x0004;
    private const uint SERVICE_START = 0x0010;

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern SafeServiceHandle OpenSCManager(string machineName, string databaseName, uint dwAccess);

    [DllImport("advapi32.dll", SetLastError = true)]
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
    private static extern SafeServiceHandle OpenService(
        SafeHandle hSCManager,
        string lpServiceName,
        uint dwDesiredAccess);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool StartService(SafeHandle hService, int dwNumServiceArgs, IntPtr lpServiceArgVectors);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool ControlService(
        SafeHandle hService,
        uint dwControl,
        out SERVICE_STATUS lpServiceStatus);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool DeleteService(SafeHandle hService);

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
    /// Installs and starts a kernel driver service.
    /// </summary>
    /// <param name="serviceName"></param>
    /// <param name="sysFilePath"></param>
    public static void InstallAndStartDriver(string serviceName, string sysFilePath)
    {
        try
        {
            if (!File.Exists(sysFilePath))
                throw new FileNotFoundException("Driver file not found.", sysFilePath);

            SafeServiceHandle scm = OpenSCManager(null, null, SC_MANAGER_CREATE_SERVICE);
            if (scm.IsInvalid)
                throw new InvalidOperationException("OpenSCManager failed.");

            // Try opening existing service
            SafeServiceHandle service = OpenService(scm, serviceName, SERVICE_START | SERVICE_QUERY_STATUS);
            if (service.IsInvalid)
            {
                // Create new service
                service = CreateService(
                    scm,
                    serviceName,
                    serviceName,
                    SERVICE_ALL_ACCESS,
                    SERVICE_KERNEL_DRIVER,
                    SERVICE_DEMAND_START,
                    SERVICE_ERROR_NORMAL,
                    sysFilePath,
                    null,
                    IntPtr.Zero,
                    null,
                    null,
                    null);

                if (service.IsInvalid)
                    throw new InvalidOperationException("CreateService failed.");
            }

            if (!StartService(service, 0, IntPtr.Zero))
            {
                int err = Marshal.GetLastWin32Error();
                if (err != 1056) // Already running
                    throw new InvalidOperationException("StartService failed: " + err);
            }
        }
        catch(Exception ex)
        {
            Log.Logger.Fatal(ex, "Exception while installing PawnIO driver");
        }
    }

    /// <summary>
    /// Stops and removes a kernel driver service.
    /// </summary>
    /// <param name="serviceName"></param>
    public static void StopAndRemoveDriver(string serviceName)
    {
        try
        {
            SafeServiceHandle scm = OpenSCManager(null, null, SC_MANAGER_CREATE_SERVICE);
            if (scm.IsInvalid)
                return;

            SafeServiceHandle service = OpenService(scm, serviceName, SERVICE_STOP | SERVICE_QUERY_STATUS);
            if (service.IsInvalid)
                return;

            ControlService(service, SERVICE_CONTROL_STOP, out _);
            DeleteService(service);
        }
        catch
        {
            throw;
        }
    }

    /// <summary>
    /// Gets the file path of the PawnIO driver.
    /// </summary>
    /// <returns></returns>
    public static string GetPawnIODriverPath()
    {
        string baseDir = AppContext.BaseDirectory;
        string driverDir = Path.Combine(baseDir, "PawnIo");
        return Path.Combine(driverDir, $"{PAWNIO_SERVICE_NAME}.sys");
    }
}
