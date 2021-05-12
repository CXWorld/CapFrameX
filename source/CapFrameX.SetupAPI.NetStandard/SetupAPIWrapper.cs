using System;

namespace CapFrameX.SetupAPI.NetStandard
{
    public class SetupAPIWrapper : ISetupAPI
    {
        public bool GetPciAbove4GDecodingStatus()
        {
            return true;
        }

        public bool GetPciLargeMemoryStatus()
        {
            return true;
        }
    }
}
