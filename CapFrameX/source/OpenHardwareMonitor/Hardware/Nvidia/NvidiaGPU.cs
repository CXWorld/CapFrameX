/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2009-2020 Michael Möller <mmoeller@openhardwaremonitor.org>
  Copyright (C) 2011 Christian Vallières
*/

using System;
using System.Globalization;
using System.Text;
using System.Linq;
using System.Diagnostics;
using CapFrameX.Monitoring.Contracts;
using Serilog;

namespace OpenHardwareMonitor.Hardware.Nvidia
{
	internal class NvidiaGPU : GPUBase
	{
		private readonly int adapterIndex;
		private readonly NvPhysicalGpuHandle handle;
		private readonly NvDisplayHandle? displayHandle;
		private readonly NVML.NvmlDevice? device;
		private readonly ISensorConfig sensorConfig;
		private readonly Stopwatch stopwatch;
		private uint lastpCounter;
		private int thermalSensorsMaxBit;

		private readonly Sensor[] temperatures;
		private readonly Sensor hotSpotTemperature;
		private readonly Sensor memoryJunctionTemperature;
		private readonly Sensor fan;
		private readonly Sensor[] clocks;
		private readonly Sensor voltage;
		private readonly Sensor[] loads;
		private readonly Sensor control;
		private readonly Sensor memoryLoad;
		private readonly Sensor memoryUsed;
		private readonly Sensor memoryFree;
		private readonly Sensor memoryAvail;
		private readonly Sensor power;
		private readonly Sensor pcieSlotPower;
		private readonly Sensor sixteenPinPower;
		private readonly Sensor eightPin1Power;
		private readonly Sensor eightPin2Power;
		private readonly Sensor eightPin3Power;
		private readonly Sensor pcieThroughputRx;
		private readonly Sensor pcieThroughputTx;
		private readonly Sensor monitorRefreshRate;
		private readonly Sensor powerLimit;
		private readonly Sensor temperatureLimit;
		private readonly Sensor voltageLimit;
		private readonly Control fanControl;

		public NvidiaGPU(int adapterIndex, NvPhysicalGpuHandle handle,
		  NvDisplayHandle? displayHandle, ISettings settings, ISensorConfig config, IProcessService processService)
		  : base(GetName(handle), new Identifier("nvidiagpu",
			  adapterIndex.ToString(CultureInfo.InvariantCulture)), settings, processService)
		{
			this.adapterIndex = adapterIndex;
			this.handle = handle;
			this.displayHandle = displayHandle;
			this.sensorConfig = config;
			this.stopwatch = new Stopwatch();

			Log.Information($"Nv graphics card detected: {name}");

			NvGPUThermalSettings thermalSettings = GetThermalSettings();
			Log.Information($"NvAPI GetThermalSettings sensor count: {thermalSettings.Count}");
			temperatures = new Sensor[thermalSettings.Count];
			for (int i = 0; i < temperatures.Length; i++)
			{
				NvSensor sensor = thermalSettings.Sensor[i];
				string name;
				switch (sensor.Target)
				{
					case NvThermalTarget.BOARD: 
						name = "GPU Board";
						Log.Information($"NvAPI NvThermalTarget: {sensor.Target}");
						break;
					case NvThermalTarget.GPU: 
						name = "GPU Core";
						Log.Information($"NvAPI NvThermalTarget: {sensor.Target}");
						break;
					case NvThermalTarget.MEMORY:
						name = "GPU Memory";
						Log.Information($"NvAPI NvThermalTarget: {sensor.Target}");
						break;
					case NvThermalTarget.POWER_SUPPLY:
						name = "GPU Power Supply";
						Log.Information($"NvAPI NvThermalTarget: {sensor.Target}");
						break;
					case NvThermalTarget.UNKNOWN:
						name = "GPU Unknown";
						Log.Information($"NvAPI NvThermalTarget: {sensor.Target}");
						break;
					default:
						name = "GPU";
						Log.Information($"NvAPI NvThermalTarget: default");
						break;
				}
				temperatures[i] = new Sensor(name, i, SensorType.Temperature, this,
				  new ParameterDescription[0], settings);
				ActivateSensor(temperatures[i]);
			}

			hotSpotTemperature = new Sensor("GPU Hot Spot", (int)thermalSettings.Count + 1, SensorType.Temperature, this, settings);
			memoryJunctionTemperature = new Sensor("GPU Memory Junction", (int)thermalSettings.Count + 2, SensorType.Temperature, this, settings);

			clocks = new Sensor[2];
			clocks[0] = new Sensor("GPU Core", 0, SensorType.Clock, this, settings);
			clocks[1] = new Sensor("GPU Memory", 1, SensorType.Clock, this, settings);
			// clocks[2] = new Sensor("GPU Shader", 2, SensorType.Clock, this, settings);

			for (int i = 0; i < clocks.Length; i++)
				ActivateSensor(clocks[i]);

			loads = new Sensor[4];
			loads[0] = new Sensor("GPU Core", 0, SensorType.Load, this, settings);
			loads[1] = new Sensor("GPU Frame Buffer", 1, SensorType.Load, this, settings);
			loads[2] = new Sensor("GPU Video Engine", 2, SensorType.Load, this, settings);
			loads[3] = new Sensor("GPU Bus Interface", 3, SensorType.Load, this, settings);
			memoryLoad = new Sensor("GPU Memory", 4, SensorType.Load, this, settings);
			memoryFree = new Sensor("GPU Memory Free", 1, SensorType.Data, this, settings);
			memoryUsed = new Sensor("GPU Memory Used", 2, SensorType.Data, this, settings);
			memoryAvail = new Sensor("GPU Memory Total", 3, SensorType.Data, this, settings);
			memoryUsageDedicated = new Sensor("GPU Memory Dedicated", 4, SensorType.Data, this, settings);
			memoryUsageShared = new Sensor("GPU Memory Shared", 5, SensorType.Data, this, settings);
			processMemoryUsageDedicated = new Sensor("GPU Memory Dedicated Game", 6, SensorType.Data, this, settings);
			processMemoryUsageShared = new Sensor("GPU Memory Shared Game", 7, SensorType.Data, this, settings);

			fan = new Sensor("GPU Fan", 0, SensorType.Fan, this, settings);
			control = new Sensor("GPU Fan", 1, SensorType.Control, this, settings);
			powerLimit = new Sensor("GPU Power Limit", 0, SensorType.Factor, this, settings);
			temperatureLimit = new Sensor("GPU Thermal Limit", 1, SensorType.Factor, this, settings);
			voltageLimit = new Sensor("GPU Voltage Limit", 2, SensorType.Factor, this, settings);
			voltage = new Sensor("GPU Voltage", 0, SensorType.Voltage, this, settings);
			power = new Sensor("GPU Power", 0, SensorType.Power, this, settings);
			pcieSlotPower = new Sensor("PCIe Slot Power", 1, SensorType.Power, this, settings);
			sixteenPinPower = new Sensor("16-Pin Power", 2, SensorType.Power, this, settings);
			eightPin1Power = new Sensor("8-Pin #1 Power", 3, SensorType.Power, this, settings);
			eightPin2Power = new Sensor("8-Pin #2 Power", 4, SensorType.Power, this, settings);
			eightPin3Power = new Sensor("8-Pin #3 Power", 5, SensorType.Power, this, settings);
			monitorRefreshRate = new Sensor("Monitor Refresh Rate", 0, SensorType.Frequency, this, settings);

			NvGPUCoolerSettings coolerSettings = GetCoolerSettings();
			if (coolerSettings.Count > 0)
			{
				fanControl = new Control(control, settings,
				  coolerSettings.Cooler[0].DefaultMin,
				  coolerSettings.Cooler[0].DefaultMax);
				fanControl.ControlModeChanged += ControlModeChanged;
				fanControl.SoftwareControlValueChanged += SoftwareControlValueChanged;
				ControlModeChanged(fanControl);
				control.Control = fanControl;
			}

			// Set max bit thermal sensor struct mask
			for (int i = 0; i < 32; i++)
			{
				try
				{
					thermalSensorsMaxBit = i;
					var thermalSensor = new PrivateThermalSensorsV2()
					{
						Version = NVAPI.GPU_THERMAL_STATUS_VER,
						Mask = 1u << i
					};

					var status = NVAPI.NvAPI_GPU_ThermalGetStatus(handle, ref thermalSensor);

					if (status != NvStatus.OK)
						throw new Exception();
				}
				catch
				{
					break;
				}
			}

			if (NVML.IsInitialized)
			{
				if (NVAPI.NvAPI_GPU_GetBusId != null &&
					NVAPI.NvAPI_GPU_GetBusId(handle, out uint busId) == NvStatus.OK)
				{
					if (NVML.NvmlDeviceGetHandleByPciBusId != null &&
					  NVML.NvmlDeviceGetHandleByPciBusId(
					  "0000:" + busId.ToString("X2") + ":00.0", out var result)
					  == NVML.NvmlReturn.Success)
					{
						device = result;
						pcieThroughputRx = new Sensor("GPU PCIE Rx", 0,
						  SensorType.Throughput, this, settings);
						pcieThroughputTx = new Sensor("GPU PCIE Tx", 1,
						  SensorType.Throughput, this, settings);
					}
				}
			}

			Update();
		}

