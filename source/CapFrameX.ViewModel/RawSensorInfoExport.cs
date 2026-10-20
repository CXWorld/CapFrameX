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
            var sensorRuns = runs.ToArray();
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
                var gpuLoadLimit = SensorReport.GetPercentageInGpuLoadLimit(run.GpuUsage.ToList());

                for (int i = 0; i < measureCount; i++)
                {
                    var lineValues = columns.Select(array =>
                        array.Length >= measureCount ? string.Format("{0:0.##}", array.GetValue(i)) : string.Empty);
                    builder.AppendLine(string.Join("\t", lineValues) + "\t" + gpuLoadLimit);
                }
            }

            return builder.ToString();
        }
    }
}
