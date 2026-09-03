using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace CapFrameX.RadeonMonitor
{
    internal sealed class PawnIoClient : IDisposable
    {
        private readonly object syncRoot = new();
        private IntPtr handle;
        private bool disposed;

        private PawnIoClient(IntPtr handle, uint libraryVersion)
        {
            this.handle = handle;
            LibraryVersion = libraryVersion;
        }

        public uint LibraryVersion { get; }

        public static PawnIoClient Open(string modulePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(modulePath);

            byte[] module = ReadModuleBlob(modulePath);
            if (module.Length == 0)
            {
                throw new InvalidDataException("The PawnIO module is empty.");
            }

            ThrowIfFailed(NativeMethods.pawnio_version(out uint version), "query PawnIOLib version");
            ThrowIfFailed(NativeMethods.pawnio_open(out IntPtr handle), "open PawnIO");

            try
            {
                int loadResult = PciBusSynchronization.Execute(() =>
                    NativeMethods.pawnio_load(handle, module, (nuint)module.Length));
                ThrowIfFailed(loadResult, "load RadeonSMU module");
                return new PawnIoClient(handle, version);
            }
            catch
            {
                NativeMethods.pawnio_close(handle);
                throw;
            }
        }

        private static byte[] ReadModuleBlob(string modulePath)
        {
            byte[] module = File.ReadAllBytes(modulePath);

            // pawnio_load expects a signature-length DWORD before raw AMX images.
            if (module.Length >= 6 &&
                BitConverter.ToUInt32(module, 0) == (uint)module.Length &&
                module[4] == 0xE1 &&
                module[5] == 0xF1)
            {
                byte[] blob = new byte[module.Length + sizeof(uint)];
                Buffer.BlockCopy(module, 0, blob, sizeof(uint), module.Length);
                return blob;
            }

            return module;
        }

        public ulong[] Execute(string functionName, int outputCount)
        {
            return Execute(functionName, Array.Empty<ulong>(), outputCount);
        }

        public ulong[] Execute(string functionName, ulong[] input, int outputCount)
        {
            PawnIoExecutionResult result = ExecuteWithStatus(functionName, input, outputCount);
            ThrowIfFailed(result.HResult, $"execute {result.FunctionName}");
            return result.Output;
        }

        public PawnIoExecutionResult ExecuteWithStatus(string functionName, int outputCount)
        {
            return ExecuteWithStatus(functionName, Array.Empty<ulong>(), outputCount);
        }

        public PawnIoExecutionResult ExecuteWithStatus(
            string functionName,
            ulong[] input,
            int outputCount)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            ArgumentException.ThrowIfNullOrWhiteSpace(functionName);
            ArgumentNullException.ThrowIfNull(input);
            ArgumentOutOfRangeException.ThrowIfNegative(outputCount);

            ulong[] output = new ulong[outputCount];
            lock (syncRoot)
            {
                int hResult = NativeMethods.pawnio_execute(
                    handle,
                    functionName,
                    input,
                    (nuint)input.Length,
                    output,
                    (nuint)output.Length,
                    out nuint returnSize);

                if (hResult >= 0 && returnSize != (nuint)outputCount)
                {
                    throw new InvalidDataException(
                        $"{functionName} returned {returnSize} entries; {outputCount} were expected.");
                }

                return new PawnIoExecutionResult(functionName, hResult, output);
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            lock (syncRoot)
            {
                if (handle != IntPtr.Zero)
                {
                    NativeMethods.pawnio_close(handle);
                    handle = IntPtr.Zero;
                }

                disposed = true;
            }
        }

        internal static void ThrowIfFailed(int hResult, string operation)
        {
            if (hResult >= 0)
            {
                return;
            }

            Exception? innerException = Marshal.GetExceptionForHR(hResult);
            string systemMessage = innerException?.Message ?? new Win32Exception(hResult).Message;
            throw new PawnIoException(operation, hResult, systemMessage);
        }

        private static class NativeMethods
        {
            private const string LibraryName = "PawnIOLib.dll";

            [DllImport(LibraryName, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
            internal static extern int pawnio_version(out uint version);

            [DllImport(LibraryName, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
            internal static extern int pawnio_open(out IntPtr handle);

            [DllImport(LibraryName, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
            internal static extern int pawnio_load(
                IntPtr handle,
                [In] byte[] blob,
                nuint size);

            [DllImport(
                LibraryName,
                ExactSpelling = true,
                CallingConvention = CallingConvention.StdCall,
                CharSet = CharSet.Ansi)]
            internal static extern int pawnio_execute(
                IntPtr handle,
                [MarshalAs(UnmanagedType.LPStr)] string name,
                [In] ulong[] input,
                nuint inputSize,
                [Out] ulong[] output,
                nuint outputSize,
                out nuint returnSize);

            [DllImport(LibraryName, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
            internal static extern int pawnio_close(IntPtr handle);
        }
    }

    internal readonly record struct PawnIoExecutionResult(
        string FunctionName,
        int HResult,
        ulong[] Output)
    {
        public bool Succeeded => HResult >= 0;
    }

    internal sealed class PawnIoException : Exception
    {
        public PawnIoException(string operation, int hResult, string systemMessage)
            : base($"Failed to {operation}: {systemMessage} (HRESULT 0x{hResult:X8}).")
        {
            HResult = hResult;
        }
    }
}