		private static string GetName(NvPhysicalGpuHandle handle)
		{
			if (NVAPI.NvAPI_GPU_GetFullName(handle, out string gpuName) == NvStatus.OK)
			{
				var name = gpuName.Trim();
				return name.Contains("NVIDIA") ? name : $"NVIDIA {name}";
			}
			else
			{
				return "NVIDIA graphics card";
			}
		}

		public override HardwareType HardwareType
		{
			get { return HardwareType.GpuNvidia; }
		}

		public override Vendor Vendor => Vendor.Nvidia;

		private NvGPUThermalSettings GetThermalSettings()
		{
			NvGPUThermalSettings settings = new NvGPUThermalSettings
			{
				Version = NVAPI.GPU_THERMAL_SETTINGS_VER,
				Count = NVAPI.MAX_THERMAL_SENSORS_PER_GPU,
				Sensor = new NvSensor[NVAPI.MAX_THERMAL_SENSORS_PER_GPU]
			};

			if (!(NVAPI.NvAPI_GPU_GetThermalSettings != null &&
			  NVAPI.NvAPI_GPU_GetThermalSettings(handle, (int)NvThermalTarget.ALL,
				ref settings) == NvStatus.OK))
			{
				settings.Count = 0;
			}
			return settings;
		}

		private NvGPUCoolerSettings GetCoolerSettings()
		{
			NvGPUCoolerSettings settings = new NvGPUCoolerSettings
			{
				Version = NVAPI.GPU_COOLER_SETTINGS_VER,
				Cooler = new NvCooler[NVAPI.MAX_COOLER_PER_GPU]
			};

			if (!(NVAPI.NvAPI_GPU_GetCoolerSettings != null &&
			  NVAPI.NvAPI_GPU_GetCoolerSettings(handle, 0,
				ref settings) == NvStatus.OK))
			{
				settings.Count = 0;
			}
			return settings;
		}

