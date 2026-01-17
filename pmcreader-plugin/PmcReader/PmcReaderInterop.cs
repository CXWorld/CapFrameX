using PmcReader.Interop;
using System;

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
    }
}
