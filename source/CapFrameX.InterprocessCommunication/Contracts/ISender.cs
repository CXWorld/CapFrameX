using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.InterprocessCommunication.Contracts
{
    public interface ISender
    {
        void OSDOn();
        void OSDOff();
        void OSDToggle();
    }
}
