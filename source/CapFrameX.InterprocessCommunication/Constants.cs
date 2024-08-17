using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.InterprocessCommunication
{
    internal static class Constants
    {
        public const int Port = 12345;
        public const string PipeName = "CXPipe";
    }

    internal enum Topic
    {
        OSDOn,
        OSDOff,
        OSDToggle
    }
}
