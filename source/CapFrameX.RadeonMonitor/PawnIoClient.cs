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

            byte[] module = File.ReadAllBytes(modulePath);
            if (module.Length == 0)
            {
                throw new InvalidDataException("The PawnIO module is empty.");
            }

            ThrowIfFailed(NativeMethods.pawnio_version(out uint version), "query PawnIOLib version");
            ThrowIfFailed(NativeMethods.pawnio_open(out IntPtr handle), "open PawnIO");

            try
            {
                ThrowIfFailed(
                    NativeMethods.pawnio_load(handle, module, (nuint)module.Length),
                    "load RadeonSMU module");
                return new PawnIoClient(handle, version);
            }
            catch
            {
                NativeMethods.pawnio_close(handle);
                throw;
            }
        }

        public ulong[] Execute(string functionName, int outputCount)
        {
            return Execute(functionName, Array.Empty<ulong>(), outputCount);
        }

        public ulong[] Execute(string functionName, ulong[] input, int outputCount)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            ArgumentException.ThrowIfNullOrWhiteSpace(functionName);
            ArgumentNullException.ThrowIfNull(input);
            ArgumentOutOfRangeException.ThrowIfNegative(outputCount);

            ulong[] output = new ulong[outputCount];
            lock (syncRoot)
            {
                ThrowIfFailed(
                    NativeMethods.pawnio_execute(
                        handle,
                        functionName,
                        input,
                        (nuint)input.Length,
                        output,
                        (nuint)output.Length,
                        out nuint returnSize),
                    $"execute {functionName}");

                if (returnSize != (nuint)outputCount)
                {
                    throw new InvalidDataException(
                        $"{functionName} returned {returnSize} entries; {outputCount} were expected.");
                }
            }

            return output;
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

        private static void ThrowIfFailed(int hResult, string operation)
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

    internal sealed class PawnIoException : Exception
    {
        public PawnIoException(string operation, int hResult, string systemMessage)
            : base($"Failed to {operation}: {systemMessage} (HRESULT 0x{hResult:X8}).")
        {
            HResult = hResult;
        }
    }
}
