using CapFrameX.Data.Session.Contracts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.Data.Session.Classes
{
    public class SessionSensorEntry : ISessionSensorEntry
    {
        public string Name { get; }
        public string Type { get; }
        public string StableIdentifier { get; set; }
        public LinkedList<double> Values { get; } = new LinkedList<double>();

        public SessionSensorEntry(string name, string type)
        {
            Name = name;
            Type = type;
        }
    }

    public class SessionSensorData2 : Dictionary<string, ISessionSensorEntry>, ISessionSensorData2
    {
        [JsonIgnore]
        public ISessionSensorEntry MeasureTime => this[nameof(MeasureTime)];
        [JsonIgnore]
        public ISessionSensorEntry BetweenMeasureTime => this[nameof(BetweenMeasureTime)];
        [JsonIgnore]
        double[] ISessionSensorData.MeasureTime { get => MeasureTime.Values.ToArray(); set => throw new NotImplementedException(); }
        [JsonIgnore]
        public int[] CpuUsage { get => ToInt32Values(Values.FirstOrDefault(c => c.Name.Contains("CPU Total") && c.Type == "Load")); set => throw new NotImplementedException(); }
        [JsonIgnore]
        public int[] CpuMaxThreadUsage { get => ToInt32Values(Values.FirstOrDefault(c => c.Name.Contains("CPU Max") && c.Type == "Load")); set => throw new NotImplementedException(); }
        [JsonIgnore]
        public int[] CpuMaxClock { get => ToInt32Values(Values.FirstOrDefault(c => c.Name.Equals("CPU Max") && c.Type == "Clock")); set => throw new NotImplementedException(); }
        [JsonIgnore]
        public int[] CpuPower { get => ToInt32Values(Values.FirstOrDefault(c => c.Name.Contains("CPU Package") && c.Type == "Power")); set => throw new NotImplementedException(); }
        [JsonIgnore]
        public int[] CpuTemp { get => ToInt32Values(Values.FirstOrDefault(c => c.Name.Contains("CPU Package") && c.Type == "Temperature")); set => throw new NotImplementedException(); }
        [JsonIgnore]
        public int[] GpuUsage { get => ToInt32Values(Values.FirstOrDefault(c => c.Name.Contains("GPU Core") && c.Type == "Load")); set => throw new NotImplementedException(); }
        [JsonIgnore]
        public int[] GpuClock { get => ToInt32Values(Values.FirstOrDefault(c => c.Name.Contains("GPU Core") && c.Type == "Clock")); set => throw new NotImplementedException(); }
        [JsonIgnore]
        // Prefer total/board power sensors over Intel's core-only "GPU TDP" so the analysis metric reflects TBP when both are enabled.
        public int[] GpuPower { get => ToInt32Values(Values.FirstOrDefault(c => (c.Name.Contains("GPU Power") || c.Name.Contains("GPU Total") || c.Name.Contains("GPU TBP")) && c.Type == "Power")
            ?? Values.FirstOrDefault(c => c.Name.Contains("GPU TDP") && c.Type == "Power")); set => throw new NotImplementedException(); }
        [JsonIgnore]
        public int[] GpuTBPSim { get => ToInt32Values(Values.FirstOrDefault(c => c.Name.Contains("GPU TBP Sim") && c.Type == "Power")); set => throw new NotImplementedException(); }
        [JsonIgnore]
        public int[] GpuTemp { get => ToInt32Values(Values.FirstOrDefault(c => c.Name.Contains("GPU Core") && c.Type == "Temperature")); set => throw new NotImplementedException(); }
        [JsonIgnore]
        public double[] RamUsage { get => FillMissingValues(Values.FirstOrDefault(c => c.Name.Contains("RAM Game Used") && c.Type == "Data")); set => throw new NotImplementedException(); }
        [JsonIgnore]
        public int[] VRamUsage { get => ToInt32Values(Values.FirstOrDefault(c => c.Name.Contains("Dedicated") && !c.Name.Contains("Game") && c.Type == "SmallData")); set => throw new NotImplementedException(); }
        [JsonIgnore]
        public double[] VRamUsageGB { get => FillMissingValues(Values.FirstOrDefault(c => c.Name.Contains("Dedicated") && !c.Name.Contains("Game") && c.Type == "Data")); set => throw new NotImplementedException(); }
        [JsonIgnore]
        public int[] GPUPowerLimit { get => ToInt32Values(Values.FirstOrDefault(c => c.Name.Contains("GPU Power Limit") && c.Type == "Factor")); set => throw new NotImplementedException(); }
        [JsonIgnore]
        public double[] BetweenMeasureTimes { get => BetweenMeasureTime.Values.ToArray(); set => throw new NotImplementedException(); }

        public SessionSensorData2(bool initialAdd = true)
        {
            if (initialAdd)
            {
                Add("MeasureTime", new SessionSensorEntry("MeasureTime", "Time"));
                Add("BetweenMeasureTime", new SessionSensorEntry("BetweenMeasureTime", "Time"));
            }
        }

        private static int[] ToInt32Values(ISessionSensorEntry entry)
            => FillMissingValues(entry).Select(Convert.ToInt32).ToArray();

        // The series stay index-aligned with MeasureTime, so a slot without a reading cannot be dropped.
        // SessionSensorDataLive logs such slots as NaN (a sensor that had no value yet, or none in that
        // measurement), which Convert.ToInt32 rejects. They take the preceding reading, leading ones the
        // first reading; a sensor without any reading counts as not logged.
        private static double[] FillMissingValues(ISessionSensorEntry entry)
        {
            if (entry == null)
                return Array.Empty<double>();

            var values = entry.Values.ToArray();
            int firstReading = Array.FindIndex(values, double.IsFinite);
            if (firstReading < 0)
                return Array.Empty<double>();

            double lastReading = values[firstReading];
            for (int i = 0; i < values.Length; i++)
            {
                if (double.IsFinite(values[i]))
                    lastReading = values[i];
                else
                    values[i] = lastReading;
            }

            return values;
        }
    }
}
