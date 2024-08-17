/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2009-2020 Michael Möller <mmoeller@openhardwaremonitor.org>
	
*/

using CapFrameX.Monitoring.Contracts;
using Microsoft.Win32;
using Serilog;
using System;
using System.Globalization;
using System.IO;

namespace OpenHardwareMonitor.Hardware.ATI
{
    internal sealed class ATIGPU : GPUBase
	{
		private readonly int adapterIndex;
		private readonly int busNumber;
		private readonly int deviceNumber;
		private readonly AdlGeneration adlGeneration;
		private readonly string driverpath;
		private readonly ISensorConfig sensorConfig;
		private readonly Sensor temperatureCore;
		private readonly Sensor temperatureMemory;
		private readonly Sensor temperatureVrmCore;
		private readonly Sensor temperatureVrmMemory;
		//private readonly Sensor temperatureVrmMemory0;
		//private readonly Sensor temperatureVrmMemory1;
		private readonly Sensor temperatureLiquid;
		private readonly Sensor temperaturePlx;
		private readonly Sensor temperatureHotSpot;
		private readonly Sensor temperatureVrmSoc;
		private readonly Sensor powerCore;
		private readonly Sensor powerPpt;
		private readonly Sensor powerSocket;
		private readonly Sensor powerTotal;
		private readonly Sensor powerSoc;
		private readonly Sensor powerTotalBoardSimulated;
		private readonly Sensor powerTotalBoard;
		private readonly Sensor fan;
		private readonly Sensor coreClock;
		private readonly Sensor memoryClock;
		private readonly Sensor socClock;
		private readonly Sensor coreVoltage;
		private readonly Sensor memoryVoltage;
		private readonly Sensor socVoltage;
		private readonly Sensor coreLoad;
		private readonly Sensor memoryControllerLoad;
		private readonly Sensor controlSensor;
		private readonly Control fanControl;
		private readonly Sensor memoryUsed;

		private IntPtr context;
		private readonly int overdriveVersion;

