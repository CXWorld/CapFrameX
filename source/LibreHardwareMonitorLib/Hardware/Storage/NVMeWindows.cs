// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using LibreHardwareMonitor.Interop;
using Microsoft.Win32.SafeHandles;
using Serilog;
using Windows.Win32;
using Windows.Win32.Storage.FileSystem;
using Windows.Win32.Storage.Nvme;
using Windows.Win32.System.Ioctl;

namespace LibreHardwareMonitor.Hardware.Storage;

internal sealed class NVMeWindows : INVMeDrive
{
    private const int CancelCompletionWaitMilliseconds = 250;
    private const int ErrorIoPending = 997;
    private const int IoTimeoutMilliseconds = 1000;

    private readonly string _deviceId;
    private int _ioPending;

    public NVMeWindows(string deviceId)
    {
        _deviceId = deviceId;
    }

    // Windows generic driver NVMe access.
    public SafeHandle Identify(StorageInfo storageInfo)
    {
        return IdentifyDevice(storageInfo);
    }

    public unsafe bool IdentifyController(SafeHandle hDevice, out NVME_IDENTIFY_CONTROLLER_DATA data)
    {
        data = new NVME_IDENTIFY_CONTROLLER_DATA();
        if (hDevice?.IsInvalid != false)
            return false;

        int cb = sizeof(STORAGE_PROPERTY_QUERY) + sizeof(STORAGE_PROTOCOL_SPECIFIC_DATA) + sizeof(NVME_IDENTIFY_CONTROLLER_DATA);
        NVME_IDENTIFY_CONTROLLER_DATA resultData = new();

        bool result = ExecuteIoControl(
            hDevice,
            cb,
            "identify controller",
            ptr =>
            {
                STORAGE_PROPERTY_QUERY* query = (STORAGE_PROPERTY_QUERY*)ptr;
                query->PropertyId = STORAGE_PROPERTY_ID.StorageAdapterProtocolSpecificProperty;
                query->QueryType = STORAGE_QUERY_TYPE.PropertyStandardQuery;

                STORAGE_PROTOCOL_SPECIFIC_DATA* protocolData = (STORAGE_PROTOCOL_SPECIFIC_DATA*)(&query->AdditionalParameters);
                protocolData->ProtocolType = STORAGE_PROTOCOL_TYPE.ProtocolTypeNvme;
                protocolData->DataType = (uint)STORAGE_PROTOCOL_NVME_DATA_TYPE.NVMeDataTypeIdentify;
                protocolData->ProtocolDataRequestValue = 1;
                protocolData->ProtocolDataOffset = (uint)sizeof(STORAGE_PROTOCOL_SPECIFIC_DATA);
                protocolData->ProtocolDataLength = (uint)sizeof(NVME_IDENTIFY_CONTROLLER_DATA);
            },
            ptr =>
            {
                var dataDescriptor = (STORAGE_PROTOCOL_DATA_DESCRIPTOR*)ptr;
                STORAGE_PROTOCOL_SPECIFIC_DATA* protocolData = &dataDescriptor->ProtocolSpecificData;
                resultData = *(NVME_IDENTIFY_CONTROLLER_DATA*)((byte*)protocolData + protocolData->ProtocolDataOffset);
            });

        data = resultData;
        return result;
    }

    public unsafe bool HealthInfoLog(SafeHandle hDevice, out NVME_HEALTH_INFO_LOG data)
    {
        data = new NVME_HEALTH_INFO_LOG();
        if (hDevice?.IsInvalid != false)
            return false;

        int cb = sizeof(STORAGE_PROPERTY_QUERY) + sizeof(STORAGE_PROTOCOL_SPECIFIC_DATA) + sizeof(NVME_HEALTH_INFO_LOG);
        NVME_HEALTH_INFO_LOG resultData = new();

        bool result = ExecuteIoControl(
            hDevice,
            cb,
            "read health log",
            ptr =>
            {
                STORAGE_PROPERTY_QUERY* query = (STORAGE_PROPERTY_QUERY*)ptr;
                query->PropertyId = STORAGE_PROPERTY_ID.StorageAdapterProtocolSpecificProperty;
                query->QueryType = STORAGE_QUERY_TYPE.PropertyStandardQuery;

                STORAGE_PROTOCOL_SPECIFIC_DATA* protocolData = (STORAGE_PROTOCOL_SPECIFIC_DATA*)(&query->AdditionalParameters);
                protocolData->ProtocolType = STORAGE_PROTOCOL_TYPE.ProtocolTypeNvme;
                protocolData->DataType = (uint)STORAGE_PROTOCOL_NVME_DATA_TYPE.NVMeDataTypeLogPage;
                protocolData->ProtocolDataRequestValue = (uint)NVME_LOG_PAGES.NVME_LOG_PAGE_HEALTH_INFO;
                protocolData->ProtocolDataOffset = (uint)sizeof(STORAGE_PROTOCOL_SPECIFIC_DATA);
                protocolData->ProtocolDataLength = (uint)sizeof(NVME_HEALTH_INFO_LOG);
            },
            ptr =>
            {
                var dataDescriptor = (STORAGE_PROTOCOL_DATA_DESCRIPTOR*)ptr;
                STORAGE_PROTOCOL_SPECIFIC_DATA* protocolData = &dataDescriptor->ProtocolSpecificData;
                resultData = *(NVME_HEALTH_INFO_LOG*)((byte*)protocolData + protocolData->ProtocolDataOffset);
            });

        data = resultData;
        return result;
    }

