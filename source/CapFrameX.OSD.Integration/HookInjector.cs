using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace CapFrameX.OSD.Integration
{
    /// <summary>
    /// Loads the in-game hook renderer (cfx_osd_hook.dll) into a target process via
    /// CreateRemoteThread(LoadLibraryW). CapFrameX already knows the detected game PID
    /// (<c>IProcessService.ProcessIdStream</c>), so it addresses the process directly —
    /// no manual injector, no on-disk proxy DLL.
    ///
    /// Bitness must match the target DLL. For a 32-bit (WOW64) game, kernel32 is at a different
    /// base than it is in CapFrameX's x64 process. The injector therefore enumerates the target's
    /// 32-bit modules and combines its kernel32 base with the LoadLibraryW RVA read from the
    /// 32-bit system image. No bitness-matched helper process is required.
    /// </summary>
    internal static class HookInjector
    {
        private const uint PROCESS_CREATE_THREAD = 0x0002;
        private const uint PROCESS_VM_OPERATION = 0x0008;
        private const uint PROCESS_VM_WRITE = 0x0020;
        private const uint PROCESS_VM_READ = 0x0010;
        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        private const uint MEM_COMMIT = 0x1000;
        private const uint MEM_RESERVE = 0x2000;
        private const uint MEM_RELEASE = 0x8000;
        private const uint PAGE_READWRITE = 0x04;
        private const uint LIST_MODULES_32BIT = 0x01;
        private const uint LOAD_LIBRARY_AS_IMAGE_RESOURCE = 0x00000020;
        private const uint WAIT_OBJECT_0 = 0x00000000;
        private const uint WAIT_TIMEOUT = 0x00000102;
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

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibraryEx(string fileName, IntPtr file, uint flags);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr module);

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

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool IsWow64Process(IntPtr process, out bool isWow64);

        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool EnumProcessModulesEx(IntPtr process, [Out] IntPtr[] modules,
            uint bytes, out uint bytesNeeded, uint filterFlag);

        [DllImport("psapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint GetModuleBaseName(IntPtr process, IntPtr module,
            StringBuilder baseName, uint size);

        /// <summary>True if the process runs as 32-bit (WOW64) and needs the x86 hook.</summary>
        internal static bool IsWow64(IntPtr processHandle)
        {
            try
            {
                if (IsWow64Process2(processHandle, out ushort processMachine, out _))
                    return processMachine == IMAGE_FILE_MACHINE_I386;
            }
            catch (EntryPointNotFoundException)
            {
                // IsWow64Process2 was added in Windows 10. Preserve 32-bit support on older builds.
                if (IsWow64Process(processHandle, out bool isWow64)) return isWow64;
            }
            return false;
        }

        /// <summary>
        /// Determines whether process <paramref name="pid"/> is 32-bit (WOW64), used to pick the
        /// x64 vs x86 hook DLL. Needs only QUERY_LIMITED_INFORMATION.
        /// </summary>
        internal static bool TryGetIsWow64(int pid, out bool isWow64, out string error)
        {
            isWow64 = false; error = null;
            IntPtr proc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (proc == IntPtr.Zero)
                proc = OpenProcess(PROCESS_QUERY_INFORMATION, false, pid);
            if (proc == IntPtr.Zero)
            {
                error = $"OpenProcess (bitness query) failed ({Marshal.GetLastWin32Error()}) — elevation may be required";
                return false;
            }
            try { isWow64 = IsWow64(proc); return true; }
            finally { CloseHandle(proc); }
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
                byte[] pathBytes = System.Text.Encoding.Unicode.GetBytes(dllPath + "\0");
                IntPtr remote = VirtualAllocEx(proc, IntPtr.Zero, (uint)pathBytes.Length, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
                if (remote == IntPtr.Zero)
                {
                    error = $"VirtualAllocEx failed ({Marshal.GetLastWin32Error()})";
                    return false;
                }

                bool releaseRemote = true;
                try
                {
                    if (!WriteProcessMemory(proc, remote, pathBytes, (uint)pathBytes.Length, out _))
                    {
                        error = $"WriteProcessMemory failed ({Marshal.GetLastWin32Error()})";
                        return false;
                    }

                    if (!TryResolveLoadLibraryW(proc, IsWow64(proc), out IntPtr loadLib,
                        out error))
                    {
                        return false;
                    }

                    // A 64-bit caller creating a thread at a 32-bit address in a WOW64 target is
                    // established Windows behavior, but not a formal API contract. Keep real WOW64
                    // injection smoke tests in the supported Windows-build matrix.
                    IntPtr thread = CreateRemoteThread(proc, IntPtr.Zero, 0, loadLib, remote, 0, IntPtr.Zero);
                    if (thread == IntPtr.Zero)
                    {
                        error = $"CreateRemoteThread failed ({Marshal.GetLastWin32Error()})";
                        return false;
                    }

                    try
                    {
                        uint waitResult = WaitForSingleObject(thread, LoadLibraryTimeoutMs);
                        if (waitResult != WAIT_OBJECT_0)
                        {
                            // The remote thread may still read its argument. Leak this tiny buffer
                            // until the target exits rather than freeing memory underneath it.
                            releaseRemote = false;
                            error = waitResult == WAIT_TIMEOUT
                                ? $"LoadLibraryW timed out after {LoadLibraryTimeoutMs} ms"
                                : $"waiting for LoadLibraryW failed ({Marshal.GetLastWin32Error()})";
                            return false;
                        }
                        if (!GetExitCodeThread(thread, out uint loaded))
                        {
                            error = $"GetExitCodeThread failed ({Marshal.GetLastWin32Error()})";
                            return false;
                        }
                        if (loaded == 0)
                        {
                            error = "LoadLibraryW returned NULL in the target (hook DLL failed to load)";
                            return false;
                        }
                        return true;
                    }
                    finally { CloseHandle(thread); }
                }
                finally
                {
                    if (releaseRemote) VirtualFreeEx(proc, remote, 0, MEM_RELEASE);
                }
            }
            finally
            {
                CloseHandle(proc);
            }
        }

        private static bool TryResolveLoadLibraryW(IntPtr process, bool isWow64,
            out IntPtr address, out string error)
        {
            address = IntPtr.Zero;
            error = null;
            if (!isWow64)
            {
                IntPtr kernel32 = GetModuleHandle("kernel32.dll");
                address = kernel32 == IntPtr.Zero
                    ? IntPtr.Zero
                    : GetProcAddress(kernel32, "LoadLibraryW");
                if (address != IntPtr.Zero) return true;
                error = $"could not resolve LoadLibraryW ({Marshal.GetLastWin32Error()})";
                return false;
            }

            if (!TryFindWow64ModuleBase(process, "kernel32.dll", out IntPtr kernel32Base,
                out error))
                return false;
            if (!TryGetWow64LoadLibraryRva(out uint loadLibraryRva, out error))
                return false;

            long remoteAddress = kernel32Base.ToInt64() + loadLibraryRva;
            if (kernel32Base.ToInt64() <= 0 || remoteAddress <= 0 || remoteAddress > uint.MaxValue)
            {
                error = "resolved 32-bit LoadLibraryW address is outside the WOW64 address space";
                return false;
            }
            address = new IntPtr(remoteAddress);
            return true;
        }

        private static bool TryFindWow64ModuleBase(IntPtr process, string moduleName,
            out IntPtr moduleBase, out string error)
        {
            moduleBase = IntPtr.Zero;
            error = null;
            var modules = new IntPtr[64];
            for (int attempt = 0; attempt < 3; attempt++)
            {
                uint bufferBytes = checked((uint)(modules.Length * IntPtr.Size));
                if (!EnumProcessModulesEx(process, modules, bufferBytes, out uint bytesNeeded,
                    LIST_MODULES_32BIT))
                {
                    error = $"could not enumerate 32-bit target modules ({Marshal.GetLastWin32Error()})";
                    return false;
                }
                if (bytesNeeded > bufferBytes)
                {
                    uint required = bytesNeeded / (uint)IntPtr.Size + 16;
                    if (required > 4096)
                    {
                        error = "32-bit target module list is unexpectedly large";
                        return false;
                    }
                    modules = new IntPtr[required];
                    continue;
                }

                int count = (int)(bytesNeeded / (uint)IntPtr.Size);
                var name = new StringBuilder(260);
                for (int i = 0; i < count; i++)
                {
                    name.Clear();
                    if (GetModuleBaseName(process, modules[i], name, (uint)name.Capacity) == 0)
                        continue;
                    if (!string.Equals(name.ToString(), moduleName,
                        StringComparison.OrdinalIgnoreCase))
                        continue;
                    moduleBase = modules[i];
                    return true;
                }
                break;
            }

            error = $"{moduleName} was not found in the 32-bit target module list";
            return false;
        }

        internal static bool TryGetWow64LoadLibraryRva(out uint loadLibraryRva,
            out string error)
        {
            loadLibraryRva = 0;
            string windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (string.IsNullOrEmpty(windowsDirectory))
            {
                error = "could not locate the Windows directory";
                return false;
            }

            string kernel32Path = Path.Combine(windowsDirectory, "SysWOW64", "kernel32.dll");
            if (!File.Exists(kernel32Path))
            {
                error = $"32-bit kernel32.dll not found at '{kernel32Path}'";
                return false;
            }
            return TryGetExportRva(kernel32Path, "LoadLibraryW", out loadLibraryRva, out error);
        }

        private static bool TryGetExportRva(string imagePath, string exportName,
            out uint functionRva, out string error)
        {
            functionRva = 0;
            error = null;
            IntPtr mappedModule = LoadLibraryEx(imagePath, IntPtr.Zero,
                LOAD_LIBRARY_AS_IMAGE_RESOURCE);
            if (mappedModule == IntPtr.Zero)
            {
                error = $"could not map '{imagePath}' ({Marshal.GetLastWin32Error()})";
                return false;
            }

            try
            {
                // Resource/image mappings encode their type in the low handle bits.
                IntPtr image = new IntPtr(mappedModule.ToInt64() & ~3L);
                if ((ushort)Marshal.ReadInt16(image) != 0x5A4D)
                    return Fail("invalid DOS header", out error);
                int ntOffset = Marshal.ReadInt32(image, 0x3C);
                if (ntOffset < 0x40 || ntOffset > 0x100000 ||
                    Marshal.ReadInt32(image, ntOffset) != 0x00004550)
                    return Fail("invalid PE header", out error);

                int optionalHeader = checked(ntOffset + 24);
                ushort optionalHeaderSize = (ushort)Marshal.ReadInt16(image, ntOffset + 20);
                ushort magic = (ushort)Marshal.ReadInt16(image, optionalHeader);
                int dataDirectories = magic == 0x010B ? 96 : magic == 0x020B ? 112 : -1;
                if (dataDirectories < 0 || optionalHeaderSize < dataDirectories + 8)
                    return Fail("invalid optional header", out error);

                uint imageSize = ReadUInt32(image, optionalHeader + 56);
                uint exportRva = ReadUInt32(image, optionalHeader + dataDirectories);
                uint exportSize = ReadUInt32(image, optionalHeader + dataDirectories + 4);
                if (!ContainsRange(imageSize, exportRva, Math.Max(exportSize, 40)))
                    return Fail("invalid export directory", out error);

                IntPtr exports = Add(image, exportRva);
                uint functionCount = ReadUInt32(exports, 20);
                uint nameCount = ReadUInt32(exports, 24);
                uint functionsRva = ReadUInt32(exports, 28);
                uint namesRva = ReadUInt32(exports, 32);
                uint ordinalsRva = ReadUInt32(exports, 36);
                if (!ContainsArray(imageSize, functionsRva, functionCount, 4) ||
                    !ContainsArray(imageSize, namesRva, nameCount, 4) ||
                    !ContainsArray(imageSize, ordinalsRva, nameCount, 2))
                    return Fail("invalid export tables", out error);

                for (uint i = 0; i < nameCount; i++)
                {
                    uint nameRva = ReadUInt32(Add(image, namesRva), checked((int)(i * 4)));
                    if (!TryReadAnsiString(image, imageSize, nameRva, out string name) ||
                        !string.Equals(name, exportName, StringComparison.Ordinal))
                        continue;

                    ushort ordinal = (ushort)Marshal.ReadInt16(Add(image, ordinalsRva),
                        checked((int)(i * 2)));
                    if (ordinal >= functionCount)
                        return Fail($"invalid ordinal for {exportName}", out error);
                    functionRva = ReadUInt32(Add(image, functionsRva), ordinal * 4);
                    if (!ContainsRange(imageSize, functionRva, 1))
                        return Fail($"invalid RVA for {exportName}", out error);
                    if (functionRva >= exportRva && functionRva - exportRva < exportSize)
                    {
                        TryReadAnsiString(image, imageSize, functionRva, out string forwarder);
                        return Fail($"{exportName} is forwarded" +
                            (string.IsNullOrEmpty(forwarder) ? "" : $" to {forwarder}"), out error);
                    }
                    return true;
                }

                return Fail($"{exportName} export not found", out error);
            }
            catch (Exception ex)
            {
                error = $"could not parse '{imagePath}': {ex.Message}";
                return false;
            }
            finally { FreeLibrary(mappedModule); }
        }

        private static uint ReadUInt32(IntPtr address, int offset = 0)
            => unchecked((uint)Marshal.ReadInt32(address, offset));

        private static IntPtr Add(IntPtr address, uint offset)
            => new IntPtr(address.ToInt64() + offset);

        private static bool ContainsRange(uint imageSize, uint rva, uint size)
            => rva != 0 && rva < imageSize && size <= imageSize - rva;

        private static bool ContainsArray(uint imageSize, uint rva, uint count, uint itemSize)
            => count <= int.MaxValue / itemSize && count <= uint.MaxValue / itemSize &&
               ContainsRange(imageSize, rva, count * itemSize);

        private static bool TryReadAnsiString(IntPtr image, uint imageSize, uint rva,
            out string value)
        {
            value = null;
            if (!ContainsRange(imageSize, rva, 1)) return false;
            IntPtr start = Add(image, rva);
            int limit = (int)Math.Min(imageSize - rva, 512);
            int length = 0;
            while (length < limit && Marshal.ReadByte(start, length) != 0) length++;
            if (length == limit) return false;
            value = Marshal.PtrToStringAnsi(start, length);
            return true;
        }

        private static bool Fail(string message, out string error)
        {
            error = message;
            return false;
        }
    }
}
