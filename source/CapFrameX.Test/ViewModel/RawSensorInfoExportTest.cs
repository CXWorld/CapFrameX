using CapFrameX.Data.Session.Classes;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Sensor.Reporting;
using CapFrameX.ViewModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.Test.ViewModel
{
    /// <summary>
    /// "Copy raw sensor info" on the Analysis page: the tab-separated sensor table it puts on the clipboard.
    /// </summary>
    [TestClass]
    public class RawSensorInfoExportTest
    {
        [TestMethod]
        public void BuildText_MatchesThePreviousExport()
        {
            var runs = new[] { SensorData(samples: 40, gapAt: 5), SensorData(samples: 25, gapAt: -1) };

            Assert.AreEqual(PreviousExport(runs), RawSensorInfoExport.BuildText(runs));
        }

        [TestMethod]
        public void BuildText_WritesAHeaderAndOneRowPerMeasureTime()
        {
            var lines = RawSensorInfoExport.BuildText(new[] { SensorData(samples: 4, gapAt: -1) })
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

            Assert.AreEqual(5, lines.Length);
            StringAssert.StartsWith(lines[0], "Time(s)\tCPU load(%)");
            StringAssert.EndsWith(lines[0], "Time in GPU limit(%)");
        }

        [TestMethod]
        public void LongSensorLog_IsExportedInLinearTime()
        {
            // 5 h at 250 ms. Every row read every sensor array and the whole GPU load again: O(n²),
            // which froze the UI on such a log. The limit only has to catch that.
            var runs = new[] { SensorData(samples: 72000, gapAt: -1) };

            var export = Task.Run(() => RawSensorInfoExport.BuildText(runs));

            Assert.IsTrue(export.Wait(TimeSpan.FromSeconds(30)), "The export must not scale with the square of the samples.");
            Assert.AreEqual(72001, export.Result.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length);
        }

        /// <summary>A sensor log with CPU and GPU load, GPU clock and a NaN slot at <paramref name="gapAt"/>.</summary>
        private static ISessionSensorData2 SensorData(int samples, int gapAt)
        {
            var data = new SessionSensorData2();
            var cpuLoad = Add(data, "CPU Total", "Load");
            var gpuLoad = Add(data, "GPU Core", "Load");
            var gpuClock = Add(data, "GPU Core", "Clock", key: "GPU Core Clock");
            for (int i = 0; i < samples; i++)
            {
                data.MeasureTime.Values.AddLast((i + 1) * 0.25);
                data.BetweenMeasureTime.Values.AddLast(0.25);
                cpuLoad.Values.AddLast(i == gapAt ? double.NaN : 20 + i % 50 + 0.4);
                gpuLoad.Values.AddLast(90 + i % 10);
                gpuClock.Values.AddLast(1800 + i % 7 * 15);
            }
            return data;
        }

        private static SessionSensorEntry Add(SessionSensorData2 data, string name, string type, string key = null)
        {
            var entry = new SessionSensorEntry(name, type);
            data.Add(key ?? name, entry);
            return entry;
        }

        /// <summary>DataViewModel.OnCopyRawSensorInfo before the export was moved, without the clipboard.</summary>
        private static string PreviousExport(IEnumerable<ISessionSensorData2> rawSensorInfos)
        {
            var propertyInfos = typeof(ISessionSensorData).GetProperties()
                .Where(pi => pi.GetCustomAttributes(false).OfType<SensorDataExportAttribute>().Any())
                .Where(pi => rawSensorInfos.All(rsi =>
                {
                    var values = pi.GetValue(rsi) as Array;
                    return values.Length > 0;
                }));

            StringBuilder builder = new StringBuilder();

            builder.AppendLine(string.Join("\t", propertyInfos.Select(pi =>
            {
                var attribute = pi.GetCustomAttributes(false).OfType<SensorDataExportAttribute>().FirstOrDefault();
                return attribute.Description;
            })) + "\t" + "Time in GPU limit(%)");

            foreach (var run in rawSensorInfos)
            {
                for (int i = 0; i < run.MeasureTime.Values.Count; i++)
                {
                    var gpuLoadLimit = SensorReport.GetPercentageInGpuLoadLimit(run.GpuUsage.ToList());

                    var lineValues = propertyInfos.Select(pi =>
                    {
                        var array = pi.GetValue(run) as Array;
                        return array.Length >= run.MeasureTime.Values.Count ? string.Format("{0:0.##}", array.GetValue(i)) : string.Empty;
                    });
                    builder.AppendLine(string.Join("\t", lineValues) + "\t" + gpuLoadLimit);
                }
            }

            return builder.ToString();
        }
    }
}
