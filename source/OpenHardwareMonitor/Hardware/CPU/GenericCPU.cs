/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2010-2011 Michael Möller <mmoeller@openhardwaremonitor.org>
	
*/

using CapFrameX.Contracts.Sensor;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;

namespace OpenHardwareMonitor.Hardware.CPU
{
    internal class GenericCPU : Hardware
    {
        protected readonly CPUID[][] cpuid;

        private readonly ISensorConfig sensorConfig;
        private readonly Dictionary<int, int> threadCountMap = new Dictionary<int, int>();
        private readonly Dictionary<int, int> threadCoreMap = new Dictionary<int, int>();

        protected readonly uint family;
        protected readonly uint model;
        protected readonly uint stepping;

        protected readonly int processorIndex;
        protected readonly int coreCount;
        private readonly bool isInvariantTimeStampCounter;
        private readonly double estimatedTimeStampCounterFrequency;
        private readonly double estimatedTimeStampCounterFrequencyError;

        private ulong lastTimeStampCount;
        private long lastTime;
        private readonly Vendor vendor;

        private readonly CPULoad cpuLoad;
        private readonly Sensor totalLoad;
        private readonly Sensor maxLoad;
        private readonly Sensor[] coreLoads;

        private const uint CPUID_CORE_MASK_STATUS = 0x1A;

        private bool IsBigLittleDesign()
        {
            // Alder Lake (10nm)
            // Raptor Lake (10nm)?
            // Zen 5 (3nm)?
            return vendor == Vendor.Intel && family == 0x06 && (model == 0x97 || model == 0x9A);
        }

        protected string CoreString(int i)
        {
            if (coreCount == 1)
            {
                return $"CPU Core{GetCoreLabel(i)}";
            }
            
            return "CPU Core #" + (i + 1) + GetCoreLabel(i);
        }

        // https://github.com/InstLatx64/InstLatX64_Demo/commit/e149a972655aff9c41f3eac66ad51fcfac1262b5
        protected string GetCoreLabel(int i)
        {
            string corelabel = string.Empty;

            if (IsBigLittleDesign())
            {
                var previousAffinity = ThreadAffinity.Set(cpuid[i][0].Affinity);
                if (Opcode.Cpuid(CPUID_CORE_MASK_STATUS, 0, out uint eax, out uint ebx, out uint ecx, out uint edx))
                {
                    switch (eax >> 24)
                    {
                        case 0x20: corelabel = " E"; break;
                        case 0x40: corelabel = " P"; break;
                        default: break;
                    }
                }

                ThreadAffinity.Set(previousAffinity);               
            }

            return corelabel;
        }

        private string BuildCoreThreadString(int i)
        {
            int core = threadCoreMap[i];
            int coreThreadCount = threadCountMap[core];

            if (coreThreadCount == 1)
            {
                return CoreString(core) + " - Thread #1";
            }
            else if (coreCount == 1)
            {
                return $"{CoreString(core)} - Thread #" + (i + 1);
            }
            else
            {
                return $"{CoreString(core)} - Thread #" + ((i % coreThreadCount) + 1);
            }
        }

