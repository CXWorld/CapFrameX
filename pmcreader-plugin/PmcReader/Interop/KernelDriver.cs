// From LibreHardwareMonitor
// Mozilla Public License 2.0
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors
// All Rights Reserved

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace PmcReader.Interop
{
    internal class KernelDriver
    {
        // Raw Win32 error codes (Marshal.GetLastWin32Error()).
        private const int ERROR_SERVICE_ALREADY_RUNNING = 1056;
        private const int ERROR_SERVICE_MARKED_FOR_DELETE = 1072;
        private const int ERROR_SERVICE_EXISTS = 1073;
        private const int ERROR_SERVICE_DOES_NOT_EXIST = 1060;

        private const uint SERVICE_STOPPED = 1;
        private const uint SERVICE_STOP_PENDING = 3;
        private const uint SERVICE_RUNNING = 4;

        private readonly string _id;
        private SafeFileHandle _device;

        public int lastError;

        public KernelDriver(string id)
        {
            _id = id;
        }

        public bool IsOpen
        {
            get { return _device != null; }
        }

        /// <summary>
        /// True only when this instance created the service itself and is therefore
        /// allowed to remove it again on close. Stays false when we merely opened a
        /// device or started a service that a third-party tool (e.g. ZenTimings) or a
        /// persistent user installation provided - those must never be torn down by us.
        /// </summary>
        public bool CreatedByUs { get; private set; }

        /// <summary>
        /// Creates and starts the driver service from the given .sys file. Used both for
        /// the WinRing0 driver bundled with the plugin and for a user-provided override.
        /// </summary>
        public bool Install(string path, out string errorMessage)
        {
            CreatedByUs = false;
            lastError = 0;

            // Least privilege: we only need to connect and create a service.
            IntPtr manager = AdvApi32.OpenSCManager(null, null,
                AdvApi32.SC_MANAGER_ACCESS_MASK.SC_MANAGER_CONNECT | AdvApi32.SC_MANAGER_ACCESS_MASK.SC_MANAGER_CREATE_SERVICE);
            if (manager == IntPtr.Zero)
            {
                lastError = Marshal.GetLastWin32Error();
                errorMessage = "OpenSCManager failed (are you running elevated?).";
                return false;
            }

            try
            {
                // Kernel drivers expect an NT path, e.g. \??\C:\path\to\driver.sys
                string ntPath = path;
                if (!path.StartsWith(@"\??\", StringComparison.OrdinalIgnoreCase))
                    ntPath = @"\??\" + path;

                IntPtr service = CreateServiceWithRetry(manager, ntPath, out int createError);
                if (service == IntPtr.Zero)
                {
                    lastError = createError;
                    errorMessage = createError == ERROR_SERVICE_EXISTS
                        ? "Service already exists"
                        : "CreateService failed: " + new Win32Exception(createError).Message;
                    return false;
                }

                try
                {
                    if (!AdvApi32.StartService(service, 0, null))
                    {
                        int startError = Marshal.GetLastWin32Error();
                        if (startError != ERROR_SERVICE_ALREADY_RUNNING)
                        {
                            lastError = startError;
                            errorMessage = "StartService failed: " + new Win32Exception(startError).Message;

                            // We created this service; remove the half-finished registration
                            // so we don't leave junk (or a FAILED-1060-style ghost) behind.
                            AdvApi32.DeleteService(service);
                            return false;
                        }
                    }

                    CreatedByUs = true;
                    SetDeviceSecurity();
                    errorMessage = null;
                    return true;
                }
                finally
                {
                    AdvApi32.CloseServiceHandle(service);
                }
            }
            finally
            {
                AdvApi32.CloseServiceHandle(manager);
            }
        }

        /// <summary>
        /// Opens an already-registered driver service and starts it if necessary.
        /// Never creates a service and never sets <see cref="CreatedByUs"/>, so an
        /// existing (user- or third-party-installed) service is left intact.
        /// Returns false (with <paramref name="errorMessage"/> = null) when no such
        /// service exists.
        /// </summary>
        public bool TryStartExistingService(out string errorMessage)
        {
            errorMessage = null;
            lastError = 0;

            IntPtr manager = AdvApi32.OpenSCManager(null, null, AdvApi32.SC_MANAGER_ACCESS_MASK.SC_MANAGER_CONNECT);
            if (manager == IntPtr.Zero)
            {
                lastError = Marshal.GetLastWin32Error();
                errorMessage = "OpenSCManager failed: " + new Win32Exception(lastError).Message;
                return false;
            }

            try
            {
                IntPtr service = AdvApi32.OpenService(manager, _id,
                    AdvApi32.SERVICE_ACCESS_MASK.SERVICE_QUERY_STATUS | AdvApi32.SERVICE_ACCESS_MASK.SERVICE_START);
                if (service == IntPtr.Zero)
                {
                    lastError = Marshal.GetLastWin32Error();
                    if (lastError != ERROR_SERVICE_DOES_NOT_EXIST)
                        errorMessage = "OpenService failed: " + new Win32Exception(lastError).Message;
                    return false;
                }

                try
                {
                    if (!TryQueryState(service, out uint state))
                    {
                        lastError = Marshal.GetLastWin32Error();
                        errorMessage = "QueryServiceStatus failed: " + new Win32Exception(lastError).Message;
                        return false;
                    }

                    if (state == SERVICE_RUNNING)
                        return true;

                    if (state == SERVICE_STOP_PENDING)
                        WaitForState(service, SERVICE_STOPPED, 5000);

                    if (!AdvApi32.StartService(service, 0, null))
                    {
                        int startError = Marshal.GetLastWin32Error();
                        if (startError == ERROR_SERVICE_ALREADY_RUNNING)
                            return true;

                        lastError = startError;
                        errorMessage = "StartService failed: " + new Win32Exception(startError).Message;
                        return false;
                    }

                    if (WaitForState(service, SERVICE_RUNNING, 10000))
                        return true;

                    errorMessage = "Service did not reach the running state.";
                    return false;
                }
                finally
                {
                    AdvApi32.CloseServiceHandle(service);
                }
            }
            finally
            {
                AdvApi32.CloseServiceHandle(manager);
            }
        }

        /// <summary>
        /// Returns the on-disk driver path (ImagePath) registered for this service, with
        /// any leading NT "\??\" prefix stripped, or null when the service does not exist
        /// or its configuration cannot be read. Used to decide whether an existing
        /// registration is a leftover of ours (safe to reclaim) or belongs to another tool.
        /// </summary>
        public string TryGetServiceImagePath()
        {
            IntPtr manager = AdvApi32.OpenSCManager(null, null, AdvApi32.SC_MANAGER_ACCESS_MASK.SC_MANAGER_CONNECT);
            if (manager == IntPtr.Zero)
                return null;

            try
            {
                IntPtr service = AdvApi32.OpenService(manager, _id, AdvApi32.SERVICE_ACCESS_MASK.SERVICE_QUERY_CONFIG);
                if (service == IntPtr.Zero)
                    return null;

                try
                {
                    // First call sizes the buffer (fails with ERROR_INSUFFICIENT_BUFFER).
                    AdvApi32.QueryServiceConfig(service, IntPtr.Zero, 0, out uint bytesNeeded);
                    if (bytesNeeded == 0)
                        return null;

                    IntPtr buffer = Marshal.AllocHGlobal((int)bytesNeeded);
                    try
                    {
                        if (!AdvApi32.QueryServiceConfig(service, buffer, bytesNeeded, out _))
                            return null;

                        AdvApi32.QUERY_SERVICE_CONFIG config =
                            (AdvApi32.QUERY_SERVICE_CONFIG)Marshal.PtrToStructure(buffer, typeof(AdvApi32.QUERY_SERVICE_CONFIG));

                        string imagePath = config.lpBinaryPathName;
                        if (string.IsNullOrEmpty(imagePath))
                            return null;

                        if (imagePath.StartsWith(@"\??\", StringComparison.OrdinalIgnoreCase))
                            imagePath = imagePath.Substring(4);

                        return imagePath;
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(buffer);
                    }
                }
                finally
                {
                    AdvApi32.CloseServiceHandle(service);
                }
            }
            finally
            {
                AdvApi32.CloseServiceHandle(manager);
            }
        }

        public bool Open()
        {
            IntPtr rawHandle = Kernel32.CreateFile(@"\\.\" + _id, 0xC0000000, FileShare.ReadWrite, IntPtr.Zero, FileMode.Open, FileAttributes.Normal, IntPtr.Zero);
            int error = Marshal.GetLastWin32Error();

            _device = new SafeFileHandle(rawHandle, true);
            if (_device.IsInvalid)
            {
                lastError = error;
                _device.Close();
                _device.Dispose();
                _device = null;
            }

            return _device != null;
        }

        public bool DeviceIOControl(Kernel32.IOControlCode ioControlCode, object inBuffer)
        {
            if (_device == null)
                return false;


            bool b = Kernel32.DeviceIoControl(_device, ioControlCode, inBuffer, inBuffer == null ? 0 : (uint)Marshal.SizeOf(inBuffer), null, 0, out uint _, IntPtr.Zero);
            return b;
        }

        public bool DeviceIOControl<T>(Kernel32.IOControlCode ioControlCode, object inBuffer, ref T outBuffer)
        {
            if (_device == null)
                return false;


            object boxedOutBuffer = outBuffer;
            bool b = Kernel32.DeviceIoControl(_device,
                                              ioControlCode,
                                              inBuffer,
                                              inBuffer == null ? 0 : (uint)Marshal.SizeOf(inBuffer),
                                              boxedOutBuffer,
                                              (uint)Marshal.SizeOf(boxedOutBuffer),
                                              out uint _,
                                              IntPtr.Zero);

            if (!b)
            {
                int error = Marshal.GetLastWin32Error();
                lastError = error;
            }

            outBuffer = (T)boxedOutBuffer;
            return b;
        }

        public void Close()
        {
            if (_device != null)
            {
                _device.Close();
                _device.Dispose();
                _device = null;
            }
        }

        public bool Delete()
        {
            IntPtr manager = AdvApi32.OpenSCManager(null, null, AdvApi32.SC_MANAGER_ACCESS_MASK.SC_MANAGER_CONNECT);
            if (manager == IntPtr.Zero)
                return false;


            IntPtr service = AdvApi32.OpenService(manager, _id, AdvApi32.SERVICE_ACCESS_MASK.SERVICE_ALL_ACCESS);
            if (service == IntPtr.Zero)
            {
                AdvApi32.CloseServiceHandle(manager);
                return true;
            }


            AdvApi32.SERVICE_STATUS status = new AdvApi32.SERVICE_STATUS();
            AdvApi32.ControlService(service, AdvApi32.SERVICE_CONTROL.SERVICE_CONTROL_STOP, ref status);
            AdvApi32.DeleteService(service);
            AdvApi32.CloseServiceHandle(service);
            AdvApi32.CloseServiceHandle(manager);

            return true;
        }

        private IntPtr CreateServiceWithRetry(IntPtr manager, string ntPath, out int error)
        {
            error = 0;
            var sw = Stopwatch.StartNew();

            while (true)
            {
                IntPtr service = AdvApi32.CreateService(manager,
                                                        _id,
                                                        _id,
                                                        AdvApi32.SERVICE_ACCESS_MASK.SERVICE_ALL_ACCESS,
                                                        AdvApi32.SERVICE_TYPE.SERVICE_KERNEL_DRIVER,
                                                        AdvApi32.SERVICE_START.SERVICE_DEMAND_START,
                                                        AdvApi32.SERVICE_ERROR.SERVICE_ERROR_NORMAL,
                                                        ntPath,
                                                        null,
                                                        null,
                                                        null,
                                                        null,
                                                        null);

                if (service != IntPtr.Zero)
                    return service;

                error = Marshal.GetLastWin32Error();

                // A just-deleted service can linger as "marked for delete" for a moment.
                if (error == ERROR_SERVICE_MARKED_FOR_DELETE && sw.ElapsedMilliseconds < 5000)
                {
                    Thread.Sleep(200);
                    continue;
                }

                return IntPtr.Zero;
            }
        }

        private void SetDeviceSecurity()
        {
#if !NETSTANDARD2_0
            try
            {
                // restrict the driver access to system (SY) and builtin admins (BA)
                var deviceFileInfo = new FileInfo(@"\\.\" + _id);
                FileSecurity fileSecurity = deviceFileInfo.GetAccessControl();
                fileSecurity.SetSecurityDescriptorSddlForm("O:BAG:SYD:(A;;FA;;;SY)(A;;FA;;;BA)");
                deviceFileInfo.SetAccessControl(fileSecurity);
            }
            catch
            {
            }
#endif
        }

        private static bool TryQueryState(IntPtr service, out uint state)
        {
            state = 0;
            AdvApi32.SERVICE_STATUS status = new AdvApi32.SERVICE_STATUS();
            if (!AdvApi32.QueryServiceStatus(service, ref status))
                return false;

            state = status.dwCurrentState;
            return true;
        }

        private static bool WaitForState(IntPtr service, uint desiredState, int timeoutMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (!TryQueryState(service, out uint state))
                    return false;

                if (state == desiredState)
                    return true;

                Thread.Sleep(100);
            }

            return false;
        }
    }
}
