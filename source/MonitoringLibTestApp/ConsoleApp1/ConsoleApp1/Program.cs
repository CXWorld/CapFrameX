using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonitoringLibTestApp
{
    public class Program
    {
        private static SensorService _sensorService = new SensorService();

        static void Main(string[] args)
        {
            _sensorService.UpdateSensors();

            Console.WriteLine(_sensorService.CpuTemp.ToString());
            Console.WriteLine(_sensorService.CPUFrequency.ToString("0") + " MHz");
            Console.WriteLine(_sensorService.CpuPPT + " W");
            Console.WriteLine(_sensorService.VCoreVoltage.ToString());
        }
    }
}