		public ATIGPU(string name, int adapterIndex, int busNumber,
		  int deviceNumber, IntPtr context, AdlGeneration adlGeneration, string driverPath,
			ISettings settings, ISensorConfig sensorConfig, IProcessService processService)
		  : base(name, new Identifier("atigpu",
			adapterIndex.ToString(CultureInfo.InvariantCulture)), settings, processService)
		{
			this.adapterIndex = adapterIndex;
			this.busNumber = busNumber;
			this.deviceNumber = deviceNumber;
			this.adlGeneration = adlGeneration;
			this.driverpath = driverPath;
			this.sensorConfig = sensorConfig;
			this.context = context;

			Log.Logger.Information($"AMD graphics card detected: {name}");
			Log.Logger.Information($"AMD GPU lib: {adlGeneration}");

			if (ADL.ADL_Overdrive_Caps(adapterIndex, out _, out _,
			  out overdriveVersion) != ADL.ADL_OK)
			{
				overdriveVersion = -1;
			}

			Log.Logger.Information($"ADL Overdrive version: {overdriveVersion}.");

			this.temperatureCore =
			  new Sensor("GPU Core", 0, SensorType.Temperature, this, settings);
			this.temperatureMemory =
			  new Sensor("GPU Memory", 1, SensorType.Temperature, this, settings);
			this.temperatureVrmCore =
			  new Sensor("GPU VRM Core", 2, SensorType.Temperature, this, settings);
			this.temperatureVrmMemory =
			  new Sensor("GPU VRM Memory", 3, SensorType.Temperature, this, settings);
			//this.temperatureVrmMemory0 =
			//  new Sensor("GPU VRM Memory #1", 4, SensorType.Temperature, this, settings);
			//this.temperatureVrmMemory1 =
			//  new Sensor("GPU VRM Memory #2", 5, SensorType.Temperature, this, settings);
			this.temperatureVrmSoc =
			  new Sensor("GPU VRM SOC", 6, SensorType.Temperature, this, settings);
			this.temperatureLiquid =
			  new Sensor("GPU Liquid", 7, SensorType.Temperature, this, settings);
			this.temperaturePlx =
			  new Sensor("GPU PLX", 8, SensorType.Temperature, this, settings);
			this.temperatureHotSpot =
			  new Sensor("GPU Hot Spot", 9, SensorType.Temperature, this, settings);

			this.powerTotal = new Sensor("GPU Total", 0, SensorType.Power, this, settings);
			this.powerCore = new Sensor("GPU Core", 1, SensorType.Power, this, settings);
			this.powerPpt = new Sensor("GPU PPT", 2, SensorType.Power, this, settings);
			this.powerSocket = new Sensor("GPU Socket", 3, SensorType.Power, this, settings);
			this.powerSoc = new Sensor("GPU SOC", 4, SensorType.Power, this, settings);
			this.powerTotalBoardSimulated = new Sensor("GPU TBP Sim", 5, SensorType.Power, this, settings);
			this.powerTotalBoard = new Sensor("GPU TBP", 6, SensorType.Power, this, settings);

			this.fan = new Sensor("GPU Fan", 0, SensorType.Fan, this, settings);

			this.coreClock = new Sensor("GPU Core", 0, SensorType.Clock, this, settings);
			this.memoryClock = new Sensor("GPU Memory", 1, SensorType.Clock, this, settings);
			this.socClock = new Sensor("GPU SOC", 2, SensorType.Clock, this, settings);

			this.coreVoltage = new Sensor("GPU Core", 0, SensorType.Voltage, this, settings);
			this.memoryVoltage = new Sensor("GPU Memory", 1, SensorType.Voltage, this, settings);
			this.socVoltage = new Sensor("GPU SOC", 2, SensorType.Voltage, this, settings);

			this.coreLoad = new Sensor("GPU Core", 0, SensorType.Load, this, settings);
			this.memoryControllerLoad = new Sensor("GPU Memory Controller", 1, SensorType.Load, this, settings);

			this.controlSensor = new Sensor("GPU Fan", 0, SensorType.Control, this, settings);

			this.memoryUsageDedicated = new Sensor("GPU Memory Dedicated", 0, SensorType.Data, this, settings);
			this.memoryUsageShared = new Sensor("GPU Memory Shared", 1, SensorType.Data, this, settings);
			this.processMemoryUsageDedicated = new Sensor("GPU Memory Dedicated Game", 2, SensorType.Data, this, settings);
			this.processMemoryUsageShared = new Sensor("GPU Memory Shared Game", 3, SensorType.Data, this, settings);
			this.memoryUsed = new Sensor("GPU Memory Used", 4, SensorType.Data, this, settings);


			ADLFanSpeedInfo afsi = new ADLFanSpeedInfo();
			if (ADL.ADL_Overdrive5_FanSpeedInfo_Get(adapterIndex, 0, ref afsi)
			  != ADL.ADL_OK)
			{
				afsi.MaxPercent = 100;
				afsi.MinPercent = 0;
			}

			this.fanControl = new Control(controlSensor, settings, afsi.MinPercent,
			  afsi.MaxPercent);
			this.fanControl.ControlModeChanged += ControlModeChanged;
			this.fanControl.SoftwareControlValueChanged +=
			  SoftwareControlValueChanged;
			ControlModeChanged(fanControl);
			this.controlSensor.Control = fanControl;

			Update();
		}

		private void SoftwareControlValueChanged(IControl control)
		{
			if (control.ControlMode == ControlMode.Software)
			{
				ADLFanSpeedValue adlf = new ADLFanSpeedValue();
				adlf.SpeedType = ADL.ADL_DL_FANCTRL_SPEED_TYPE_PERCENT;
				adlf.Flags = ADL.ADL_DL_FANCTRL_FLAG_USER_DEFINED_SPEED;
				adlf.FanSpeed = (int)control.SoftwareValue;
				ADL.ADL_Overdrive5_FanSpeed_Set(adapterIndex, 0, ref adlf);
			}
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
			ADL.ADL_Overdrive5_FanSpeedToDefault_Set(adapterIndex, 0);
		}

