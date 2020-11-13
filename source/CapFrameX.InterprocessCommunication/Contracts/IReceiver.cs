using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.InterprocessCommunication.Contracts
{
    public interface IReceiver
    {
        void RegisterOnOSDOff(Action<object> callBack);
        void RegisterOnOSDOn(Action<object> callBack);
        void RegisterOnOSDToggle(Action<object> callBack);
    }
}
