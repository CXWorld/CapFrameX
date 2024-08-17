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
        public int[] CpuUsage { get => Values.FirstOrDefault(c => c.Name.Contains("CPU Total") && c.Type == "Load")?.Values.Select(Convert.ToInt32).ToArray() ?? Array.Empty<int>(); set => throw new NotImplementedException(); }
        [JsonIgnore]
        public int[] CpuMaxThreadUsage { get => Values.FirstOrDefault(c => c.Name.Contains("CPU Max") && c.Type == "Load")?.Values.Select(Convert.ToInt32).ToArray() ?? Array.Empty<int>(); set => throw new NotImplementedException(); }
        [JsonIgnore]
        public int[] CpuMaxClock { get => Values.FirstOrDefault(c => c.Name.Contains("CPU Max") && c.Type == "Clock")?.Values.Select(Convert.ToInt32).ToArray() ?? Array.Empty<int>(); set => throw new NotImplementedException(); }
        [JsonIgnore]
        public int[] CpuPower { get => Values.FirstOrDefault(c => c.Name.Contains("CPU Package") && c.Type == "Power")?.Values.Select(Convert.ToInt32).ToArray() ?? Array.Empty<int>(); set => throw new NotImplementedException(); }
        [JsonIgnore]
        public int[] CpuTemp { get => Values.FirstOrDefault(c => c.Name.Contains("CPU Package") && c.Type == "Temperature")?.Values.Select(Convert.ToInt32).ToArray() ?? Array.Empty<int>(); set => throw new NotImplementedException(); }
        [JsonIgnore]
        public int[] GpuUsage { get => Values.FirstOrDefault(c => c.Name.Contains("GPU Core") && c.Type == "Load")?.Values.Select(Convert.ToInt32).ToArray() ?? Array.Empty<int>(); set => throw new NotImplementedException(); }
        [JsonIgnore]
        public int[] GpuClock { get => Values.FirstOrDefault(c => c.Name.Contains("GPU Core") && c.Type == "Clock")?.Values.Select(Convert.ToInt32).ToArray() ?? Array.Empty<int>(); set => throw new NotImplementedException(); }
        [JsonIgnore]
        public int[] GpuPower { get => Values.FirstOrDefault(c => (c.Name.Contains("GPU Power") || c.Name.Contains("GPU Total") || c.Name.Contains("GPU TDP") || c.Name.Contains("GPU TBP")) && c.Type == "Power")?.Values.Select(Convert.ToInt32).ToArray() ?? Array.Empty<int>(); set => throw new NotImplementedException(); }
        [JsonIgnore]
        public int[] GpuTBPSim { get => Values.FirstOrDefault(c => c.Name.Contains("GPU TBP Sim") && c.Type == "Power")?.Values.Select(Convert.ToInt32).ToArray() ?? Array.Empty<int>(); set => throw new NotImplementedException(); }
        [JsonIgnore]
        public int[] GpuTemp { get => Values.FirstOrDefault(c => c.Name.Contains("GPU Core") && c.Type == "Temperature")?.Values.Select(Convert.ToInt32).ToArray() ?? Array.Empty<int>(); set => throw new NotImplementedException(); }
        [JsonIgnore]
        public double[] RamUsage { get => Values.FirstOrDefault(c => c.Name.Contains("Used Memory Game") && c.Type == "Data")?.Values.ToArray() ?? Array.Empty<double>(); set => throw new NotImplementedException(); }
        [JsonIgnore]
        public int[] VRamUsage { get => Values.FirstOrDefault(c => c.Name.Contains("Dedicated") && !c.Name.Contains("Game") && c.Type == "SmallData")?.Values.Select(Convert.ToInt32).ToArray() ?? Array.Empty<int>(); set => throw new NotImplementedException(); }
        [JsonIgnore]
        public double[] VRamUsageGB { get => Values.FirstOrDefault(c => c.Name.Contains("Dedicated") && !c.Name.Contains("Game") && c.Type == "Data")?.Values.ToArray() ?? Array.Empty<double>(); set => throw new NotImplementedException(); }
        [JsonIgnore]
        public int[] GPUPowerLimit { get => Values.FirstOrDefault(c => c.Name.Contains("GPU Power Limit") && c.Type == "Factor")?.Values.Select(Convert.ToInt32).ToArray() ?? Array.Empty<int>(); set => throw new NotImplementedException(); }
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
    }
}
