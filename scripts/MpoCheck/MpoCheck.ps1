<#
.SYNOPSIS
    Checks whether this PC supports multiplane overlays (MPO) and whether a game keeps its
    hardware plane while an overlay window is shown above it.

.DESCRIPTION
    1. Reads the MPO plane caps of every display from the display driver (D3DKMT).
    2. Renders a fullscreen D3D11 flip-model test scene and records its presents with PresentMon:
       alone, then with an overlay window built like the CapFrameX hook-free overlay, updating at
       each of the given rates.
    3. Writes a report and packs it with the PresentMon captures into a zip on the desktop.

    Windowed swapchains are not tested: whether DWM gives a window its own plane depends on
    everything else on the desktop, so the result does not repeat.

    PresentMon is taken from the CapFrameX installation, or from a PresentMon-<version>-x64.exe
    next to this script. Recording needs administrator rights (or membership in the
    "Performance Log Users" group); the script asks for elevation when needed.

.PARAMETER PresentMonPath
    PresentMon console executable to use instead of searching for one.

.PARAMETER OutputDirectory
    Folder that receives the result folder and zip. Default: the desktop.

.PARAMETER CaptureSeconds
    Length of each PresentMon capture.

.PARAMETER OverlayRates
    Comma-separated update rates of the overlay window in Hz, one capture each. 10 Hz is the
    default refresh rate of the hook-free overlay.

.PARAMETER NoPause
    Neither waits for Enter at the end nor opens Explorer.
#>
[CmdletBinding()]
param(
    [string]$PresentMonPath,
    [string]$OutputDirectory = [Environment]::GetFolderPath('Desktop'),
    [ValidateRange(3, 60)][int]$CaptureSeconds = 6,
    [string]$OverlayRates = '10,60',
    [switch]$NoPause
)

$ErrorActionPreference = 'Stop'
$ScriptVersion = '1.0'
$Inv = [Globalization.CultureInfo]::InvariantCulture

function Exit-Check([int]$code) {
    if (-not $NoPause) { Read-Host 'Press Enter to close' | Out-Null }
    exit $code
}

