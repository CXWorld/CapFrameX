using CapFrameX.Contracts.PMD;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;

namespace CapFrameX.PMD.Benchlab
{
    public interface IBenchlabService
    {
        int CpuPowerSensorIndex { get; }

        int GpuPowerSensorIndex { get; }

        int MainboardPowerSensorIndex { get; }

        int SytemPowerSensorIndex { get; }

        int MonitoringInterval { get; set; }

        int MinMonitoringInterval { get; set; }

        bool IsServiceRunning { get; }

        IObservable<SensorSample> PmdSensorStream { get; }

        IObservable<EPmdServiceStatus> PmdServiceStatusStream { get; }

        Task StartService();

        void ShutDownService();

        IEnumerable<Point> GetEPS12VPowerPmdDataPoints(IList<SensorSample> sensorData);

        IEnumerable<Point> GetPciExpressPowerPmdDataPoints(IList<SensorSample> sensorData);
    }
}
