using CapFrameX.InterprocessCommunication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PipeTestConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("receiver or sender?");
                if (Console.ReadLine() == "receiver")
                {
                    var receiver = new Receiver();
                    receiver.RegisterOnOSDOff((payload) => Console.WriteLine($"Received Message: {payload}"));
                }
                else
                {
                    var sender = new Sender();
                    foreach (var x in Enumerable.Range(0, 10))
                    {
                        sender.OSDOff();
                    }
                }
            } catch(Exception e)
            {
                Console.Error.Write(e);
            }

            while(true)
            {
                Thread.Sleep(1000);
            }
        }
    }
}
