using Microsoft.Win32.SafeHandles;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace CapFrameX.PresentMonInterface.AmdFlm
{
    internal static class AmdFlmNative
    {
        internal const int Ok = 0;
        internal const int NoSample = 1;
        private const string DllName = "CapFrameX.FLM.dll";

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

            internal static Config CreateDefault(bool gameUsesFrameGeneration)
            {
                return new Config
                {
                    StructSize = (uint)Marshal.SizeOf<Config>(),
                    Codec = 0,
                    GameUsesFrameGeneration = gameUsesFrameGeneration ? 1 : 0,
                    InitAmfUsingDx12 = 0,
                    MouseHorizontalStep = 50,
                    CaptureStartX = 0.125f,
                    CaptureStartY = 0.03125f,
                    CaptureWidth = 0.75f,
                    CaptureHeight = 0.0149f,
                    ThresholdCoefficient = 5.0f,
                    AverageFilterFrames = 100,
                    FilmGrainThreshold = 4
                };
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
