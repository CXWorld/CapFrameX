using CapFrameX.Contracts.Sensor;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.Sensor
{
    public enum EReportSensorName
    {
        CpuUsage,
        CpuMaxThreadUsage,
        RamUsage,
        GpuUsage,
        GpuPower,
        GpuTemp,
        VRamUsage
    }

    public static class SensorReport
    {
        public static IEnumerable<ISensorReportItem> GetReportFromSessionSensorData
            (IEnumerable<ISessionSensorData> sessionsSensorData)
        {
            if (sessionsSensorData == null || !sessionsSensorData.Any() 
                || !sessionsSensorData.All(session => session != null))
                return Enumerable.Empty<ISensorReportItem>();

            var sensorReportItems = new List<ISensorReportItem>();
            try
            {
                foreach (var item in Enum.GetValues(typeof(EReportSensorName)).Cast<EReportSensorName>())
                {
                    var reportItem = new SensorReportItem
                    {
                        Name = item.GetDescription()
                    };

                    switch (item)
                    {
                        case EReportSensorName.CpuUsage:
                            reportItem.AverageValue = Math.Round(sessionsSensorData.Select(session => session.CpuUsage.Average()).Average());
                            reportItem.MinValue = sessionsSensorData.Select(session => session.CpuUsage.Min()).Min();
                            reportItem.MaxValue = sessionsSensorData.Select(session => session.CpuUsage.Max()).Max();
                            break;
                        case EReportSensorName.CpuMaxThreadUsage:
                            reportItem.AverageValue = Math.Round(sessionsSensorData.Select(session => session.CpuMaxThreadUsage.Average()).Average());
                            reportItem.MinValue = sessionsSensorData.Select(session => session.CpuMaxThreadUsage.Min()).Min();
                            reportItem.MaxValue = sessionsSensorData.Select(session => session.CpuMaxThreadUsage.Max()).Max();
                            break;
                        case EReportSensorName.RamUsage:
                            reportItem.AverageValue = Math.Round(sessionsSensorData.Select(session => session.RamUsage.Average()).Average(), 2);
                            reportItem.MinValue = Math.Round(sessionsSensorData.Select(session => session.RamUsage.Min()).Min(), 2);
                            reportItem.MaxValue = Math.Round(sessionsSensorData.Select(session => session.RamUsage.Max()).Max(), 2);
                            break;
                        case EReportSensorName.GpuUsage:
                            reportItem.AverageValue = Math.Round(sessionsSensorData.Select(session => session.GpuUsage.Average()).Average());
                            reportItem.MinValue = sessionsSensorData.Select(session => session.GpuUsage.Min()).Min();
                            reportItem.MaxValue = sessionsSensorData.Select(session => session.GpuUsage.Max()).Max();
                            break;
                        case EReportSensorName.GpuPower:
                            reportItem.AverageValue = Math.Round(sessionsSensorData.Select(session => session.GpuPower.Average()).Average());
                            reportItem.MinValue = sessionsSensorData.Select(session => session.GpuPower.Min()).Min();
                            reportItem.MaxValue = sessionsSensorData.Select(session => session.GpuPower.Max()).Max();
                            break;
                        case EReportSensorName.GpuTemp:
                            reportItem.AverageValue = Math.Round(sessionsSensorData.Select(session => session.GpuTemp.Average()).Average());
                            reportItem.MinValue = sessionsSensorData.Select(session => session.GpuTemp.Min()).Min();
                            reportItem.MaxValue = sessionsSensorData.Select(session => session.GpuTemp.Max()).Max();
                            break;
                        case EReportSensorName.VRamUsage:
                            reportItem.AverageValue = Math.Round(sessionsSensorData.Select(session => session.VRamUsage.Average()).Average());
                            reportItem.MinValue = sessionsSensorData.Select(session => session.VRamUsage.Min()).Min();
                            reportItem.MaxValue = sessionsSensorData.Select(session => session.VRamUsage.Max()).Max();
                            break;
                    }

                    sensorReportItems.Add(reportItem);
                }

                return sensorReportItems;
            }
            catch { return sensorReportItems; }
        }
    }
}