    public static SafeHandle IdentifyDevice(StorageInfo storageInfo)
    {
        SafeFileHandle handle = PInvoke.CreateFile(
            storageInfo.DeviceId,
            (uint)FileAccess.ReadWrite,
            FILE_SHARE_MODE.FILE_SHARE_READ | FILE_SHARE_MODE.FILE_SHARE_WRITE,
            null,
            FILE_CREATION_DISPOSITION.OPEN_EXISTING,
            FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_NORMAL | FILE_FLAGS_AND_ATTRIBUTES.FILE_FLAG_OVERLAPPED,
            null);
        if (handle?.IsInvalid != false)
            return null;

        var nvme = new NVMeWindows(storageInfo.DeviceId);
        if (nvme.IdentifyController(handle, out _))
            return handle;

        handle.Close();
        return null;
    }

    private unsafe bool ExecuteIoControl(
        SafeHandle hDevice,
        int bufferSize,
        string operation,
        BufferHandler initializeBuffer,
        BufferHandler consumeBuffer)
    {
        if (Interlocked.CompareExchange(ref _ioPending, 1, 0) != 0)
            return false;

        PendingIo request = null;
        bool deferredCleanup = false;

        try
        {
            request = new PendingIo(hDevice, bufferSize);
            initializeBuffer(request.Buffer);

            bool completed = DeviceIoControlNative(
                request.DeviceHandle,
                PInvoke.IOCTL_STORAGE_QUERY_PROPERTY,
                (void*)request.Buffer,
                (uint)bufferSize,
                (void*)request.Buffer,
                (uint)bufferSize,
                null,
                request.OverlappedPointer);

            if (!completed)
            {
                int error = Marshal.GetLastWin32Error();
                if (error != ErrorIoPending)
                    return false;

                if (!request.Completion.WaitOne(IoTimeoutMilliseconds))
                {
                    Log.Warning(
                        "NVMe {Operation} for {DeviceId} exceeded {TimeoutMilliseconds} ms; cancelling the outstanding I/O.",
                        operation,
                        _deviceId,
                        IoTimeoutMilliseconds);

                    CancelIoExNative(request.DeviceHandle, request.OverlappedPointer);

                    // Cancellation normally completes immediately. If a faulty driver does not
                    // acknowledge it in time, retain all native state and clean it up only after
                    // the completion event is eventually signalled. The sensor worker can return
                    // now without freeing memory still owned by the kernel.
                    if (!request.Completion.WaitOne(CancelCompletionWaitMilliseconds))
                    {
                        Log.Warning(
                            "NVMe {Operation} cancellation for {DeviceId} did not complete within {TimeoutMilliseconds} ms; suppressing further requests until the driver completes it.",
                            operation,
                            _deviceId,
                            CancelCompletionWaitMilliseconds);

                        deferredCleanup = true;
                        request.RegisterDeferredCleanup(() =>
                        {
                            Interlocked.Exchange(ref _ioPending, 0);
                            Log.Debug(
                                "NVMe {Operation} cancellation for {DeviceId} completed asynchronously; requests are enabled again.",
                                operation,
                                _deviceId);
                        });
                        request = null;
                        return false;
                    }

                    GetOverlappedResultNative(request.DeviceHandle, request.OverlappedPointer, out _, false);
                    return false;
                }

                if (!GetOverlappedResultNative(request.DeviceHandle, request.OverlappedPointer, out _, false))
                    return false;
            }

            consumeBuffer(request.Buffer);
            return true;
        }
        finally
        {
            if (!deferredCleanup)
            {
                request?.Dispose();
                Interlocked.Exchange(ref _ioPending, 0);
            }
        }
    }