function Test-CanTrace {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    if ($principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { return $true }
    # Performance Log Users may start ETW sessions without elevation.
    return [bool]($identity.Groups | Where-Object { $_.Value -eq 'S-1-5-32-559' })
}

if (-not (Test-CanTrace)) {
    Write-Host 'PresentMon needs administrator rights to record presents. Windows will ask for permission now.'
    $argList = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"",
                 '-OutputDirectory', "`"$OutputDirectory`"", '-CaptureSeconds', $CaptureSeconds, '-OverlayRates', $OverlayRates)
    if ($PresentMonPath) { $argList += @('-PresentMonPath', "`"$PresentMonPath`"") }
    if ($NoPause) { $argList += '-NoPause' }
    try {
        Start-Process powershell.exe -Verb RunAs -ArgumentList $argList | Out-Null
    } catch {
        Write-Host "The check was not started with administrator rights: $($_.Exception.Message)" -ForegroundColor Red
        Exit-Check 1
    }
    exit 0
}

# ---------------------------------------------------------------------------------------------
# Native part: plane caps (D3DKMT) and the D3D11 test scene with a DirectComposition overlay
# window. The overlay mirrors CapFrameX.OSD's OverlayWindow/GraphicsDevice: WS_EX_NOREDIRECTIONBITMAP
# | TOPMOST | TRANSPARENT | NOACTIVATE | TOOLWINDOW | LAYERED, and a BGRA8 flip-discard composition
# swapchain with premultiplied alpha.
# ---------------------------------------------------------------------------------------------
$nativeSource = @'
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace CfxMpoCheck
{
    public sealed class PlaneCaps
    {
        public bool Ok;
        public string Error;
        public uint VidPnSourceId, MaxPlanes, MaxRGBPlanes, MaxYUVPlanes, Caps;

        public string CapsText()
        {
            string[] names = { "Rotation", "RotationWithoutIndependentFlip", "VerticalFlip", "HorizontalFlip",
                "StretchRGB", "StretchYUV", "BilinearFilter", "HighFilter", "Shared", "Immediate",
                "Plane0ForVirtualModeOnly", "Version3DDISupport" };
            var sb = new StringBuilder();
            for (int i = 0; i < names.Length; i++)
            {
                if ((Caps & (1u << i)) == 0) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(names[i]);
            }
            return sb.ToString();
        }
    }

    public sealed class DisplayInfo
    {
        public string DeviceName, AdapterName;
        public bool Primary;
        public int Width, Height, RefreshHz, ScalePercent;
        public PlaneCaps Planes;
    }

    public static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        struct POINT { public int X, Y; }

        [StructLayout(LayoutKind.Sequential)]
        struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }

        [StructLayout(LayoutKind.Sequential)]
        struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam; public IntPtr lParam; public uint time; public int ptX; public int ptY; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct WNDCLASSEX
        {
            public uint cbSize; public uint style; public IntPtr lpfnWndProc; public int cbClsExtra; public int cbWndExtra;
            public IntPtr hInstance; public IntPtr hIcon; public IntPtr hCursor; public IntPtr hbrBackground;
            public string lpszMenuName; public string lpszClassName; public IntPtr hIconSm;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct DISPLAY_DEVICE
        {
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
            public int StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
            public int dmFields;
            public int dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
            public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
            public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct KmtOpenAdapterFromGdiDisplayName
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
            public uint hAdapter;
            public uint AdapterLuidLow;
            public int AdapterLuidHigh;
            public uint VidPnSourceId;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct KmtGetMultiPlaneOverlayCaps
        {
            public uint hAdapter, VidPnSourceId, MaxPlanes, MaxRGBPlanes, MaxYUVPlanes, OverlayCaps;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public uint[] Pad; // ABI headroom
        }

        delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool EnumDisplayDevices(string device, uint index, ref DISPLAY_DEVICE dd, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool EnumDisplaySettings(string device, int mode, ref DEVMODE dm);
        [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(POINT pt, uint flags);
        [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);
        [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
        [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint dpiX, out uint dpiY);
        [DllImport("gdi32.dll")] static extern int D3DKMTOpenAdapterFromGdiDisplayName(ref KmtOpenAdapterFromGdiDisplayName p);
        [DllImport("gdi32.dll")] static extern int D3DKMTGetMultiPlaneOverlayCaps(ref KmtGetMultiPlaneOverlayCaps p);
        [DllImport("gdi32.dll")] static extern int D3DKMTCloseAdapter(ref uint hAdapter);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern ushort RegisterClassEx(ref WNDCLASSEX wc);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr CreateWindowEx(uint exStyle, string className, string title, uint style, int x, int y, int w, int h,
                                            IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr DefWindowProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] public static extern bool DestroyWindow(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hwnd, int cmd);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int w, int h, uint flags);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hwnd, out RECT rect);
        [DllImport("user32.dll")] static extern IntPtr SetCursor(IntPtr cursor);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool PeekMessage(out MSG msg, IntPtr hwnd, uint min, uint max, uint remove);
        [DllImport("user32.dll")] static extern bool TranslateMessage(ref MSG msg);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr DispatchMessage(ref MSG msg);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);

        const string ClassName = "CfxMpoCheckWindow";
        static readonly WndProcDelegate s_wndProc = WndProc;
        static bool s_classRegistered;

        public static IntPtr EnterPerMonitorDpi()
        {
            try { return SetThreadDpiAwarenessContext(new IntPtr(-4)); } // PER_MONITOR_AWARE_V2
            catch (EntryPointNotFoundException) { return IntPtr.Zero; }
        }

        public static void LeaveDpi(IntPtr previous)
        {
            if (previous == IntPtr.Zero) return;
            try { SetThreadDpiAwarenessContext(previous); } catch (EntryPointNotFoundException) { }
        }

        public static DisplayInfo[] GetDisplays()
        {
            var list = new List<DisplayInfo>();
            IntPtr previous = EnterPerMonitorDpi();
            try
            {
                for (uint i = 0; ; i++)
                {
                    var dd = new DISPLAY_DEVICE();
                    dd.cb = Marshal.SizeOf(typeof(DISPLAY_DEVICE));
                    if (!EnumDisplayDevices(null, i, ref dd, 0)) break;
                    if ((dd.StateFlags & 0x1) == 0) continue; // DISPLAY_DEVICE_ATTACHED_TO_DESKTOP

                    var info = new DisplayInfo { DeviceName = dd.DeviceName, AdapterName = dd.DeviceString, Primary = (dd.StateFlags & 0x4) != 0 };
                    var dm = new DEVMODE();
                    dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
                    if (EnumDisplaySettings(dd.DeviceName, -1, ref dm)) // ENUM_CURRENT_SETTINGS
                    {
                        info.Width = dm.dmPelsWidth;
                        info.Height = dm.dmPelsHeight;
                        info.RefreshHz = dm.dmDisplayFrequency;
                        IntPtr monitor = MonitorFromPoint(new POINT { X = dm.dmPositionX + 1, Y = dm.dmPositionY + 1 }, 2);
                        try
                        {
                            uint dpiX, dpiY;
                            if (GetDpiForMonitor(monitor, 0, out dpiX, out dpiY) == 0)
                                info.ScalePercent = (int)Math.Round(dpiX * 100.0 / 96.0);
                        }
                        catch (DllNotFoundException) { }
                        catch (EntryPointNotFoundException) { }
                    }
                    info.Planes = GetPlaneCaps(dd.DeviceName);
                    list.Add(info);
                }
            }
            finally { LeaveDpi(previous); }
            return list.ToArray();
        }

        public static PlaneCaps GetPlaneCaps(string gdiDisplayName)
        {
            var result = new PlaneCaps();
            try
            {
                var open = new KmtOpenAdapterFromGdiDisplayName { DeviceName = gdiDisplayName };
                int status = D3DKMTOpenAdapterFromGdiDisplayName(ref open);
                if (status < 0)
                {
                    result.Error = string.Format("D3DKMTOpenAdapterFromGdiDisplayName failed (0x{0:X8})", status);
                    return result;
                }
                try
                {
                    var caps = new KmtGetMultiPlaneOverlayCaps { hAdapter = open.hAdapter, VidPnSourceId = open.VidPnSourceId, Pad = new uint[16] };
                    status = D3DKMTGetMultiPlaneOverlayCaps(ref caps);
                    if (status < 0)
                    {
                        result.Error = string.Format("D3DKMTGetMultiPlaneOverlayCaps failed (0x{0:X8})", status);
                        return result;
                    }
                    result.Ok = true;
                    result.VidPnSourceId = open.VidPnSourceId;
                    result.MaxPlanes = caps.MaxPlanes;
                    result.MaxRGBPlanes = caps.MaxRGBPlanes;
                    result.MaxYUVPlanes = caps.MaxYUVPlanes;
                    result.Caps = caps.OverlayCaps;
                }
                finally
                {
                    uint adapter = open.hAdapter;
                    D3DKMTCloseAdapter(ref adapter);
                }
            }
            catch (EntryPointNotFoundException e) { result.Error = e.Message; }
            return result;
        }

        public static RECT PrimaryMonitorRect()
        {
            IntPtr monitor = MonitorFromPoint(new POINT(), 1); // MONITOR_DEFAULTTOPRIMARY
            var info = new MONITORINFO();
            info.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            if (!GetMonitorInfo(monitor, ref info)) throw new Win32Exception(Marshal.GetLastWin32Error(), "GetMonitorInfo");
            return info.rcMonitor;
        }

        public static IntPtr CreateWindow(uint exStyle, uint style, int x, int y, int w, int h, string title)
        {
            if (!s_classRegistered)
            {
                var wc = new WNDCLASSEX();
                wc.cbSize = (uint)Marshal.SizeOf(typeof(WNDCLASSEX));
                wc.lpfnWndProc = Marshal.GetFunctionPointerForDelegate(s_wndProc);
                wc.hInstance = GetModuleHandle(null);
                wc.lpszClassName = ClassName;
                if (RegisterClassEx(ref wc) == 0)
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error != 1410) throw new Win32Exception(error, "RegisterClassEx"); // ERROR_CLASS_ALREADY_EXISTS
                }
                s_classRegistered = true;
            }
            IntPtr hwnd = CreateWindowEx(exStyle, ClassName, title, style, x, y, w, h, IntPtr.Zero, IntPtr.Zero, GetModuleHandle(null), IntPtr.Zero);
            if (hwnd == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateWindowEx");
            return hwnd;
        }

        public static void PumpMessages()
        {
            MSG msg;
            while (PeekMessage(out msg, IntPtr.Zero, 0, 0, 1)) // PM_REMOVE
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }

        static IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            switch (msg)
            {
                case 0x0010: return IntPtr.Zero;   // WM_CLOSE: the test closes its windows itself
                case 0x0014: return new IntPtr(1); // WM_ERASEBKGND
                case 0x0020:                       // WM_SETCURSOR: hide the cursor over the scene like a game
                    if ((lParam.ToInt64() & 0xFFFF) == 1) { SetCursor(IntPtr.Zero); return new IntPtr(1); }
                    break;
            }
            return DefWindowProc(hwnd, msg, wParam, lParam);
        }
    }

    // Minimal COM vtable access for D3D11, DXGI and DirectComposition.
    static class Com
    {
        public static readonly Guid IID_IDXGIDevice = new Guid("54ec77fa-1377-44e6-8c32-88fd5f44c84c");
        public static readonly Guid IID_IDXGIFactory2 = new Guid("50c83a1c-e072-4c48-87b0-3630fa36a6d0");
        public static readonly Guid IID_ID3D11Texture2D = new Guid("6f15aaf2-d208-4e89-9ab4-489535d34f9c");
        public static readonly Guid IID_IDCompositionDevice = new Guid("c37ea93a-e7aa-450d-b16f-9746cb0407f3");

        [StructLayout(LayoutKind.Sequential)]
        public struct DXGI_SWAP_CHAIN_DESC1
        {
            public uint Width, Height;
            public int Format, Stereo;
            public uint SampleCount, SampleQuality, BufferUsage, BufferCount;
            public int Scaling, SwapEffect, AlphaMode;
            public uint Flags;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DXGI_ADAPTER_DESC
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
            public uint VendorId, DeviceId, SubSysId, Revision;
            public UIntPtr DedicatedVideoMemory, DedicatedSystemMemory, SharedSystemMemory;
            public uint LuidLow;
            public int LuidHigh;
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int QueryInterfaceFn(IntPtr self, ref Guid iid, out IntPtr obj);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int GetParentFn(IntPtr self, ref Guid iid, out IntPtr parent);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int GetAdapterFn(IntPtr self, out IntPtr adapter);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int GetAdapterDescFn(IntPtr self, out DXGI_ADAPTER_DESC desc);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int MakeWindowAssociationFn(IntPtr self, IntPtr hwnd, uint flags);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int CreateSwapChainForHwndFn(IntPtr self, IntPtr device, IntPtr hwnd, ref DXGI_SWAP_CHAIN_DESC1 desc, IntPtr fullscreenDesc, IntPtr output, out IntPtr swapChain);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int CreateSwapChainForCompositionFn(IntPtr self, IntPtr device, ref DXGI_SWAP_CHAIN_DESC1 desc, IntPtr output, out IntPtr swapChain);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int PresentFn(IntPtr self, uint syncInterval, uint flags);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int GetBufferFn(IntPtr self, uint buffer, ref Guid iid, out IntPtr surface);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int CreateRenderTargetViewFn(IntPtr self, IntPtr resource, IntPtr desc, out IntPtr view);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void ClearRenderTargetViewFn(IntPtr self, IntPtr view, float[] color);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int CommitFn(IntPtr self);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int CreateTargetForHwndFn(IntPtr self, IntPtr hwnd, int topmost, out IntPtr target);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int CreateVisualFn(IntPtr self, out IntPtr visual);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int SetPointerFn(IntPtr self, IntPtr value);

        // vtable slots
        public const int QueryInterface = 0;
        public const int DXGIObject_GetParent = 6;
        public const int DXGIDevice_GetAdapter = 7;
        public const int DXGIAdapter_GetDesc = 8;
        public const int DXGIFactory_MakeWindowAssociation = 8;
        public const int DXGIFactory2_CreateSwapChainForHwnd = 15;
        public const int DXGIFactory2_CreateSwapChainForComposition = 24;
        public const int DXGISwapChain_Present = 8;
        public const int DXGISwapChain_GetBuffer = 9;
        public const int D3D11Device_CreateRenderTargetView = 9;
        public const int D3D11DeviceContext_ClearRenderTargetView = 50;
        public const int DCompDevice_Commit = 3;
        public const int DCompDevice_CreateTargetForHwnd = 6;
        public const int DCompDevice_CreateVisual = 7;
        public const int DCompTarget_SetRoot = 3;
        public const int DCompVisual_SetContent = 15;

        [DllImport("d3d11.dll")]
        public static extern int D3D11CreateDevice(IntPtr adapter, int driverType, IntPtr software, uint flags, IntPtr featureLevels,
                                                   uint featureLevelCount, uint sdkVersion, out IntPtr device, out int featureLevel, out IntPtr context);
        [DllImport("dcomp.dll")]
        public static extern int DCompositionCreateDevice(IntPtr dxgiDevice, ref Guid iid, out IntPtr device);

        public static T Fn<T>(IntPtr obj, int slot) where T : class
        {
            IntPtr vtable = Marshal.ReadIntPtr(obj);
            IntPtr fn = Marshal.ReadIntPtr(vtable, slot * IntPtr.Size);
            return (T)(object)Marshal.GetDelegateForFunctionPointer(fn, typeof(T));
        }

        public static void Check(int hr, string what)
        {
            if (hr < 0) throw new COMException(string.Format("{0} failed (0x{1:X8})", what, hr), hr);
        }

        public static void Release(ref IntPtr obj)
        {
            if (obj != IntPtr.Zero) { Marshal.Release(obj); obj = IntPtr.Zero; }
        }

        public static IntPtr CreateBackBufferView(IntPtr device, IntPtr swapChain)
        {
            Guid iid = IID_ID3D11Texture2D;
            IntPtr texture;
            Check(Fn<GetBufferFn>(swapChain, DXGISwapChain_GetBuffer)(swapChain, 0, ref iid, out texture), "IDXGISwapChain::GetBuffer");
            try
            {
                IntPtr view;
                Check(Fn<CreateRenderTargetViewFn>(device, D3D11Device_CreateRenderTargetView)(device, texture, IntPtr.Zero, out view),
                      "ID3D11Device::CreateRenderTargetView");
                return view;
            }
            finally { Marshal.Release(texture); }
        }
    }

    // The game stand-in: a topmost fullscreen window (so a console cannot cover it) with a
    // flip-model swapchain, plus the overlay window on request. It renders below the refresh rate
    // so its present queue stays empty and MsUntilDisplayed measures the display path, not the queue.
    public sealed class TestScene : IDisposable
    {
        [DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint period);
        [DllImport("winmm.dll")] static extern uint timeEndPeriod(uint period);

        const uint WS_POPUP = 0x80000000, WS_VISIBLE = 0x10000000;
        const uint WS_EX_TOPMOST = 0x8, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_LAYERED = 0x80000,
                   WS_EX_NOREDIRECTIONBITMAP = 0x200000, WS_EX_NOACTIVATE = 0x8000000;
        const int FormatR8G8B8A8 = 28, FormatB8G8R8A8 = 87;

        readonly ManualResetEvent _ready = new ManualResetEvent(false);
        readonly double _frameIntervalMs;
        Thread _thread;
        volatile bool _stop;
        volatile int _overlayHz;

        public volatile string Error;
        public string AdapterName;
        public int SwapWidth, SwapHeight;
        public long Frames, OverlayFrames;
        // Matches PresentMon's SwapChainAddress column; the overlay's presents come from the same process.
        public long SwapChainAddress;

        public TestScene(int framesPerSecond)
        {
            _frameIntervalMs = 1000.0 / Math.Max(1, framesPerSecond);
        }

        public bool Start(int timeoutMs)
        {
            _thread = new Thread(Run);
            _thread.IsBackground = true;
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
            return _ready.WaitOne(timeoutMs) && Error == null;
        }

        // Shows the overlay window updating at the given rate, or hides it with 0.
        public void SetOverlay(int updatesPerSecond) { _overlayHz = updatesPerSecond; }

        public void Dispose()
        {
            _stop = true;
            if (_thread != null) _thread.Join(5000);
        }

        void Run()
        {
            IntPtr hwnd = IntPtr.Zero, device = IntPtr.Zero, context = IntPtr.Zero, dxgiDevice = IntPtr.Zero;
            IntPtr adapter = IntPtr.Zero, factory = IntPtr.Zero, swapChain = IntPtr.Zero, view = IntPtr.Zero;
            Overlay overlay = null;
            Native.EnterPerMonitorDpi(); // windows of this thread get physical pixels, no DWM DPI scaling
            try
            {
                Native.RECT monitor = Native.PrimaryMonitorRect();
                hwnd = Native.CreateWindow(WS_EX_TOPMOST, WS_POPUP | WS_VISIBLE, monitor.Left, monitor.Top,
                                           monitor.Right - monitor.Left, monitor.Bottom - monitor.Top, "CapFrameX MPO check");
                Native.SetForegroundWindow(hwnd);
                Native.RECT client;
                Native.GetClientRect(hwnd, out client);
                SwapWidth = client.Right - client.Left;
                SwapHeight = client.Bottom - client.Top;

                int featureLevel;
                Com.Check(Com.D3D11CreateDevice(IntPtr.Zero, 1, IntPtr.Zero, 0x20, IntPtr.Zero, 0, 7, out device, out featureLevel, out context),
                          "D3D11CreateDevice");
                Guid iid = Com.IID_IDXGIDevice;
                Com.Check(Com.Fn<Com.QueryInterfaceFn>(device, Com.QueryInterface)(device, ref iid, out dxgiDevice), "QueryInterface(IDXGIDevice)");
                Com.Check(Com.Fn<Com.GetAdapterFn>(dxgiDevice, Com.DXGIDevice_GetAdapter)(dxgiDevice, out adapter), "IDXGIDevice::GetAdapter");
                Com.DXGI_ADAPTER_DESC adapterDesc;
                if (Com.Fn<Com.GetAdapterDescFn>(adapter, Com.DXGIAdapter_GetDesc)(adapter, out adapterDesc) >= 0)
                    AdapterName = adapterDesc.Description;
                iid = Com.IID_IDXGIFactory2;
                Com.Check(Com.Fn<Com.GetParentFn>(adapter, Com.DXGIObject_GetParent)(adapter, ref iid, out factory), "IDXGIAdapter::GetParent(IDXGIFactory2)");

                var desc = new Com.DXGI_SWAP_CHAIN_DESC1
                {
                    Width = (uint)SwapWidth, Height = (uint)SwapHeight, Format = FormatR8G8B8A8, SampleCount = 1,
                    BufferUsage = 0x20, BufferCount = 2, Scaling = 0, SwapEffect = 4 // FLIP_DISCARD
                };
                Com.Check(Com.Fn<Com.CreateSwapChainForHwndFn>(factory, Com.DXGIFactory2_CreateSwapChainForHwnd)(
                    factory, device, hwnd, ref desc, IntPtr.Zero, IntPtr.Zero, out swapChain), "CreateSwapChainForHwnd");
                SwapChainAddress = swapChain.ToInt64();
                Com.Fn<Com.MakeWindowAssociationFn>(factory, Com.DXGIFactory_MakeWindowAssociation)(factory, hwnd, 2); // no Alt+Enter
                view = Com.CreateBackBufferView(device, swapChain);

                var clear = Com.Fn<Com.ClearRenderTargetViewFn>(context, Com.D3D11DeviceContext_ClearRenderTargetView);
                var present = Com.Fn<Com.PresentFn>(swapChain, Com.DXGISwapChain_Present);
                var clock = Stopwatch.StartNew();
                var color = new float[4];
                double nextFrameMs = 0;
                timeBeginPeriod(1);
                while (!_stop)
                {
                    double nowMs = clock.Elapsed.TotalMilliseconds;
                    if (nextFrameMs < nowMs - _frameIntervalMs) nextFrameMs = nowMs; // fell behind: no catch-up burst
                    while ((nowMs = clock.Elapsed.TotalMilliseconds) < nextFrameMs)
                    {
                        if (nextFrameMs - nowMs > 2.0) Thread.Sleep(1); else Thread.SpinWait(100);
                    }
                    nextFrameMs += _frameIntervalMs;

                    Native.PumpMessages();
                    int overlayHz = _overlayHz;
                    if (overlayHz > 0 && overlay == null)
                    {
                        overlay = new Overlay();
                        overlay.Create(device, dxgiDevice, factory, monitor, clear);
                    }
                    else if (overlayHz <= 0 && overlay != null)
                    {
                        overlay.Dispose();
                        overlay = null;
                    }

                    double t = clock.Elapsed.TotalSeconds;
                    color[0] = (float)(0.25 + 0.2 * Math.Sin(t * 1.7));
                    color[1] = 0.18f;
                    color[2] = (float)(0.35 + 0.25 * Math.Cos(t * 1.1));
                    color[3] = 1f;
                    clear(context, view, color);
                    Com.Check(present(swapChain, 1, 0), "IDXGISwapChain::Present");
                    Frames++;
                    if (overlay != null && overlay.Tick(context, clock.Elapsed.TotalMilliseconds, overlayHz)) OverlayFrames++;
                    if (Frames == 1) _ready.Set();
                }
            }
            catch (Exception e)
            {
                Error = e.Message;
            }
            finally
            {
                timeEndPeriod(1);
                if (overlay != null) overlay.Dispose();
                Com.Release(ref view);
                Com.Release(ref swapChain);
                Com.Release(ref factory);
                Com.Release(ref adapter);
                Com.Release(ref dxgiDevice);
                Com.Release(ref context);
                Com.Release(ref device);
                if (hwnd != IntPtr.Zero) Native.DestroyWindow(hwnd);
                _ready.Set();
            }
        }

        // Built like the CapFrameX hook-free overlay window; presents at the requested rate, as the
        // hook-free renderer does at its refresh rate setting.
        sealed class Overlay : IDisposable
        {
            IntPtr _hwnd, _swapChain, _view, _dcomp, _target, _visual;
            Com.ClearRenderTargetViewFn _clear;
            Com.PresentFn _present;
            double _lastPresentMs = double.MinValue;
            readonly float[] _color = new float[4];

            public void Create(IntPtr device, IntPtr dxgiDevice, IntPtr factory, Native.RECT monitor, Com.ClearRenderTargetViewFn clear)
            {
                const int w = 420, h = 160;
                int x = monitor.Left + 40, y = monitor.Top + 40;
                _hwnd = Native.CreateWindow(WS_EX_NOREDIRECTIONBITMAP | WS_EX_TOPMOST | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE |
                                            WS_EX_TOOLWINDOW | WS_EX_LAYERED, WS_POPUP, x, y, w, h, "CapFrameX MPO check overlay");
                var desc = new Com.DXGI_SWAP_CHAIN_DESC1
                {
                    Width = w, Height = h, Format = FormatB8G8R8A8, SampleCount = 1, BufferUsage = 0x20, BufferCount = 2,
                    Scaling = 0, SwapEffect = 4, AlphaMode = 1 // STRETCH, FLIP_DISCARD, PREMULTIPLIED
                };
                Com.Check(Com.Fn<Com.CreateSwapChainForCompositionFn>(factory, Com.DXGIFactory2_CreateSwapChainForComposition)(
                    factory, device, ref desc, IntPtr.Zero, out _swapChain), "CreateSwapChainForComposition");
                Guid iid = Com.IID_IDCompositionDevice;
                Com.Check(Com.DCompositionCreateDevice(dxgiDevice, ref iid, out _dcomp), "DCompositionCreateDevice");
                Com.Check(Com.Fn<Com.CreateTargetForHwndFn>(_dcomp, Com.DCompDevice_CreateTargetForHwnd)(_dcomp, _hwnd, 1, out _target), "CreateTargetForHwnd");
                Com.Check(Com.Fn<Com.CreateVisualFn>(_dcomp, Com.DCompDevice_CreateVisual)(_dcomp, out _visual), "CreateVisual");
                Com.Check(Com.Fn<Com.SetPointerFn>(_visual, Com.DCompVisual_SetContent)(_visual, _swapChain), "IDCompositionVisual::SetContent");
                Com.Check(Com.Fn<Com.SetPointerFn>(_target, Com.DCompTarget_SetRoot)(_target, _visual), "IDCompositionTarget::SetRoot");
                _view = Com.CreateBackBufferView(device, _swapChain);
                _clear = clear;
                _present = Com.Fn<Com.PresentFn>(_swapChain, Com.DXGISwapChain_Present);
                Com.Check(Com.Fn<Com.CommitFn>(_dcomp, Com.DCompDevice_Commit)(_dcomp), "IDCompositionDevice::Commit");
                Native.ShowWindow(_hwnd, 4); // SW_SHOWNOACTIVATE
                Native.SetWindowPos(_hwnd, new IntPtr(-1), x, y, w, h, 0x0010 | 0x0040); // HWND_TOPMOST, NOACTIVATE | SHOWWINDOW
            }

            public bool Tick(IntPtr context, double nowMs, int updatesPerSecond)
            {
                if (nowMs - _lastPresentMs < 1000.0 / updatesPerSecond) return false;
                _lastPresentMs = nowMs;
                const float alpha = 0.8f; // premultiplied
                _color[0] = 0.02f * alpha;
                _color[1] = (float)(0.25 + 0.1 * Math.Sin(nowMs / 300.0)) * alpha;
                _color[2] = 0.02f * alpha;
                _color[3] = alpha;
                _clear(context, _view, _color);
                int hr = _present(_swapChain, 0, 0);
                if (hr < 0 && hr != unchecked((int)0x887A000A)) Com.Check(hr, "overlay Present"); // DXGI_ERROR_WAS_STILL_DRAWING
                return true;
            }

            public void Dispose()
            {
                Com.Release(ref _view);
                Com.Release(ref _visual);
                Com.Release(ref _target);
                Com.Release(ref _dcomp);
                Com.Release(ref _swapChain);
                if (_hwnd != IntPtr.Zero) { Native.DestroyWindow(_hwnd); _hwnd = IntPtr.Zero; }
            }
        }
    }
}
'@

