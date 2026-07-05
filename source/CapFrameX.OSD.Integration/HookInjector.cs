using System;
using System.Runtime.InteropServices;

namespace CapFrameX.OSD.Integration
{
    /// <summary>
    /// Loads the in-game hook renderer (cfx_osd_hook.dll) into a target process via
    /// CreateRemoteThread(LoadLibraryW). CapFrameX already knows the detected game PID
    /// (<c>IProcessService.ProcessIdStream</c>), so it addresses the process directly —
    /// no manual injector, no on-disk proxy DLL.
    ///
    /// Bitness must match: CapFrameX is x64 and injects the x64 hook DLL into x64 games.
    /// 32-bit targets are detected and refused here (an x86 hook build is a later step).
    /// </summary>
    internal static class HookInjector
    {
        private const uint PROCESS_CREATE_THREAD = 0x0002;
        private const uint PROCESS_VM_OPERATION = 0x0008;
        private const uint PROCESS_VM_WRITE = 0x0020;
        private const uint PROCESS_VM_READ = 0x0010;
        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint MEM_COMMIT = 0x1000;
        private const uint MEM_RESERVE = 0x2000;
        private const uint MEM_RELEASE = 0x8000;
        private const uint PAGE_READWRITE = 0x04;
        // Bounded wait for the remote LoadLibraryW: DllMain returns immediately (it only
        // spawns worker threads), so this returns in milliseconds. A cap ensures the
        // background injection task can never wait forever.
        private const uint LoadLibraryTimeoutMs = 15000;
        private const ushort IMAGE_FILE_MACHINE_I386 = 0x014C;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAllocEx(IntPtr proc, IntPtr addr, uint size, uint type, uint protect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualFreeEx(IntPtr proc, IntPtr addr, uint size, uint type);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr proc, IntPtr addr, byte[] buffer, uint size, out UIntPtr written);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr GetProcAddress(IntPtr module, string name);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string name);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateRemoteThread(IntPtr proc, IntPtr attrs, uint stackSize,
            IntPtr startAddr, IntPtr param, uint flags, IntPtr threadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr handle, uint ms);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetExitCodeThread(IntPtr thread, out uint exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool IsWow64Process2(IntPtr process, out ushort processMachine, out ushort nativeMachine);

        /// <summary>True if the process runs as 32-bit (WOW64) — the x64 hook DLL cannot inject there.</summary>
        internal static bool IsWow64(IntPtr processHandle)
        {
            try
            {
                if (IsWow64Process2(processHandle, out ushort processMachine, out _))
                    return processMachine == IMAGE_FILE_MACHINE_I386;
            }
            catch (EntryPointNotFoundException) { /* pre-Win8.1: assume native */ }
            return false;
        }

        /// <summary>
        /// Injects <paramref name="dllPath"/> into process <paramref name="pid"/>. Returns
        /// false with a reason in <paramref name="error"/> on any failure (never throws).
        /// </summary>
        internal static bool TryInject(int pid, string dllPath, out string error)
        {
            error = null;
            IntPtr proc = OpenProcess(PROCESS_CREATE_THREAD | PROCESS_VM_OPERATION | PROCESS_VM_WRITE |
                                      PROCESS_VM_READ | PROCESS_QUERY_INFORMATION, false, pid);
            if (proc == IntPtr.Zero)
            {
                error = $"OpenProcess failed ({Marshal.GetLastWin32Error()}) — elevation may be required";
                return false;
            }

            try
            {
                if (IsWow64(proc))
                {
                    error = "target is 32-bit (WOW64); the x64 hook DLL cannot be injected (x86 build pending)";
                    return false;
                }

                byte[] pathBytes = System.Text.Encoding.Unicode.GetBytes(dllPath + "\0");
                IntPtr remote = VirtualAllocEx(proc, IntPtr.Zero, (uint)pathBytes.Length, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
                if (remote == IntPtr.Zero)
                {
                    error = $"VirtualAllocEx failed ({Marshal.GetLastWin32Error()})";
                    return false;
                }

                try
                {
                    if (!WriteProcessMemory(proc, remote, pathBytes, (uint)pathBytes.Length, out _))
                    {
                        error = $"WriteProcessMemory failed ({Marshal.GetLastWin32Error()})";
                        return false;
                    }

                    // kernel32 is loaded at the same base in every process of the same bitness,
                    // so LoadLibraryW's address in this process is valid in the target too.
                    IntPtr loadLib = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryW");
                    if (loadLib == IntPtr.Zero)
                    {
                        error = "could not resolve LoadLibraryW";
                        return false;
                    }

                    IntPtr thread = CreateRemoteThread(proc, IntPtr.Zero, 0, loadLib, remote, 0, IntPtr.Zero);
                    if (thread == IntPtr.Zero)
                    {
                        error = $"CreateRemoteThread failed ({Marshal.GetLastWin32Error()})";
                        return false;
                    }

                    WaitForSingleObject(thread, LoadLibraryTimeoutMs);
                    GetExitCodeThread(thread, out uint loaded); // low 32 bits of HMODULE; 0 => failed
                    CloseHandle(thread);
                    if (loaded == 0)
                    {
                        error = "LoadLibraryW returned NULL in the target (hook DLL failed to load)";
                        return false;
                    }
                    return true;
                }
                finally
                {
                    VirtualFreeEx(proc, remote, 0, MEM_RELEASE);
                }
            }
            finally
            {
                CloseHandle(proc);
            }
        }
    }
}
