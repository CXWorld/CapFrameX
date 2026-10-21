using CapFrameX.Data.Session.Contracts;
using CapFrameX.Sensor.Reporting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CapFrameX.ViewModel
{
    /// <summary>
    /// The tab-separated sensor table of "Copy raw sensor info": one row per measure time, one column
    /// per exported sensor series, and the share of time in the GPU load limit.
    /// </summary>
    internal static class RawSensorInfoExport
    {
        public static string BuildText(IEnumerable<ISessionSensorData2> runs)
        {
            // A run of a record without sensor logging has no sensor data, e.g. one with PC latency only.
            var sensorRuns = runs.Where(run => run != null).ToArray();
            if (sensorRuns.Length == 0)
                return string.Empty;

            var propertyInfos = typeof(ISessionSensorData).GetProperties()
                .Where(pi => pi.GetCustomAttributes(false).OfType<SensorDataExportAttribute>().Any())
                .Where(pi => sensorRuns.All(run => (pi.GetValue(run) as Array).Length > 0))
                .ToArray();

            var builder = new StringBuilder();

            // Header
            builder.AppendLine(string.Join("\t", propertyInfos.Select(pi =>
            {
                var attribute = pi.GetCustomAttributes(false).OfType<SensorDataExportAttribute>().FirstOrDefault();
                return attribute.Description;
            })) + "\t" + "Time in GPU limit(%)");

            // Content. Every exported property builds a new array from the sensor's linked list, and the
            // GPU limit share walks the whole GPU load: read for each row, both made the copy O(n²) and
            // froze the UI on long sensor logs. They are read once per run.
            foreach (var run in sensorRuns)
            {
                int measureCount = run.MeasureTime.Values.Count;
                var columns = propertyInfos.Select(pi => pi.GetValue(run) as Array).ToArray();
                // Without GPU load there is no share to give: 0 of 0 samples is NaN.
                var gpuUsage = run.GpuUsage;
                var gpuLoadLimit = gpuUsage.Length > 0
                    ? SensorReport.GetPercentageInGpuLoadLimit(gpuUsage).ToString()
                    : string.Empty;

                for (int i = 0; i < measureCount; i++)
                {
                    // A sensor can miss its last samples, e.g. when the capture stopped in the middle of
                    // a measurement; only those cells stay empty, not the whole column.
                    var lineValues = columns.Select(array =>
                        i < array.Length ? string.Format("{0:0.##}", array.GetValue(i)) : string.Empty);
                    builder.AppendLine(string.Join("\t", lineValues) + "\t" + gpuLoadLimit);
                }
            }

            return builder.ToString();
        }
    }
}
