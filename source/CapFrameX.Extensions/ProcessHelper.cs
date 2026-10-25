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


        private const int ErrorInvalidParameter = 87;
        private const uint StillActive = 259;

        /// <summary>
        /// Checks if given process is still alive. Only a conclusive answer counts as exited:
        /// a PID Windows no longer knows, or a process that has an exit code. A process that
        /// denies the query (a protected one) is alive; it used to count as exited, so every
        /// pass of the process list dropped it and its next row added it again.
        /// </summary>
        /// <param name="processId">process id</param>
        /// <returns>true if process is alive, false if not</returns>
        static public bool IsProcessAlive(int processId)
        {
            // The limited right is granted for protected processes too, unlike QueryInformation.
            IntPtr h = OpenProcess(ProcessAccessFlags.QueryLimitedInformation, false, processId);

            if (h == IntPtr.Zero)
                return Marshal.GetLastWin32Error() != ErrorInvalidParameter;

            bool b = GetExitCodeProcess(h, out uint code);
            CloseHandle(h);

            return !b || code == StillActive;
        }
    }
}
