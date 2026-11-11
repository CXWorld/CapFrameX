using Microsoft.Win32;
using System;
using System.Globalization;
using System.Runtime.InteropServices;

namespace CapFrameX.Overlay
{
    public enum MpoSupportStatus
    {
        /// <summary>The display driver did not report its planes (remote session, basic display driver).</summary>
        Unknown,
        /// <summary>At least one overlay plane for RGB content such as a game.</summary>
        Supported,
        /// <summary>No overlay plane for a game: every visible window above it makes DWM compose it.</summary>
        NoOverlayPlane,
        /// <summary>MPO is switched off in Windows (OverlayTestMode = 5), whatever the driver reports.</summary>
        SwitchedOff
    }

    /// <summary>
    /// Whether a display can keep a game on its own hardware plane while the hook-free overlay
    /// window is visible above it. Without such a plane DWM composes the game into the desktop
    /// image, one refresh later. The planes come from the display driver, as in
    /// scripts\MpoCheck: the counts include the base plane, so a game needs two RGB planes.
    /// The caps only rule MPO out; with planes reported, DWM may still compose for a while.
    /// </summary>
    public sealed class MpoSupportInfo
    {
        public const int OverlayTestModeMpoOff = 5;

        public MpoSupportInfo(string deviceName, bool capsKnown, uint maxPlanes, uint maxRgbPlanes,
            uint maxYuvPlanes, int? overlayTestMode, string error = null)
        {
            DeviceName = deviceName;
            CapsKnown = capsKnown;
            MaxPlanes = maxPlanes;
            MaxRgbPlanes = maxRgbPlanes;
            MaxYuvPlanes = maxYuvPlanes;
            OverlayTestMode = overlayTestMode;
            Error = error;
            Status = overlayTestMode == OverlayTestModeMpoOff ? MpoSupportStatus.SwitchedOff
                : !capsKnown ? MpoSupportStatus.Unknown
                : RgbOverlayPlanes > 0 ? MpoSupportStatus.Supported
                : MpoSupportStatus.NoOverlayPlane;
        }

        public string DeviceName { get; }
        public bool CapsKnown { get; }
        public uint MaxPlanes { get; }
        public uint MaxRgbPlanes { get; }
        public uint MaxYuvPlanes { get; }
        public int? OverlayTestMode { get; }
        public string Error { get; }
        public MpoSupportStatus Status { get; }

        /// <summary>Overlay planes a game can use: RGB planes within the total, minus the base plane.</summary>
        public int RgbOverlayPlanes =>
            CapsKnown ? Math.Max(0, (int)Math.Min(MaxPlanes, MaxRgbPlanes) - 1) : 0;

        public bool WarnsHookFree =>
            Status == MpoSupportStatus.NoOverlayPlane || Status == MpoSupportStatus.SwitchedOff;

        public override string ToString()
        {
            string planes = CapsKnown
                ? string.Format(CultureInfo.InvariantCulture, "planes {0} (RGB {1}, YUV {2})",
                    MaxPlanes, MaxRgbPlanes, MaxYuvPlanes)
                : "planes unknown (" + (Error ?? "no answer") + ")";
            string testMode = OverlayTestMode.HasValue
                ? OverlayTestMode.Value.ToString(CultureInfo.InvariantCulture)
                : "not set";
            return string.Format(CultureInfo.InvariantCulture, "{0}: {1}, {2}, OverlayTestMode {3}",
                DeviceName, Status, planes, testMode);
        }
    }

    public static class MpoSupportProbe
    {
        private const string DwmKey = @"SOFTWARE\Microsoft\Windows\Dwm";

        public static MpoSupportInfo Query(string gdiDisplayName)
        {
            int? overlayTestMode = ReadOverlayTestMode();
            if (string.IsNullOrWhiteSpace(gdiDisplayName))
                return new MpoSupportInfo(gdiDisplayName, false, 0, 0, 0, overlayTestMode, "no display");

            try
            {
                var open = new KmtOpenAdapterFromGdiDisplayName { DeviceName = gdiDisplayName };
                int status = D3DKMTOpenAdapterFromGdiDisplayName(ref open);
                if (status < 0)
                    return Failed(gdiDisplayName, overlayTestMode, "D3DKMTOpenAdapterFromGdiDisplayName", status);

                try
                {
                    var caps = new KmtGetMultiPlaneOverlayCaps
                    {
                        hAdapter = open.hAdapter,
                        VidPnSourceId = open.VidPnSourceId,
                        Reserved = new uint[16]
                    };
                    status = D3DKMTGetMultiPlaneOverlayCaps(ref caps);
                    if (status < 0)
                        return Failed(gdiDisplayName, overlayTestMode, "D3DKMTGetMultiPlaneOverlayCaps", status);

                    return new MpoSupportInfo(gdiDisplayName, true, caps.MaxPlanes, caps.MaxRGBPlanes,
                        caps.MaxYUVPlanes, overlayTestMode);
                }
                finally
                {
                    uint adapter = open.hAdapter;
                    D3DKMTCloseAdapter(ref adapter);
                }
            }
            catch (Exception ex) when (ex is EntryPointNotFoundException || ex is DllNotFoundException)
            {
                return new MpoSupportInfo(gdiDisplayName, false, 0, 0, 0, overlayTestMode, ex.Message);
            }
        }

        private static MpoSupportInfo Failed(string deviceName, int? overlayTestMode, string call, int status) =>
            new MpoSupportInfo(deviceName, false, 0, 0, 0, overlayTestMode,
                string.Format(CultureInfo.InvariantCulture, "{0} failed (0x{1:X8})", call, status));

        private static int? ReadOverlayTestMode()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(DwmKey))
                    return key?.GetValue("OverlayTestMode") is int value ? value : (int?)null;
            }
            catch (Exception ex) when (ex is System.Security.SecurityException || ex is UnauthorizedAccessException)
            {
                return null;
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct KmtOpenAdapterFromGdiDisplayName
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
            public uint hAdapter;
            public uint AdapterLuidLow;
            public int AdapterLuidHigh;
            public uint VidPnSourceId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KmtGetMultiPlaneOverlayCaps
        {
            public uint hAdapter, VidPnSourceId, MaxPlanes, MaxRGBPlanes, MaxYUVPlanes, OverlayCaps;
            // Headroom in case a driver writes past the documented layout, as in scripts\MpoCheck.
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public uint[] Reserved;
        }

        [DllImport("gdi32.dll")]
        private static extern int D3DKMTOpenAdapterFromGdiDisplayName(ref KmtOpenAdapterFromGdiDisplayName p);

        [DllImport("gdi32.dll")]
        private static extern int D3DKMTGetMultiPlaneOverlayCaps(ref KmtGetMultiPlaneOverlayCaps p);

        [DllImport("gdi32.dll")]
        private static extern int D3DKMTCloseAdapter(ref uint hAdapter);
    }
}
