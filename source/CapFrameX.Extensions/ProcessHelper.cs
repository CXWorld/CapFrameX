using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

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

        [DllImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW",
            CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool QueryFullProcessImageName(IntPtr hProcess, int flags,
            StringBuilder name, ref int size);

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

        /// <summary>
        /// Path of the process's executable. Needs only QueryLimitedInformation, which protected
        /// processes grant; Process.MainModule needs module access that Call of Duty's Ricochet
        /// anti-cheat denies even to administrators (2026-10-25).
        /// </summary>
        public static bool TryGetImagePath(int processId, out string imagePath)
        {
            imagePath = null;
            IntPtr h = OpenProcess(ProcessAccessFlags.QueryLimitedInformation, false, processId);
            if (h == IntPtr.Zero) return false;
            try
            {
                var buffer = new StringBuilder(1024);
                int length = buffer.Capacity;
                if (!QueryFullProcessImageName(h, 0, buffer, ref length)) return false;
                imagePath = buffer.ToString(0, length);
                return true;
            }
            finally
            {
                CloseHandle(h);
            }
        }

        /// <summary>
        /// The file description of the process's executable, read from the file instead of
        /// through Process.MainModule. Null when the path or the file cannot be read.
        /// </summary>
        public static string GetFileDescription(int processId)
        {
            if (!TryGetImagePath(processId, out string imagePath)) return null;
            try
            {
                return FileVersionInfo.GetVersionInfo(imagePath).FileDescription?.TrimEnd();
            }
            catch (FileNotFoundException)
            {
                return null;
            }
        }
    }
}
