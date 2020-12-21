/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2009-2020 Michael Möller <mmoeller@openhardwaremonitor.org>
	
*/

using CapFrameX.Contracts.Sensor;
using Microsoft.Win32;
using Serilog;
using System;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Linq;
using System.Text;

namespace OpenHardwareMonitor.Hardware.ATI
{
    internal sealed class ATIGPU : Hardware
    {
        private readonly int adapterIndex;
        private readonly int busNumber;
        private readonly int deviceNumber;
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
        private readonly Sensor memoryUsageDedicated;
        private readonly Sensor memoryUsageShared;
        private readonly PerformanceCounter dedicatedVramUsagePerformCounter;
        private readonly PerformanceCounter sharedVramUsagePerformCounter;

        private IntPtr context;
        private readonly int overdriveVersion;

        public ATIGPU(string name, int adapterIndex, int busNumber,
          int deviceNumber, IntPtr context, ISettings settings, ISensorConfig config)
          : base(name, new Identifier("atigpu",
            adapterIndex.ToString(CultureInfo.InvariantCulture)), settings)
        {
            this.adapterIndex = adapterIndex;
            this.busNumber = busNumber;
            this.deviceNumber = deviceNumber;
            this.sensorConfig = config;

            this.context = context;

            if (ADL.ADL_Overdrive_Caps(adapterIndex, out _, out _,
              out overdriveVersion) != ADL.ADL_OK)
            {
                overdriveVersion = -1;
            }

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

            this.fan = new Sensor("GPU Fan", 0, SensorType.Fan, this, settings);

            this.coreClock = new Sensor("GPU Core", 0, SensorType.Clock, this, settings);
            this.memoryClock = new Sensor("GPU Memory", 1, SensorType.Clock, this, settings);
            this.socClock = new Sensor("GPU SOC", 2, SensorType.Clock, this, settings);

            this.coreVoltage = new Sensor("GPU Core", 0, SensorType.Voltage, this, settings);
            this.memoryVoltage = new Sensor("GPU Memory", 1, SensorType.Voltage, this, settings);
            this.socVoltage = new Sensor("GPU SOC", 2, SensorType.Voltage, this, settings);

            this.coreLoad = new Sensor("GPU Core", 0, SensorType.Load, this, settings);
            this.memoryControllerLoad = new Sensor("GPU Memory Controller", 1, SensorType.Load, this, settings);

            try
            {
                if (PerformanceCounterCategory.Exists("GPU Adapter Memory"))
                {
                    var category = new PerformanceCounterCategory("GPU Adapter Memory");
                    var instances = category.GetInstanceNames();

                    var (Usage, Index) = instances
                        .Select(instance => new PerformanceCounter("GPU Adapter Memory", "Dedicated Usage", instance))
                        .Select((u, i) => (Usage: u.RawValue, Index: i)).Max();

                    dedicatedVramUsagePerformCounter = new PerformanceCounter("GPU Adapter Memory", "Dedicated Usage", instances[Index]);
                    sharedVramUsagePerformCounter = new PerformanceCounter("GPU Adapter Memory", "Shared Usage", instances[Index]);

                    this.memoryUsageDedicated = new Sensor("GPU Memory Dedicated", 0, SensorType.SmallData, this, settings);
                    this.memoryUsageShared = new Sensor("GPU Memory Shared", 1, SensorType.SmallData, this, settings);
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Error while creating GPU memory performance counter.");
            }

            this.controlSensor = new Sensor("GPU Fan", 0, SensorType.Control, this, settings);

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

        private void GetODNTemperature(ADLODNTemperatureType type,
          Sensor sensor)
        {
            if (ADL.ADL2_OverdriveN_Temperature_Get(context, adapterIndex,
                type, out int temperature) == ADL.ADL_OK)
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
            if (sensorConfig.GetSensorEvaluate(sensor.Identifier.ToString()))
            {
                if (ADL.ADL2_Overdrive6_CurrentPower_Get(context, adapterIndex, type,
                  out int power) == ADL.ADL_OK)
                {
                    sensor.Value = power * (1.0f / 0xFF);
                    ActivateSensor(sensor);
                }
                else
                {
                    sensor.Value = null;
                }
            }
            else
                sensor.Value = null;

        }

        public override string GetReport()
        {
            var r = new StringBuilder();

            r.AppendLine("AMD GPU");
            r.AppendLine();

            r.Append("AdapterIndex: ");
            r.AppendLine(adapterIndex.ToString(CultureInfo.InvariantCulture));
            r.AppendLine();

            r.AppendLine("ADL Overdrive");
            r.AppendLine();
            int status = ADL.ADL_Overdrive_Caps(adapterIndex,
              out int supported, out int enabled, out int version);

            r.Append(" Status: ");
            r.AppendLine(status == ADL.ADL_OK ? "OK" :
                status.ToString(CultureInfo.InvariantCulture));
            r.Append(" Supported: ");
            r.AppendLine(supported.ToString(CultureInfo.InvariantCulture));
            r.Append(" Enabled: ");
            r.AppendLine(enabled.ToString(CultureInfo.InvariantCulture));
            r.Append(" Version: ");
            r.AppendLine(version.ToString(CultureInfo.InvariantCulture));
            r.AppendLine();

            if (context != IntPtr.Zero && overdriveVersion >= 6)
            {
                r.AppendLine("Overdrive6 CurrentPower:");
                r.AppendLine();
                for (int i = 0; i < 4; i++)
                {
                    var pt = ((ADLODNCurrentPowerType)i).ToString();
                    var ps = ADL.ADL2_Overdrive6_CurrentPower_Get(
                      context, adapterIndex, (ADLODNCurrentPowerType)i,
                      out int power);
                    if (ps == ADL.ADL_OK)
                    {
                        r.AppendFormat(" Power[{0}].Value: {1}{2}", pt,
                          power * (1.0f / 0xFF), Environment.NewLine);
                    }
                    else
                    {
                        r.AppendFormat(" Power[{0}].Status: {1}{2}", pt,
                          ps, Environment.NewLine);
                    }
                }
                r.AppendLine();
            }

            if (context != IntPtr.Zero && overdriveVersion >= 7)
            {
                r.AppendLine("OverdriveN Temperature:");
                r.AppendLine();
                for (int i = 1; i < 8; i++)
                {
                    var tt = ((ADLODNTemperatureType)i).ToString();
                    var ts = ADL.ADL2_OverdriveN_Temperature_Get(
                      context, adapterIndex, (ADLODNTemperatureType)i,
                      out int temperature);
                    if (ts == ADL.ADL_OK)
                    {
                        r.AppendFormat(" Temperature[{0}].Value: {1}{2}", tt,
                          0.001f * temperature, Environment.NewLine);
                    }
                    else
                    {
                        r.AppendFormat(" Temperature[{0}].Status: {1}{2}", tt,
                          ts, Environment.NewLine);
                    }
                }
                r.AppendLine();
            }

            if (context != IntPtr.Zero && overdriveVersion >= 8)
            {
                r.AppendLine("Performance Metrics:");
                r.AppendLine();
                var ps = ADL.ADL2_New_QueryPMLogData_Get(context, adapterIndex,
                  out var data);

                if (ps == ADL.ADL_OK)
                {
                    for (int i = 0; i < data.Sensors.Length; i++)
                    {
                        if (data.Sensors[i].Supported)
                        {
                            var st = ((ADLSensorType)i).ToString();
                            r.AppendFormat(" Sensor[{0}].Value: {1}{2}", st,
                              data.Sensors[i].Value, Environment.NewLine);
                        }
                    }
                }
                else
                {
                    r.Append(" Status: ");
                    r.AppendLine(ps.ToString(CultureInfo.InvariantCulture));
                }
                r.AppendLine();
            }

            return r.ToString();
        }

        private void GetPMLog(ADLPMLogDataOutput data,
          ADLSensorType sensorType, Sensor sensor, float factor = 1.0f)
        {
            if (sensorConfig.GetSensorEvaluate(sensor.Identifier.ToString()))
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
                    if (ADL.ADL_Overdrive5_Temperature_Get(adapterIndex, 0, ref adlt)
                      == ADL.ADL_OK)
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
                if (ADL.ADL_Overdrive5_FanSpeed_Get(adapterIndex, 0, ref adlf)
                  == ADL.ADL_OK)
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
                if (ADL.ADL_Overdrive5_FanSpeed_Get(adapterIndex, 0, ref adlf)
                  == ADL.ADL_OK)
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
                if (ADL.ADL_Overdrive5_CurrentActivity_Get(adapterIndex, ref adlp)
                  == ADL.ADL_OK)
                {
                    if (adlp.EngineClock > 0)
                    {
                        coreClock.Value = 0.01f * adlp.EngineClock;
                        ActivateSensor(coreClock);
                    }
                    else
                    {
                        coreClock.Value = null;
                    }

                    if (adlp.MemoryClock > 0)
                    {
                        memoryClock.Value = 0.01f * adlp.MemoryClock;
                        ActivateSensor(memoryClock);
                    }
                    else
                    {
                        memoryClock.Value = null;
                    }

                    if (adlp.Vddc > 0)
                    {
                        coreVoltage.Value = 0.001f * adlp.Vddc;
                        ActivateSensor(coreVoltage);
                    }
                    else
                    {
                        coreVoltage.Value = null;
                    }

                    coreLoad.Value = Math.Min(adlp.ActivityPercent, 100);
                    ActivateSensor(coreLoad);
                }
                else
                {
                    coreClock.Value = null;
                    memoryClock.Value = null;
                    coreVoltage.Value = null;
                    coreLoad.Value = null;
                }
            }

            // update VRAM usage
            if (dedicatedVramUsagePerformCounter != null)
            {
                try
                {
                    if (sensorConfig.GetSensorEvaluate(memoryUsageDedicated.Identifier.ToString()))
                    {
                        memoryUsageDedicated.Value = dedicatedVramUsagePerformCounter.NextValue() / 1024f / 1024f;
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
                    if (sensorConfig.GetSensorEvaluate(memoryUsageShared.Identifier.ToString()))
                    {
                        memoryUsageShared.Value = (float)sharedVramUsagePerformCounter.NextValue() / 1024f / 1024f;
                        ActivateSensor(memoryUsageShared);
                    }
                    else
                        memoryUsageShared.Value = null;
                }
                catch { memoryUsageShared.Value = null; }
            }
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
            int numberOfAdapters = 0;
            ADL.ADL_Adapter_NumberOfAdapters_Get(ref numberOfAdapters);
            ADLAdapterInfo[] adapterInfo = new ADLAdapterInfo[numberOfAdapters];
            if (numberOfAdapters > 0 && ADL.ADL_Adapter_AdapterInfo_Get(adapterInfo) == ADL.ADL_OK)
            {
                var path = adapterInfo[0].DriverPath.Replace("\\Registry\\Machine\\", "");
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(path))
                {
                    if (key != null)
                    {
                        var sv = key.GetValue("RadeonSoftwareVersion");
                        var se = key.GetValue("RadeonSoftwareEdition");
                        var radeonSoftwareVersion = sv == null ? string.Empty : sv.ToString();
                        var radeonSoftwareEdition = se == null ? string.Empty : se.ToString();

                        return $"{radeonSoftwareEdition} {radeonSoftwareVersion}";
                    }
                }
            }

            return base.GetDriverVersion();
        }
    }
}
