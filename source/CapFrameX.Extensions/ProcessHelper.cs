using System;
using System.Runtime.InteropServices;

namespace CapFrameX.Extensions
{
    public static class ProcessHelper
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(ProcessAccessFlags access, bool inheritHandle, int procId);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

        [Flags]
        public enum ProcessAccessFlags : uint
        {
            All = 0x001F0FFF,
            Terminate = 0x00000001,
            CreateThread = 0x00000002,
            VirtualMemoryOperation = 0x00000008,
            VirtualMemoryRead = 0x00000010,
            VirtualMemoryWrite = 0x00000020,
            DuplicateHandle = 0x00000040,
            CreateProcess = 0x000000080,
            SetQuota = 0x00000100,
            SetInformation = 0x00000200,
            QueryInformation = 0x00000400,
            QueryLimitedInformation = 0x00001000,
            Synchronize = 0x00100000
        }


        /// <summary>
        /// Checks if given process is still alive
        /// </summary>
        /// <param name="processId">process id</param>
        /// <returns>true if process is alive, false if not</returns>
        static public bool IsProcessAlive(int processId)
        {
            IntPtr h = OpenProcess(ProcessAccessFlags.QueryInformation, true, processId);

            if (h == IntPtr.Zero)
                return false;

            bool b = GetExitCodeProcess(h, out uint code);
            CloseHandle(h);

            if (b)
                b = (code == 259) /* STILL_ACTIVE  */;

            return b;
        }
    }
}
