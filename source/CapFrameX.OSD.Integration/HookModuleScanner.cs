using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace CapFrameX.OSD.Integration
{
    internal readonly struct HookModuleRecord
    {
        internal HookModuleRecord(string name, string path)
        {
            Name = name;
            Path = path;
        }

        internal string Name { get; }
        internal string Path { get; }
    }

    /// <summary>
    /// Enumerates a target's mapped modules with their paths. <see cref="HookTargetPolicy"/>
    /// answers "is this name loaded"; the compatibility probe additionally needs to know how
    /// many copies of a DLL are resident and where a DLL was loaded from — a <c>dxgi.dll</c>
    /// outside the system directory is a presentation proxy, and two resident copies of the
    /// FidelityFX loader are what stalled the hook's export arming.
    /// </summary>
    internal static class HookModuleScanner
    {
        private const uint Th32csSnapModule = 0x00000008;
        private const uint Th32csSnapModule32 = 0x00000010;
        private const int ErrorNoMoreFiles = 18;
        private const int ErrorBadLength = 24;
        private const int SnapshotRetryDelayMs = 15;
        private static readonly IntPtr InvalidHandleValue = new IntPtr(-1);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ModuleEntry32
        {
            public uint Size;
            public uint ModuleId;
            public uint ProcessId;
            public uint GlobalUsageCount;
            public uint ProcUsageCount;
            public IntPtr BaseAddress;
            public uint BaseSize;
            public IntPtr Module;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string ModuleName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string ExePath;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool Module32FirstW(IntPtr snapshot, ref ModuleEntry32 entry);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool Module32NextW(IntPtr snapshot, ref ModuleEntry32 entry);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        /// <summary>
        /// A failed scan is reported as such, never as an empty list: absence of evidence must
        /// not read as evidence of absence when the decision it feeds cannot be undone.
        /// </summary>
        internal static bool TryEnumerate(int pid, out IReadOnlyList<HookModuleRecord> modules,
            out string error)
        {
            modules = Array.Empty<HookModuleRecord>();
            error = null;
            if (pid <= 0)
            {
                error = "no target process";
                return false;
            }

            IntPtr snapshot = InvalidHandleValue;
            int snapshotError = 0;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                snapshot = CreateToolhelp32Snapshot(Th32csSnapModule | Th32csSnapModule32,
                    unchecked((uint)pid));
                if (snapshot != InvalidHandleValue) break;
                snapshotError = Marshal.GetLastWin32Error();
                // The loader is still building the list of a starting process.
                if (snapshotError != ErrorBadLength) break;
                Thread.Sleep(SnapshotRetryDelayMs);
            }

            if (snapshot == InvalidHandleValue)
            {
                error = $"module scan failed ({snapshotError})";
                return false;
            }

            try
            {
                var result = new List<HookModuleRecord>();
                var entry = new ModuleEntry32 { Size = (uint)Marshal.SizeOf<ModuleEntry32>() };
                if (!Module32FirstW(snapshot, ref entry))
                {
                    error = $"module scan returned no modules ({Marshal.GetLastWin32Error()})";
                    return false;
                }

                do
                {
                    if (!string.IsNullOrWhiteSpace(entry.ModuleName))
                        result.Add(new HookModuleRecord(entry.ModuleName, entry.ExePath));
                    entry.Size = (uint)Marshal.SizeOf<ModuleEntry32>();
                }
                while (Module32NextW(snapshot, ref entry));

                int enumerationError = Marshal.GetLastWin32Error();
                if (enumerationError != ErrorNoMoreFiles)
                {
                    error = $"module enumeration failed ({enumerationError})";
                    return false;
                }

                modules = result;
                return true;
            }
            finally
            {
                CloseHandle(snapshot);
            }
        }
    }
}
