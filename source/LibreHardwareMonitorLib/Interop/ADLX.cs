// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) CapFrameX and Contributors.
// All Rights Reserved.

using System;
using System.IO;
using System.Runtime.InteropServices;

// ReSharper disable InconsistentNaming

namespace LibreHardwareMonitor.Interop;

/// <summary>
/// AMD ADLX (AMD Display Library X) interop wrapper.
/// Replaces the legacy ADL2 API with the modern ADLX interface.
/// </summary>
internal static class ADLX
{
    public const int ATI_VENDOR_ID = 0x1002;

    private const string DllName = "CapFrameX.ADLX.dll";

    private const int MAX_DRIVER_PATH_LEN = 200;
    private const int MAX_GPU_NAME_LEN = 100;
    private const int MAX_VENDOR_ID_LEN = 20;

    private static bool _dllLoaded;
    private static bool _dllLoadAttempted;

    /// <summary>
    /// GPU type enumeration matching ADLX_GPU_TYPE.
    /// </summary>
    public enum GpuType
    {
        Undefined = 0,
        Integrated = 1,
        Discrete = 2
    }

    /// <summary>
    /// Telemetry support flags structure matching AdlxTelemetrySupport in ADLXManager.h.
    /// Used to query what metrics are supported before actual data is available.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct AdlxTelemetrySupport
    {
        [MarshalAs(UnmanagedType.I1)]
        public bool GpuUsageSupported;
        [MarshalAs(UnmanagedType.I1)]
        public bool GpuClockSpeedSupported;
        [MarshalAs(UnmanagedType.I1)]
        public bool GpuVRAMClockSpeedSupported;
        [MarshalAs(UnmanagedType.I1)]
        public bool GpuTemperatureSupported;
        [MarshalAs(UnmanagedType.I1)]
        public bool GpuHotspotTemperatureSupported;
        [MarshalAs(UnmanagedType.I1)]
        public bool GpuPowerSupported;
        [MarshalAs(UnmanagedType.I1)]
        public bool GpuFanSpeedSupported;
        [MarshalAs(UnmanagedType.I1)]
        public bool GpuVramSupported;
        [MarshalAs(UnmanagedType.I1)]
        public bool GpuVoltageSupported;
        [MarshalAs(UnmanagedType.I1)]
        public bool GpuTotalBoardPowerSupported;
        [MarshalAs(UnmanagedType.I1)]
        public bool GpuIntakeTemperatureSupported;
        [MarshalAs(UnmanagedType.I1)]
        public bool GpuMemoryTemperatureSupported;
        [MarshalAs(UnmanagedType.I1)]
        public bool NpuFrequencySupported;
        [MarshalAs(UnmanagedType.I1)]
        public bool NpuActivityLevelSupported;
        [MarshalAs(UnmanagedType.I1)]
        public bool GpuSharedMemorySupported;
    }

    /// <summary>
    /// Telemetry data structure matching AdlxTelemetryData in ADLXManager.h.
    /// Must match the native struct layout exactly.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct AdlxTelemetryData
    {
        // GPU Usage
        [MarshalAs(UnmanagedType.I1)]
        public bool GpuUsageSupported;
        public double GpuUsageValue;

        // GPU Core Frequency
        [MarshalAs(UnmanagedType.I1)]
        public bool GpuClockSpeedSupported;
        public double GpuClockSpeedValue;

        // GPU VRAM Frequency
        [MarshalAs(UnmanagedType.I1)]
        public bool GpuVRAMClockSpeedSupported;
        public double GpuVRAMClockSpeedValue;

        // GPU Core Temperature
        [MarshalAs(UnmanagedType.I1)]
        public bool GpuTemperatureSupported;
        public double GpuTemperatureValue;

        // GPU Hotspot Temperature
        [MarshalAs(UnmanagedType.I1)]
        public bool GpuHotspotTemperatureSupported;
        public double GpuHotspotTemperatureValue;

        // GPU Power
        [MarshalAs(UnmanagedType.I1)]
        public bool GpuPowerSupported;
        public double GpuPowerValue;

        // Fan Speed
        [MarshalAs(UnmanagedType.I1)]
        public bool GpuFanSpeedSupported;
        public double GpuFanSpeedValue;

        // VRAM Usage
        [MarshalAs(UnmanagedType.I1)]
        public bool GpuVramSupported;
        public double GpuVramValue;

        // GPU Voltage
        [MarshalAs(UnmanagedType.I1)]
        public bool GpuVoltageSupported;
        public double GpuVoltageValue;

        // GPU TBP (Total Board Power)
        [MarshalAs(UnmanagedType.I1)]
        public bool GpuTotalBoardPowerSupported;
        public double GpuTotalBoardPowerValue;

        // GPU Intake Temperature
        [MarshalAs(UnmanagedType.I1)]
        public bool GpuIntakeTemperatureSupported;
        public double GpuIntakeTemperatureValue;

        // GPU Memory Temperature (IADLXGPUMetrics1)
        [MarshalAs(UnmanagedType.I1)]
        public bool GpuMemoryTemperatureSupported;
        public double GpuMemoryTemperatureValue;

        // NPU Frequency (IADLXGPUMetrics1)
        [MarshalAs(UnmanagedType.I1)]
        public bool NpuFrequencySupported;
        public double NpuFrequencyValue;

        // NPU Activity Level (IADLXGPUMetrics1)
        [MarshalAs(UnmanagedType.I1)]
        public bool NpuActivityLevelSupported;
        public double NpuActivityLevelValue;

        // GPU Shared Memory (IADLXGPUMetrics2)
        [MarshalAs(UnmanagedType.I1)]
        public bool GpuSharedMemorySupported;
        public double GpuSharedMemoryValue;
    }