		public int BusNumber { get { return busNumber; } }

		public int DeviceNumber { get { return deviceNumber; } }


		public override HardwareType HardwareType
		{
			get { return HardwareType.GpuAti; }
		}

		public override Vendor Vendor => Vendor.AMD;

		private void GetODNTemperature(ADLODNTemperatureType type, Sensor sensor)
		{
			bool eval = sensorConfig.GetSensorEvaluate(sensor.IdentifierString);
			if (eval && (ADL.ADL2_OverdriveN_Temperature_Get(context, adapterIndex,
					type, out int temperature) == ADL.ADL_OK))
			{
				if (temperature >= 1E03)
					sensor.Value = 1E-03f * temperature;
				else
					sensor.Value = temperature;

				if (sensor.Value != 0)
					ActivateSensor(sensor);
			}
			else
			{
				sensor.Value = null;
			}
		}

		private void GetOD6Power(ADLODNCurrentPowerType type, Sensor sensor)
		{
			bool eval = sensorConfig.GetSensorEvaluate(sensor.IdentifierString);
			if (eval && (ADL.ADL2_Overdrive6_CurrentPower_Get(context, adapterIndex, type,
			  out int power) == ADL.ADL_OK))
			{
				sensor.Value = power * (1.0f / 0xFF);
				ActivateSensor(sensor);
			}
			else
			{
				sensor.Value = null;
			}
		}

		private void GetPMLog(ADLPMLogDataOutput data,
		  ADLSensorType sensorType, Sensor sensor, float factor = 1.0f)
		{
			if (sensorConfig.GetSensorEvaluate(sensor.IdentifierString))
			{
				int i = (int)sensorType;
				if (i < data.Sensors.Length && data.Sensors[i].Supported)
				{
					sensor.Value = data.Sensors[i].Value * factor;
					ActivateSensor(sensor);
				}
			}
			else
				sensor.Value = null;
		}

		public override void Update()
		{
			if (adlGeneration == AdlGeneration.ADL)
				UpdateAdl();
			else
				UpdateAdlX();

			UpdateWindowsPerformanceCounters();
		}

		private void UpdateAdlX()
		{
			var adlxTelemetryData = new AdlxTelemetryData();
			if (ADLX.GetAdlxTelemetry((uint)this.adapterIndex, (uint)this.sensorConfig.SensorLoggingRefreshPeriod, ref adlxTelemetryData))
			{
				// GPU Usage
				if (adlxTelemetryData.gpuUsageSupported)
				{
					coreLoad.Value = (float)adlxTelemetryData.gpuUsageValue;
					ActivateSensor(coreLoad);
				}
				else
					coreLoad.Value = null;

				// GPU Core Frequency
				if (adlxTelemetryData.gpuClockSpeedSupported)
				{
					coreClock.Value = (float)adlxTelemetryData.gpuClockSpeedValue;
					ActivateSensor(coreClock);
				}
				else
					coreClock.Value = null;

				// GPU VRAM Frequency
				if (adlxTelemetryData.gpuVRAMClockSpeedSupported)
				{
					memoryClock.Value = (float)adlxTelemetryData.gpuVRAMClockSpeedValue;
					ActivateSensor(memoryClock);
				}
				else
					memoryClock.Value = null;

				// GPU Temperature
				if (adlxTelemetryData.gpuTemperatureSupported)
				{
					temperatureCore.Value = (float)adlxTelemetryData.gpuTemperatureValue;
					ActivateSensor(temperatureCore);
				}
				else
					temperatureCore.Value = null;

				// GPU Hotspot Temperature
				if (adlxTelemetryData.gpuHotspotTemperatureSupported)
				{
					temperatureHotSpot.Value = (float)adlxTelemetryData.gpuHotspotTemperatureValue;
					ActivateSensor(temperatureHotSpot);
				}
				else
					temperatureHotSpot.Value = null;

				// GPU Power
				if (adlxTelemetryData.gpuPowerSupported)
				{
					powerTotal.Value = (float)adlxTelemetryData.gpuPowerValue;
					ActivateSensor(powerTotal);
				}
				else
					powerTotal.Value = null;

				// Fan Speed
				if (adlxTelemetryData.gpuFanSpeedSupported)
				{
					fan.Value = (float)adlxTelemetryData.gpuFanSpeedValue;
					ActivateSensor(fan);
				}
				else
					fan.Value = null;

				// VRAM Usage
				if (adlxTelemetryData.gpuVramSupported)
				{
					memoryUsed.Value = (float)(adlxTelemetryData.gpuVramValue * 1E-03);
					ActivateSensor(memoryUsed);
				}
				else
					memoryUsed.Value = null;

				// GPU Voltage
				if (adlxTelemetryData.gpuVoltageSupported)
				{
					coreVoltage.Value = (float)(adlxTelemetryData.gpuVoltageValue * 1E-03);
					ActivateSensor(coreVoltage);
				}
				else
					coreVoltage.Value = null;

				// GPU TBP
				if (adlxTelemetryData.gpuTotalBoardPowerSupported)
				{
					powerTotalBoard.Value = (float)adlxTelemetryData.gpuTotalBoardPowerValue;
					ActivateSensor(powerTotalBoard);
				}
				else
					powerTotalBoard.Value = null;
			}
		}

