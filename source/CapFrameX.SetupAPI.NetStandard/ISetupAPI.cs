using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.SetupAPI.NetStandard
{
    public interface ISetupAPI
    {
       bool GetPciAbove4GDecodingStatus();

        bool GetPciLargeMemoryStatus();
    }
}