        public GenericCPU(int processorIndex, CPUID[][] cpuid, ISettings settings, ISensorConfig config)
            : base(cpuid[0][0].Name, CreateIdentifier(cpuid[0][0].Vendor, processorIndex), settings)
        {
            this.sensorConfig = config;
            this.cpuid = cpuid;

            this.vendor = cpuid[0][0].Vendor;

            this.family = cpuid[0][0].Family;
            this.model = cpuid[0][0].Model;
            this.stepping = cpuid[0][0].Stepping;
            FillThreadMaps(cpuid);

            bool hasGlobalThreadCount = threadCountMap.Values.Distinct().Count() == 1;

            Log.Logger.Information("CPUID core count: {coreCount}.", cpuid.Length);

            if (hasGlobalThreadCount)
                Log.Logger.Information("CPUID thread count per core: {coreThreadCount}.", cpuid[0].Length);
            else
                Log.Logger.Information("CPU has different thread counts per core.");

            this.processorIndex = processorIndex;
            this.coreCount = cpuid.Length;

            // check if processor has MSRs
            if (cpuid[0][0].Data.GetLength(0) > 1
              && (cpuid[0][0].Data[1, 3] & 0x20) != 0)
                HasModelSpecificRegisters = true;
            else
                HasModelSpecificRegisters = false;

            // check if processor has a TSC
            if (cpuid[0][0].Data.GetLength(0) > 1
              && (cpuid[0][0].Data[1, 3] & 0x10) != 0)
                HasTimeStampCounter = true;
            else
                HasTimeStampCounter = false;

            // check if processor supports an invariant TSC 
            if (cpuid[0][0].ExtData.GetLength(0) > 7
              && (cpuid[0][0].ExtData[7, 3] & 0x100) != 0)
                isInvariantTimeStampCounter = true;
            else
                isInvariantTimeStampCounter = false;

            if (coreCount > 1 || threadCountMap.Values.Max() > 1)
                totalLoad = new Sensor("CPU Total", 0, SensorType.Load, this, settings);
            else
                totalLoad = null;
            coreLoads = new Sensor[threadCountMap.Values.Sum()];
            for (int i = 0; i < coreLoads.Length; i++)
                coreLoads[i] = new Sensor(BuildCoreThreadString(i), i + 1,
                  SensorType.Load, this, settings);
            maxLoad = new Sensor("CPU Max", coreLoads.Length + 1, SensorType.Load, this, settings);
            cpuLoad = new CPULoad(cpuid);

            if (cpuLoad.IsAvailable)
            {
                foreach (Sensor sensor in coreLoads)
                    ActivateSensor(sensor);
                if (totalLoad != null)
                    ActivateSensor(totalLoad);
                if (maxLoad != null)
                    ActivateSensor(maxLoad);
            }

            if (HasTimeStampCounter)
            {
                var previousAffinity = ThreadAffinity.Set(cpuid[0][0].Affinity);

                EstimateTimeStampCounterFrequency(
                  out estimatedTimeStampCounterFrequency,
                  out estimatedTimeStampCounterFrequencyError);

                ThreadAffinity.Set(previousAffinity);
            }
            else
            {
                estimatedTimeStampCounterFrequency = 0;
            }

            TimeStampCounterFrequency = estimatedTimeStampCounterFrequency;
        }

        private void FillThreadMaps(CPUID[][] cpuid)
        {
            int threadCount = 0;
            for (int i = 0; i < cpuid.Length; i++)
            {
                threadCountMap.Add(i, cpuid[i].Length);

                for (int t = 0; t < cpuid[i].Length; t++)
                {
                    threadCoreMap.Add(threadCount++, i);
                }
            }
        }

        private static Identifier CreateIdentifier(Vendor vendor,
          int processorIndex)
        {
            string s;
            switch (vendor)
            {
                case Vendor.AMD: s = "amdcpu"; break;
                case Vendor.Intel: s = "intelcpu"; break;
                default: s = "genericcpu"; break;
            }
            return new Identifier(s,
              processorIndex.ToString(CultureInfo.InvariantCulture));
        }

        private void EstimateTimeStampCounterFrequency(out double frequency,
          out double error)
        {
            // preload the function
            EstimateTimeStampCounterFrequency(0, out double f, out double e);
            EstimateTimeStampCounterFrequency(0, out f, out e);

            // estimate the frequency
            error = double.MaxValue;
            frequency = 0;
            for (int i = 0; i < 5; i++)
            {
                EstimateTimeStampCounterFrequency(0.025, out f, out e);
                if (e < error)
                {
                    error = e;
                    frequency = f;
                }

                if (error < 1e-4)
                    break;
            }
        }

        private void EstimateTimeStampCounterFrequency(double timeWindow,
          out double frequency, out double error)
        {
            long ticks = (long)(timeWindow * Stopwatch.Frequency);
            ulong countBegin, countEnd;

            long timeBegin = Stopwatch.GetTimestamp() +
              (long)Math.Ceiling(0.001 * ticks);
            long timeEnd = timeBegin + ticks;

            while (Stopwatch.GetTimestamp() < timeBegin) { }
            countBegin = Opcode.Rdtsc();
            long afterBegin = Stopwatch.GetTimestamp();

            while (Stopwatch.GetTimestamp() < timeEnd) { }
            countEnd = Opcode.Rdtsc();
            long afterEnd = Stopwatch.GetTimestamp();

            double delta = (timeEnd - timeBegin);
            frequency = 1e-6 *
              (((double)(countEnd - countBegin)) * Stopwatch.Frequency) / delta;

            double beginError = (afterBegin - timeBegin) / delta;
            double endError = (afterEnd - timeEnd) / delta;
            error = beginError + endError;
        }

        private static void AppendMSRData(StringBuilder r, uint msr, GroupAffinity affinity)
        {
            if (Ring0.RdmsrTx(msr, out uint eax, out uint edx, affinity))
            {
                r.Append(" ");
                r.Append((msr).ToString("X8", CultureInfo.InvariantCulture));
                r.Append("  ");
                r.Append((edx).ToString("X8", CultureInfo.InvariantCulture));
                r.Append("  ");
                r.Append((eax).ToString("X8", CultureInfo.InvariantCulture));
                r.AppendLine();
            }
        }

