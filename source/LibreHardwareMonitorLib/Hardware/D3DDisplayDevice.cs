// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.
// Ported from: https://github.com/processhacker/processhacker/blob/master/plugins/ExtendedTools/gpumon.c

using System;
using System.Linq;
using System.Runtime.InteropServices;
using Windows.Wdk.Graphics.Direct3D;
using Windows.Win32;
using Windows.Win32.Devices.DeviceAndDriverInstallation;
using Windows.Win32.Foundation;

namespace LibreHardwareMonitor.Hardware;

internal static class D3DDisplayDevice
{
    public static unsafe string[] GetDeviceIdentifiers()
    {
        if (PInvoke.CM_Get_Device_Interface_List_Size(out uint size, PInvoke.GUID_DISPLAY_DEVICE_ARRIVAL, null, CM_GET_DEVICE_INTERFACE_LIST_FLAGS.CM_GET_DEVICE_INTERFACE_LIST_PRESENT) !=
            CONFIGRET.CR_SUCCESS)
            return null;

        char[] data = new char[size];
        fixed (char* pData = data)
        {
            if (PInvoke.CM_Get_Device_Interface_List(PInvoke.GUID_DISPLAY_DEVICE_ARRIVAL, null, pData, (uint)data.Length, CM_GET_DEVICE_INTERFACE_LIST_FLAGS.CM_GET_DEVICE_INTERFACE_LIST_PRESENT) ==
                CONFIGRET.CR_SUCCESS)
            {
                return new string(data).Split('\0').Where(m => !string.IsNullOrEmpty(m)).ToArray();
            }
        }

        return null;
    }