		private NvFanCoolersStatus GetFanCoolersStatus()
		{
			var coolers = new NvFanCoolersStatus
			{
				Version = NVAPI.GPU_FAN_COOLERS_STATUS_VER,
				Items =
			  new NvFanCoolersStatusItem[NVAPI.MAX_FAN_COOLERS_STATUS_ITEMS]
			};

			if (!(NVAPI.NvAPI_GPU_ClientFanCoolersGetStatus != null &&
			   NVAPI.NvAPI_GPU_ClientFanCoolersGetStatus(handle, ref coolers)
			   == NvStatus.OK))
			{
				coolers.Count = 0;
			}
			return coolers;
		}

		private uint[] GetClocks()
		{
			NvClocks allClocks = new NvClocks
			{
				Version = NVAPI.GPU_CLOCKS_VER,
				Clock = new uint[NVAPI.MAX_CLOCKS_PER_GPU]
			};

			if (NVAPI.NvAPI_GPU_GetAllClocks != null &&
			  NVAPI.NvAPI_GPU_GetAllClocks(handle, ref allClocks) == NvStatus.OK)
			{
				return allClocks.Clock;
			}
			return null;
		}

		public override void Update()
		{
			if (temperatures.Any(sensor => sensorConfig.GetSensorEvaluate(sensor.IdentifierString)))
			{
				NvGPUThermalSettings settings = GetThermalSettings();
				foreach (Sensor sensor in temperatures)
					sensor.Value = settings.Sensor[sensor.Index].CurrentTemp;
			}
			else
			{
				foreach (Sensor sensor in temperatures)
					sensor.Value = null;
			}

			if ((sensorConfig.GetSensorEvaluate(hotSpotTemperature.IdentifierString)
				|| sensorConfig.GetSensorEvaluate(memoryJunctionTemperature.IdentifierString))
				&& thermalSensorsMaxBit > 0)
			{
				var thermalSensor = new PrivateThermalSensorsV2()
				{
					Version = NVAPI.GPU_THERMAL_STATUS_VER,
					Mask = (1u << thermalSensorsMaxBit) - 1
				};

				if (NVAPI.NvAPI_GPU_ThermalGetStatus != null &&
					NVAPI.NvAPI_GPU_ThermalGetStatus(handle, ref thermalSensor) == NvStatus.OK)
				{
					hotSpotTemperature.Value = thermalSensor.Temperatures[1] / 256f;
					// Ampere - 9, Ada Lovelace - 7
					var memJuncTemp = thermalSensor.Temperatures[9] == 0 ? thermalSensor.Temperatures[7] : thermalSensor.Temperatures[9];
					memoryJunctionTemperature.Value = memJuncTemp / 256f;
				}

				if (sensorConfig.GetSensorEvaluate(hotSpotTemperature.IdentifierString)
					&& hotSpotTemperature.Value != null && hotSpotTemperature.Value != 0)
				{
					ActivateSensor(hotSpotTemperature);
				}
				else
				{
					hotSpotTemperature.Value = null;
				}

				if (sensorConfig.GetSensorEvaluate(memoryJunctionTemperature.IdentifierString)
					&& memoryJunctionTemperature.Value != null && memoryJunctionTemperature.Value != 0)
				{
					ActivateSensor(memoryJunctionTemperature);
				}
				else
				{
					memoryJunctionTemperature.Value = null;
				}
			}
			else
			{
				hotSpotTemperature.Value = null;
				memoryJunctionTemperature.Value = null;
			}

			bool tachReadingOk = false;
			if (sensorConfig.GetSensorEvaluate(fan.IdentifierString))
			{
				if (NVAPI.NvAPI_GPU_GetTachReading != null &&
				  NVAPI.NvAPI_GPU_GetTachReading(handle, out int fanValue) == NvStatus.OK)
				{
					fan.Value = fanValue;
					ActivateSensor(fan);
					tachReadingOk = true;
				}
			}
			else
			{
				fan.Value = null;
			}

			if (clocks.Any(sensor => sensorConfig.GetSensorEvaluate(sensor.IdentifierString)))
			{
				uint[] values = GetClocks();
				if (values != null)
				{
					clocks[1].Value = 0.001f * values[8];
					if (values[30] != 0)
					{
						clocks[0].Value = 0.0005f * values[30];
						// clocks[2].Value = 0.001f * values[30];
					}
					else
					{
						clocks[0].Value = 0.001f * values[0];
						// clocks[2].Value = 0.001f * values[14];
					}
				}
			}
			else
			{
				for (int i = 0; i < clocks.Length; i++)
				{
					clocks[i].Value = null;
				}
			}

			if (sensorConfig.GetSensorEvaluate(voltage.IdentifierString))
			{
				var gpuVoltageStatus = new NvGpuVoltageStatus
				{
					Version = NVAPI.GPU_VOLTAGE_STATUS_VER,
					Unknown2 = new uint[8],
					Unknown3 = new uint[8]
				};

				if (NVAPI.NvAPI_GPU_GetCurrentVoltage != null &&
					 NVAPI.NvAPI_GPU_GetCurrentVoltage(handle, ref gpuVoltageStatus) == NvStatus.OK)
				{
					voltage.Value = gpuVoltageStatus.ValueInuV / 1E06f;
					ActivateSensor(voltage);
				}
			}
			else
			{
				voltage.Value = null;
			}

			if (loads.Any(sensor => sensorConfig.GetSensorEvaluate(sensor.IdentifierString)))
			{
				var infoEx = new NvDynamicPstatesInfoEx
				{
					Version = NVAPI.GPU_DYNAMIC_PSTATES_INFO_EX_VER,
					UtilizationDomains =
					new NvUtilizationDomainEx[NVAPI.NVAPI_MAX_GPU_UTILIZATIONS]
				};

				if (NVAPI.NvAPI_GPU_GetDynamicPstatesInfoEx != null &&
				  NVAPI.NvAPI_GPU_GetDynamicPstatesInfoEx(handle, ref infoEx) == NvStatus.OK)
				{
					for (int i = 0; i < loads.Length; i++)
					{
						if (infoEx.UtilizationDomains[i].Present)
						{
							loads[i].Value = infoEx.UtilizationDomains[i].Percentage;
							ActivateSensor(loads[i]);
						}
					}
				}
				else
				{
					var info = new NvDynamicPstatesInfo
					{
						Version = NVAPI.GPU_DYNAMIC_PSTATES_INFO_VER,
						UtilizationDomains =
					  new NvUtilizationDomain[NVAPI.NVAPI_MAX_GPU_UTILIZATIONS]
					};

					if (NVAPI.NvAPI_GPU_GetDynamicPstatesInfo != null &&
					  NVAPI.NvAPI_GPU_GetDynamicPstatesInfo(handle, ref info) == NvStatus.OK)
					{
						for (int i = 0; i < loads.Length; i++)
						{
							if (info.UtilizationDomains[i].Present)
							{
								loads[i].Value = info.UtilizationDomains[i].Percentage;
								ActivateSensor(loads[i]);
							}
						}
					}
				}
			}
			else
			{
				for (int i = 0; i < loads.Length; i++)
				{
					loads[i].Value = null;
				}
			}

			if (sensorConfig.GetSensorEvaluate(control.IdentifierString)
				|| sensorConfig.GetSensorEvaluate(fan.IdentifierString))
			{
				var coolerSettings = GetCoolerSettings();
				var coolerSettingsOk = false;
				if (coolerSettings.Count > 0)
				{
					control.Value = coolerSettings.Cooler[0].CurrentLevel;
					ActivateSensor(control);
					coolerSettingsOk = true;
				}

				if (!tachReadingOk || !coolerSettingsOk)
				{
					var coolersStatus = GetFanCoolersStatus();
					if (coolersStatus.Count > 0)
					{
						if (!coolerSettingsOk)
						{
							control.Value = coolersStatus.Items[0].CurrentLevel;
							ActivateSensor(control);
							coolerSettingsOk = true;
						}
						if (!tachReadingOk)
						{
							fan.Value = coolersStatus.Items[0].CurrentRpm;
							ActivateSensor(fan);
							tachReadingOk = true;
						}
					}
				}
			}
			else
			{
				control.Value = null;
				fan.Value = null;
			}

			if (!(!sensorConfig.GetSensorEvaluate(memoryAvail.IdentifierString) &&
				!sensorConfig.GetSensorEvaluate(memoryUsed.IdentifierString) &&
				!sensorConfig.GetSensorEvaluate(memoryFree.IdentifierString) &&
				!sensorConfig.GetSensorEvaluate(memoryLoad.IdentifierString)))
			{
				NvDisplayDriverMemoryInfo memoryInfo = new NvDisplayDriverMemoryInfo
				{
					Version = NVAPI.DISPLAY_DRIVER_MEMORY_INFO_VER,
					Values = new uint[NVAPI.MAX_MEMORY_VALUES_PER_GPU]
				};

				if (NVAPI.NvAPI_GetDisplayDriverMemoryInfo != null && displayHandle.HasValue &&
				  NVAPI.NvAPI_GetDisplayDriverMemoryInfo(displayHandle.Value, ref memoryInfo) ==
				  NvStatus.OK)
				{
					uint totalMemory = memoryInfo.Values[0];
					uint freeMemory = memoryInfo.Values[4];
					float usedMemory = Math.Max(totalMemory - freeMemory, 0);
					memoryFree.Value = (float)freeMemory / 1024 / 1024;
					memoryAvail.Value = (float)totalMemory / 1024 / 1024;
					memoryUsed.Value = usedMemory / 1024 / 1024;
					memoryLoad.Value = 100f * usedMemory / totalMemory;
					ActivateSensor(memoryAvail);
					ActivateSensor(memoryUsed);
					ActivateSensor(memoryFree);
					ActivateSensor(memoryLoad);
				}
			}
			else
			{
				memoryAvail.Value = null;
				memoryUsed.Value = null;
				memoryFree.Value = null;
				memoryLoad.Value = null;
			}

			if (sensorConfig.GetSensorEvaluate(power.IdentifierString)
				|| sensorConfig.GetSensorEvaluate(pcieSlotPower.IdentifierString)
				|| sensorConfig.GetSensorEvaluate(sixteenPinPower.IdentifierString))
			{
				var channels = new NvGpuPowerMonitorPowerChannelStatus[NVAPI.POWER_STATUS_CHANNEL_COUNT];
				for (int i = 0; i < channels.Length; i++)
				{
					channels[i].Rsvd = new byte[NVAPI.POWER_STATUS_RSVD_SIZE];
				}

				var powerStatus = new NvGpuPowerStatus
				{
					Version = NVAPI.GPU_POWER_MONITOR_STATUS_VER,
					Rsvd = new byte[NVAPI.POWER_STATUS_RSVD_SIZE],
					Channels = channels
				};

				if (NVAPI.NvAPI_GPU_PowerMonitorGetStatus != null &&
				   NVAPI.NvAPI_GPU_PowerMonitorGetStatus(handle, ref powerStatus) == NvStatus.OK)
				{
					power.Value = powerStatus.TotalGpuPowermW * 1E-03f;
					ActivateSensor(power);

					//for (int i = 0; i < powerStatus.Channels.Length; i++)
					//{
					//	if (powerStatus.Channels[i].PwrAvgmW > 0)
					//		Console.WriteLine($"Channel {i}: {powerStatus.Channels[i].PwrAvgmW * 1E-03f}W");
					//}

					// Channel 4 = PCIe slot
					if (powerStatus.Channels[4].PwrAvgmW > 0)
					{
						pcieSlotPower.Value = powerStatus.Channels[4].PwrAvgmW * 1E-03f;
						ActivateSensor(pcieSlotPower);
					}

					// very dirty 12VHPWR detection
					if (this.name.Contains("3090 Ti") || this.name.Contains("4090")
						|| this.name.Contains("4080") || this.name.Contains("4070"))
					{						
						// Channel 5 = 16-pin
						sixteenPinPower.Value = powerStatus.Channels[5].PwrAvgmW * 1E-03f;
						ActivateSensor(sixteenPinPower);
					}
					// Channel 5-7 = 8-pin
					else
					{
						if (powerStatus.Channels[5].PwrAvgmW > 0)
						{
							eightPin1Power.Value = powerStatus.Channels[5].PwrAvgmW * 1E-03f;
							ActivateSensor(eightPin1Power);
						}

						if (powerStatus.Channels[6].PwrAvgmW > 0)
						{
							eightPin2Power.Value = powerStatus.Channels[6].PwrAvgmW * 1E-03f;
							ActivateSensor(eightPin2Power);
						}

						if (powerStatus.Channels[7].PwrAvgmW > 0)
						{
							eightPin3Power.Value = powerStatus.Channels[7].PwrAvgmW * 1E-03f;
							ActivateSensor(eightPin3Power);
						}
					}
				}
			}
			else
			{
				power.Value = null;
			}

			// update VRAM usage
			if (dedicatedVramUsagePerformCounter != null)
			{
				try
				{
					if (sensorConfig.GetSensorEvaluate(memoryUsageDedicated.IdentifierString))
					{
						memoryUsageDedicated.Value = dedicatedVramUsagePerformCounter.NextValue() / SCALE;
						ActivateSensor(memoryUsageDedicated);
					}
					else
						memoryUsageDedicated.Value = null;

				}
				catch { memoryUsageDedicated.Value = null; }
			}

			if (sharedVramUsagePerformCounter != null)
			{
				try
				{
					if (sensorConfig.GetSensorEvaluate(memoryUsageShared.IdentifierString))
					{
						memoryUsageShared.Value = (float)sharedVramUsagePerformCounter.NextValue() / SCALE;
						ActivateSensor(memoryUsageShared);
					}
					else
						memoryUsageShared.Value = null;
				}
				catch { memoryUsageShared.Value = null; }
			}

			try
			{
				if (sensorConfig.GetSensorEvaluate(processMemoryUsageDedicated.IdentifierString))
				{
					lock (_performanceCounterLock)
					{
						processMemoryUsageDedicated.Value = dedicatedVramUsageProcessPerformCounter == null
						? 0f : (float)dedicatedVramUsageProcessPerformCounter.NextValue() / SCALE;
					}
					ActivateSensor(processMemoryUsageDedicated);
				}
				else
					processMemoryUsageDedicated.Value = null;
			}
			catch { processMemoryUsageDedicated.Value = null; }

			try
			{
				if (sensorConfig.GetSensorEvaluate(processMemoryUsageShared.IdentifierString))
				{
					lock (_performanceCounterLock)
					{
						processMemoryUsageShared.Value = sharedVramUsageProcessPerformCounter == null
						? 0f : (float)sharedVramUsageProcessPerformCounter.NextValue() / SCALE;
					}
					ActivateSensor(processMemoryUsageShared);
				}
				else
					processMemoryUsageShared.Value = null;
			}
			catch { processMemoryUsageShared.Value = null; }

			if (pcieThroughputRx != null)
			{
				if (sensorConfig.GetSensorEvaluate(pcieThroughputRx.IdentifierString))
				{
					if (NVML.NvmlDeviceGetPcieThroughput(device.Value,
					  NVML.NvmlPcieUtilCounter.RxBytes, out uint value)
					  == NVML.NvmlReturn.Success)
					{
						pcieThroughputRx.Value = value / (1024f * 1024f);
						ActivateSensor(pcieThroughputRx);
					}
				}
				else
				{
					pcieThroughputRx.Value = null;
				}
			}

			if (pcieThroughputTx != null)
			{
				if (sensorConfig.GetSensorEvaluate(pcieThroughputTx.IdentifierString))
				{
					if (NVML.NvmlDeviceGetPcieThroughput(device.Value,
					  NVML.NvmlPcieUtilCounter.TxBytes, out uint value)
					  == NVML.NvmlReturn.Success)
					{
						pcieThroughputTx.Value = value / (1024f * 1024f);
						ActivateSensor(pcieThroughputTx);
					}
				}
				else
				{
					pcieThroughputTx.Value = null;
				}
			}

			if (sensorConfig.GetSensorEvaluate(monitorRefreshRate.IdentifierString))
			{
				if (NVAPI.NvAPI_GetVBlankCounter(displayHandle.Value, out uint pCounter)
					== NvStatus.OK)
				{
					var deltaTicks = stopwatch.ElapsedTicks;
					stopwatch.Restart();

					lock (_displayLock)
					{
						var currentRefreshRate = (float)(pCounter - lastpCounter) / deltaTicks * Stopwatch.Frequency;
						refreshRateBuffer.Add(currentRefreshRate);
						var refreshRateFiltered = (float)Math.Ceiling(refreshRateBuffer.RefreshRates.Average());
						monitorRefreshRate.Value = refreshRateFiltered > refreshRateCurrentWindowHandle ? refreshRateCurrentWindowHandle : refreshRateFiltered;
					}

					lastpCounter = pCounter;
					ActivateSensor(monitorRefreshRate);
				}
			}
			else
			{
				monitorRefreshRate.Value = null;
			}

			// Performance limits
			if (!(!sensorConfig.GetSensorEvaluate(powerLimit.IdentifierString) &&
			  !sensorConfig.GetSensorEvaluate(temperatureLimit.IdentifierString) &&
			  !sensorConfig.GetSensorEvaluate(voltageLimit.IdentifierString)))
			{
				PerformanceStatusV1 performanceStatus = new PerformanceStatusV1
				{
					Version = NVAPI.GPU_PERFORMANCE_STATUS_VER,
					TimersInNanoSecond = new ulong[NVAPI.PERFORMANCE_STATUS_TIMER_COUNT],
					Unknown5 = new uint[NVAPI.PERFORMANCE_STATUS_UNKNOWN_COUNT]
				};

				if (NVAPI.NvAPI_GPU_PerfGetStatus(handle, ref performanceStatus) == NvStatus.OK)
				{
					var currentActiveLimit = performanceStatus.PerformanceLimit;
					powerLimit.Value = ((currentActiveLimit & PerformanceLimit.PowerLimit)
						== PerformanceLimit.PowerLimit) ? 1 : 0;
					ActivateSensor(powerLimit);
					temperatureLimit.Value = ((currentActiveLimit & PerformanceLimit.TemperatureLimit)
						== PerformanceLimit.TemperatureLimit) ? 1 : 0;
					ActivateSensor(temperatureLimit);
					voltageLimit.Value = ((currentActiveLimit & PerformanceLimit.VoltageLimit)
						== PerformanceLimit.VoltageLimit) ? 1 : 0;
					ActivateSensor(voltageLimit);
				}
			}
			else
			{
				powerLimit.Value = null;
				temperatureLimit.Value = null;
				voltageLimit.Value = null;
			}
		}