# ---------------------------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------------------------
$reportLines = New-Object System.Collections.Generic.List[string]

function Add-Report([string]$text = '', [string]$color) {
    $reportLines.Add($text)
    if ($color) { Write-Host $text -ForegroundColor $color } else { Write-Host $text }
}

function Format-Number($value, [string]$format = 'F1') {
    if ($null -eq $value) { return 'n/a' }
    return ([double]$value).ToString($format, $Inv)
}

function Find-PresentMon {
    if ($PresentMonPath) {
        if (Test-Path -LiteralPath $PresentMonPath) { return (Resolve-Path -LiteralPath $PresentMonPath).Path }
        throw "PresentMon was not found at '$PresentMonPath'."
    }
    $folders = New-Object System.Collections.Generic.List[string]
    $folders.Add($PSScriptRoot)
    foreach ($key in 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall',
                     'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall') {
        foreach ($entry in (Get-ChildItem $key -ErrorAction SilentlyContinue)) {
            $p = Get-ItemProperty $entry.PSPath -ErrorAction SilentlyContinue
            if ($p -and $p.DisplayName -like 'CapFrameX*' -and $p.InstallLocation) {
                $folders.Add((Join-Path $p.InstallLocation 'PresentMon'))
            }
        }
    }
    foreach ($programFiles in @($env:ProgramFiles, ${env:ProgramFiles(x86)})) {
        if ($programFiles) { $folders.Add((Join-Path $programFiles 'CapFrameX\PresentMon')) }
    }
    foreach ($folder in $folders) {
        if (-not $folder -or -not (Test-Path -LiteralPath $folder)) { continue }
        # The console capture tool only: not Intel's GUI app (PresentMon.exe) or its service.
        $exe = Get-ChildItem -LiteralPath $folder -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -match '^(CX-)?PresentMon-\d[\d.]*-x64\.exe$' } |
            Sort-Object @{ Expression = { $_.Name -like 'CX-*' }; Descending = $true }, @{ Expression = 'LastWriteTime'; Descending = $true } |
            Select-Object -First 1
        if ($exe) { return $exe.FullName }
    }
    return $null
}

