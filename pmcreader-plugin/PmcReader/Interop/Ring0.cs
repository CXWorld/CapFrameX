// From LibreHardwareMonitor, with some modifications
// Mozilla Public License 2.0
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors
// All Rights Reserved

using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;


namespace PmcReader.Interop
{
    internal static class Ring0
    {
        // Device/service name the classic WinRing0 1.2.0 driver exposes. The device
        // symbolic link is fixed inside the driver binary, so we use the same name for
        // both the service and the \\.\ device path.
        private const string ServiceName = "WinRing0_1_2_0";

        private static KernelDriver _driver;
        private static bool _installedByUs;

        // Path of the bundled driver we extracted to disk for installation, if any.
        // Null when we reused an existing device/service or used a user-provided path.
        // Cleaned up once we remove the service we created.
        private static string _extractedDriverPath;

        private static Mutex _isaBusMutex;
        private static Mutex _pciBusMutex;

        private static readonly StringBuilder Report = new StringBuilder();

        public const uint INVALID_PCI_ADDRESS = 0xFFFFFFFF;

        private const uint OLS_TYPE = 40000;

        // Intel PCM uses 50000 for winring0 access
        private const uint PCM_OLS_TYPE = 50000;

        public static readonly Kernel32.IOControlCode
            IOCTL_OLS_GET_REFCOUNT = new Kernel32.IOControlCode(OLS_TYPE, 0x801, Kernel32.IOControlCode.Access.Any);

        public static readonly Kernel32.IOControlCode
            IOCTL_OLS_READ_MSR = new Kernel32.IOControlCode(OLS_TYPE, 0x821, Kernel32.IOControlCode.Access.Any);

        public static readonly Kernel32.IOControlCode
            IOCTL_OLS_WRITE_MSR = new Kernel32.IOControlCode(OLS_TYPE, 0x822, Kernel32.IOControlCode.Access.Any);

        public static readonly Kernel32.IOControlCode
            IOCTL_OLS_READ_IO_PORT_BYTE = new Kernel32.IOControlCode(OLS_TYPE, 0x833, Kernel32.IOControlCode.Access.Read);

        public static readonly Kernel32.IOControlCode
            IOCTL_OLS_WRITE_IO_PORT_BYTE = new Kernel32.IOControlCode(OLS_TYPE, 0x836, Kernel32.IOControlCode.Access.Write);

        public static readonly Kernel32.IOControlCode
            IOCTL_OLS_READ_PCI_CONFIG = new Kernel32.IOControlCode(OLS_TYPE, 0x851, Kernel32.IOControlCode.Access.Read);

        public static readonly Kernel32.IOControlCode
            IOCTL_OLS_WRITE_PCI_CONFIG = new Kernel32.IOControlCode(OLS_TYPE, 0x852, Kernel32.IOControlCode.Access.Write);

        public static readonly Kernel32.IOControlCode
            IOCTL_OLS_READ_MEMORY = new Kernel32.IOControlCode(OLS_TYPE, 0x841, Kernel32.IOControlCode.Access.Read);

        // Intel PCM-Memory uses different control codes
        public static readonly Kernel32.IOControlCode
            IOCTL_OLS_READ_PCI_CONFIG_PCM = new Kernel32.IOControlCode(PCM_OLS_TYPE, 0x802, Kernel32.IOControlCode.Method.Buffered, Kernel32.IOControlCode.Access.Any);

        public static readonly Kernel32.IOControlCode
            IOCTL_OLS_WRITE_PCI_CONFIG_PCM = new Kernel32.IOControlCode(PCM_OLS_TYPE, 0x803, Kernel32.IOControlCode.Method.Buffered, Kernel32.IOControlCode.Access.Any);

        public static bool IsOpen
        {
            get { return _driver != null; }
        }

        private static Assembly GetAssembly()
        {
            return typeof(Ring0).Assembly;
        }

