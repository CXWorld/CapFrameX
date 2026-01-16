// From LibreHardwareMonitor, with some modifications
// Mozilla Public License 2.0
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors
// All Rights Reserved

using System;
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
        private static KernelDriver _driver;
        private static string _fileName;
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

        private static string GetTempFileName()
        {
            // try to create one in the application folder
            string location = GetAssembly().Location;
            if (!string.IsNullOrEmpty(location))
            {
                try
                {
                    string fileName = Path.ChangeExtension(location, ".sys");

                    using (File.Create(fileName))
                        return fileName;
                }
                catch (Exception)
                { }
            }

            // if this failed, try to get a file in the temporary folder
            try
            {
                return Path.GetTempFileName();
            }
            catch (IOException)
            {
                // some I/O exception
            }
            catch (UnauthorizedAccessException)
            {
                // we do not have the right to create a file in the temp folder
            }
            catch (NotSupportedException)
            {
                // invalid path format of the TMP system environment variable
            }

            return null;
        }

        private static bool ExtractDriver(string fileName)
        {
            string resourceName = nameof(PmcReader) + "." + nameof(Interop) + "." + "WinRing0x64.sys";

            string[] names = GetAssembly().GetManifestResourceNames();
            byte[] buffer = null;
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i].Replace('\\', '.') == resourceName)
                {
                    using (Stream stream = GetAssembly().GetManifestResourceStream(names[i]))
                    {
                        if (stream != null)
                        {
                            buffer = new byte[stream.Length];
                            stream.Read(buffer, 0, buffer.Length);
                        }
                    }
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

        public static void Open()
        {
            if (_driver != null)
                return;

            // clear the current report
            Report.Length = 0;

            _driver = new KernelDriver("WinRing0_1_2_0");
            _driver.Open();

            if (!_driver.IsOpen)
            {
                // driver is not loaded, try to install and open
                _fileName = GetTempFileName();
                if (_fileName != null && ExtractDriver(_fileName))
                {
                    if (_driver.Install(_fileName, out string installError))
                    {
                        _driver.Open();

                        if (!_driver.IsOpen)
                        {
                            _driver.Delete();
                            Report.AppendLine("Status: Opening driver failed after install");
                        }
                    }
                    else
                    {
                        string errorFirstInstall = installError;

                        // install failed, try to delete and reinstall
                        _driver.Delete();

                        // wait a short moment to give the OS a chance to remove the driver
                        Thread.Sleep(2000);

                        if (_driver.Install(_fileName, out string errorSecondInstall))
                        {
                            _driver.Open();

                            if (!_driver.IsOpen)
                            {
                                _driver.Delete();
                                Report.AppendLine("Status: Opening driver failed after reinstall");
                            }
                        }
                        else
                        {
                            Report.AppendLine("Status: Installing driver \"" + _fileName + "\" failed" + (File.Exists(_fileName) ? " and file exists" : string.Empty));
                            Report.AppendLine("First Exception: " + errorFirstInstall);
                            Report.AppendLine("Second Exception: " + errorSecondInstall);
                        }
                    }
                }
                else
                {
                    Report.AppendLine("Status: Extracting driver failed");
                }

                try
                {
                    // try to delete the driver file
                    if (File.Exists(_fileName) && _fileName != null)
                        File.Delete(_fileName);

                    _fileName = null;
                }
                catch (IOException)
                { }
                catch (UnauthorizedAccessException)
                { }
            }

            if (!_driver.IsOpen)
                _driver = null;


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
                uint refCount = 0;
                _driver.DeviceIOControl(Interop.Ring0.IOCTL_OLS_GET_REFCOUNT, null, ref refCount);
                _driver.Close();

                if (refCount <= 1)
                    _driver.Delete();

                _driver = null;
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

            // try to delete temporary driver file again if failed during open
            if (_fileName != null && File.Exists(_fileName))
            {
                try
                {
                    File.Delete(_fileName);
                    _fileName = null;
                }
                catch (IOException)
                { }
                catch (UnauthorizedAccessException)
                { }
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

        public static bool WriteMsr(uint index, ulong value)
        {
            if (_driver == null)
                return false;

            WriteMsrInput input = new WriteMsrInput { Register = index, Value = value };
            return _driver.DeviceIOControl(Interop.Ring0.IOCTL_OLS_WRITE_MSR, input);
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