        protected virtual uint[] GetMSRs()
        {
            return null;
        }

        public override string GetReport()
        {
            StringBuilder r = new StringBuilder();

            switch (vendor)
            {
                case Vendor.AMD: r.AppendLine("AMD CPU"); break;
                case Vendor.Intel: r.AppendLine("Intel CPU"); break;
                default: r.AppendLine("Generic CPU"); break;
            }

            r.AppendLine();
            r.AppendFormat("Name: {0}{1}", name, Environment.NewLine);
            r.AppendFormat("Number of Cores: {0}{1}", coreCount,
              Environment.NewLine);
            r.AppendFormat("Threads per Core: {0}{1}", cpuid[0].Length,
              Environment.NewLine);
            r.AppendLine(string.Format(CultureInfo.InvariantCulture,
              "Timer Frequency: {0} MHz", Stopwatch.Frequency * 1e-6));
            r.AppendLine("Time Stamp Counter: " + (HasTimeStampCounter ? (
              isInvariantTimeStampCounter ? "Invariant" : "Not Invariant") : "None"));
            r.AppendLine(string.Format(CultureInfo.InvariantCulture,
              "Estimated Time Stamp Counter Frequency: {0} MHz",
              Math.Round(estimatedTimeStampCounterFrequency * 100) * 0.01));
            r.AppendLine(string.Format(CultureInfo.InvariantCulture,
              "Estimated Time Stamp Counter Frequency Error: {0} Mhz",
              Math.Round(estimatedTimeStampCounterFrequency *
              estimatedTimeStampCounterFrequencyError * 1e5) * 1e-5));
            r.AppendLine(string.Format(CultureInfo.InvariantCulture,
              "Time Stamp Counter Frequency: {0} MHz",
              Math.Round(TimeStampCounterFrequency * 100) * 0.01));
            r.AppendLine();

            uint[] msrArray = GetMSRs();
            if (msrArray != null && msrArray.Length > 0)
            {
                for (int i = 0; i < cpuid.Length; i++)
                {
                    r.AppendLine("MSR Core #" + (i + 1));
                    r.AppendLine();
                    r.AppendLine(" MSR       EDX       EAX");
                    foreach (uint msr in msrArray)
                        AppendMSRData(r, msr, cpuid[i][0].Affinity);
                    r.AppendLine();
                }
            }

            return r.ToString();
        }

        public override HardwareType HardwareType
        {
            get { return HardwareType.CPU; }
        }

        public bool HasModelSpecificRegisters { get; }

        public bool HasTimeStampCounter { get; }

        public double TimeStampCounterFrequency { get; set; }

        public override void Update()
        {
            if (sensorConfig.GetSensorEvaluate(totalLoad.IdentifierString)
                || sensorConfig.GetSensorEvaluate(maxLoad.IdentifierString)
                || coreLoads.Any(sensor => sensorConfig.GetSensorEvaluate(sensor.IdentifierString)))
            {
                if (HasTimeStampCounter && isInvariantTimeStampCounter)
                {
                    // make sure always the same thread is used
                    var previousAffinity = ThreadAffinity.Set(cpuid[0][0].Affinity);

                    // read time before and after getting the TSC to estimate the error
                    long firstTime = Stopwatch.GetTimestamp();
                    ulong timeStampCount = Opcode.Rdtsc();
                    long time = Stopwatch.GetTimestamp();

                    // restore the thread affinity mask
                    ThreadAffinity.Set(previousAffinity);

                    double delta = ((double)(time - lastTime)) / Stopwatch.Frequency;
                    double error = ((double)(time - firstTime)) / Stopwatch.Frequency;

                    // only use data if they are measured accuarte enough (max 0.1ms delay)
                    if (error < 1E-04)
                    {
                        // ignore the first reading because there are no initial values 
                        // ignore readings with too large or too small time window
                        if (lastTime != 0 && delta > 0.5 && delta < 2)
                        {
                            // update the TSC frequency with the new value
                            TimeStampCounterFrequency =
                              (timeStampCount - lastTimeStampCount) / (1e6 * delta);
                        }

                        lastTimeStampCount = timeStampCount;
                        lastTime = time;
                    }
                }
            }

            if (cpuLoad.IsAvailable)
            {
                cpuLoad.Update();
                for (int i = 0; i < coreLoads.Length; i++)
                    coreLoads[i].Value = cpuLoad.GetCoreLoad(i);
                if (totalLoad != null)
                    totalLoad.Value = cpuLoad.GetTotalLoad();
                if (maxLoad != null)
                    maxLoad.Value = cpuLoad.GetMaxLoad();
            }
        }
    }
}
