// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Win32;

namespace LibreHardwareMonitor.Interop;

internal static class Cupti
{
    private const string WindowsDllName = "cupti64.dll";
    private const float DramSectorBytes = 32f;

    private static readonly object _syncRoot = new();
    private static FreeLibrarySafeHandle _windowsDll;
    private static bool _initialized;

    private static CuptiEventGetIdFromNameDelegate _cuptiEventGetIdFromName;
    private static CuptiEventGroupAddEventDelegate _cuptiEventGroupAddEvent;
    private static CuptiEventGroupCreateDelegate _cuptiEventGroupCreate;
    private static CuptiEventGroupDestroyDelegate _cuptiEventGroupDestroy;
    private static CuptiEventGroupEnableDelegate _cuptiEventGroupEnable;
    private static CuptiEventGroupReadEventDelegate _cuptiEventGroupReadEvent;
    private static CuptiEventGroupResetAllEventsDelegate _cuptiEventGroupResetAllEvents;

    private static readonly Dictionary<string, DeviceSampler> _deviceSamplers = new(StringComparer.OrdinalIgnoreCase);

    public static bool IsAvailable { get; private set; }

    public static bool Initialize()
    {
        lock (_syncRoot)
        {
            if (_initialized)
                return IsAvailable;

            _initialized = true;

            if (Software.OperatingSystem.IsUnix)
                return IsAvailable = false;

            _windowsDll = LoadCuptiLibrary();
            if (_windowsDll.IsInvalid)
                return IsAvailable = false;

            if (!InitializeDelegates())
                return IsAvailable = false;

            try
            {
                IsAvailable = cuInit(0) == CuResult.Success;
            }
            catch (DllNotFoundException)
            {
                IsAvailable = false;
            }
            catch (EntryPointNotFoundException)
            {
                IsAvailable = false;
            }

            return IsAvailable;
        }
    }

    public static bool TryGetMemoryThroughput(uint? busId, int adapterIndex, out float readMiB, out float writeMiB)
    {
        readMiB = 0f;
        writeMiB = 0f;

        if (!Initialize())
            return false;

        string deviceKey = busId.HasValue ? $"bus:{busId.Value}" : $"index:{adapterIndex}";
        DeviceSampler sampler;
        lock (_syncRoot)
        {
            if (!_deviceSamplers.TryGetValue(deviceKey, out sampler))
            {
                sampler = DeviceSampler.TryCreate(busId, adapterIndex);
                if (sampler == null)
                    return false;

                _deviceSamplers[deviceKey] = sampler;
            }
        }

        return sampler.TryRead(out readMiB, out writeMiB);
    }

    private static FreeLibrarySafeHandle LoadCuptiLibrary()
    {
        FreeLibrarySafeHandle library = PInvoke.LoadLibrary(WindowsDllName);
        if (!library.IsInvalid)
            return library;

        string programFiles = Environment.ExpandEnvironmentVariables("%ProgramW6432%");
        foreach (string candidate in EnumerateCuptiCandidates(programFiles))
        {
            library = PInvoke.LoadLibrary(candidate);
            if (!library.IsInvalid)
                return library;
        }

        return library;
    }