    /// <summary>
    /// Device info structure matching AdlxDeviceInfo in ADLXManager.h.
    /// Must match the native struct layout exactly.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct AdlxDeviceInfo
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_GPU_NAME_LEN)]
        public string GpuName;

        // Undefined = 0, Integrated = 1, Discrete = 2
        public uint GpuType;

        public int Id;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_VENDOR_ID_LEN)]
        public string VendorId;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_DRIVER_PATH_LEN)]
        public string DriverPath;
    }

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "IntializeAdlx")]
    private static extern bool IntializeAdlx_Native();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "CloseAdlx")]
    private static extern void CloseAdlx_Native();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "GetAtiAdpaterCount")]
    private static extern uint GetAtiAdapterCount_Native();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "GetAdlxTelemetry")]
    private static extern bool GetAdlxTelemetry_Native(uint index, uint historyLength, ref AdlxTelemetryData telemetryData);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "GetAdlxTelemetrySupport")]
    private static extern bool GetAdlxTelemetrySupport_Native(uint index, ref AdlxTelemetrySupport telemetrySupport);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "GetAdlxDeviceInfo")]
    private static extern bool GetAdlxDeviceInfo_Native(uint index, ref AdlxDeviceInfo deviceInfo);

    /// <summary>
    /// Checks if the ADLX DLL is available and can be loaded.
    /// </summary>
    public static bool IsAvailable()
    {
        if (_dllLoadAttempted)
            return _dllLoaded;

        _dllLoadAttempted = true;

        try
        {
            // Try to find the DLL in the application directory
            string assemblyPath = AppDomain.CurrentDomain.BaseDirectory;
            string dllPath = Path.Combine(assemblyPath, DllName);

            if (!File.Exists(dllPath))
            {
                // Try x64 subdirectory
                dllPath = Path.Combine(assemblyPath, "x64", DllName);
            }

            if (File.Exists(dllPath))
            {
                _dllLoaded = true;
            }
        }
        catch
        {
            _dllLoaded = false;
        }

        return _dllLoaded;
    }

    /// <summary>
    /// Initializes the ADLX library.
    /// </summary>
    /// <returns>True if initialization succeeded, false otherwise.</returns>
    public static bool Initialize()
    {
        if (!IsAvailable())
            return false;

        try
        {
            return IntializeAdlx_Native();
        }
        catch (DllNotFoundException)
        {
            _dllLoaded = false;
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Closes the ADLX library and releases resources.
    /// </summary>
    public static void Close()
    {
        if (!_dllLoaded)
            return;

        try
        {
            CloseAdlx_Native();
        }
        catch
        {
            // Ignore exceptions during cleanup
        }
    }

    /// <summary>
    /// Gets the number of AMD adapters detected by ADLX.
    /// </summary>
    /// <returns>Number of AMD GPU adapters.</returns>
    public static uint GetAdapterCount()
    {
        if (!_dllLoaded)
            return 0;

        try
        {
            return GetAtiAdapterCount_Native();
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Gets telemetry data for the specified GPU adapter.
    /// </summary>
    /// <param name="index">Adapter index (0-based).</param>
    /// <param name="historyLength">History length in milliseconds.</param>
    /// <param name="telemetryData">Output telemetry data structure.</param>
    /// <returns>True if telemetry was retrieved successfully, false otherwise.</returns>
    public static bool GetTelemetry(uint index, uint historyLength, ref AdlxTelemetryData telemetryData)
    {
        if (!_dllLoaded)
            return false;

        try
        {
            return GetAdlxTelemetry_Native(index, historyLength, ref telemetryData);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the supported telemetry metrics for the specified GPU adapter.
    /// This method queries support flags directly without needing actual telemetry data,
    /// useful for activating sensors before telemetry history is available.
    /// </summary>
    /// <param name="index">Adapter index (0-based).</param>
    /// <param name="telemetrySupport">Output support flags structure.</param>
    /// <returns>True if support flags were retrieved successfully, false otherwise.</returns>
    public static bool GetTelemetrySupport(uint index, ref AdlxTelemetrySupport telemetrySupport)
    {
        if (!_dllLoaded)
            return false;

        try
        {
            return GetAdlxTelemetrySupport_Native(index, ref telemetrySupport);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets device information for the specified GPU adapter.
    /// </summary>
    /// <param name="index">Adapter index (0-based).</param>
    /// <param name="deviceInfo">Output device info structure.</param>
    /// <returns>True if device info was retrieved successfully, false otherwise.</returns>
    public static bool GetDeviceInfo(uint index, ref AdlxDeviceInfo deviceInfo)
    {
        if (!_dllLoaded)
            return false;

        try
        {
            return GetAdlxDeviceInfo_Native(index, ref deviceInfo);
        }
        catch
        {
            return false;
        }
    }
}
