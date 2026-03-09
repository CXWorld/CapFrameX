namespace CapFrameX.PmcReader.Plugin
{
    public static class PmcReaderInterop
    {
        public static void Open()
        {
            global::PmcReader.PmcReaderInterop.Open();
        }

        public static void Close()
        {
            global::PmcReader.PmcReaderInterop.Close();
        }

        public static string GetManufacturerId()
        {
            return global::PmcReader.PmcReaderInterop.GetManufacturerId();
        }

        public static void GetProcessorVersion(out byte family, out byte model, out byte stepping)
        {
            global::PmcReader.PmcReaderInterop.GetProcessorVersion(out family, out model, out stepping);
        }

        public static bool TryGetExtendedTopology(int threadId, out uint eax, out uint ebx, out uint ecx, out uint edx)
        {
            return global::PmcReader.PmcReaderInterop.TryGetExtendedTopology(threadId, out eax, out ebx, out ecx, out edx);
        }

        public static bool TryGetExtendedApicId(int threadId, out uint extendedApicId)
        {
            return global::PmcReader.PmcReaderInterop.TryGetExtendedApicId(threadId, out extendedApicId);
        }
    }
}
