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
		public static IEnumerable<ISensorReportItem> GetReportFromSessionSensorData(IEnumerable<ISessionSensorData> sessionsSensorData, double startTime = 0, double endTime = double.PositiveInfinity)
		{
			if (sessionsSensorData == null || !sessionsSensorData.Any() || sessionsSensorData.Any(session => session == null))
			{
				return Enumerable.Empty<ISensorReportItem>();
			}

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
						case EReportSensorName.CpuMaxClock when HasValues(sessionsSensorData, session => session.CpuMaxClock, out var values):
							AddSensorEntry(item, Math.Round(values.Average()), values.Min(), values.Max());
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
						case EReportSensorName.RamUsage when HasValues(sessionsSensorData, session => session.RamUsage, out var values):
							AddSensorEntry(item, Math.Round(values.Average(), 2), Math.Round(values.Min(), 2), Math.Round(values.Max(), 2));
							break;
					}
				}

				return sensorReportItems;
			}
			catch { return sensorReportItems; }

			bool HasValues<T>(IEnumerable<ISessionSensorData> sessionSensorData, Func<ISessionSensorData, IEnumerable<T>> selector, out List<T> values)
			{
				values = new List<T>();
				var measureTimes = sessionSensorData.SelectMany(x => x.MeasureTime).ToArray();
				var selectedValues = sessionsSensorData.SelectMany(selector).ToArray();
				for(int i = 0; i < selectedValues.Count(); i++)
				{
					var measureTime = measureTimes[i];
					if(measureTime >= startTime && measureTime <= endTime)
					{
						values.Add(selectedValues[i]);
					}
				}
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