function Get-ModeClass([string]$mode) {
    if ($mode -like 'Hardware Composed*') { return 'plane' }   # the swapchain is on an MPO plane
    if ($mode -like 'Hardware*') { return 'flip' }             # independent flip on the primary plane
    if ($mode -like 'Composed*') { return 'composed' }         # DWM composes it
    return 'other'
}

function Get-Number([string]$text) {
    $value = 0.0
    if ([double]::TryParse($text, [Globalization.NumberStyles]::Float, $Inv, [ref]$value)) { return $value }
    return $null
}

function Get-Median($values) {
    $sorted = @($values | Sort-Object)
    if ($sorted.Count -eq 0) { return $null }
    return $sorted[[int][Math]::Floor($sorted.Count / 2)]
}

function ConvertTo-Address([string]$text) {
    if (-not $text) { return 0L }
    try { return [Convert]::ToInt64(($text -replace '^0[xX]', ''), 16) } catch { return 0L }
}

# Time of the Present call in ms; PresentMon versions name the columns differently.
function Get-PresentTime($row) {
    $time = Get-Number $row.TimeInMs
    if ($null -ne $time) { return $time }
    $time = Get-Number $row.TimeInSeconds
    if ($null -ne $time) { return $time * 1000 }
    $busy = Get-Number $row.MsCPUBusy
    if ($null -eq $busy) { $busy = 0 }
    $time = Get-Number $row.CPUStartTimeInMs
    if ($null -ne $time) { return $time + $busy }
    $time = Get-Number $row.CPUStartTime
    if ($null -ne $time) { return $time * 1000 + $busy }
    return $null
}

