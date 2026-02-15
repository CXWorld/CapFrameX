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
			//Voltage, // V
            //Current, // A
            //Power, // W
            //Clock, // MHz
            //Temperature, // °C
            //Load, // %
            //Frequency, // Hz
            //Fan, // RPM
            //Flow, // L/h
            //Control, // %
            //Level, // %
            //Factor, // 1
            //Data, // GB = 2^30 Bytes
            //SmallData, // MB = 2^20 Bytes
            //Throughput, // B/s
            //TimeSpan, // Seconds
            //Timing, // ns
            //Energy, // milliwatt-hour (mWh)
            //Noise, // dBA
            //Conductivity, // µS/cm
            //Humidity // %

            ["Voltage"] = 3,
            ["Current"] = 1,
            ["Clock"] = 0,
            ["Temperature"] = 0,
            ["Load"] = 0,
            ["Frequency"] = 0,
            ["Fan"] = 0,
            ["Flow"] = 1,
            ["Control"] = 0,
            ["Level"] = 0,
            ["Power"] = 1,
            ["Data"] = 2,
            ["SmallData"] = 0,
            ["Throughput"] = 1,
            ["Time"] = 3,
            ["TimeSpan"] = 3,
            ["Timing"] = 1,
            ["Factor"] = 2,
            ["Energy"] = 1,
            ["Noise"] = 1,
            ["Conductivity"] = 1,
            ["Humidity"] = 0,
            ["LoadLimit"] = 0
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
                            AddSensorEntry(item, Math.Round(values.Average(), MidpointRounding.AwayFromZero), values.Min(), values.Max());
                            break;
                        case EReportSensorName.CpuMaxThreadUsage when HasValues(sessionsSensorData, session => session.CpuMaxThreadUsage, out var values):
                            AddSensorEntry(item, Math.Round(values.Average(), MidpointRounding.AwayFromZero), values.Min(), values.Max());
                            break;
                        case EReportSensorName.CpuPower when HasValues(sessionsSensorData, session => session.CpuPower, out var values):
                            AddSensorEntry(item, Math.Round(values.Average(), MidpointRounding.AwayFromZero), values.Min(), values.Max());
                            break;
                        case EReportSensorName.CpuTemp when HasValues(sessionsSensorData, session => session.CpuTemp, out var values):
                            AddSensorEntry(item, Math.Round(values.Average(), MidpointRounding.AwayFromZero), values.Min(), values.Max());
                            break;
                        case EReportSensorName.CpuMaxClock when HasValues(sessionsSensorData, session => session.CpuMaxClock, out var values):
                            AddSensorEntry(item, Math.Round(values.Average(), MidpointRounding.AwayFromZero), values.Min(), values.Max());
                            break;
                        case EReportSensorName.GpuUsage when HasValues(sessionsSensorData, session => session.GpuUsage, out var values):
                            AddSensorEntry(item, Math.Round(values.Average(), MidpointRounding.AwayFromZero), values.Min(), values.Max());
                            break;
                        case EReportSensorName.GpuLoadLimit when HasValues(sessionsSensorData, session => session.GpuUsage, out var values):
                            AddSensorEntry(item, GetPercentageInGpuLoadLimit(values), GetPercentageInGpuLoadLimit(values), GetPercentageInGpuLoadLimit(values));
                            break;
                        case EReportSensorName.GpuClock when HasValues(sessionsSensorData, session => session.GpuClock, out var values):
                            AddSensorEntry(item, Math.Round(values.Average(), MidpointRounding.AwayFromZero), values.Min(), values.Max());
                            break;
                        case EReportSensorName.GpuPower when HasValues(sessionsSensorData, session => session.GpuPower, out var values):
                            AddSensorEntry(item, Math.Round(values.Average(), MidpointRounding.AwayFromZero), values.Min(), values.Max());
                            break;
                        case EReportSensorName.GpuTBPSim when HasValues(sessionsSensorData, session => session.GpuTBPSim, out var values):
                            AddSensorEntry(item, Math.Round(values.Average(), MidpointRounding.AwayFromZero), values.Min(), values.Max());
                            break;
                        case EReportSensorName.GpuTemp when HasValues(sessionsSensorData, session => session.GpuTemp, out var values):
                            AddSensorEntry(item, Math.Round(values.Average(), MidpointRounding.AwayFromZero), values.Min(), values.Max());
                            break;
                        case EReportSensorName.VRamUsage when HasValues(sessionsSensorData, session => session.VRamUsage, out var values):
                            AddSensorEntry(item, Math.Round(values.Average(), MidpointRounding.AwayFromZero), values.Min(), values.Max());
                            break;
                        case EReportSensorName.VRamUsageGB when HasValues(sessionsSensorData, session => session.VRamUsageGB, out var values):
                            AddSensorEntry(item, Math.Round(values.Average(), 2, MidpointRounding.AwayFromZero), Math.Round(values.Min(), 2), Math.Round(values.Max(), 2, MidpointRounding.AwayFromZero));
                            break;
                        case EReportSensorName.RamUsage when HasValues(sessionsSensorData, session => session.RamUsage, out var values):
                            AddSensorEntry(item, Math.Round(values.Average(), 2, MidpointRounding.AwayFromZero), Math.Round(values.Min(), 2), Math.Round(values.Max(), 2, MidpointRounding.AwayFromZero));
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
            foreach (var sensor in GetSensorReportEntries(sessionsSensorData, startTime, endTime)
                .Where(x => x.Type != "Time"))
            {
                if (sensor.Values.Any())
                {
                    roundingDictionary.TryGetValue(sensor.Type, out var roundingDigits);
                    sensorReportItems.Add(new SensorReportItem
                    {
                        Name = sensor.DisplayName,
                        MinValue = Math.Round(sensor.Values.Min(), roundingDigits, MidpointRounding.AwayFromZero),
                        AverageValue = Math.Round(sensor.Values.Average(), roundingDigits, MidpointRounding.AwayFromZero),
                        MaxValue = Math.Round(sensor.Values.Max(), roundingDigits, MidpointRounding.AwayFromZero),
                        RoundingDigits = roundingDigits
                    });
                }
            }

            return sensorReportItems;
        }


        public static IEnumerable<SensorDictEntry> GetSensorReportEntries(IEnumerable<ISessionSensorData2> sessionsSensorData, double startTime = 0, double endTime = double.PositiveInfinity)
        {
            var sensorDict = new Dictionary<string, List<double>>();
            var sensorMeasureTimes = new Dictionary<string, List<double>>();
            var sensorMetadata = new Dictionary<string, (string Name, string Type)>();
            var sensorSessionCount = new Dictionary<string, int>();
            int totalSessionCount = 0;

            foreach (var sensorData in sessionsSensorData)
            {
                if (!sensorData.ContainsKey("MeasureTime"))
                    continue;

                totalSessionCount++;
                var sessionMeasureTimeValues = sensorData.MeasureTime.Values.ToList();

                foreach (var sensor in sensorData)
                {
                    var key = sensor.Key;
                    if (!sensorDict.TryGetValue(key, out var sensorValues))
                    {
                        sensorValues = new List<double>();
                        sensorDict.Add(key, sensorValues);
                        sensorMeasureTimes[key] = new List<double>();
                        sensorMetadata[key] = (sensor.Value.Name, sensor.Value.Type);
                        sensorSessionCount[key] = 0;
                    }
                    sensorValues.AddRange(sensor.Value.Values);
                    sensorMeasureTimes[key].AddRange(sessionMeasureTimeValues);
                    sensorSessionCount[key]++;
                }
            }

            // Remove sensors not present in all sessions
            var keysToRemove = sensorSessionCount
                .Where(kv => kv.Value < totalSessionCount)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                sensorDict.Remove(key);
                sensorMeasureTimes.Remove(key);
                sensorMetadata.Remove(key);
            }

            foreach (var sensor in sensorDict)
            {
                if (!sensorMeasureTimes.TryGetValue(sensor.Key, out var measureTimes)
                    || sensor.Value.Count != measureTimes.Count)
                    continue;

                var filteredValueList = new List<double>();
                for (int i = 0; i < sensor.Value.Count; i++)
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
                    case "Current":
                        return "(A)";
                    case "Clock":
                        return "(MHz)";
                    case "Temperature":
                        return "(°C)";
                    case "Load":
                        return "(%)";
                    case "Frequency":
                        return "(Hz)";
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
                        return "(GB/s)";
                    case "Time":
                        return "(s)";
                    case "TimeSpan":
                        return "(s)";
                    case "Timing":
                        return "(ns)";
                    case "Energy":
                        return "(mWh)";
                    case "Noise":
                        return "(dBA)";
                    case "Conductivity":
                        return "(µS/cm)";
                    case "Humidity":
                        return "(%)";
                    case "LoadLimit":
                        return "(%)";
                    default:
                        return string.Empty;
                }
            }

            var gpuLoadKey = sensorMetadata.FirstOrDefault(kv => kv.Value.Name == "GPU Core" && kv.Value.Type == "Load").Key;
            if (gpuLoadKey != null && sensorDict.TryGetValue(gpuLoadKey, out var gpuCoreLoadValues))
            {
                var gpuLimitKey = "__GPULimitTime__";
                sensorDict.Add(gpuLimitKey, Enumerable.Repeat(GetPercentageInGpuLoadLimit(gpuCoreLoadValues.Select(Convert.ToInt32)), gpuCoreLoadValues.Count()).ToList());
                sensorMetadata[gpuLimitKey] = ("GPU Limit Time", "LoadLimit");
            }

            var order = new string[] { "measuretime", "gpu", "cpu" }.ToList();
            var sensorDictOrdered = sensorDict
                .Where(x => sensorMetadata.ContainsKey(x.Key))
                .Select(x =>
            {
                var meta = sensorMetadata[x.Key];
                return new SensorDictEntry()
                {
                    Name = meta.Name,
                    Type = meta.Type,
                    Values = x.Value.ToArray(),
                    DisplayName = $"{meta.Name} {GetSensorNameSuffix(meta.Type)}"
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

        public static double GetAverageSensorValues(IEnumerable<ISessionSensorData> sessionsSensorData, EReportSensorName sensorname, double startTime = 0, double endTime = double.PositiveInfinity, bool useTBP = false)
        {
            var reportItems = GetReportFromSessionSensorData(sessionsSensorData, startTime, endTime);

            if (reportItems == null || !reportItems.Any())
                return 0;

            if (useTBP && reportItems.Any(reportItem => reportItem.Name == EReportSensorName.GpuTBPSim.GetAttribute<DescriptionAttribute>().Description))
                sensorname = EReportSensorName.GpuTBPSim;


            var item = reportItems.FirstOrDefault(reportItem =>
              reportItem.Name == sensorname
              .GetAttribute<DescriptionAttribute>().Description);



            return item != null ? item.AverageValue : 0;
        }

        public static double GetPercentageInGpuLoadLimit(IEnumerable<int> values)
        {
            double limitvalues = values.Count(val => val >= 97);
            double percentage = Math.Round((limitvalues / values.Count()) * 100, MidpointRounding.AwayFromZero);

            return percentage;
        }
    }
}
