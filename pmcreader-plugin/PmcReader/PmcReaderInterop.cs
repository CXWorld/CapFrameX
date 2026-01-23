using PmcReader.Interop;

namespace PmcReader
{
    public static class PmcReaderInterop
    {
        public static void Open()
        {
            Ring0.Open();
            OpCode.Open();
        }

        public static void Close()
        {
            OpCode.Close();
            Ring0.Close();
        }

        public static string GetManufacturerId()
        {
            return OpCode.GetManufacturerId();
        }

        public static void GetProcessorVersion(out byte family, out byte model, out byte stepping)
        {
            OpCode.GetProcessorVersion(out family, out model, out stepping);
        }

        public static bool TryGetExtendedTopology(int threadId, out uint eax, out uint ebx, out uint ecx, out uint edx)
        {
            return OpCode.CpuidTx(0x8000001E, 0, out eax, out ebx, out ecx, out edx, 1UL << threadId);
        }

        public static bool TryGetExtendedApicId(int threadId, out uint extendedApicId)
        {
            return OpCode.CpuidTx(0x8000001E, 0, out extendedApicId, out _, out _, out _, 1UL << threadId);
        }
    }
}