    private static IEnumerable<string> EnumerateCuptiCandidates(string programFiles)
    {
        if (string.IsNullOrWhiteSpace(programFiles))
            yield break;

        string cudaRoot = Path.Combine(programFiles, "NVIDIA GPU Computing Toolkit", "CUDA");
        if (!Directory.Exists(cudaRoot))
            yield break;

        IEnumerable<string> versionDirs;
        try
        {
            versionDirs = Directory.EnumerateDirectories(cudaRoot, "v*", SearchOption.TopDirectoryOnly);
        }
        catch
        {
            yield break;
        }

        foreach (string versionDir in versionDirs)
        {
            string cuptiDir = Path.Combine(versionDir, "extras", "CUPTI", "lib64");
            if (!Directory.Exists(cuptiDir))
                continue;

            IEnumerable<string> dlls;
            try
            {
                dlls = Directory.EnumerateFiles(cuptiDir, "cupti64_*.dll", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                continue;
            }

            foreach (string dllPath in dlls)
                yield return dllPath;
        }
    }

    private static bool InitializeDelegates()
    {
        IntPtr func = PInvoke.GetProcAddress(_windowsDll, "cuptiEventGetIdFromName");
        if (func == IntPtr.Zero)
            return false;
        _cuptiEventGetIdFromName = Marshal.GetDelegateForFunctionPointer<CuptiEventGetIdFromNameDelegate>(func);

        func = PInvoke.GetProcAddress(_windowsDll, "cuptiEventGroupCreate");
        if (func == IntPtr.Zero)
            return false;
        _cuptiEventGroupCreate = Marshal.GetDelegateForFunctionPointer<CuptiEventGroupCreateDelegate>(func);

        func = PInvoke.GetProcAddress(_windowsDll, "cuptiEventGroupDestroy");
        if (func == IntPtr.Zero)
            return false;
        _cuptiEventGroupDestroy = Marshal.GetDelegateForFunctionPointer<CuptiEventGroupDestroyDelegate>(func);

        func = PInvoke.GetProcAddress(_windowsDll, "cuptiEventGroupAddEvent");
        if (func == IntPtr.Zero)
            return false;
        _cuptiEventGroupAddEvent = Marshal.GetDelegateForFunctionPointer<CuptiEventGroupAddEventDelegate>(func);

        func = PInvoke.GetProcAddress(_windowsDll, "cuptiEventGroupEnable");
        if (func == IntPtr.Zero)
            return false;
        _cuptiEventGroupEnable = Marshal.GetDelegateForFunctionPointer<CuptiEventGroupEnableDelegate>(func);

        func = PInvoke.GetProcAddress(_windowsDll, "cuptiEventGroupReadEvent");
        if (func == IntPtr.Zero)
            return false;
        _cuptiEventGroupReadEvent = Marshal.GetDelegateForFunctionPointer<CuptiEventGroupReadEventDelegate>(func);

        func = PInvoke.GetProcAddress(_windowsDll, "cuptiEventGroupResetAllEvents");
        if (func == IntPtr.Zero)
            return false;
        _cuptiEventGroupResetAllEvents = Marshal.GetDelegateForFunctionPointer<CuptiEventGroupResetAllEventsDelegate>(func);

        return true;
    }

    private sealed class DeviceSampler
    {
        private readonly IntPtr _context;
        private readonly IntPtr _eventGroup;
        private readonly uint _readEventId;
        private readonly uint _writeEventId;
        private readonly float _readScale;
        private readonly float _writeScale;
        private readonly Stopwatch _stopwatch;
        private readonly object _lock = new();

        private ulong _lastRead;
        private ulong _lastWrite;
        private bool _hasPrevious;

        private DeviceSampler(int device, IntPtr context, IntPtr eventGroup, uint readEventId, float readScale, uint writeEventId, float writeScale)
        {
            _context = context;
            _eventGroup = eventGroup;
            _readEventId = readEventId;
            _readScale = readScale;
            _writeEventId = writeEventId;
            _writeScale = writeScale;
            _stopwatch = Stopwatch.StartNew();
        }

        public static DeviceSampler TryCreate(uint? busId, int adapterIndex)
        {
            if (cuDeviceGetCount(out int count) != CuResult.Success || count == 0)
                return null;

            int device = TryFindDeviceByBusId(busId, count) ?? TryFindDeviceByIndex(adapterIndex, count) ?? -1;
            if (device < 0)
                return null;

            if (cuCtxCreate(out IntPtr context, 0, device) != CuResult.Success)
                return null;

            bool hasRead = TryResolveEventId(device, ReadEventNames, out uint readEventId, out float readScale);
            bool hasWrite = TryResolveEventId(device, WriteEventNames, out uint writeEventId, out float writeScale);
            if (!hasRead && !hasWrite)
            {
                cuCtxDestroy(context);
                return null;
            }

            if (cuptiEventGroupCreate(context, out IntPtr eventGroup, 0) != CuptiResult.Success)
            {
                cuCtxDestroy(context);
                return null;
            }

            bool hasEvent = false;
            if (readEventId != 0 && cuptiEventGroupAddEvent(eventGroup, readEventId) == CuptiResult.Success)
                hasEvent = true;

            if (writeEventId != 0 && cuptiEventGroupAddEvent(eventGroup, writeEventId) == CuptiResult.Success)
                hasEvent = true;

            if (!hasEvent || cuptiEventGroupEnable(eventGroup) != CuptiResult.Success)
            {
                cuptiEventGroupDestroy(eventGroup);
                cuCtxDestroy(context);
                return null;
            }

            cuptiEventGroupResetAllEvents(eventGroup);

            return new DeviceSampler(device, context, eventGroup, readEventId, readScale, writeEventId, writeScale);
        }

        public bool TryRead(out float readMiB, out float writeMiB)
        {
            readMiB = 0f;
            writeMiB = 0f;

            lock (_lock)
            {
                if (_eventGroup == IntPtr.Zero)
                    return false;

                if (cuCtxPushCurrent(_context) != CuResult.Success)
                    return false;

                try
                {
                    ulong bytesRead = 0;
                    ulong bytesWrite = 0;

                    if (_readEventId != 0)
                    {
                        ulong size = sizeof(ulong);
                        if (cuptiEventGroupReadEvent(_eventGroup, CuptiEventReadFlags.None, _readEventId, ref size, out ulong value) != CuptiResult.Success)
                            return false;
                        bytesRead = (ulong)(value * _readScale);
                    }

                    if (_writeEventId != 0)
                    {
                        ulong size = sizeof(ulong);
                        if (cuptiEventGroupReadEvent(_eventGroup, CuptiEventReadFlags.None, _writeEventId, ref size, out ulong value) != CuptiResult.Success)
                            return false;
                        bytesWrite = (ulong)(value * _writeScale);
                    }

                    double elapsedSeconds = _stopwatch.Elapsed.TotalSeconds;
                    _stopwatch.Restart();

                    if (!_hasPrevious || elapsedSeconds <= 0)
                    {
                        _lastRead = bytesRead;
                        _lastWrite = bytesWrite;
                        _hasPrevious = true;
                        return false;
                    }

                    ulong deltaRead = bytesRead >= _lastRead ? bytesRead - _lastRead : 0;
                    ulong deltaWrite = bytesWrite >= _lastWrite ? bytesWrite - _lastWrite : 0;

                    _lastRead = bytesRead;
                    _lastWrite = bytesWrite;

                    readMiB = (float)(deltaRead / elapsedSeconds / (1024d * 1024d));
                    writeMiB = (float)(deltaWrite / elapsedSeconds / (1024d * 1024d));

                    return true;
                }
                finally
                {
                    cuCtxPopCurrent(out _);
                }
            }
        }

        private static int? TryFindDeviceByBusId(uint? busId, int deviceCount)
        {
            if (!busId.HasValue)
                return null;

            for (int i = 0; i < deviceCount; i++)
            {
                if (cuDeviceGet(out int device, i) != CuResult.Success)
                    continue;

                if (!TryGetBusNumber(device, out uint deviceBusId))
                    continue;

                if (deviceBusId == busId.Value)
                    return device;
            }

            return null;
        }

        private static int? TryFindDeviceByIndex(int adapterIndex, int deviceCount)
        {
            if (adapterIndex < 0 || adapterIndex >= deviceCount)
                return null;

            if (cuDeviceGet(out int device, adapterIndex) != CuResult.Success)
                return null;

            return device;
        }

        private static bool TryGetBusNumber(int device, out uint busId)
        {
            busId = 0;
            var buffer = new StringBuilder(32);
            if (cuDeviceGetPCIBusId(buffer, buffer.Capacity, device) != CuResult.Success)
                return false;

            string pciBusId = buffer.ToString();
            string[] parts = pciBusId.Split(':');
            if (parts.Length < 2)
                return false;

            return uint.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out busId);
        }

        private static bool TryResolveEventId(int device, (string Name, float Scale)[] candidates, out uint eventId, out float scale)
        {
            foreach ((string name, float candidateScale) in candidates)
            {
                if (cuptiEventGetIdFromName(device, name, out eventId) == CuptiResult.Success)
                {
                    scale = candidateScale;
                    return true;
                }
            }

            eventId = 0;
            scale = 1f;
            return false;
        }
    }