# The frames of one swapchain in present order. The overlay window presents from the same process,
# and PresentMon writes a frame once it is complete, so a composed frame can follow later ones.
# Its MsBetweenPresents/MsBetweenDisplayChange then refer to an older frame; the gaps are taken
# from the screen times (present time + MsUntilDisplayed) instead.
function Select-SwapChainFrames($rows, [long]$address) {
    if (-not $rows) { return @() }
    $frames = foreach ($row in $rows) {
        if ((ConvertTo-Address $row.SwapChainAddress) -ne $address) { continue }
        $time = Get-PresentTime $row
        if ($null -eq $time) { continue }
        [pscustomobject]@{ Row = $row; Time = $time }
    }
    return @($frames | Sort-Object Time)
}

function Measure-Capture($frames) {
    if (-not $frames -or $frames.Count -eq 0) { return $null }
    $modes = @{}
    $classes = @{ plane = 0; flip = 0; composed = 0; other = 0 }
    $untilDisplayed = New-Object System.Collections.Generic.List[double]
    $untilByClass = @{ plane = (New-Object System.Collections.Generic.List[double]); flip = (New-Object System.Collections.Generic.List[double])
                       composed = (New-Object System.Collections.Generic.List[double]); other = (New-Object System.Collections.Generic.List[double]) }
    $maxFrameGap = 0.0
    $lastScreen = $null
    $dropped = 0
    $runs = New-Object System.Collections.Generic.List[string]
    $current = $null
    $count = 0
    foreach ($frame in $frames) {
        $row = $frame.Row
        $mode = $row.PresentMode
        if (-not $mode) { $mode = 'Unknown' }
        if ($modes.ContainsKey($mode)) { $modes[$mode]++ } else { $modes[$mode] = 1 }
        $class = Get-ModeClass $mode
        $classes[$class]++
        $until = Get-Number $row.MsUntilDisplayed
        if ($null -ne $until -and $until -gt 0) {
            $untilDisplayed.Add($until)
            $untilByClass[$class].Add($until)
            $screen = $frame.Time + $until
            if ($null -ne $lastScreen -and $screen - $lastScreen -gt $maxFrameGap) { $maxFrameGap = $screen - $lastScreen }
            if ($null -eq $lastScreen -or $screen -gt $lastScreen) { $lastScreen = $screen }
        } else {
            $dropped++
        }
        if ($mode -ne $current) {
            if ($null -ne $current) { $runs.Add("$current x$count") }
            $current = $mode
            $count = 0
        }
        $count++
    }
    $runs.Add("$current x$count")
    $runCount = $runs.Count
    if ($runCount -gt 6) { $runs = @($runs[0..5]) + "... ($runCount runs)" }

    $top = $modes.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 1
    $total = [double]$frames.Count
    $lastTime = $frames[$frames.Count - 1].Time
    $composedFrames = @($frames | Where-Object { (Get-ModeClass $_.Row.PresentMode) -eq 'composed' })
    $lastComposedTime = $null
    if ($composedFrames.Count -gt 0) { $lastComposedTime = $composedFrames[$composedFrames.Count - 1].Time }
    $endFrames = @($frames | Where-Object { $_.Time -ge $lastTime - 1000 })
    $endComposed = @($endFrames | Where-Object { (Get-ModeClass $_.Row.PresentMode) -eq 'composed' }).Count / [double]$endFrames.Count
    return [pscustomobject]@{
        Rows                 = $frames.Count
        FirstTime            = $frames[0].Time
        LastTime             = $lastTime
        LastComposedTime     = $lastComposedTime
        EndComposed          = $endComposed
        TopMode              = $top.Key
        TopShare             = $top.Value / $total
        Plane                = $classes.plane / $total
        Flip                 = $classes.flip / $total
        Composed             = $classes.composed / $total
        MedianUntilDisplayed = (Get-Median $untilDisplayed)
        MedianUntilDirect    = (Get-Median (@($untilByClass.plane) + @($untilByClass.flip)))
        MedianUntilComposed  = (Get-Median $untilByClass.composed)
        MaxFrameGap          = $maxFrameGap
        Dropped              = $dropped
        Runs                 = ($runs -join ' -> ')
    }
}

