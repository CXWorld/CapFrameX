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
        /// Looks for a WinRing0 driver the user has explicitly provided (opt-in). The
        /// bundled driver is on Microsoft's vulnerable-driver blocklist and is never
        /// dropped/installed automatically; only a path the user points us at is used,
        /// at the user's own risk. Resolution order:
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

            // 2) Service is registered but not running (e.g. a persistent user install).
            //    Start it - this loads the on-disk driver without us dropping any binary.
            if (_driver.TryStartExistingService(out string startError))
            {
                if (_driver.Open())
                {
                    PmcDiagnostics.Info("Ring0.Open: started existing WinRing0 service and opened device.");
                    FinishOpen();
                    return;
                }

                PmcDiagnostics.Log("Ring0.Open: WinRing0 service started but device could not be opened. " + DescribeWin32Error(_driver.lastError));
            }
            else if (startError != null)
            {
                PmcDiagnostics.Log("Ring0.Open: could not start existing WinRing0 service. " + startError);
            }

            // 3) Opt-in only: install a user-provided WinRing0 driver (own risk). The
            //    bundled blocklisted driver is never used here.
            if (TryGetUserProvidedDriver(out string userDriverPath))
            {
                PmcDiagnostics.Info("Ring0.Open: installing user-provided WinRing0 driver \"" + userDriverPath + "\" (at user's own risk).");
                if (_driver.Install(userDriverPath, out string installError))
                {
                    _installedByUs = true;
                    if (_driver.Open())
                    {
                        PmcDiagnostics.Info("Ring0.Open: user-provided WinRing0 driver installed and opened.");
                        FinishOpen();
                        return;
                    }

                    PmcDiagnostics.Log("Ring0.Open: user-provided driver installed but device not openable. " + DescribeWin32Error(_driver.lastError));
                }
                else
                {
                    PmcDiagnostics.Log("Ring0.Open: install of user-provided WinRing0 driver failed. " + installError + " " + DescribeWin32Error(_driver.lastError));
                }
            }

            // Nothing worked: WinRing0 is unavailable, PMC sensors stay disabled.
            const string guidance = "WinRing0 kernel driver is not available. The driver is on Microsoft's "
                + "vulnerable-driver blocklist and is therefore not shipped or auto-installed by CapFrameX. "
                + "Install WinRing0 yourself (at your own risk) to enable PMC sensors.";
            Report.AppendLine("Status: " + guidance);
            string lastErrorText = DescribeWin32Error(_driver.lastError);
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
                        _driver.Delete();
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
