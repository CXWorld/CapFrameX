using CapFrameX.Contracts.Sensor;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace CapFrameX.Sensor
{
	public enum EReportSensorName
	{
		[Description("CPU load (%)")]
		CpuUsage,
		[Description("CPU max thread load (%)")]
		CpuMaxThreadUsage,
		[Description("CPU power (W)")]
		CpuPower,
		[Description("CPU temp (°C)")]
		CpuTemp,
		[Description("GPU load (%)")]
		GpuUsage,
		[Description("GPU power (W)")]
		GpuPower,
		[Description("GPU temp. (°C)")]
		GpuTemp,
		[Description("GPU VRAM usage (MB)")]
		VRamUsage,
		[Description("RAM usage (GB)")]
		RamUsage
	}

	public static class SensorReport
	{
		public static IEnumerable<ISensorReportItem> GetReportFromSessionSensorData
			(IEnumerable<ISessionSensorData> sessionsSensorData)
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
					Name = sensorName.GetDescription(),
					MinValue = min,
					AverageValue = avg,
					MaxValue = max
				});
			}
		}
	}
}