    /// <summary>
    /// Returns <c>true</c> if any <em>currently present</em> display adapter matches the given PCI
    /// vendor token (e.g. <c>"VEN_1002"</c> for AMD, <c>"VEN_8086"</c> for Intel). The check uses the
    /// CM_GET_DEVICE_INTERFACE_LIST_PRESENT interface list, so an installed-but-inactive adapter
    /// (e.g. a disabled Ryzen iGPU on a machine that renders on a discrete card) is excluded.
    /// If adapter enumeration fails, <paramref name="fallbackWhenUnknown"/> is returned (default
    /// <c>true</c>) so callers can preserve legacy behaviour rather than skip a real GPU.
    /// </summary>
    public static bool IsAdapterVendorPresent(string vendorToken, bool fallbackWhenUnknown = true)
    {
        string[] identifiers = GetDeviceIdentifiers();
        if (identifiers == null || identifiers.Length == 0)
            return fallbackWhenUnknown;

        foreach (string id in identifiers)
        {
            if (id != null && id.IndexOf(vendorToken, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    public static string GetActualDeviceIdentifier(string deviceIdentifier)
    {
        if (string.IsNullOrEmpty(deviceIdentifier))
            return deviceIdentifier;

        // For example:
        // \\?\ROOT#BasicRender#0000#{1ca05180-a699-450a-9a0c-de4fbe3ddd89}  -->  ROOT\BasicRender\0000
        // \\?\PCI#VEN_1002&DEV_731F&SUBSYS_57051682&REV_C4#6&e539058&0&00000019#{1ca05180-a699-450a-9a0c-de4fbe3ddd89}  -->  PCI\VEN_1002&DEV_731F&SUBSYS_57051682&REV_C4\6&e539058&0&00000019

        if (deviceIdentifier.StartsWith(@"\\?\"))
            deviceIdentifier = deviceIdentifier.Substring(4);

        if (deviceIdentifier.Length > 0 && deviceIdentifier[deviceIdentifier.Length - 1] == '}')
        {
            int lastIndex = deviceIdentifier.LastIndexOf('{');
            if (lastIndex > 0)
                deviceIdentifier = deviceIdentifier.Substring(0, lastIndex - 1);
        }

        return deviceIdentifier.Replace('#', '\\');
    }

    public static bool TryGetDeviceInfoByAdapterLuid(
        string adapterLuidInstanceName,
        string vendorToken,
        out string deviceIdentifier,
        out D3DDeviceInfo deviceInfo)
    {
        deviceIdentifier = null;
        deviceInfo = default;

        if (string.IsNullOrWhiteSpace(adapterLuidInstanceName))
            return false;

        foreach (string candidate in GetVendorDeviceIdentifiers(vendorToken))
        {
            if (GetDeviceInfoByIdentifier(candidate, out D3DDeviceInfo candidateInfo) &&
                string.Equals(candidateInfo.AdapterLuidInstanceName, adapterLuidInstanceName, StringComparison.OrdinalIgnoreCase))
            {
                deviceIdentifier = candidate;
                deviceInfo = candidateInfo;
                return true;
            }
        }

        return false;
    }

    public static bool TryGetDeviceInfoByPnpIdentifier(
        string pnpIdentifier,
        string vendorToken,
        out string deviceIdentifier,
        out D3DDeviceInfo deviceInfo)
    {
        deviceIdentifier = null;
        deviceInfo = default;

        string[] candidates = GetVendorDeviceIdentifiers(vendorToken);
        string[] matches = candidates
            .Where(candidate => DeviceIdentifiersMatch(candidate, pnpIdentifier))
            .ToArray();

        // A PNP identifier must resolve unambiguously. If ADLX cannot provide one, a single
        // present AMD adapter is still safe to associate; multiple adapters are not.
        string match = matches.Length == 1
            ? matches[0]
            : matches.Length == 0 && candidates.Length == 1
                ? candidates[0]
                : null;

        if (match == null || !GetDeviceInfoByIdentifier(match, out deviceInfo))
            return false;

        deviceIdentifier = match;
        return true;
    }

    internal static bool DeviceIdentifiersMatch(string first, string second)
    {
        string normalizedFirst = NormalizeDeviceIdentifier(first);
        string normalizedSecond = NormalizeDeviceIdentifier(second);

        if (normalizedFirst == null || normalizedSecond == null)
            return false;

        return string.Equals(normalizedFirst, normalizedSecond, StringComparison.OrdinalIgnoreCase) ||
               normalizedFirst.StartsWith(normalizedSecond + "\\", StringComparison.OrdinalIgnoreCase) ||
               normalizedSecond.StartsWith(normalizedFirst + "\\", StringComparison.OrdinalIgnoreCase);
    }

    public static bool GetDeviceInfoByIdentifier(string deviceIdentifier, out D3DDeviceInfo deviceInfo)
    {
        deviceInfo = new D3DDeviceInfo();

        OpenAdapterFromDeviceName(out NTSTATUS status, deviceIdentifier, out D3DKMT_OPENADAPTERFROMDEVICENAME adapter);
        if (status != NTSTATUS.STATUS_SUCCESS)
            return false;

        // The adapter is closed on every path, including a failed or rejected query.
        bool queried;
        try
        {
            queried = QueryDeviceInfo(adapter, ref deviceInfo);
        }
        finally
        {
            CloseAdapter(out status, adapter);
        }

        return queried && status == NTSTATUS.STATUS_SUCCESS;
    }

    // Per-node (engine) statistics are deliberately not queried: nothing reads them, and every
    // D3DKMT_QUERYSTATISTICS_NODE call costs about 0.5 ms, which made this per-poll query take
    // 3.7 ms on an 8-node RX 9070 XT. Everything below takes microseconds.
    private static bool QueryDeviceInfo(D3DKMT_OPENADAPTERFROMDEVICENAME adapter, ref D3DDeviceInfo deviceInfo)
    {
        deviceInfo.AdapterLuidInstanceName = GetAdapterLuidInstanceName(adapter.AdapterLuid);

        GetAdapterType(out NTSTATUS status, adapter, out D3DKMT_ADAPTERTYPE adapterType);
        if (status != NTSTATUS.STATUS_SUCCESS)
            return false;

        if (adapterType.Anonymous.Anonymous.SoftwareDevice)
            return false;

        deviceInfo.Integrated = adapterType.Anonymous.Anonymous.HybridIntegrated;

        GetQueryStatisticsAdapterInformation(out status, adapter, out D3DKMT_QUERYSTATISTICS_ADAPTER_INFORMATION adapterInformation);
        if (status != NTSTATUS.STATUS_SUCCESS)
            return false;

        uint segmentCount = adapterInformation.NbSegments;

        GetSegmentSize(out status, adapter, out D3DKMT_SEGMENTSIZEINFO segmentSizeInfo);
        if (status != NTSTATUS.STATUS_SUCCESS)
            return false;

        deviceInfo.GpuSharedLimit = segmentSizeInfo.SharedSystemMemorySize;
        deviceInfo.GpuVideoMemoryLimit = segmentSizeInfo.DedicatedVideoMemorySize;
        deviceInfo.GpuDedicatedLimit = segmentSizeInfo.DedicatedSystemMemorySize;

        for (uint segmentId = 0; segmentId < segmentCount; segmentId++)
        {
            GetQueryStatisticsSegment(out status, adapter, segmentId, out D3DKMT_QUERYSTATISTICS_SEGMENT_INFORMATION segmentInformation);
            if (status != NTSTATUS.STATUS_SUCCESS)
                return false;

            ulong bytesResident = segmentInformation.BytesResident;
            ulong bytesCommitted = segmentInformation.BytesCommitted;

            uint aperture = segmentInformation.Aperture;

            if (aperture == 1)
            {
                deviceInfo.GpuSharedUsed += bytesResident;
                deviceInfo.GpuSharedCommitted += bytesCommitted;
            }
            else
            {
                deviceInfo.GpuDedicatedUsed += bytesResident;
                deviceInfo.GpuDedicatedCommitted += bytesCommitted;
            }
        }

        return true;
    }

    private static unsafe void GetSegmentSize
    (
        out NTSTATUS status,
        D3DKMT_OPENADAPTERFROMDEVICENAME adapter,
        out D3DKMT_SEGMENTSIZEINFO sizeInformation)
    {
        IntPtr segmentSizePtr = Marshal.AllocHGlobal(sizeof(D3DKMT_SEGMENTSIZEINFO));

        var queryAdapterInfo = new D3DKMT_QUERYADAPTERINFO
        {
            hAdapter = adapter.hAdapter,
            Type = KMTQUERYADAPTERINFOTYPE.KMTQAITYPE_GETSEGMENTSIZE,
            pPrivateDriverData = (void*)segmentSizePtr,
            PrivateDriverDataSize = (uint)sizeof(D3DKMT_SEGMENTSIZEINFO)
        };

        status = Windows.Wdk.PInvoke.D3DKMTQueryAdapterInfo(ref queryAdapterInfo);
        sizeInformation = *(D3DKMT_SEGMENTSIZEINFO*)segmentSizePtr;
        Marshal.FreeHGlobal(segmentSizePtr);
    }

    private static void GetQueryStatisticsSegment
    (
        out NTSTATUS status,
        D3DKMT_OPENADAPTERFROMDEVICENAME adapter,
        uint segmentId,
        out D3DKMT_QUERYSTATISTICS_SEGMENT_INFORMATION segmentInformation)
    {
        var queryStatistics = new D3DKMT_QUERYSTATISTICS
        {
            AdapterLuid = adapter.AdapterLuid,
            Type = D3DKMT_QUERYSTATISTICS_TYPE.D3DKMT_QUERYSTATISTICS_SEGMENT,
            Anonymous = new D3DKMT_QUERYSTATISTICS._Anonymous_e__Union { QuerySegment = new D3DKMT_QUERYSTATISTICS_QUERY_SEGMENT { SegmentId = segmentId } }
        };

        status = Windows.Wdk.PInvoke.D3DKMTQueryStatistics(queryStatistics);
        segmentInformation = queryStatistics.QueryResult.SegmentInformation;
    }

    private static void GetQueryStatisticsAdapterInformation
    (
        out NTSTATUS status,
        D3DKMT_OPENADAPTERFROMDEVICENAME adapter,
        out D3DKMT_QUERYSTATISTICS_ADAPTER_INFORMATION adapterInformation)
    {
        var queryStatistics = new D3DKMT_QUERYSTATISTICS { AdapterLuid = adapter.AdapterLuid, Type = D3DKMT_QUERYSTATISTICS_TYPE.D3DKMT_QUERYSTATISTICS_ADAPTER };

        status = Windows.Wdk.PInvoke.D3DKMTQueryStatistics(queryStatistics);

        adapterInformation = queryStatistics.QueryResult.AdapterInformation;
    }

    private static unsafe void GetAdapterType(out NTSTATUS status, D3DKMT_OPENADAPTERFROMDEVICENAME adapter, out D3DKMT_ADAPTERTYPE adapterTypeResult)
    {
        IntPtr adapterTypePtr = Marshal.AllocHGlobal(sizeof(D3DKMT_ADAPTERTYPE));

        var queryAdapterInfo = new D3DKMT_QUERYADAPTERINFO
        {
            hAdapter = adapter.hAdapter, Type = KMTQUERYADAPTERINFOTYPE.KMTQAITYPE_ADAPTERTYPE, pPrivateDriverData = (void*)adapterTypePtr, PrivateDriverDataSize = (uint)sizeof(D3DKMT_ADAPTERTYPE)
        };

        status = Windows.Wdk.PInvoke.D3DKMTQueryAdapterInfo(ref queryAdapterInfo);
        adapterTypeResult = *(D3DKMT_ADAPTERTYPE*)adapterTypePtr;
        Marshal.FreeHGlobal(adapterTypePtr);
    }

    private static unsafe void OpenAdapterFromDeviceName(out NTSTATUS status, string displayDeviceName, out D3DKMT_OPENADAPTERFROMDEVICENAME adapter)
    {
        fixed (char* pDisplayDeviceName = displayDeviceName)
        {
            adapter = new D3DKMT_OPENADAPTERFROMDEVICENAME { pDeviceName = new PCWSTR(pDisplayDeviceName) };
            status = Windows.Wdk.PInvoke.D3DKMTOpenAdapterFromDeviceName(ref adapter);
        }
    }

    private static void CloseAdapter(out NTSTATUS status, D3DKMT_OPENADAPTERFROMDEVICENAME adapter)
    {
        var closeAdapter = new D3DKMT_CLOSEADAPTER { hAdapter = adapter.hAdapter };
        status = Windows.Wdk.PInvoke.D3DKMTCloseAdapter(closeAdapter);
    }

    public static string GetAdapterLuidInstanceName(int highPart, uint lowPart)
    {
        return $"luid_0x{unchecked((uint)highPart):X8}_0x{lowPart:X8}";
    }

    private static string GetAdapterLuidInstanceName(LUID luid)
    {
        return GetAdapterLuidInstanceName(luid.HighPart, luid.LowPart);
    }

    private static string[] GetVendorDeviceIdentifiers(string vendorToken)
    {
        string[] identifiers = GetDeviceIdentifiers();
        if (identifiers == null)
            return Array.Empty<string>();

        return identifiers
            .Where(identifier => !string.IsNullOrEmpty(identifier) &&
                (string.IsNullOrEmpty(vendorToken) || identifier.IndexOf(vendorToken, StringComparison.OrdinalIgnoreCase) >= 0))
            .ToArray();
    }

    private static string NormalizeDeviceIdentifier(string deviceIdentifier)
    {
        if (string.IsNullOrWhiteSpace(deviceIdentifier))
            return null;

        return GetActualDeviceIdentifier(deviceIdentifier).Trim().TrimEnd('\\');
    }

    public struct D3DDeviceInfo
    {
        public string AdapterLuidInstanceName;

        public ulong GpuSharedLimit;
        public ulong GpuDedicatedLimit;
        public ulong GpuVideoMemoryLimit;

        public ulong GpuSharedUsed;
        public ulong GpuDedicatedUsed;

        public ulong GpuSharedCommitted;
        public ulong GpuDedicatedCommitted;

        public bool Integrated;
    }
}