    private unsafe delegate void BufferHandler(IntPtr buffer);

    [DllImport("kernel32.dll", EntryPoint = "CancelIoEx", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern unsafe bool CancelIoExNative(IntPtr hFile, NativeOverlapped* lpOverlapped);

    [DllImport("kernel32.dll", EntryPoint = "DeviceIoControl", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern unsafe bool DeviceIoControlNative(
        IntPtr hDevice,
        uint dwIoControlCode,
        void* lpInBuffer,
        uint nInBufferSize,
        void* lpOutBuffer,
        uint nOutBufferSize,
        uint* lpBytesReturned,
        NativeOverlapped* lpOverlapped);

    [DllImport("kernel32.dll", EntryPoint = "GetOverlappedResult", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern unsafe bool GetOverlappedResultNative(
        IntPtr hFile,
        NativeOverlapped* lpOverlapped,
        out uint lpNumberOfBytesTransferred,
        [MarshalAs(UnmanagedType.Bool)] bool bWait);

    private sealed class PendingIo : IDisposable
    {
        private readonly object _lifetimeLock = new();
        private readonly SafeHandle _safeHandle;
        private Action _deferredCleanup;
        private bool _disposed;
        private bool _handleReferenceAdded;
        private RegisteredWaitHandle _registeredWait;

        public unsafe PendingIo(SafeHandle safeHandle, int bufferSize)
        {
            _safeHandle = safeHandle;

            try
            {
                safeHandle.DangerousAddRef(ref _handleReferenceAdded);
                DeviceHandle = safeHandle.DangerousGetHandle();

                Buffer = Marshal.AllocHGlobal(bufferSize);
                Marshal.Copy(new byte[bufferSize], 0, Buffer, bufferSize);

                Overlapped = Marshal.AllocHGlobal(sizeof(NativeOverlapped));
                *(NativeOverlapped*)Overlapped = default;

                Completion = new EventWaitHandle(false, EventResetMode.ManualReset);
                OverlappedPointer->EventHandle = Completion.SafeWaitHandle.DangerousGetHandle();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public IntPtr Buffer { get; private set; }

        public EventWaitHandle Completion { get; private set; }

        public IntPtr DeviceHandle { get; }

        public IntPtr Overlapped { get; private set; }

        public unsafe NativeOverlapped* OverlappedPointer => (NativeOverlapped*)Overlapped;

        public void Dispose()
        {
            RegisteredWaitHandle registeredWait;
            EventWaitHandle completion;
            IntPtr buffer;
            IntPtr overlapped;

            lock (_lifetimeLock)
            {
                if (_disposed)
                    return;

                _disposed = true;
                registeredWait = _registeredWait;
                _registeredWait = null;
                completion = Completion;
                Completion = null;
                buffer = Buffer;
                Buffer = IntPtr.Zero;
                overlapped = Overlapped;
                Overlapped = IntPtr.Zero;
            }

            registeredWait?.Unregister(null);
            completion?.Dispose();

            if (overlapped != IntPtr.Zero)
                Marshal.FreeHGlobal(overlapped);

            if (buffer != IntPtr.Zero)
                Marshal.FreeHGlobal(buffer);

            if (_handleReferenceAdded)
            {
                _safeHandle.DangerousRelease();
                _handleReferenceAdded = false;
            }
        }

        public void RegisterDeferredCleanup(Action deferredCleanup)
        {
            lock (_lifetimeLock)
            {
                if (_disposed)
                    return;

                _deferredCleanup = deferredCleanup;
                _registeredWait = ThreadPool.RegisterWaitForSingleObject(
                    Completion,
                    (_, __) => CompleteDeferred(),
                    null,
                    Timeout.Infinite,
                    true);
            }
        }

        private unsafe void CompleteDeferred()
        {
            try
            {
                GetOverlappedResultNative(DeviceHandle, OverlappedPointer, out _, false);
            }
            finally
            {
                Action cleanup = _deferredCleanup;
                Dispose();
                cleanup?.Invoke();
            }
        }
    }
}
