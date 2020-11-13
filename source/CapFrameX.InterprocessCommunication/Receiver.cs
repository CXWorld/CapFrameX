using CapFrameX.InterprocessCommunication.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.InterprocessCommunication
{
    public class Receiver: IReceiver
    {
        public void RegisterOnOSDOff(Action<object> callBack)
        {
            RegisterTopic("OSDOff", callBack);
        }

        public void RegisterOnOSDOn(Action<object> callBack)
        {
            RegisterTopic("OSDOn", callBack);
        }

        public void RegisterOnOSDToggle(Action<object> callBack)
        {
            RegisterTopic("OSDToggle", callBack);
        }

        private void RegisterTopic(string topic, Action<object> onEventReceived)
        {

        }
    }
}
