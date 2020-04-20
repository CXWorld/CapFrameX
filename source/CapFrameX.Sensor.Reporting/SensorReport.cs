using CapFrameX.Data.Session.Contracts;
using CapFrameX.Sensor.Reporting.Contracts;
using CapFrameX.Sensor.Reporting.Data;
using System;
using System.Collections.Generic;
using CapFrameX.Extensions.NetStandard;
using System.Linq;
using System.ComponentModel;

namespace CapFrameX.Sensor.Reporting
{
	public static class SensorReport
	{
		public static IEnumerable<ISensorReportItem> GetReportFromSessionSensorData(IEnumerable<ISessionSensorData> sessionsSensorData)
		{
			if (sessionsSensorData == null || !sessionsSensorData.Any()
				|| sessionsSensorData.Any(session => session == null))
				return Enumerable.Empty<ISensorReportItem>();

			var sensorReportItems = new List<ISensorReportItem>();
			try
			{
				foreach (var item in Enum.GetValues(typeof(EReportSensorName)).Cast<EReportSensorName>())
				{
					switch (item)
					{
						case EReportSensorName.CpuUsage when HasValues(sessionsSensorData, session => session.CpuUsage, out var values):
							AddSensorEntry(item, Math.Round(values.Average()), values.Min(), values.Max());
							break;
						case EReportSensorName.CpuMaxThreadUsage when HasValues(sessionsSensorData, session => session.CpuMaxThreadUsage, out var values):
							AddSensorEntry(item, Math.Round(values.Average()), values.Min(), values.Max());
							break;
						case EReportSensorName.CpuPower when HasValues(sessionsSensorData, session => session.CpuPower, out var values):
							AddSensorEntry(item, Math.Round(values.Average()), values.Min(), values.Max());
							break;
						case EReportSensorName.CpuTemp when HasValues(sessionsSensorData, session => session.CpuTemp, out var values):
							AddSensorEntry(item, Math.Round(values.Average()), values.Min(), values.Max());
							break;
						case EReportSensorName.RamUsage when HasValues(sessionsSensorData, session => session.RamUsage, out var values):
							AddSensorEntry(item, Math.Round(values.Average(), 2), Math.Round(values.Min(), 2), Math.Round(values.Max(), 2));
							break;
						case EReportSensorName.GpuUsage when HasValues(sessionsSensorData, session => session.GpuUsage, out var values):
							AddSensorEntry(item, Math.Round(values.Average()), values.Min(), values.Max());
							break;
						case EReportSensorName.GpuPower when HasValues(sessionsSensorData, session => session.GpuPower, out var values):
							AddSensorEntry(item, Math.Round(values.Average()), values.Min(), values.Max());
							break;
						case EReportSensorName.GpuTemp when HasValues(sessionsSensorData, session => session.GpuTemp, out var values):
							AddSensorEntry(item, Math.Round(values.Average()), values.Min(), values.Max());
							break;
						case EReportSensorName.VRamUsage when HasValues(sessionsSensorData, session => session.VRamUsage, out var values):
							AddSensorEntry(item, Math.Round(values.Average()), values.Min(), values.Max());
							break;
					}
				}

				return sensorReportItems;
			}
			catch { return sensorReportItems; }

			bool HasValues<T>(IEnumerable<ISessionSensorData> sessionSensorData, Func<ISessionSensorData, IEnumerable<T>> selector, out IEnumerable<T> values)
			{
				values = sessionsSensorData.SelectMany(selector);
				return values.Any();
			}

			void AddSensorEntry(EReportSensorName sensorName, double avg, double min, double max)
			{
				sensorReportItems.Add(new SensorReportItem
				{
					Name = sensorName.GetAttribute<DescriptionAttribute>().Description,
					MinValue = min,
					AverageValue = avg,
					MaxValue = max
				});
			}
		}
	}
}