        /// <summary>
        /// Looks for a WinRing0 driver the user has explicitly provided. When present it
        /// takes precedence over the driver bundled with the plugin, letting the user
        /// point CapFrameX at a different (e.g. self-signed or newer) WinRing0 build.
        /// Resolution order:
        ///   1. environment variable CX_PMC_WINRING0_PATH
        ///   2. a "winring0path.txt" sidecar file next to the plugin assembly, whose
        ///      single line is the full path to the .sys file.
        /// </summary>
        private static bool TryGetUserProvidedDriver(out string path)
        {
            path = null;

            try
            {
                string fromEnv = Environment.GetEnvironmentVariable("CX_PMC_WINRING0_PATH");
                if (!string.IsNullOrWhiteSpace(fromEnv) && File.Exists(fromEnv.Trim()))
                {
                    path = fromEnv.Trim();
                    return true;
                }

                string location = GetAssembly().Location;
                if (!string.IsNullOrEmpty(location))
                {
                    string dir = Path.GetDirectoryName(location);
                    if (dir != null)
                    {
                        string sidecar = Path.Combine(dir, "winring0path.txt");
                        if (File.Exists(sidecar))
                        {
                            string configured = File.ReadAllText(sidecar).Trim();
                            if (!string.IsNullOrEmpty(configured) && File.Exists(configured))
                            {
                                path = configured;
                                return true;
                            }
                        }
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        // Logical resource name of the bundled 64-bit WinRing0 driver. The plugin only
        // ships and supports the x64 build (interop is x64-only).
        private const string DriverResourceName = "PmcReader.Interop.WinRing0x64.sys";

        // Fixed file name for the driver we drop to disk. It is deliberately deterministic
        // (not random, and not derived from the host assembly): a later run must be able to
        // recognise a registration it left behind and reclaim it - ExistingServiceIsReclaimable
        // matches on the exact image path, which a random name would never match across runs.
        // The file holds the bundled WinRing0 driver; the name reflects that for clarity.
        private const string ExtractedDriverFileName = "CapFrameX_WinRing0x64.sys";

        /// <summary>
        /// Picks a writable path to drop the bundled driver onto disk so it can be
        /// installed as a kernel service. Prefers a fixed file next to the plugin assembly
        /// and falls back to the same name in the temp folder when that location is
        /// read-only. The path is deterministic across runs on purpose (see
        /// <see cref="ExtractedDriverFileName"/>).
        /// </summary>
        private static string GetTempFileName()
        {
            // Prefer a deterministic path next to the plugin assembly.
            string location = GetAssembly().Location;
            if (!string.IsNullOrEmpty(location))
            {
                try
                {
                    string dir = Path.GetDirectoryName(location);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        string fileName = Path.Combine(dir, ExtractedDriverFileName);

                        // Probe writability (and create the file so ExtractDriver can reopen it).
                        using (File.Create(fileName))
                        {
                        }

                        return fileName;
                    }
                }
                catch (Exception)
                {
                }
            }

            // Fall back to the same fixed name in the temp folder when the assembly
            // directory is read-only. Kept deterministic so reclaim still works across runs.
            try
            {
                return Path.Combine(Path.GetTempPath(), ExtractedDriverFileName);
            }
            catch (ArgumentException)
            {
                // invalid characters in the temp path
            }
            catch (NotSupportedException)
            {
                // invalid path format of the TMP system environment variable
            }

            return null;
        }

        /// <summary>
        /// Writes the WinRing0 driver embedded in this assembly to <paramref name="fileName"/>.
        /// Returns false when the resource is missing or the file could not be written.
        /// </summary>
        private static bool ExtractDriver(string fileName)
        {
            string[] names = GetAssembly().GetManifestResourceNames();
            byte[] buffer = null;
            for (int i = 0; i < names.Length; i++)
            {
                string normalized = names[i].Replace('\\', '.');
                if (string.Equals(normalized, DriverResourceName, StringComparison.OrdinalIgnoreCase)
                    || normalized.EndsWith("WinRing0x64.sys", StringComparison.OrdinalIgnoreCase))
                {
                    using (Stream stream = GetAssembly().GetManifestResourceStream(names[i]))
                    {
                        if (stream != null)
                        {
                            buffer = new byte[stream.Length];
                            stream.Read(buffer, 0, buffer.Length);
                        }
                    }

                    break;
                }
            }

            if (buffer == null)
                return false;

            try
            {
                using (FileStream target = new FileStream(fileName, FileMode.Create))
                {
                    target.Write(buffer, 0, buffer.Length);
                    target.Flush();
                }
            }
            catch (IOException)
            {
                // for example there is not enough space on the disk
                return false;
            }

            // make sure the file is actually written to the file system
            for (int i = 0; i < 20; i++)
            {
                try
                {
                    if (File.Exists(fileName) &&
                        new FileInfo(fileName).Length == buffer.Length)
                    {
                        return true;
                    }

                    Thread.Sleep(100);
                }
                catch (IOException)
                {
                    Thread.Sleep(10);
                }
            }

            // file still has not the right size, something is wrong
            return false;
        }

        /// <summary>
        /// Removes the bundled driver we extracted to disk (if any). Never touches a
        /// user-provided driver file.
        /// </summary>
        private static void CleanupExtractedDriverFile()
        {
            if (_extractedDriverPath == null)
                return;

            try
            {
                if (File.Exists(_extractedDriverPath))
                    File.Delete(_extractedDriverPath);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            _extractedDriverPath = null;
        }

        /// <summary>
        /// Installs the driver at <paramref name="path"/> as the WinRing0 service and
        /// opens its device. Marks the service as created-by-us so it is removed again on
        /// <see cref="Close"/>. Returns true only when the device is open afterwards.
        /// </summary>
        private static bool TryInstallAndOpen(string path, string source)
        {
            if (_driver.Install(path, out string installError))
            {
                _installedByUs = true;
                if (_driver.Open())
                {
                    PmcDiagnostics.Info("Ring0.Open: " + source + " WinRing0 driver installed and opened.");
                    return true;
                }

                // The service registered and started, but we cannot open its device
                // (typically a device-ACL or Memory Integrity/HVCI issue, not something a
                // retry would fix). Remove the service we just created so we never leave a
                // live kernel driver - or a registration pointing at a soon-to-be-deleted
                // .sys - behind.
                PmcDiagnostics.Log("Ring0.Open: " + source + " WinRing0 driver installed but device not openable. " + DescribeWin32Error(_driver.lastError));
                _driver.Delete();
                _installedByUs = false;
            }
            else
            {
                PmcDiagnostics.Log("Ring0.Open: install of " + source + " WinRing0 driver failed. " + installError + " " + DescribeWin32Error(_driver.lastError));
            }

            return false;
        }

        /// <summary>
        /// True only when the registered WinRing0 service points at the very driver file
        /// we are about to install - i.e. a leftover from an earlier CapFrameX run that we
        /// may safely remove and recreate. A service pointing at a different .sys belongs
        /// to another tool (or a persistent user install) and is never touched. Returns
        /// false when the service is absent or its configuration cannot be read.
        /// </summary>
        private static bool ExistingServiceIsReclaimable(string ourDriverPath)
        {
            string imagePath = _driver.TryGetServiceImagePath();
            if (string.IsNullOrEmpty(imagePath) || string.IsNullOrEmpty(ourDriverPath))
                return false;

            try
            {
                return string.Equals(
                    Path.GetFullPath(imagePath),
                    Path.GetFullPath(ourDriverPath),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string DescribeWin32Error(int error)
        {
            if (error == 0)
                return string.Empty;

            string meaning;
            switch (error)
            {
                case 2:
                    meaning = "driver file not found (a registered service points at a missing .sys, or it was removed by security software)";
                    break;
                case 5:
                    meaning = "access denied (CapFrameX must run elevated)";
                    break;
                case 577:
                    meaning = "driver signature/image hash rejected (likely blocklisted or Memory Integrity/HVCI enforced)";
                    break;
                case 1058:
                    meaning = "service is disabled";
                    break;
                case 1060:
                    meaning = "service is not installed";
                    break;
                case 1072:
                    meaning = "service is marked for deletion";
                    break;
                case 1275:
                    meaning = "driver blocked by Windows (vulnerable-driver blocklist / Memory Integrity / HVCI)";
                    break;
                default:
                    meaning = new Win32Exception(error).Message;
                    break;
            }

            return "Win32 error " + error + ": " + meaning;
        }

        public static void Open()
        {
            if (_driver != null)
                return;

            // clear the current report
            Report.Length = 0;
            _installedByUs = false;
            _extractedDriverPath = null;

            PmcDiagnostics.Log("Ring0.Open: locating WinRing0 device...");
            _driver = new KernelDriver(ServiceName);

            // 1) Device already present: a previous run, or a third-party tool such as
            //    ZenTimings / HWiNFO, registered WinRing0. As an elevated process we may
            //    open it regardless of who created it - and we must not tear it down.
            if (_driver.Open())
            {
                PmcDiagnostics.Info("Ring0.Open: using existing WinRing0 device (not owned by CapFrameX).");
                FinishOpen();
                return;
            }

            // 2) Service is registered but not running (e.g. a persistent user install or
            //    a third-party tool). Start it - this loads the on-disk driver without us
            //    dropping any binary.
            if (_driver.TryStartExistingService(out string startError))
            {
                if (_driver.Open())
                {
                    PmcDiagnostics.Info("Ring0.Open: started existing WinRing0 service and opened device.");
                    FinishOpen();
                    return;
                }

                // An existing service we did not create this run is running, but its device
                // cannot be opened (a restrictive ACL set by another tool, or Memory
                // Integrity/HVCI on the loaded image). We must never install over or tear
                // down a service we do not own, so stop here instead of falling through to
                // the install path below.
                int existingError = _driver.lastError;
                const string existingGuidance = "An existing WinRing0 service is running but its device could "
                    + "not be opened. It may be owned by another tool (e.g. HWiNFO / ZenTimings) that restricts "
                    + "access, or be blocked by Memory Integrity / HVCI. CapFrameX will not replace a service it "
                    + "did not create.";
                Report.AppendLine("Status: " + existingGuidance);
                string existingDetail = DescribeWin32Error(existingError);
                if (existingDetail.Length > 0)
                    Report.AppendLine("Detail: " + existingDetail);

                PmcDiagnostics.Warning("Ring0.Open: " + existingGuidance + (existingDetail.Length > 0 ? " (" + existingDetail + ")" : string.Empty));
                _driver = null;
                return;
            }
            else if (startError != null)
            {
                PmcDiagnostics.Log("Ring0.Open: could not start existing WinRing0 service. " + startError);
            }

            // 3) Install a WinRing0 driver that CapFrameX manages itself. A user-provided
            //    path wins (lets the user swap in a different/newer signed build);
            //    otherwise the driver bundled with the plugin is extracted and installed.
            string driverPath;
            string driverSource;

            if (TryGetUserProvidedDriver(out string userDriverPath))
            {
                driverPath = userDriverPath;
                driverSource = "user-provided";
                PmcDiagnostics.Info("Ring0.Open: installing user-provided WinRing0 driver \"" + userDriverPath + "\".");
            }
            else
            {
                _extractedDriverPath = GetTempFileName();
                if (_extractedDriverPath != null && ExtractDriver(_extractedDriverPath))
                {
                    driverPath = _extractedDriverPath;
                    driverSource = "bundled";
                    PmcDiagnostics.Info("Ring0.Open: installing bundled WinRing0 driver (extracted to \"" + _extractedDriverPath + "\").");
                }
                else
                {
                    driverPath = null;
                    driverSource = null;
                    PmcDiagnostics.Warning("Ring0.Open: could not extract the bundled WinRing0 driver.");
                    CleanupExtractedDriverFile();
                }
            }

            if (driverPath != null)
            {
                if (TryInstallAndOpen(driverPath, driverSource))
                {
                    // Keep the extracted .sys on disk: the service's image path points at
                    // it. It is removed in Close() when we tear the service down.
                    FinishOpen();
                    return;
                }

                // The install may have failed because a registration left by an earlier
                // CapFrameX run still occupies our service name. Reclaim it ONLY when it
                // points at our own driver file - never a service owned by another tool -
                // then try once more.
                if (!_installedByUs && ExistingServiceIsReclaimable(driverPath))
                {
                    PmcDiagnostics.Info("Ring0.Open: reclaiming a stale CapFrameX WinRing0 registration and retrying.");
                    _driver.Delete();
                    Thread.Sleep(2000);

                    if (TryInstallAndOpen(driverPath, driverSource))
                    {
                        FinishOpen();
                        return;
                    }
                }
            }

            // Nothing worked. CapFrameX bundles the driver and installs it automatically,
            // so a failure here means Windows refused to load it. Defensively tear down any
            // service we created but could not open (TryInstallAndOpen already does this)
            // before removing the extracted file, so we never leave a live driver behind.
            int lastError = _driver.lastError;
            if (_installedByUs)
                _driver.Delete();
            CleanupExtractedDriverFile();

            const string guidance = "WinRing0 kernel driver could not be loaded. CapFrameX bundles the driver "
                + "and installs it automatically, so this usually means CapFrameX is not running elevated, or "
                + "Memory Integrity / HVCI / Windows' vulnerable-driver blocklist is blocking WinRing0. Run "
                + "CapFrameX as administrator and, if necessary, disable Memory Integrity to enable PMC sensors.";
            Report.AppendLine("Status: " + guidance);
            string lastErrorText = DescribeWin32Error(lastError);
            if (lastErrorText.Length > 0)
                Report.AppendLine("Detail: " + lastErrorText);

            PmcDiagnostics.Warning("Ring0.Open: " + guidance + (lastErrorText.Length > 0 ? " (" + lastErrorText + ")" : string.Empty));

            _driver = null;
        }

        private static void FinishOpen()
        {
            const string isaMutexName = "Global\\Access_ISABUS.HTP.Method";
            TryCreateOrOpenExistingMutex(isaMutexName, out _isaBusMutex);

            const string pciMutexName = "Global\\Access_PCI";
            TryCreateOrOpenExistingMutex(pciMutexName, out _pciBusMutex);
        }

        private static bool TryCreateOrOpenExistingMutex(string name, out Mutex mutex)
        {
#if NETFRAMEWORK
            MutexSecurity mutexSecurity = new();
            SecurityIdentifier identity = new(WellKnownSidType.WorldSid, null);
            mutexSecurity.AddAccessRule(new MutexAccessRule(identity, MutexRights.Synchronize | MutexRights.Modify, AccessControlType.Allow));

            try
            {
                // If the CreateMutex call fails, the framework will attempt to use OpenMutex
                // to open the named mutex requesting SYNCHRONIZE and MUTEX_MODIFY rights.
                mutex = new Mutex(false, name, out _, mutexSecurity);
                return true;
            }
            catch
            {
                // WaitHandleCannotBeOpenedException:
                // The mutex cannot be opened, probably because a Win32 object of a different type with the same name already exists.

                // UnauthorizedAccessException:
                // The mutex exists, but the current process or thread token does not have permission to open the mutex with SYNCHRONIZE | MUTEX_MODIFY rights.
                mutex = null;
                return false;
            }
#else
            try
            {
                mutex = new Mutex(false, name);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                try
                {
                    mutex = Mutex.OpenExisting(name);
                    return true;
                }
                catch { }

                mutex = null;
            }
            return false;
#endif
        }

        public static void Close()
        {
            if (_driver != null)
            {
                if (_installedByUs)
                {
                    // We created the service: honour the WinRing0 refcount and remove it
                    // only when we are the last user.
                    uint refCount = 0;
                    _driver.DeviceIOControl(Interop.Ring0.IOCTL_OLS_GET_REFCOUNT, null, ref refCount);
                    _driver.Close();

                    if (refCount <= 1)
                    {
                        _driver.Delete();

                        // Service gone: the bundled .sys we extracted is no longer
                        // referenced, so drop it from disk too.
                        CleanupExtractedDriverFile();
                    }
                }
                else
                {
                    // Third-party or persistent user installation: just release our handle,
                    // never delete a service we did not create (avoids racing ZenTimings etc.).
                    _driver.Close();
                }

                _driver = null;
                _installedByUs = false;
            }

            if (_isaBusMutex != null)
            {
                _isaBusMutex.Close();
                _isaBusMutex = null;
            }

            if (_pciBusMutex != null)
            {
                _pciBusMutex.Close();
                _pciBusMutex = null;
            }
        }

        public static ulong ThreadAffinitySet(ulong mask)
        {
            return ThreadAffinity.Set(mask);
        }

        public static string GetReport()
        {
            if (Report.Length > 0)
            {
                StringBuilder r = new StringBuilder();
                r.AppendLine("Ring0");
                r.AppendLine();
                r.Append(Report);
                r.AppendLine();
                return r.ToString();
            }

            return null;
        }

        public static bool WaitIsaBusMutex(int millisecondsTimeout)
        {
            if (_isaBusMutex == null)
                return true;


            try
            {
                return _isaBusMutex.WaitOne(millisecondsTimeout, false);
            }
            catch (AbandonedMutexException)
            {
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        public static void ReleaseIsaBusMutex()
        {
            _isaBusMutex?.ReleaseMutex();
        }

        /// <summary>
        /// Wait for a signal on the PCI bus mutex
        /// </summary>
        /// <param name="millisecondsTimeout"></param>
        /// <returns></returns>
        public static bool WaitPciBusMutex(int millisecondsTimeout)
        {
            if (_pciBusMutex == null)
                return true;

            try
            {
                // WaitOne waits to acquire a mutex
                return _pciBusMutex.WaitOne(millisecondsTimeout, false);
            }
            catch (AbandonedMutexException)
            {
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        /// <summary>
        /// Releases the PCI bus mutex
        /// </summary>
        public static void ReleasePciBusMutex()
        {
            _pciBusMutex?.ReleaseMutex();
        }

        public static bool ReadMsr(uint index, out ulong value)
        {
            value = 0;
            if (_driver == null)
            {
                return false;
            }

            bool result = _driver.DeviceIOControl(Interop.Ring0.IOCTL_OLS_READ_MSR, index, ref value);
            return result;
        }

        public static bool ReadMsr(uint index, out ulong value, ulong threadAffinityMask)
        {
            ulong mask = ThreadAffinity.Set(threadAffinityMask);
            bool result = ReadMsr(index, out value);
            ThreadAffinity.Set(mask);
            return result;
        }

        public static bool WriteMsr(uint index, ulong value, ulong threadAffinityMask)
        {
            ulong mask = ThreadAffinity.Set(threadAffinityMask);
            bool result = WriteMsr(index, value);
            ThreadAffinity.Set(mask);
            return result;
        }

        private static bool _writeMsrDiagLogged;

        public static bool WriteMsr(uint index, ulong value)
        {
            if (_driver == null)
            {
                if (!_writeMsrDiagLogged)
                {
                    PmcDiagnostics.Warning("WriteMsr: _driver is NULL - Ring0 driver not loaded!");
                    _writeMsrDiagLogged = true;
                }
                return false;
            }

            WriteMsrInput input = new WriteMsrInput { Register = index, Value = value };
            bool result = _driver.DeviceIOControl(Interop.Ring0.IOCTL_OLS_WRITE_MSR, input);
            if (!result && !_writeMsrDiagLogged)
            {
                PmcDiagnostics.Warning("WriteMsr: IOCTL FAILED for MSR 0x{0:X} (driver IS open, IOCTL returned false)", index);
                _writeMsrDiagLogged = true;
            }
            return result;
        }

        public static byte ReadIoPort(uint port)
        {
            if (_driver == null)
                return 0;


            uint value = 0;
            _driver.DeviceIOControl(Interop.Ring0.IOCTL_OLS_READ_IO_PORT_BYTE, port, ref value);
            return (byte)(value & 0xFF);
        }

        public static void WriteIoPort(uint port, byte value)
        {
            if (_driver == null)
                return;


            WriteIoPortInput input = new WriteIoPortInput { PortNumber = port, Value = value };
            _driver.DeviceIOControl(Interop.Ring0.IOCTL_OLS_WRITE_IO_PORT_BYTE, input);
        }

        public static uint GetPciAddress(byte bus, byte device, byte function)
        {
            return (uint)(((bus & 0xFF) << 8) | ((device & 0x1F) << 3) | (function & 7));
        }

        public static bool ReadPciConfig(uint pciAddress, uint regAddress, out uint value)
        {
            if (_driver == null || (regAddress & 3) != 0)
            {
                value = 0;
                return false;
            }

            ReadPciConfigInput input = new ReadPciConfigInput { PciAddress = pciAddress, RegAddress = regAddress };

            value = 0;
            return _driver.DeviceIOControl(Interop.Ring0.IOCTL_OLS_READ_PCI_CONFIG, input, ref value);
        }

        public static bool WritePciConfig(uint pciAddress, uint regAddress, uint value)
        {
            if (_driver == null || (regAddress & 3) != 0)
                return false;


            WritePciConfigInput input = new WritePciConfigInput { PciAddress = pciAddress, RegAddress = regAddress, Value = value };
            return _driver.DeviceIOControl(Interop.Ring0.IOCTL_OLS_WRITE_PCI_CONFIG, input);
        }

        public static bool WritePciConfigPcm(uint pciAddress, uint regAddress, uint value)
        {
            if (_driver == null || (regAddress & 3) != 0)
                return false;


            WritePciConfigInput input = new WritePciConfigInput { PciAddress = pciAddress, RegAddress = regAddress, Value = value };
            return _driver.DeviceIOControl(Interop.Ring0.IOCTL_OLS_WRITE_PCI_CONFIG_PCM, input);
        }

        public static bool ReadMemory<T>(ulong address, ref T buffer)
        {
            if (_driver == null)
                return false;


            ReadMemoryInput input = new ReadMemoryInput { Address = address, UnitSize = 1, Count = (uint)Marshal.SizeOf(buffer) };
            return _driver.DeviceIOControl(Interop.Ring0.IOCTL_OLS_READ_MEMORY, input, ref buffer);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct WriteMsrInput
        {
            public uint Register;
            public ulong Value;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct WriteIoPortInput
        {
            public uint PortNumber;
            public byte Value;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct ReadPciConfigInput
        {
            public uint PciAddress;
            public uint RegAddress;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct WritePciConfigInput
        {
            public uint PciAddress;
            public uint RegAddress;
            public uint Value;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct ReadMemoryInput
        {
            public ulong Address;
            public uint UnitSize;
            public uint Count;
        }
    }
}
