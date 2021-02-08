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

        public static Dictionary<string, int> roundingDictionary = new Dictionary<string, int>()
        {
            ["Power"] = 1,
            ["Data"] = 2,
            ["Voltage"] = 3,
            ["Time"] = 3
        };

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
                        case EReportSensorName.GpuLoadLimit when HasValues(sessionsSensorData, session => session.GpuUsage, out var values):
                            AddSensorEntry(item, GetPercentageInGpuLoadLimit(values), GetPercentageInGpuLoadLimit(values), GetPercentageInGpuLoadLimit(values));
                            break;
                        case EReportSensorName.GpuClock when HasValues(sessionsSensorData, session => session.GpuClock, out var values):
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
                        case EReportSensorName.VRamUsageGB when HasValues(sessionsSensorData, session => session.VRamUsageGB, out var values):
                            AddSensorEntry(item, Math.Round(values.Average(), 2), Math.Round(values.Min(), 2), Math.Round(values.Max(), 2));
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
                var selectedValues = sessionsSensorData.SelectMany(run =>
                {
                    var selectedValuesOfRun = selector(run);
                    return selectedValuesOfRun ?? Enumerable.Empty<T>();
                })?.ToArray();
                if (selectedValues is null)
                {
                    return values.Any();
                }
                for (int i = 0; i < selectedValues.Count(); i++)
                {
                    var measureTime = measureTimes[i];
                    if (measureTime >= startTime && measureTime <= endTime)
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

        public class SensorDictEntry
        {
            public string Name;
            public string Type;
            public double[] Values;
            public string DisplayName;
        }

        public static IEnumerable<ISensorReportItem> GetFullReportFromSessionSensorData(IEnumerable<ISessionSensorData2> sessionsSensorData, double startTime = 0, double endTime = double.PositiveInfinity)
        {
            if (sessionsSensorData == null || !sessionsSensorData.Any() || sessionsSensorData.Any(session => session == null))
            {
                return Enumerable.Empty<ISensorReportItem>();
            }

            var sensorReportItems = new List<ISensorReportItem>();
            foreach (var sensor in GetSensorReportEntries(sessionsSensorData, startTime, endTime).Where(x => x.Type != "Time"))
            {
                if (sensor.Values.Any())
                {
                    roundingDictionary.TryGetValue(sensor.Type, out var roundingDigits);
                    sensorReportItems.Add(new SensorReportItem
                    {
                        Name = sensor.DisplayName,
                        MinValue = Math.Round(sensor.Values.Min(), roundingDigits),
                        AverageValue = Math.Round(sensor.Values.Average(), roundingDigits),
                        MaxValue = Math.Round(sensor.Values.Max(), roundingDigits),
                        RoundingDigits = roundingDigits
                    });
                }
            }

            return sensorReportItems;
        }


        public static IEnumerable<SensorDictEntry> GetSensorReportEntries(IEnumerable<ISessionSensorData2> sessionsSensorData, double startTime = 0, double endTime = double.PositiveInfinity)
        {
            var measureTimes = sessionsSensorData.SelectMany(x => x.MeasureTime.Values).ToArray();
            var sensorDict = new Dictionary<string, List<double>>();
            foreach (var sensorData in sessionsSensorData)
            {
                foreach (var sensor in sensorData)
                {
                    var sensorDictKey = sensor.Value.Name + "/" + sensor.Value.Type;
                    if (!sensorDict.TryGetValue(sensorDictKey, out var sensorValues))
                    {
                        sensorValues = new List<double>();
                        sensorDict.Add(sensorDictKey, sensorValues);
                    }
                    sensorValues.AddRange(sensor.Value.Values);
                }
            }

            foreach (var sensor in sensorDict)
            {
                var filteredValueList = new List<double>();
                for (int i = 0; i < sensor.Value.Count(); i++)
                {
                    var measureTime = measureTimes[i];
                    if (measureTime >= startTime && measureTime <= endTime)
                    {
                        filteredValueList.Add(sensor.Value[i]);
                    }
                }
                sensor.Value.RemoveAll(x => true);
                sensor.Value.AddRange(filteredValueList);
            }

            string GetSensorNameSuffix(string type)
            {
                switch (type)
                {
                    case "Voltage":
                        return "(V)";
                    case "Clock":
                        return "(MHz)";
                    case "Temperature":
                        return "(°C)";
                    case "Load":
                        return "(%)";
                    case "Fan":
                        return "(RPM)";
                    case "Flow":
                        return "(L/h)";
                    case "Control":
                        return "(%)";
                    case "Level":
                        return "(%)";
                    case "Power":
                        return "(W)";
                    case "Data":
                        return "(GB)";
                    case "SmallData":
                        return "(MB)";
                    case "Throughput":
                        return "(MB/s)";
                    case "Time":
                        return "(s)";
                    case "LoadLimit":
                        return "(%)";
                    default:
                        return string.Empty;
                }
            }

            if (sensorDict.TryGetValue("GPU Core/Load", out var gpuCoreLoadValues))
            {
                sensorDict.Add("GPU Limit Time/LoadLimit", Enumerable.Repeat(GetPercentageInGpuLoadLimit(gpuCoreLoadValues.Select(Convert.ToInt32)), gpuCoreLoadValues.Count()).ToList());
            }

            var order = new string[] { "measuretime", "gpu", "cpu" }.ToList();
            var sensorDictOrdered = sensorDict.Select(x =>
            {
                var nameSplitted = x.Key.Split('/');
                return new SensorDictEntry()
                {
                    Name = nameSplitted[0],
                    Type = nameSplitted[1],
                    Values = x.Value.ToArray(),
                    DisplayName = $"{nameSplitted[0]} {GetSensorNameSuffix(nameSplitted[1])}"
                };
            }).OrderBy(entry => entry.Name, Comparer<string>.Create((a, b) =>
            {
                var orderA = order.FindIndex(x => a.IndexOf(x, StringComparison.OrdinalIgnoreCase) > -1);
                if (orderA == -1) orderA = order.Count;
                var orderB = order.FindIndex(x => b.IndexOf(x, StringComparison.OrdinalIgnoreCase) > -1);
                if (orderB == -1) orderB = order.Count;

                return orderA.CompareTo(orderB);
            })).ThenBy(entry => entry.Type.Replace("SmallData", "Z").Replace("Data", "Z")).ThenBy(x => x.DisplayName.Replace("Package", " ").Length).ThenBy(x => x.DisplayName);


            return sensorDictOrdered;
        }

        public static double GetAverageCpuPower(IEnumerable<ISessionSensorData> sessionsSensorData, double startTime = 0, double endTime = double.PositiveInfinity)
        {
            var reportItems = GetReportFromSessionSensorData(sessionsSensorData, startTime, endTime);

            if (reportItems == null || !reportItems.Any())
                return 0;

            var item = reportItems.FirstOrDefault(reportItem =>
              reportItem.Name == EReportSensorName.CpuPower
              .GetAttribute<DescriptionAttribute>().Description);

            return item != null ? item.AverageValue : 0;
        }

        public static double GetAverageGpuPower(IEnumerable<ISessionSensorData> sessionsSensorData, double startTime = 0, double endTime = double.PositiveInfinity)
        {
            var reportItems = GetReportFromSessionSensorData(sessionsSensorData, startTime, endTime);

            if (reportItems == null || !reportItems.Any())
                return 0;

            var item = reportItems.FirstOrDefault(reportItem =>
              reportItem.Name == EReportSensorName.GpuPower
              .GetAttribute<DescriptionAttribute>().Description);

            return item != null ? item.AverageValue : 0;
        }

        public static double GetPercentageInGpuLoadLimit(IEnumerable<int> values)
        {
            double limitvalues = values.Count(val => val >= 97);
            double percentage = Math.Round((limitvalues / values.Count()) * 100);

            return percentage;
        }
    }
}
