using System;
using System.Globalization;
using System.Reactive.Linq;
using System.Runtime.InteropServices;

namespace MonitoringLibTestApp
{
    public class Program
    {
        private const double SAMPLE_INTERVAL_MS = 1000;
        private static ConsoleEventDelegate _handler;
        private static IDisposable _sensorUpdateHeartBeat;
        private static SensorService _sensorService = new SensorService();

        static void Main(string[] args)
        {
            _handler = new ConsoleEventDelegate(ConsoleEventCallback);
            SetConsoleCtrlHandler(_handler, true);

            _sensorService.ActivateAllSensors();
            _sensorUpdateHeartBeat = GetSensorUpdateHeartBeat();

            Console.ReadKey();
        }

        private static IDisposable GetSensorUpdateHeartBeat()
        {
            return Observable
                .Timer(DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(SAMPLE_INTERVAL_MS))
                .Subscribe(x =>
                {
                    _sensorService.UpdateSensors();

                    Console.Clear();
                    Console.WriteLine($"{_sensorService.CpuTemp.ToString("F1", CultureInfo.InvariantCulture)} °C");
                    Console.WriteLine($"{_sensorService.CPUFrequency.ToString("F0", CultureInfo.InvariantCulture)} MHz");
                    Console.WriteLine($"{_sensorService.CpuPPT.ToString("F0", CultureInfo.InvariantCulture)} W");
                    Console.WriteLine($"{_sensorService.VCoreVoltage.ToString("F1", CultureInfo.InvariantCulture)} V");
                });
        }

        private delegate bool ConsoleEventDelegate(int eventType);
        [DllImport("kernel32.dll", SetLastError = true)]

        private static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate callback, bool add);

        static bool ConsoleEventCallback(int eventType)
        {
            if (eventType == 2)
            {
                _sensorUpdateHeartBeat?.Dispose();
                _sensorService.Computer?.Close();
            }
            return false;
        }
    }
}
