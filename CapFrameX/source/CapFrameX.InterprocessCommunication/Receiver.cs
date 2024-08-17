using CapFrameX.InterprocessCommunication.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Pipes;
using Newtonsoft.Json;

namespace CapFrameX.InterprocessCommunication
{
    public class Receiver: IReceiver, IDisposable
    {
        private Dictionary<string, NamedPipeServerStream> _pipes = new Dictionary<string, NamedPipeServerStream>();

        public void Dispose()
        {
            foreach (var client in _pipes.Values)
            {
                client.Dispose();
            }
        }

        public void RegisterOnOSDOff(Action<object> callBack)
        {
            RegisterTopic(Topic.OSDOff, callBack);
        }

        public void RegisterOnOSDOn(Action<object> callBack)
        {
            RegisterTopic(Topic.OSDOn, callBack);
        }

        public void RegisterOnOSDToggle(Action<object> callBack)
        {
           RegisterTopic(Topic.OSDToggle, callBack);
        }

        private void RegisterTopic(Topic topic, Action<object> onEventReceived)
        {
            var pipename = $"{Constants.PipeName}-{topic}";
            _pipes.TryGetValue(pipename, out var server);
            if (server is null)
            {
                server = new NamedPipeServerStream(pipename, PipeDirection.InOut);
                _pipes.Add(pipename, server);
            }

            server.WaitForConnection();
            Console.WriteLine($"Receiver: Pipe {pipename} connected");

            var ss = new StreamString(server);

            void waitForMessage()
            {
                var payload = JsonConvert.DeserializeObject<object>(ss.ReadString());
                onEventReceived(payload);
                waitForMessage();
            };

            waitForMessage();
        }
    }
}