		private void UpdateAdl()
		{
			if (context != IntPtr.Zero && overdriveVersion >= 8 &&
			  ADL.ADL2_New_QueryPMLogData_Get(context, adapterIndex,
			  out var data) == ADL.ADL_OK)
			{
				GetPMLog(data, ADLSensorType.TEMPERATURE_EDGE, temperatureCore);
				GetPMLog(data, ADLSensorType.TEMPERATURE_MEM, temperatureMemory);
				GetPMLog(data, ADLSensorType.TEMPERATURE_VRVDDC, temperatureVrmCore);
				GetPMLog(data, ADLSensorType.TEMPERATURE_VRMVDD, temperatureVrmMemory);
				//GetPMLog(data, ADLSensorType.TEMPERATURE_VRMVDD0, temperatureVrmMemory0);
				//GetPMLog(data, ADLSensorType.TEMPERATURE_VRMVDD1, temperatureVrmMemory1);
				GetPMLog(data, ADLSensorType.TEMPERATURE_VRSOC, temperatureVrmSoc);
				GetPMLog(data, ADLSensorType.TEMPERATURE_LIQUID, temperatureLiquid);
				GetPMLog(data, ADLSensorType.TEMPERATURE_PLX, temperaturePlx);
				GetPMLog(data, ADLSensorType.TEMPERATURE_HOTSPOT, temperatureHotSpot);
				GetPMLog(data, ADLSensorType.GFX_POWER, powerCore);
				GetPMLog(data, ADLSensorType.ASIC_POWER, powerTotal);
				GetPMLog(data, ADLSensorType.SOC_POWER, powerSoc);
				GetPMLog(data, ADLSensorType.FAN_RPM, fan);
				GetPMLog(data, ADLSensorType.CLK_GFXCLK, coreClock);
				GetPMLog(data, ADLSensorType.CLK_MEMCLK, memoryClock);
				GetPMLog(data, ADLSensorType.CLK_SOCCLK, socClock);
				GetPMLog(data, ADLSensorType.GFX_VOLTAGE, coreVoltage, 0.001f);
				GetPMLog(data, ADLSensorType.MEM_VOLTAGE, memoryVoltage, 0.001f);
				GetPMLog(data, ADLSensorType.SOC_VOLTAGE, socVoltage, 0.001f);
				GetPMLog(data, ADLSensorType.INFO_ACTIVITY_GFX, coreLoad);
				GetPMLog(data, ADLSensorType.INFO_ACTIVITY_MEM, memoryControllerLoad);
				GetPMLog(data, ADLSensorType.FAN_PERCENTAGE, controlSensor);

				// Simulated total board power
				if (sensorConfig.GetSensorEvaluate(powerTotalBoardSimulated.IdentifierString))
				{
					float powerTotalValue = 0;
					if (!sensorConfig.GetSensorEvaluate(powerTotal.IdentifierString))
					{
						int i = (int)ADLSensorType.ASIC_POWER;
						if (i < data.Sensors.Length && data.Sensors[i].Supported)
						{
							powerTotalValue = data.Sensors[i].Value;
						}
					}
					else if (powerTotal.Value != null)
					{
						powerTotalValue = powerTotal.Value.Value;
					}

					// Linear fitting function (model)
					// TBP = 5W + 1.15 * ASIC Power
					powerTotalBoardSimulated.Value = powerTotalValue > 0 ? (float)Math.Round(5f + 1.15f * powerTotalValue, 0) : 0;

					if (powerTotalBoardSimulated.Value > 0)
						ActivateSensor(powerTotalBoardSimulated);
				}

				if (sensorConfig.GetSensorEvaluate(this.memoryUsed.IdentifierString))
				{
					if (ADL.ADL2_Adapter_VRAMUsage_Get(context, adapterIndex, out int vramUsage) == ADL.ADL_OK)
					{
						this.memoryUsed.Value = vramUsage / 1024f;
						ActivateSensor(this.memoryUsed);
					}
				}
			}
			else
			{
				if (context != IntPtr.Zero && overdriveVersion >= 7)
				{
					GetODNTemperature(ADLODNTemperatureType.CORE, temperatureCore);
					GetODNTemperature(ADLODNTemperatureType.MEMORY, temperatureMemory);
					GetODNTemperature(ADLODNTemperatureType.VRM_CORE, temperatureVrmCore);
					GetODNTemperature(ADLODNTemperatureType.VRM_MEMORY, temperatureVrmMemory);
					GetODNTemperature(ADLODNTemperatureType.LIQUID, temperatureLiquid);
					GetODNTemperature(ADLODNTemperatureType.PLX, temperaturePlx);
					GetODNTemperature(ADLODNTemperatureType.HOTSPOT, temperatureHotSpot);
				}
				else
				{
					ADLTemperature adlt = new ADLTemperature();
					bool evalTemperatureCore = sensorConfig.GetSensorEvaluate(temperatureCore.IdentifierString);
					if (evalTemperatureCore && (ADL.ADL_Overdrive5_Temperature_Get(adapterIndex, 0, ref adlt)
						== ADL.ADL_OK))
					{
						temperatureCore.Value = 0.001f * adlt.Temperature;
						ActivateSensor(temperatureCore);
					}
					else
					{
						temperatureCore.Value = null;
					}
				}

				if (context != IntPtr.Zero && overdriveVersion >= 6)
				{
					GetOD6Power(ADLODNCurrentPowerType.TOTAL_POWER, powerTotal);
					GetOD6Power(ADLODNCurrentPowerType.CHIP_POWER, powerCore);
					GetOD6Power(ADLODNCurrentPowerType.PPT_POWER, powerPpt);
					GetOD6Power(ADLODNCurrentPowerType.SOCKET_POWER, powerSocket);
				}

				ADLFanSpeedValue adlf = new ADLFanSpeedValue
				{
					SpeedType = ADL.ADL_DL_FANCTRL_SPEED_TYPE_RPM
				};

				bool evalFan = sensorConfig.GetSensorEvaluate(fan.IdentifierString);
				if (evalFan && (ADL.ADL_Overdrive5_FanSpeed_Get(adapterIndex, 0, ref adlf)
					  == ADL.ADL_OK))
				{
					fan.Value = adlf.FanSpeed;
					ActivateSensor(fan);
				}
				else
				{
					fan.Value = null;
				}

				adlf = new ADLFanSpeedValue
				{
					SpeedType = ADL.ADL_DL_FANCTRL_SPEED_TYPE_PERCENT
				};

				bool evalControlSensor = sensorConfig.GetSensorEvaluate(controlSensor.IdentifierString);
				if (evalControlSensor && (ADL.ADL_Overdrive5_FanSpeed_Get(adapterIndex, 0, ref adlf)
					== ADL.ADL_OK))
				{
					// ADL bug: percentage is not 0 when rpm is 0
					controlSensor.Value = fan.Value == 0 ? 0 : adlf.FanSpeed;
					ActivateSensor(controlSensor);
				}
				else
				{
					controlSensor.Value = null;
				}

				ADLPMActivity adlp = new ADLPMActivity();
				if (ADL.ADL_Overdrive5_CurrentActivity_Get(adapterIndex, ref adlp) == ADL.ADL_OK)
				{
					bool evalCoreClock = sensorConfig.GetSensorEvaluate(coreClock.IdentifierString);
					if (adlp.EngineClock > 0 && evalCoreClock)
					{
						coreClock.Value = 0.01f * adlp.EngineClock;
						ActivateSensor(coreClock);
					}
					else
					{
						coreClock.Value = null;
					}

					bool evalMemoryClock = sensorConfig.GetSensorEvaluate(memoryClock.IdentifierString);
					if (adlp.MemoryClock > 0 && evalMemoryClock)
					{
						memoryClock.Value = 0.01f * adlp.MemoryClock;
						ActivateSensor(memoryClock);
					}
					else
					{
						memoryClock.Value = null;
					}

					bool evalCoreVoltage = sensorConfig.GetSensorEvaluate(coreVoltage.IdentifierString);
					if (adlp.Vddc > 0 && evalCoreVoltage)
					{
						coreVoltage.Value = 0.001f * adlp.Vddc;
						ActivateSensor(coreVoltage);
					}
					else
					{
						coreVoltage.Value = null;
					}

					if (sensorConfig.GetSensorEvaluate(coreLoad.IdentifierString))
					{
						coreLoad.Value = Math.Min(adlp.ActivityPercent, 100);
						ActivateSensor(coreLoad);
					}
				}
				else
				{
					coreClock.Value = null;
					memoryClock.Value = null;
					coreVoltage.Value = null;
					coreLoad.Value = null;
				}
			}
		}

