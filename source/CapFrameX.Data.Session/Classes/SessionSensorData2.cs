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
        public int[] CpuUsage { get => Values.FirstOrDefault(c => c.Name.Contains("CPU Total") && c.Type == "Load")?.Values.Select(ToInt32Safe).ToArray() ?? Array.Empty<int>(); set => throw new NotImplementedException(); }
        [JsonIgnore]
        public int[] CpuMaxThreadUsage { get => Values.FirstOrDefault(c => c.Name.Contains("CPU Max") && c.Type == "Load")?.Values.Select(ToInt32Safe).ToArray() ?? Array.Empty<int>(); set => throw new NotImplementedException(); }
        [JsonIgnore]
        public int[] CpuMaxClock { get => Values.FirstOrDefault(c => c.Name.Equals("CPU Max") && c.Type == "Clock")?.Values.Select(ToInt32Safe).ToArray() ?? Array.Empty<int>(); set => throw new NotImplementedException(); }
        [JsonIgnore]
        public int[] CpuPower { get => Values.FirstOrDefault(c => c.Name.Contains("CPU Package") && c.Type == "Power")?.Values.Select(ToInt32Safe).ToArray() ?? Array.Empty<int>(); set => throw new NotImplementedException(); }
        [JsonIgnore]
        public int[] CpuTemp { get => Values.FirstOrDefault(c => c.Name.Contains("CPU Package") && c.Type == "Temperature")?.Values.Select(ToInt32Safe).ToArray() ?? Array.Empty<int>(); set => throw new NotImplementedException(); }
        [JsonIgnore]
        public int[] GpuUsage { get => Values.FirstOrDefault(c => c.Name.Contains("GPU Core") && c.Type == "Load")?.Values.Select(ToInt32Safe).ToArray() ?? Array.Empty<int>(); set => throw new NotImplementedException(); }
        [JsonIgnore]
        public int[] GpuClock { get => Values.FirstOrDefault(c => c.Name.Contains("GPU Core") && c.Type == "Clock")?.Values.Select(ToInt32Safe).ToArray() ?? Array.Empty<int>(); set => throw new NotImplementedException(); }
        [JsonIgnore]
        // Prefer total/board power sensors over Intel's core-only "GPU TDP" so the analysis metric reflects TBP when both are enabled.
        public int[] GpuPower { get => (Values.FirstOrDefault(c => (c.Name.Contains("GPU Power") || c.Name.Contains("GPU Total") || c.Name.Contains("GPU TBP")) && c.Type == "Power") 
            ?? Values.FirstOrDefault(c => c.Name.Contains("GPU TDP") && c.Type == "Power"))?.Values.Select(ToInt32Safe).ToArray() ?? Array.Empty<int>(); set => throw new NotImplementedException(); }
        [JsonIgnore]
        public int[] GpuTBPSim { get => Values.FirstOrDefault(c => c.Name.Contains("GPU TBP Sim") && c.Type == "Power")?.Values.Select(ToInt32Safe).ToArray() ?? Array.Empty<int>(); set => throw new NotImplementedException(); }
        [JsonIgnore]
        public int[] GpuTemp { get => Values.FirstOrDefault(c => c.Name.Contains("GPU Core") && c.Type == "Temperature")?.Values.Select(ToInt32Safe).ToArray() ?? Array.Empty<int>(); set => throw new NotImplementedException(); }
        [JsonIgnore]
        public double[] RamUsage { get => Values.FirstOrDefault(c => c.Name.Contains("RAM Game Used") && c.Type == "Data")?.Values.ToArray() ?? Array.Empty<double>(); set => throw new NotImplementedException(); }
        [JsonIgnore]
        public int[] VRamUsage { get => Values.FirstOrDefault(c => c.Name.Contains("Dedicated") && !c.Name.Contains("Game") && c.Type == "SmallData")?.Values.Select(ToInt32Safe).ToArray() ?? Array.Empty<int>(); set => throw new NotImplementedException(); }
        [JsonIgnore]
        public double[] VRamUsageGB { get => Values.FirstOrDefault(c => c.Name.Contains("Dedicated") && !c.Name.Contains("Game") && c.Type == "Data")?.Values.ToArray() ?? Array.Empty<double>(); set => throw new NotImplementedException(); }
        [JsonIgnore]
        public int[] GPUPowerLimit { get => Values.FirstOrDefault(c => c.Name.Contains("GPU Power Limit") && c.Type == "Factor")?.Values.Select(ToInt32Safe).ToArray() ?? Array.Empty<int>(); set => throw new NotImplementedException(); }
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

        // A sensor polling tick can legitimately record a non-finite value (NaN is
        // serialized as the string "NaN" in the JSON) or a value outside the Int32
        // range when a hardware read fails for that tick. Convert.ToInt32(double)
        // throws OverflowException for any of those, which used to abort the whole
        // Analysis page update mid-way (charts left blank while the summary text
        // was already written). Map non-finite values to 0 (preserving array length
        // and therefore time alignment) and clamp out-of-range magnitudes instead.
        // The rounding mode matches Convert.ToInt32 (MidpointRounding.ToEven).
        private static int ToInt32Safe(double value)
        {
            if (!double.IsFinite(value))
                return 0;

            var rounded = Math.Round(value);
            return rounded >= int.MaxValue ? int.MaxValue
                 : rounded <= int.MinValue ? int.MinValue
                 : (int)rounded;
        }
    }
}
