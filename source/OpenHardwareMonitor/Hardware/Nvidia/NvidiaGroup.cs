/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2009-2020 Michael Möller <mmoeller@openhardwaremonitor.org>
	
*/

using CapFrameX.Extensions;
using CapFrameX.Monitoring.Contracts;
using Serilog;
using System.Collections.Generic;
using System.Linq;

namespace OpenHardwareMonitor.Hardware.Nvidia
{
    internal class NvidiaGroup : IGroup
	{
		private readonly List<Hardware> hardware = new List<Hardware>();

		public NvidiaGroup(ISettings settings, ISensorConfig sensorConfig, IProcessService processService)
		{
			if (!NVAPI.IsAvailable)
			{
				Log.Error("NvAPI not available.");
				return;
			}

			NvPhysicalGpuHandle[] handles =
			  new NvPhysicalGpuHandle[NVAPI.MAX_PHYSICAL_GPUS];
			int count;
			if (NVAPI.NvAPI_EnumPhysicalGPUs == null)
			{
				return;
			}
			else
			{
				NvStatus status = NVAPI.NvAPI_EnumPhysicalGPUs(handles, out count);
				if (status != NvStatus.OK)
				{
					return;
				}
			}

			if (NVML.NvmlInit() != NVML.NvmlReturn.Success)
			{
				Log.Error("Failed to intialize NVML.");
			}

			IDictionary<NvPhysicalGpuHandle, NvDisplayHandle> displayHandles =
			  new Dictionary<NvPhysicalGpuHandle, NvDisplayHandle>();

			if (NVAPI.NvAPI_EnumNvidiaDisplayHandle != null &&
			  NVAPI.NvAPI_GetPhysicalGPUsFromDisplay != null)
			{
				NvStatus status = NvStatus.OK;
				int i = 0;
				while (status == NvStatus.OK)
				{
					NvDisplayHandle displayHandle = new NvDisplayHandle();
					status = NVAPI.NvAPI_EnumNvidiaDisplayHandle(i, ref displayHandle);
					i++;

					if (status == NvStatus.OK)
					{
						NvPhysicalGpuHandle[] handlesFromDisplay =
						  new NvPhysicalGpuHandle[NVAPI.MAX_PHYSICAL_GPUS];

						if (NVAPI.NvAPI_GetPhysicalGPUsFromDisplay(displayHandle,
						  handlesFromDisplay, out uint countFromDisplay) == NvStatus.OK)
						{
							for (int j = 0; j < countFromDisplay; j++)
							{
								if (!displayHandles.ContainsKey(handlesFromDisplay[j]))
									displayHandles.Add(handlesFromDisplay[j], displayHandle);
							}
						}
					}
				}
			}

			for (int i = 0; i < count; i++)
			{
				displayHandles.TryGetValue(handles[i], out NvDisplayHandle displayHandle);
				hardware.Add(new NvidiaGPU(i, handles[i], displayHandle, settings, sensorConfig, processService));
			}

		}

		public void RemoveDefaultAdapter()
		{
			if (!hardware.IsNullOrEmpty())
			{
				var defaultAdapter = hardware.FirstOrDefault(gpu => gpu.Name.Contains("Microsoft"));

				if (defaultAdapter != null)
					hardware.Remove(defaultAdapter);
			}
		}

		public IHardware[] Hardware
		{
			get
			{
				return hardware.ToArray();
			}
		}

		public string GetReport()
		{
			return string.Empty;
		}

		public void Close()
		{
			foreach (Hardware gpu in hardware)
				gpu.Close();

			if (NVML.IsInitialized)
			{
				NVML.NvmlShutdown();
			}
		}
	}
}