		private void UpdateWindowsPerformanceCounters()
		{

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
		}

		public override void Close()
		{
			this.fanControl.ControlModeChanged -= ControlModeChanged;
			this.fanControl.SoftwareControlValueChanged -=
			  SoftwareControlValueChanged;

			if (this.fanControl.ControlMode != ControlMode.Undefined)
				SetDefaultFanSpeed();
			base.Close();
		}

		public override string GetDriverVersion()
		{
			string GetDriverStringFromPath(string path)
			{
				string driverString = null;

				using (RegistryKey key = Registry.LocalMachine.OpenSubKey(path))
				{
					if (key != null)
					{
						var sv = key.GetValue("RadeonSoftwareVersion");
						var radeonSoftwareVersion = sv == null ? string.Empty : sv.ToString();

						driverString = $"Adrenalin {radeonSoftwareVersion}";
					}
				}

				return driverString;
			}

			if (this.adlGeneration == AdlGeneration.ADL)
			{
				int numberOfAdapters = 0;
				ADL.ADL_Adapter_NumberOfAdapters_Get(ref numberOfAdapters);
				ADLAdapterInfo[] adapterInfo = new ADLAdapterInfo[numberOfAdapters];
				if (numberOfAdapters > 0 && ADL.ADL_Adapter_AdapterInfo_Get(adapterInfo) == ADL.ADL_OK)
				{
					var path = adapterInfo[0].DriverPath.Replace("\\Registry\\Machine\\", "");
					return GetDriverStringFromPath(path);
				}
			}
			else
			{
				// SYSTEM\CurrentControlSet\Control\Class\
				return GetDriverStringFromPath(Path.Combine("SYSTEM\\CurrentControlSet\\Control\\Class\\",this.driverpath));
			}

			return base.GetDriverVersion();
		}
	}
}