function Invoke-Capture([string]$name, [int]$seconds, [scriptblock]$during) {
    $csv = Join-Path $workDir "$name.csv"
    $pmArgs = @('--process_id', $PID, '--output_file', "`"$csv`"", '--timed', $seconds, '--terminate_after_timed',
                '--no_console_stats', '--session_name', 'CfxMpoCheck', '--stop_existing_session')
    $process = Start-Process -FilePath $presentMon -ArgumentList $pmArgs -WindowStyle Hidden -PassThru
    if ($during) { & $during }
    if (-not $process.WaitForExit(($seconds + 20) * 1000)) { $process.Kill() }
    if (-not (Test-Path -LiteralPath $csv)) { return $null }
    return @(Import-Csv -LiteralPath $csv)
}

function Format-Percent($share) { return (Format-Number ($share * 100) 'F0') + '%' }

# ---------------------------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------------------------
try {
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $workDir = Join-Path $OutputDirectory "CapFrameX-MPO-Check_$stamp"
    New-Item -ItemType Directory -Force -Path $workDir | Out-Null

    Write-Host ''
    Write-Host 'CapFrameX MPO check' -ForegroundColor Cyan
    $rates = @($OverlayRates -split '[,; ]+' | Where-Object { $_ } | ForEach-Object { [int]$_ } | Where-Object { $_ -ge 1 -and $_ -le 240 })
    # DWM may compose the game for several seconds after an overlay window appears before it
    # returns it to its plane, so the overlay stays visible for at least 10 s per capture.
    $overlaySeconds = [Math]::Max(10, $CaptureSeconds + 4)
    $seconds = 4 + $CaptureSeconds + 2 + $rates.Count * ($overlaySeconds + 4)
    Write-Host "The test takes about $seconds seconds. Your screen turns into a colored fullscreen test image,"
    Write-Host 'and a small green panel appears in the top-left corner from time to time.'
    Write-Host 'Please do not touch the mouse or keyboard until the result appears.'
    Write-Host ''

    $capframexRunning = [bool](Get-Process -Name CapFrameX -ErrorAction SilentlyContinue)
    if ($capframexRunning -and -not $NoPause) {
        Write-Host 'CapFrameX is running. Its overlay changes the result. Please close CapFrameX, then press Enter.' -ForegroundColor Yellow
        Read-Host | Out-Null
        $capframexRunning = [bool](Get-Process -Name CapFrameX -ErrorAction SilentlyContinue)
    }

    Add-Type -TypeDefinition $nativeSource -Language CSharp

    # --- system --------------------------------------------------------------------------------
    $os = Get-CimInstance Win32_OperatingSystem
    $cv = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion' -ErrorAction SilentlyContinue
    $gpus = @(Get-CimInstance Win32_VideoController | Where-Object { $_.Name })
    $monitorNames = @()
    try {
        $monitorNames = @(Get-CimInstance -Namespace root\wmi -ClassName WmiMonitorID -ErrorAction Stop | ForEach-Object {
            $decode = { param($codes) -join ($codes | Where-Object { $_ -ne 0 } | ForEach-Object { [char]$_ }) }
            ('{0} {1}' -f (& $decode $_.ManufacturerName), (& $decode $_.UserFriendlyName)).Trim()
        })
    } catch { }
    $overlayTestMode = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\Dwm' -Name OverlayTestMode -ErrorAction SilentlyContinue).OverlayTestMode
    $hwSchMode = (Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers' -Name HwSchMode -ErrorAction SilentlyContinue).HwSchMode
    $displays = [CfxMpoCheck.Native]::GetDisplays()
    $presentMon = Find-PresentMon

    Add-Report "CapFrameX MPO check $ScriptVersion - $(Get-Date -Format 'yyyy-MM-dd HH:mm')"
    Add-Report ('=' * 60)
    $build = $os.Version
    if ($cv -and $null -ne $cv.UBR) { $build = "$build.$($cv.UBR)" }
    $displayVersion = ''
    if ($cv -and $cv.DisplayVersion) { $displayVersion = " ($($cv.DisplayVersion))" }
    Add-Report "Windows:     $($os.Caption) $build$displayVersion"
    foreach ($gpu in $gpus) {
        $date = ''
        if ($gpu.DriverDate) { $date = ' ' + $gpu.DriverDate.ToString('yyyy-MM-dd') }
        Add-Report "GPU:         $($gpu.Name), driver $($gpu.DriverVersion)$date"
    }
    if ($monitorNames.Count -gt 0) { Add-Report "Monitors:    $($monitorNames -join ', ')" }
    $schedText = 'default'
    if ($hwSchMode -eq 2) { $schedText = 'on' } elseif ($hwSchMode -eq 1) { $schedText = 'off' }
    $testModeText = 'not set'
    if ($null -ne $overlayTestMode) { $testModeText = "$overlayTestMode" }
    Add-Report "Settings:    OverlayTestMode $testModeText, hardware-accelerated GPU scheduling $schedText"
    if ($presentMon) {
        $pmVersion = (Get-Item -LiteralPath $presentMon).VersionInfo.ProductVersion
        Add-Report "PresentMon:  $presentMon $pmVersion"
    }
    if ($capframexRunning) { Add-Report 'Note:        CapFrameX was running during the test.' }
    Add-Report ''
    Add-Report 'Displays and MPO planes (reported by the display driver, counts include the base plane)'
    foreach ($d in $displays) {
        $primaryText = ''
        if ($d.Primary) { $primaryText = ' primary' }
        Add-Report ("  {0}{1}: {2}x{3} @ {4} Hz, scale {5}%, {6}" -f $d.DeviceName, $primaryText, $d.Width, $d.Height, $d.RefreshHz, $d.ScalePercent, $d.AdapterName)
        if ($d.Planes.Ok) {
            Add-Report ("    planes {0} (RGB {1}, YUV {2}), caps 0x{3:X8}: {4}" -f $d.Planes.MaxPlanes, $d.Planes.MaxRGBPlanes, $d.Planes.MaxYUVPlanes, $d.Planes.Caps, $d.Planes.CapsText())
        } else {
            Add-Report "    planes unknown: $($d.Planes.Error)"
        }
    }
    Add-Report ''

    # --- live test -------------------------------------------------------------------------------
    $primary = $displays | Where-Object { $_.Primary } | Select-Object -First 1
    if (-not $primary) { $primary = $displays | Select-Object -First 1 }
    $fullscreen = $null
    $overlayResults = @()
    $sceneError = $null
    if (-not $presentMon) {
        Add-Report 'Live test skipped: PresentMon was not found. Install CapFrameX, or download the PresentMon console' 'Yellow'
        Add-Report 'application (PresentMon-<version>-x64.exe) from https://github.com/GameTechDev/PresentMon/releases' 'Yellow'
        Add-Report 'and put it next to this script.' 'Yellow'
    } else {
        Write-Host 'Running the live test...' -ForegroundColor Cyan
        # Half the refresh rate keeps the present queue empty, so every frame is shown at the next
        # refresh when it flips directly and one refresh later when DWM composes it.
        $refreshHz = 60
        if ($primary -and $primary.RefreshHz -gt 1) { $refreshHz = $primary.RefreshHz }
        $sceneFps = [Math]::Max(30, [int][Math]::Round($refreshHz / 2.0))
        $scene = New-Object CfxMpoCheck.TestScene($sceneFps)
        $gameAddress = 0L
        try {
            if (-not $scene.Start(10000)) { throw "The test scene could not be created: $($scene.Error)" }
            $gameAddress = $scene.SwapChainAddress
            Start-Sleep -Seconds 2
            $rows = Invoke-Capture 'fullscreen' $CaptureSeconds $null
            if ($rows -and -not ($rows | Where-Object { (ConvertTo-Address $_.SwapChainAddress) -eq $gameAddress } | Select-Object -First 1)) {
                # PresentMon names the swapchain differently: take the one that presented alone.
                $gameAddress = ConvertTo-Address (($rows | Group-Object SwapChainAddress | Sort-Object Count -Descending | Select-Object -First 1).Name)
            }
            $fullscreen = Measure-Capture (Select-SwapChainFrames $rows $gameAddress)
            foreach ($hz in $rates) {
                if ($scene.Error) { break }
                # The overlay appears about 2.5 s into the capture, so the capture records the switch too.
                $rows = Invoke-Capture "overlay-$($hz)hz" ($overlaySeconds + 3) {
                    Start-Sleep -Milliseconds 2500
                    $scene.SetOverlay($hz)
                }
                $frames = Select-SwapChainFrames $rows $gameAddress
                # The overlay's first present marks its appearance on PresentMon's clock.
                $appearMs = $null
                foreach ($row in $rows) {
                    if ((ConvertTo-Address $row.SwapChainAddress) -eq $gameAddress) { continue }
                    $time = Get-PresentTime $row
                    if ($null -ne $time -and ($null -eq $appearMs -or $time -lt $appearMs)) { $appearMs = $time }
                }
                $all = Measure-Capture $frames
                $after = $null
                if ($null -ne $appearMs) { $after = Measure-Capture @($frames | Where-Object { $_.Time -ge $appearMs }) }
                $overlayResults += [pscustomobject]@{ Hz = $hz; All = $all; After = $after; AppearMs = $appearMs }
                $scene.SetOverlay(0)
                Start-Sleep -Seconds 1
            }
            if ($scene.Error) { $sceneError = $scene.Error }
            elseif ($rates.Count -gt 0 -and $scene.OverlayFrames -eq 0) { $sceneError = 'The overlay window never presented.' }
        } finally { $scene.Dispose() }

        Add-Report ("Live test: D3D11 flip model, fullscreen {0}x{1} on {2}, {3} fps with VSync, {4} s per capture" -f
            $scene.SwapWidth, $scene.SwapHeight, $scene.AdapterName, $sceneFps, $CaptureSeconds)
        Add-Report '  (frames of the test scene only; the overlay window presents from the same process)'
        if ($fullscreen) {
            Add-Report ("  fullscreen alone:      {0} {1}, until displayed {2} ms, max frame gap {3} ms, {4} not shown" -f
                (Format-Percent $fullscreen.TopShare), $fullscreen.TopMode, (Format-Number $fullscreen.MedianUntilDisplayed),
                (Format-Number $fullscreen.MaxFrameGap), $fullscreen.Dropped)
        } else {
            Add-Report '  fullscreen alone:      no presents recorded' 'Yellow'
        }
        foreach ($r in $overlayResults) {
            $label = ('  + overlay at {0} Hz:' -f $r.Hz).PadRight(25)
            if (-not $r.After) { Add-Report "$label no presents recorded after the overlay appeared" 'Yellow'; continue }
            Add-Report ("{0}{1} composed over {2} s, until displayed {3} ms direct / {4} ms composed, max frame gap {5} ms, {6} not shown" -f $label,
                (Format-Percent $r.After.Composed), (Format-Number (($r.After.LastTime - $r.AppearMs) / 1000)),
                (Format-Number $r.After.MedianUntilDirect), (Format-Number $r.After.MedianUntilComposed),
                (Format-Number $r.After.MaxFrameGap), $r.After.Dropped)
        }
        if ($fullscreen) { Add-Report "  runs, fullscreen: $($fullscreen.Runs)" }
        foreach ($r in $overlayResults) { if ($r.All) { Add-Report "  runs, overlay $($r.Hz) Hz: $($r.All.Runs)" } }
        if ($sceneError) { Add-Report "  Problem: $sceneError" 'Yellow' }
        Add-Report ''
    }

    # --- verdict ---------------------------------------------------------------------------------
    Add-Report 'Result'
    if (-not $primary -or -not $primary.Planes.Ok) {
        Add-Report '  [??] The display driver did not report its MPO planes.' 'Yellow'
    } elseif ($primary.Planes.MaxPlanes -le 1) {
        Add-Report '  [--] No MPO: the display driver reports no overlay planes for the primary display.' 'Yellow'
    } else {
        Add-Report ("  [ok] MPO is supported by the display driver: {0} overlay plane(s), {1} of them for RGB content such as games." -f
            ($primary.Planes.MaxPlanes - 1), [Math]::Max(0, [int]$primary.Planes.MaxRGBPlanes - 1)) 'Green'
    }
    if ($overlayTestMode -eq 5) {
        Add-Report '  [!!] MPO is switched off in Windows (OverlayTestMode = 5 under HKLM\SOFTWARE\Microsoft\Windows\Dwm).' 'Yellow'
    }
    if ($fullscreen) {
        if ($fullscreen.Plane -ge 0.9) {
            Add-Report '  [ok] A fullscreen game gets independent flip on an MPO plane (Hardware Composed: Independent Flip).' 'Green'
        } elseif ($fullscreen.Flip -ge 0.9) {
            Add-Report "  [ok] A fullscreen game gets independent flip ($($fullscreen.TopMode))." 'Green'
        } else {
            Add-Report '  [!!] Even a fullscreen game is composed by DWM. Another window or setting forces composition;' 'Yellow'
            Add-Report '       close always-on-top tools and screen recorders, then run the check again.' 'Yellow'
        }
    }
    $stuck = $false
    $transient = $false
    foreach ($r in $overlayResults) {
        $a = $r.After
        if (-not $a -or -not $fullscreen) { continue }
        $prefix = "Overlay window at $($r.Hz) Hz:"
        $latency = $null
        $baseline = $a.MedianUntilDirect
        if ($null -eq $baseline) { $baseline = $fullscreen.MedianUntilDisplayed }
        if ($null -ne $a.MedianUntilComposed -and $null -ne $baseline) { $latency = $a.MedianUntilComposed - $baseline }
        $gapText = ''
        if ($a.MaxFrameGap -gt 2.5 * 1000.0 / $sceneFps) {
            $gapText = " A $(Format-Number $a.MaxFrameGap) ms frame gap when it appeared, $($a.Dropped) frame(s) not shown."
        }
        if ($a.EndComposed -ge 0.9) {
            $stuck = $true
            Add-Report ("  [!!] {0} DWM composes the game while the overlay is visible (still after {1} s): +{2} ms latency." -f
                $prefix, (Format-Number (($a.LastTime - $r.AppearMs) / 1000)), (Format-Number $latency)) 'Yellow'
            if ($gapText) { Add-Report "      $gapText" 'Yellow' }
        } elseif ($a.Composed -le 0.01) {
            Add-Report "  [ok] $prefix the game keeps independent flip.$gapText" 'Green'
        } else {
            $transient = $true
            Add-Report ("  [~~] {0} DWM composed the game for {1} s after the overlay appeared ({2} of the frames, +{3} ms latency)," -f
                $prefix, (Format-Number (($a.LastComposedTime - $r.AppearMs) / 1000)), (Format-Percent $a.Composed), (Format-Number $latency)) 'Yellow'
            Add-Report "       then returned it to its plane.$gapText" 'Yellow'
        }
    }
    if ($stuck) {
        Add-Report '  Note: the CapFrameX hook-free overlay is expected to cost the same here; the In-Game renderer avoids it.' 'Yellow'
    } elseif ($transient) {
        Add-Report '  Note: expect a few seconds of composition whenever the hook-free overlay appears, e.g. after the overlay hotkey.' 'Yellow'
    }

    $reportPath = Join-Path $workDir 'MpoCheck-report.txt'
    [IO.File]::WriteAllLines($reportPath, $reportLines, (New-Object Text.UTF8Encoding($false)))
    $zipPath = "$workDir.zip"
    Compress-Archive -Path (Join-Path $workDir '*') -DestinationPath $zipPath -Force
    Write-Host ''
    Write-Host "Report: $reportPath"
    Write-Host "Please send this file to the CapFrameX team: $zipPath" -ForegroundColor Cyan
    if (-not $NoPause) { Start-Process explorer.exe "/select,`"$zipPath`"" }
    Exit-Check 0
} catch {
    Write-Host ''
    Write-Host "The check failed: $($_.Exception.Message)" -ForegroundColor Red
    if ($workDir -and (Test-Path -LiteralPath $workDir)) {
        $reportLines.Add('')
        $reportLines.Add("The check failed: $($_.Exception.Message)")
        $reportLines.Add($_.ScriptStackTrace)
        [IO.File]::WriteAllLines((Join-Path $workDir 'MpoCheck-report.txt'), $reportLines, (New-Object Text.UTF8Encoding($false)))
        Write-Host "Partial report: $(Join-Path $workDir 'MpoCheck-report.txt')"
    }
    Exit-Check 1
}