		public override string GetReport()
		{
			StringBuilder r = new StringBuilder();

			r.AppendLine("Nvidia GPU");
			r.AppendLine();

			r.AppendFormat("Name: {0}{1}", name, Environment.NewLine);
			r.AppendFormat("Index: {0}{1}", adapterIndex, Environment.NewLine);

			if (displayHandle.HasValue && NVAPI.NvAPI_GetDisplayDriverVersion != null)
			{
				NvDisplayDriverVersion driverVersion = new NvDisplayDriverVersion();
				driverVersion.Version = NVAPI.DISPLAY_DRIVER_VERSION_VER;
				if (NVAPI.NvAPI_GetDisplayDriverVersion(displayHandle.Value,
				  ref driverVersion) == NvStatus.OK)
				{
					r.Append("Driver Version: ");
					r.Append(driverVersion.DriverVersion / 100);
					r.Append(".");
					r.Append((driverVersion.DriverVersion % 100).ToString("00",
					  CultureInfo.InvariantCulture));
					r.AppendLine();
					r.Append("Driver Branch: ");
					r.AppendLine(driverVersion.BuildBranch);
				}
			}
			r.AppendLine();

			if (NVAPI.NvAPI_GPU_GetPCIIdentifiers != null)
			{
				uint deviceId, subSystemId, revisionId, extDeviceId;

				NvStatus status = NVAPI.NvAPI_GPU_GetPCIIdentifiers(handle,
				  out deviceId, out subSystemId, out revisionId, out extDeviceId);

				if (status == NvStatus.OK)
				{
					r.Append("DeviceID: 0x");
					r.AppendLine(deviceId.ToString("X", CultureInfo.InvariantCulture));
					r.Append("SubSystemID: 0x");
					r.AppendLine(subSystemId.ToString("X", CultureInfo.InvariantCulture));
					r.Append("RevisionID: 0x");
					r.AppendLine(revisionId.ToString("X", CultureInfo.InvariantCulture));
					r.Append("ExtDeviceID: 0x");
					r.AppendLine(extDeviceId.ToString("X", CultureInfo.InvariantCulture));
					r.AppendLine();
				}
			}

			if (NVAPI.NvAPI_GPU_GetThermalSettings != null)
			{
				NvGPUThermalSettings settings = new NvGPUThermalSettings
				{
					Version = NVAPI.GPU_THERMAL_SETTINGS_VER,
					Count = NVAPI.MAX_THERMAL_SENSORS_PER_GPU,
					Sensor = new NvSensor[NVAPI.MAX_THERMAL_SENSORS_PER_GPU]
				};

				NvStatus status = NVAPI.NvAPI_GPU_GetThermalSettings(handle,
				  (int)NvThermalTarget.ALL, ref settings);

				r.AppendLine("Thermal Settings");
				r.AppendLine();
				if (status == NvStatus.OK)
				{
					for (int i = 0; i < settings.Count; i++)
					{
						r.AppendFormat(" Sensor[{0}].Controller: {1}{2}", i,
						  settings.Sensor[i].Controller, Environment.NewLine);
						r.AppendFormat(" Sensor[{0}].DefaultMinTemp: {1}{2}", i,
						  settings.Sensor[i].DefaultMinTemp, Environment.NewLine);
						r.AppendFormat(" Sensor[{0}].DefaultMaxTemp: {1}{2}", i,
						  settings.Sensor[i].DefaultMaxTemp, Environment.NewLine);
						r.AppendFormat(" Sensor[{0}].CurrentTemp: {1}{2}", i,
						  settings.Sensor[i].CurrentTemp, Environment.NewLine);
						r.AppendFormat(" Sensor[{0}].Target: {1}{2}", i,
						  settings.Sensor[i].Target, Environment.NewLine);
					}
				}
				else
				{
					r.Append(" Status: ");
					r.AppendLine(status.ToString());
				}
				r.AppendLine();
			}

			if (NVAPI.NvAPI_GPU_GetAllClocks != null)
			{
				NvClocks allClocks = new NvClocks
				{
					Version = NVAPI.GPU_CLOCKS_VER,
					Clock = new uint[NVAPI.MAX_CLOCKS_PER_GPU]
				};

				NvStatus status = NVAPI.NvAPI_GPU_GetAllClocks(handle, ref allClocks);

				r.AppendLine("Clocks");
				r.AppendLine();
				if (status == NvStatus.OK)
				{
					for (int i = 0; i < allClocks.Clock.Length; i++)
						if (allClocks.Clock[i] > 0)
						{
							r.AppendFormat(" Clock[{0}]: {1}{2}", i, allClocks.Clock[i],
							  Environment.NewLine);
						}
				}
				else
				{
					r.Append(" Status: ");
					r.AppendLine(status.ToString());
				}
				r.AppendLine();
			}

			if (NVAPI.NvAPI_GPU_GetTachReading != null)
			{
				NvStatus status = NVAPI.NvAPI_GPU_GetTachReading(handle, out int tachValue);

				r.AppendLine("Tachometer");
				r.AppendLine();
				if (status == NvStatus.OK)
				{
					r.AppendFormat(" Value: {0}{1}", tachValue, Environment.NewLine);
				}
				else
				{
					r.Append(" Status: ");
					r.AppendLine(status.ToString());
				}
				r.AppendLine();
			}

			if (NVAPI.NvAPI_GPU_GetDynamicPstatesInfoEx != null)
			{
				var info = new NvDynamicPstatesInfoEx
				{
					Version = NVAPI.GPU_DYNAMIC_PSTATES_INFO_EX_VER,
					UtilizationDomains =
				  new NvUtilizationDomainEx[NVAPI.NVAPI_MAX_GPU_UTILIZATIONS]
				};

				var status = NVAPI.NvAPI_GPU_GetDynamicPstatesInfoEx(handle, ref info);

				r.AppendLine("Utilization Domains Ex");
				r.AppendLine();
				if (status == NvStatus.OK)
				{
					for (int i = 0; i < info.UtilizationDomains.Length; i++)
						if (info.UtilizationDomains[i].Present)
							r.AppendFormat(" Percentage[{0}]: {1}{2}", i,
							  info.UtilizationDomains[i].Percentage, Environment.NewLine);
				}
				else
				{
					r.Append(" Status: ");
					r.AppendLine(status.ToString());
				}
				r.AppendLine();
			}

			if (NVAPI.NvAPI_GPU_GetDynamicPstatesInfo != null)
			{
				var info = new NvDynamicPstatesInfo();
				info.Version = NVAPI.GPU_DYNAMIC_PSTATES_INFO_VER;
				info.UtilizationDomains =
				  new NvUtilizationDomain[NVAPI.NVAPI_MAX_GPU_UTILIZATIONS];
				var status = NVAPI.NvAPI_GPU_GetDynamicPstatesInfo(handle, ref info);

				r.AppendLine("Utilization Domains");
				r.AppendLine();
				if (status == NvStatus.OK)
				{
					for (int i = 0; i < info.UtilizationDomains.Length; i++)
						if (info.UtilizationDomains[i].Present)
							r.AppendFormat(" Percentage[{0}]: {1}{2}", i,
							  info.UtilizationDomains[i].Percentage, Environment.NewLine);
				}
				else
				{
					r.Append(" Status: ");
					r.AppendLine(status.ToString());
				}
				r.AppendLine();
			}

			if (NVAPI.NvAPI_GPU_GetCoolerSettings != null)
			{
				NvGPUCoolerSettings settings = new NvGPUCoolerSettings();
				settings.Version = NVAPI.GPU_COOLER_SETTINGS_VER;
				settings.Cooler = new NvCooler[NVAPI.MAX_COOLER_PER_GPU];
				NvStatus status =
				  NVAPI.NvAPI_GPU_GetCoolerSettings(handle, 0, ref settings);

				r.AppendLine("Cooler Settings");
				r.AppendLine();
				if (status == NvStatus.OK)
				{
					for (int i = 0; i < settings.Count; i++)
					{
						r.AppendFormat(" Cooler[{0}].Type: {1}{2}", i,
						  settings.Cooler[i].Type, Environment.NewLine);
						r.AppendFormat(" Cooler[{0}].Controller: {1}{2}", i,
						  settings.Cooler[i].Controller, Environment.NewLine);
						r.AppendFormat(" Cooler[{0}].DefaultMin: {1}{2}", i,
						  settings.Cooler[i].DefaultMin, Environment.NewLine);
						r.AppendFormat(" Cooler[{0}].DefaultMax: {1}{2}", i,
						  settings.Cooler[i].DefaultMax, Environment.NewLine);
						r.AppendFormat(" Cooler[{0}].CurrentMin: {1}{2}", i,
						  settings.Cooler[i].CurrentMin, Environment.NewLine);
						r.AppendFormat(" Cooler[{0}].CurrentMax: {1}{2}", i,
						  settings.Cooler[i].CurrentMax, Environment.NewLine);
						r.AppendFormat(" Cooler[{0}].CurrentLevel: {1}{2}", i,
						  settings.Cooler[i].CurrentLevel, Environment.NewLine);
						r.AppendFormat(" Cooler[{0}].DefaultPolicy: {1}{2}", i,
						  settings.Cooler[i].DefaultPolicy, Environment.NewLine);
						r.AppendFormat(" Cooler[{0}].CurrentPolicy: {1}{2}", i,
						  settings.Cooler[i].CurrentPolicy, Environment.NewLine);
						r.AppendFormat(" Cooler[{0}].Target: {1}{2}", i,
						  settings.Cooler[i].Target, Environment.NewLine);
						r.AppendFormat(" Cooler[{0}].ControlType: {1}{2}", i,
						  settings.Cooler[i].ControlType, Environment.NewLine);
						r.AppendFormat(" Cooler[{0}].Active: {1}{2}", i,
						  settings.Cooler[i].Active, Environment.NewLine);
					}
				}
				else
				{
					r.Append(" Status: ");
					r.AppendLine(status.ToString());
				}
				r.AppendLine();
			}

			if (NVAPI.NvAPI_GetDisplayDriverMemoryInfo != null &&
			  displayHandle.HasValue)
			{
				NvDisplayDriverMemoryInfo memoryInfo = new NvDisplayDriverMemoryInfo();
				memoryInfo.Version = NVAPI.DISPLAY_DRIVER_MEMORY_INFO_VER;
				memoryInfo.Values = new uint[NVAPI.MAX_MEMORY_VALUES_PER_GPU];
				NvStatus status = NVAPI.NvAPI_GetDisplayDriverMemoryInfo(
				  displayHandle.Value, ref memoryInfo);

				r.AppendLine("Memory Info");
				r.AppendLine();
				if (status == NvStatus.OK)
				{
					for (int i = 0; i < memoryInfo.Values.Length; i++)
						r.AppendFormat(" Value[{0}]: {1}{2}", i,
							memoryInfo.Values[i], Environment.NewLine);
				}
				else
				{
					r.Append(" Status: ");
					r.AppendLine(status.ToString());
				}
				r.AppendLine();
			}

			if (NVAPI.NvAPI_GPU_ClientFanCoolersGetStatus != null)
			{
				var coolers = new NvFanCoolersStatus();
				coolers.Version = NVAPI.GPU_FAN_COOLERS_STATUS_VER;
				coolers.Items =
				  new NvFanCoolersStatusItem[NVAPI.MAX_FAN_COOLERS_STATUS_ITEMS];

				var status = NVAPI.NvAPI_GPU_ClientFanCoolersGetStatus(handle, ref coolers);

				r.AppendLine("Fan Coolers Status");
				r.AppendLine();
				if (status == NvStatus.OK)
				{
					for (int i = 0; i < coolers.Count; i++)
					{
						r.AppendFormat(" Items[{0}].Type: {1}{2}", i,
						  coolers.Items[i].Type, Environment.NewLine);
						r.AppendFormat(" Items[{0}].CurrentRpm: {1}{2}", i,
						  coolers.Items[i].CurrentRpm, Environment.NewLine);
						r.AppendFormat(" Items[{0}].CurrentMinLevel: {1}{2}", i,
						  coolers.Items[i].CurrentMinLevel, Environment.NewLine);
						r.AppendFormat(" Items[{0}].CurrentMaxLevel: {1}{2}", i,
						  coolers.Items[i].CurrentMaxLevel, Environment.NewLine);
						r.AppendFormat(" Items[{0}].CurrentLevel: {1}{2}", i,
						  coolers.Items[i].CurrentLevel, Environment.NewLine);
					}
				}
				else
				{
					r.Append(" Status: ");
					r.AppendLine(status.ToString());
				}
				r.AppendLine();
			}

			return r.ToString();
		}

