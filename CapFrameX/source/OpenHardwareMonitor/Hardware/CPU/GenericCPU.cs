/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2010-2011 Michael Möller <mmoeller@openhardwaremonitor.org>
	
*/

using Serilog;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;

namespace OpenHardwareMonitor.Hardware.CPU
{
	internal class GenericCPU : Hardware
	{
		protected readonly CPUID[][] cpuid;

		private readonly Dictionary<int, int> threadCountMap = new Dictionary<int, int>();
		private readonly Dictionary<int, int> threadCoreMap = new Dictionary<int, int>();

		protected readonly uint family;
		protected readonly uint model;
		protected readonly uint packageType;
		protected readonly uint stepping;

		protected readonly int processorIndex;
		protected readonly int coreCount;
		private readonly double estimatedTimeStampCounterFrequency;
		private readonly double estimatedTimeStampCounterFrequencyError;

		private readonly Vendor vendor;

		private readonly CPULoad cpuLoad;
		private readonly Sensor totalLoad;
		private readonly Sensor maxLoad;
		private readonly Sensor[] threadLoads;

		private const uint CPUID_CORE_MASK_STATUS = 0x1A;

        private bool IsHybridDesign()
        {
            // Alder Lake (Intel 7/10nm): 0x97, 0x9A
            // Raptor Lake (Intel 7/10nm): 0xB7
            // Zen 5 (3nm)?
            // Meteor Lake (Intel 4/7nm: 0xAA
            return vendor == Vendor.Intel && family == 0x06
                && (model == 0x97 || model == 0x9A || model == 0xB7 || model == 0xAA);
        }

        protected string CoreString(int i)
		{
			if (coreCount == 1)
			{
				return $"CPU Core{GetCoreLabel(i)}";
			}

			return $"CPU Core #{i + 1}{GetCoreLabel(i)}";
		}

		// https://github.com/InstLatx64/InstLatX64_Demo/commit/e149a972655aff9c41f3eac66ad51fcfac1262b5
		protected string GetCoreLabel(int i)
		{
			string corelabel = string.Empty;

			if (IsHybridDesign())
			{
				var previousAffinity = ThreadAffinity.Set(cpuid[i][0].Affinity);
				if (Opcode.Cpuid(CPUID_CORE_MASK_STATUS, 0, out uint eax, out uint ebx, out uint ecx, out uint edx))
				{
					switch (eax >> 24)
					{
						case 0x20000002: corelabel = " LP-E"; break;
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

		public GenericCPU(int processorIndex, CPUID[][] cpuid, ISettings settings)
				: base(cpuid[0][0].Name, CreateIdentifier(cpuid[0][0].Vendor, processorIndex), settings)
		{
			this.cpuid = cpuid;

			this.vendor = cpuid[0][0].Vendor;

			this.family = cpuid[0][0].Family;
			this.model = cpuid[0][0].Model;
			this.stepping = cpuid[0][0].Stepping;
			this.packageType = cpuid[0][0].PkgType;

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

			if (coreCount > 1 || threadCountMap.Values.Max() > 1)
				totalLoad = new Sensor("CPU Total", 0, SensorType.Load, this, settings);
			else
				totalLoad = null;
			threadLoads = new Sensor[threadCountMap.Values.Sum()];
			for (int i = 0; i < threadLoads.Length; i++)
				threadLoads[i] = new Sensor(BuildCoreThreadString(i), i + 1,
					SensorType.Load, this, settings);
			maxLoad = new Sensor("CPU Max", threadLoads.Length + 1, SensorType.Load, this, settings);
			cpuLoad = new CPULoad(cpuid);

			if (cpuLoad.IsAvailable)
			{
				foreach (Sensor sensor in threadLoads)
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

				EstimatedTimeStampCounterFrequencyError = estimatedTimeStampCounterFrequencyError;

				ThreadAffinity.Set(previousAffinity);
			}
			else
			{
				EstimatedTimeStampCounterFrequencyError = 0;
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

		[DllImport("CapFrameX.Hwinfo.dll")]
		public static extern long GetTimeStampCounterFrequency();

		private void EstimateTimeStampCounterFrequency(out double frequency,
			out double error)
		{
			try
			{
				frequency = GetTimeStampCounterFrequency() / 1E06;
				error = frequency == 0 ? 1 : 0;
			}
			catch
			{
				frequency = 0;
				error = 1;
			}
		}

		protected virtual uint[] GetMSRs() => null;

		public override HardwareType HardwareType => HardwareType.CPU;

		public bool HasModelSpecificRegisters { get; }

		public bool HasTimeStampCounter { get; }

		public double TimeStampCounterFrequency { get; set; }

		public double EstimatedTimeStampCounterFrequencyError { get; set; }

		public override Vendor Vendor => vendor;

		public override void Update()
		{
			if (cpuLoad.IsAvailable)
			{
				cpuLoad.Update();
				for (int i = 0; i < threadLoads.Length; i++)
					threadLoads[i].Value = cpuLoad.GetThreadLoad(i);
				if (totalLoad != null)
					totalLoad.Value = cpuLoad.GetTotalLoad();
				if (maxLoad != null)
					maxLoad.Value = cpuLoad.GetMaxLoad();
			}
		}
	}
}
