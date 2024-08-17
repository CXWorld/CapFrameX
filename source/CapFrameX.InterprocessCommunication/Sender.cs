using CapFrameX.InterprocessCommunication.Contracts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.InterprocessCommunication
{
    public class Sender : ISender, IDisposable
    {
        private Dictionary<string, NamedPipeClientStream> _pipes = new Dictionary<string, NamedPipeClientStream>();

        public void Dispose()
        {
            foreach(var client in _pipes.Values)
            {
                client.Dispose();
            }
        }

        public void OSDOff()
        {
            SendToTopic(Topic.OSDOff, nameof(OSDOff));
        }

        public void OSDOn()
        {
            SendToTopic(Topic.OSDOn, nameof(OSDOn));
        }

        public void OSDToggle()
        {
            SendToTopic(Topic.OSDToggle, nameof(OSDToggle));
        }

        private void SendToTopic(Topic topic, object payload)
        {
            var pipename = $"{Constants.PipeName}-{topic}";
            _pipes.TryGetValue(pipename, out var client);
            if (client is null)
            {
                client = new NamedPipeClientStream(".", pipename, PipeDirection.InOut);
                _pipes.Add(pipename, client);
                client.Connect();
                Console.WriteLine($"Sender: Pipe {pipename} connected");
            }


            var ss = new StreamString(client);
            var json = JsonConvert.SerializeObject(payload);
            Console.WriteLine($"Sender: Sending {json}");
            ss.WriteString(json);
        }
    }
}