		private void SoftwareControlValueChanged(IControl control)
		{
			NvGPUCoolerLevels coolerLevels = new NvGPUCoolerLevels();
			coolerLevels.Version = NVAPI.GPU_COOLER_LEVELS_VER;
			coolerLevels.Levels = new NvLevel[NVAPI.MAX_COOLER_PER_GPU];
			coolerLevels.Levels[0].Level = (int)control.SoftwareValue;
			coolerLevels.Levels[0].Policy = 1;
			NVAPI.NvAPI_GPU_SetCoolerLevels(handle, 0, ref coolerLevels);
		}

		private void ControlModeChanged(IControl control)
		{
			switch (control.ControlMode)
			{
				case ControlMode.Undefined:
					return;
				case ControlMode.Default:
					SetDefaultFanSpeed();
					break;
				case ControlMode.Software:
					SoftwareControlValueChanged(control);
					break;
				default:
					return;
			}
		}

		private void SetDefaultFanSpeed()
		{
			NvGPUCoolerLevels coolerLevels = new NvGPUCoolerLevels();
			coolerLevels.Version = NVAPI.GPU_COOLER_LEVELS_VER;
			coolerLevels.Levels = new NvLevel[NVAPI.MAX_COOLER_PER_GPU];
			coolerLevels.Levels[0].Policy = 0x20;
			NVAPI.NvAPI_GPU_SetCoolerLevels(handle, 0, ref coolerLevels);
		}

		public override void Close()
		{
			if (this.fanControl != null)
			{
				this.fanControl.ControlModeChanged -= ControlModeChanged;
				this.fanControl.SoftwareControlValueChanged -=
				  SoftwareControlValueChanged;

				if (this.fanControl.ControlMode != ControlMode.Undefined)
					SetDefaultFanSpeed();
			}

			base.Close();
		}

		public override string GetDriverVersion()
		{
			StringBuilder r = new StringBuilder();

			if (displayHandle.HasValue && NVAPI.NvAPI_GetDisplayDriverVersion != null)
			{
				NvDisplayDriverVersion driverVersion = new NvDisplayDriverVersion
				{
					Version = NVAPI.DISPLAY_DRIVER_VERSION_VER
				};

				if (NVAPI.NvAPI_GetDisplayDriverVersion(displayHandle.Value,
				  ref driverVersion) == NvStatus.OK)
				{
					r.Append(driverVersion.DriverVersion / 100);
					r.Append(".");
					r.Append((driverVersion.DriverVersion % 100).ToString("00",
					  CultureInfo.InvariantCulture));
				}
			}
			else
				return base.GetDriverVersion();

			return r.ToString();
		}
	}
}