    private static readonly (string Name, float Scale)[] ReadEventNames =
    [
        ("dram__bytes_read.sum", 1f),
        ("dram_read_bytes", 1f),
        ("dram__bytes_read", 1f),
        ("dram__sectors_read.sum", DramSectorBytes),
        ("dram_read_sectors", DramSectorBytes),
        ("dram__sectors_read", DramSectorBytes)
    ];

    private static readonly (string Name, float Scale)[] WriteEventNames =
    [
        ("dram__bytes_write.sum", 1f),
        ("dram_write_bytes", 1f),
        ("dram__bytes_write", 1f),
        ("dram__sectors_write.sum", DramSectorBytes),
        ("dram_write_sectors", DramSectorBytes),
        ("dram__sectors_write", DramSectorBytes)
    ];

    private enum CuptiResult
    {
        Success = 0
    }

    [Flags]
    private enum CuptiEventReadFlags : uint
    {
        None = 0
    }

    private enum CuResult
    {
        Success = 0
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate CuptiResult CuptiEventGetIdFromNameDelegate(int device, [MarshalAs(UnmanagedType.LPStr)] string name, out uint eventId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate CuptiResult CuptiEventGroupCreateDelegate(IntPtr context, out IntPtr eventGroup, uint flags);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate CuptiResult CuptiEventGroupDestroyDelegate(IntPtr eventGroup);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate CuptiResult CuptiEventGroupAddEventDelegate(IntPtr eventGroup, uint eventId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate CuptiResult CuptiEventGroupEnableDelegate(IntPtr eventGroup);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate CuptiResult CuptiEventGroupReadEventDelegate(IntPtr eventGroup, CuptiEventReadFlags flags, uint eventId, ref ulong bytesRead, out ulong value);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate CuptiResult CuptiEventGroupResetAllEventsDelegate(IntPtr eventGroup);

    [DllImport("nvcuda.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern CuResult cuInit(uint flags);

    [DllImport("nvcuda.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern CuResult cuDeviceGetCount(out int count);

    [DllImport("nvcuda.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern CuResult cuDeviceGet(out int device, int ordinal);

    [DllImport("nvcuda.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern CuResult cuDeviceGetPCIBusId(StringBuilder pciBusId, int length, int device);

    [DllImport("nvcuda.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern CuResult cuCtxCreate(out IntPtr context, uint flags, int device);

    [DllImport("nvcuda.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern CuResult cuCtxDestroy(IntPtr context);

    [DllImport("nvcuda.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern CuResult cuCtxPushCurrent(IntPtr context);

    [DllImport("nvcuda.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern CuResult cuCtxPopCurrent(out IntPtr context);

    private static CuptiResult cuptiEventGetIdFromName(int device, string name, out uint eventId)
        => _cuptiEventGetIdFromName(device, name, out eventId);

    private static CuptiResult cuptiEventGroupCreate(IntPtr context, out IntPtr eventGroup, uint flags)
        => _cuptiEventGroupCreate(context, out eventGroup, flags);

    private static CuptiResult cuptiEventGroupDestroy(IntPtr eventGroup)
        => _cuptiEventGroupDestroy(eventGroup);

    private static CuptiResult cuptiEventGroupAddEvent(IntPtr eventGroup, uint eventId)
        => _cuptiEventGroupAddEvent(eventGroup, eventId);

    private static CuptiResult cuptiEventGroupEnable(IntPtr eventGroup)
        => _cuptiEventGroupEnable(eventGroup);

    private static CuptiResult cuptiEventGroupReadEvent(IntPtr eventGroup, CuptiEventReadFlags flags, uint eventId, ref ulong bytesRead, out ulong value)
        => _cuptiEventGroupReadEvent(eventGroup, flags, eventId, ref bytesRead, out value);

    private static CuptiResult cuptiEventGroupResetAllEvents(IntPtr eventGroup)
        => _cuptiEventGroupResetAllEvents(eventGroup);
}
