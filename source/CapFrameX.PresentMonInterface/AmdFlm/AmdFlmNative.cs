using System;
using System.Runtime.InteropServices;
using System.Text;
using CapFrameX.Contracts.Latency;
using Microsoft.Win32.SafeHandles;

namespace CapFrameX.PresentMonInterface.AmdFlm
{
    internal static class AmdFlmNative
    {
        internal const int Ok = 0;
        internal const int NoSample = 1;
        private const string DllName = "CapFrameX.FLM.dll";

        // Mirrors FlmInteropConfig.mouseEventType: 0 = synthetic mouse move (injects input via
        // SendInput), 1 = passive click-to-photon (measures real user clicks). CapFrameX always
        // uses the passive mode — the user's mouse must never be manipulated.
        internal const int MouseEventTypePassiveClick = 1;

        [StructLayout(LayoutKind.Sequential)]
        internal struct Config
        {
            internal uint StructSize;
            internal int Codec;
            internal int GameUsesFrameGeneration;
            internal int InitAmfUsingDx12;
            internal int MouseHorizontalStep;
            internal float CaptureStartX;
            internal float CaptureStartY;
            internal float CaptureWidth;
            internal float CaptureHeight;
            internal float ThresholdCoefficient;
            internal int AverageFilterFrames;
            internal int FilmGrainThreshold;
            internal int MouseEventType;
            internal int CaptureOutputIndex;

            internal static Config Create(AmdFlmSettings settings)
            {
                return new Config
                {
                    StructSize = (uint)Marshal.SizeOf<Config>(),
                    Codec = settings.CaptureMode == 2 ? 2 : 1,
                    GameUsesFrameGeneration = 0,
                    InitAmfUsingDx12 = settings.CaptureMode == 1 ? 0 : 1,
                    MouseHorizontalStep = 50,
                    // Passive click mode monitors the screen response to a real click (muzzle
                    // flash, recoil, ability effects), which happens around the crosshair — not
                    // in the top-of-screen strip the synthetic move mode uses for camera pans.
                    CaptureStartX = (float)settings.StartX,
                    CaptureStartY = (float)settings.StartY,
                    CaptureWidth = (float)settings.Width,
                    CaptureHeight = (float)settings.Height,
                    ThresholdCoefficient = (float)settings.ThresholdCoefficient,
                    AverageFilterFrames = 100,
                    FilmGrainThreshold = 4,
                    MouseEventType = MouseEventTypePassiveClick,
                    CaptureOutputIndex = settings.CaptureOutputIndex
                };
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Diagnostics
        {
            internal uint StructSize;
            internal int State;
            internal ulong Clicks;
            internal ulong RejectedClicks;
            internal ulong Timeouts;
            internal ulong Frames;
            internal long LastFrameQpc;

            internal static Diagnostics Create()
            {
                return new Diagnostics { StructSize = (uint)Marshal.SizeOf<Diagnostics>() };
            }

            internal AmdFlmStatus ToStatus()
            {
                var (state, message) = State switch
                {
                    0 => (AmdFlmState.WarmingUp, "Learning background motion. Keep the measurement area steady."),
                    1 => (AmdFlmState.WaitingForClick, "Ready. Left-click to trigger a visible response in the measurement area."),
                    2 => (AmdFlmState.WaitingForResponse, "Click detected. Waiting for a screen response."),
                    3 => (AmdFlmState.SceneMoving, "The measurement area is moving. Let it settle before clicking."),
                    4 => (AmdFlmState.NoResponse, "No response detected within 300 ms. Adjust the area or lower the threshold, then click again."),
                    5 => (AmdFlmState.Measured, "Screen response measured. Release the mouse button before the next click."),
                    6 => (AmdFlmState.NoFrames, "No recent captured frames. Check the capture output and mode; the desktop may be idle."),
                    _ => (AmdFlmState.Error, "Unsupported FLM diagnostic state. Check the installed FLM runtime.")
                };
                return new AmdFlmStatus(state, message, Clicks, RejectedClicks, Timeouts, Frames);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Sample
        {
            internal uint StructSize;
            internal uint Flags;
            internal ulong Sequence;
            internal long InputQpc;
            internal long FrameQpc;
            internal float LatencyMs;
            internal float LatencyFrames;
            internal float Fps;
            internal uint Reserved;

            internal static Sample Create()
            {
                return new Sample { StructSize = (uint)Marshal.SizeOf<Sample>() };
            }
        }

        internal sealed class SessionHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            internal SessionHandle(IntPtr handle)
                : base(true)
            {
                SetHandle(handle);
            }

            protected override bool ReleaseHandle()
            {
                FlmDestroy(handle);
                return true;
            }
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int FlmCreate(ref Config config, out IntPtr handle);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int FlmStart(SessionHandle handle);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int FlmStop(SessionHandle handle);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int FlmTryGetSample(SessionHandle handle, ref Sample sample);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int FlmGetDiagnostics(SessionHandle handle, ref Diagnostics diagnostics);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int FlmGetLastError(IntPtr handle, StringBuilder buffer, uint bufferSize);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void FlmDestroy(IntPtr handle);

        internal static string GetLastError(SessionHandle handle = null)
        {
            const int capacity = 1024;
            var buffer = new StringBuilder(capacity);
            IntPtr nativeHandle = handle?.DangerousGetHandle() ?? IntPtr.Zero;
            FlmGetLastError(nativeHandle, buffer, capacity);
            return buffer.ToString();
        }
    }
}
