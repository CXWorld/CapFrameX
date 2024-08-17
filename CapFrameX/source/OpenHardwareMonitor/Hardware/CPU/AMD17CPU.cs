/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2020 Michael Möller <mmoeller@openhardwaremonitor.org>
	
*/

using CapFrameX.Monitoring.Contracts;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace OpenHardwareMonitor.Hardware.CPU
{
    internal sealed class AMD17CPU : AMDCPU
    {
        private readonly Core[] cores;
        private readonly Sensor coreMaxClocks;
        private readonly Sensor coreTemperature;
        private readonly Sensor tctlTemperature;
        private readonly Sensor ccdMaxTemperature;
        private readonly Sensor ccdAvgTemperature;
        private readonly Sensor[] ccdTemperatures;
        private readonly Sensor coreVoltage;
        private readonly Sensor socVoltage;
        private readonly Sensor packagePowerSensor;
        private readonly Sensor coresPowerSensor;
        private readonly Sensor busClock;
        private readonly RyzenSMU smu;
        private readonly Dictionary<KeyValuePair<uint, RyzenSMU.SmuSensorType>, Sensor> smuSensors
            = new Dictionary<KeyValuePair<uint, RyzenSMU.SmuSensorType>, Sensor>();

        private readonly ISensorConfig sensorConfig;

        private const uint FAMILY_17H_M01H_THM_TCON_TEMP = 0x00059800;
        private const uint FAMILY_17H_M01H_THM_TCON_TEMP_RANGE_SEL = 0x80000;
        private uint FAMILY_17H_M70H_CCD_TEMP(uint i) { return 0x00059954 + i * 4; }
        private const uint FAMILY_17H_M70H_CCD_TEMP_VALID = 0x800;
        private const uint MAX_CCD_COUNT = 8;

        private const uint MSR_RAPL_PWR_UNIT = 0xC0010299;
        private const uint MSR_CORE_ENERGY_STAT = 0xC001029A;
        private const uint MSR_PKG_ENERGY_STAT = 0xC001029B;
        private const uint MSR_P_STATE_0 = 0xC0010064;
        private const uint MSR_FAMILY_17H_P_STATE = 0xc0010293;
        private const uint FAMILY_17H_PCI_CONTROL_REGISTER = 0x60;
        private const uint F17H_M01H_SVI = 0x0005A000;

        private readonly float energyUnitMultiplier = 0;
        private uint lastEnergyConsumed;
        private DateTime lastEnergyTime;

        private readonly double timeStampCounterMultiplier;

        private struct TctlOffsetItem
        {
            public string Name { get; set; }
            public float Offset { get; set; }
        }
        private IEnumerable<TctlOffsetItem> tctlOffsetItems = new[] {
          new TctlOffsetItem { Name = "AMD Ryzen 5 1600X", Offset = 20.0f },
          new TctlOffsetItem { Name = "AMD Ryzen 7 1700X", Offset = 20.0f },
          new TctlOffsetItem { Name = "AMD Ryzen 7 1800X", Offset = 20.0f },
          new TctlOffsetItem { Name = "AMD Ryzen 7 2700X", Offset = 10.0f },
          new TctlOffsetItem { Name = "AMD Ryzen Threadripper 19", Offset = 27.0f },
          new TctlOffsetItem { Name = "AMD Ryzen Threadripper 29", Offset = 27.0f }
        };
        private readonly float tctlOffset = 0.0f;

        public AMD17CPU(int processorIndex, CPUID[][] cpuid, ISettings settings, ISensorConfig config)
          : base(processorIndex, cpuid, settings, config)
        {
            this.sensorConfig = config;

            string cpuName = cpuid[0][0].BrandString;
            if (!string.IsNullOrEmpty(cpuName))
            {
                foreach (var item in tctlOffsetItems)
                {
                    if (cpuName.StartsWith(item.Name))
                    {
                        tctlOffset = item.Offset;
                        break;
                    }
                }
            }

            //this.smu = new RyzenSMU(family, model, packageType);
            //var pmTable = smu.GetPmTable();

            //foreach (KeyValuePair<uint, RyzenSMU.SmuSensorType> sensor in smu.GetPmTableStructure())
            //{
            //    //_smuSensors.Add(sensor, new Sensor(sensor.Value.Name, _cpu._sensorTypeIndex[sensor.Value.Type]++, sensor.Value.Type, _cpu, _cpu._settings));
            //}

            coreTemperature = new Sensor(
              "CPU Package", 0, SensorType.Temperature, this, new[] {
            new ParameterDescription("Offset [°C]", "Temperature offset.", 0)
                }, this.settings);

            if (tctlOffset != 0.0f)
                tctlTemperature = new Sensor(
                "CPU Tctl", 1, true, SensorType.Temperature, this, new[] {
            new ParameterDescription("Offset [°C]", "Temperature offset.", 0)
                  }, this.settings);

            ccdMaxTemperature = new Sensor(
              "CPU CCD Max", 2, SensorType.Temperature, this, this.settings);

            ccdAvgTemperature = new Sensor(
              "CPU CCD Average", 3, SensorType.Temperature, this, this.settings);

            ccdTemperatures = new Sensor[MAX_CCD_COUNT];
            for (int i = 0; i < ccdTemperatures.Length; i++)
            {
                ccdTemperatures[i] = new Sensor(
                "CPU CCD #" + (i + 1), i + 4, SensorType.Temperature, this,
                  new[] {
                new ParameterDescription("Offset [°C]", "Temperature offset.", 0)
                  }, this.settings);
            }

            if (Ring0.Rdmsr(MSR_RAPL_PWR_UNIT, out uint eax, out _))
            {
                energyUnitMultiplier = 1.0f / (1 << (int)((eax >> 8) & 0x1F));
            }

            coreVoltage = new Sensor("Core (SVI2 TFN)", 1, SensorType.Voltage, this, settings);
            socVoltage = new Sensor("SoC (SVI2 TFN)", 2, SensorType.Voltage, this, settings);

            if (energyUnitMultiplier != 0)
            {
                if (Ring0.Rdmsr(MSR_PKG_ENERGY_STAT, out uint energyConsumed, out _))
                {
                    lastEnergyTime = DateTime.UtcNow;
                    lastEnergyConsumed = energyConsumed;
                    packagePowerSensor = new Sensor(
                      "CPU Package", 0, SensorType.Power, this, settings);
                    ActivateSensor(packagePowerSensor);
                }
            }
            coresPowerSensor = new Sensor("CPU Cores", 1, SensorType.Power, this,
              settings);

            busClock = new Sensor("Bus Speed", 0, SensorType.Clock, this, settings);
            timeStampCounterMultiplier = GetTimeStampCounterMultiplier();
            if (timeStampCounterMultiplier > 0)
            {

                if (EstimatedTimeStampCounterFrequencyError == 0)
                {
                    busClock.Value = (float)(TimeStampCounterFrequency / timeStampCounterMultiplier);
                }
                else
                {
                    busClock.Value = 100;
                }

                ActivateSensor(busClock);
            }

            this.cores = new Core[coreCount];
            for (int i = 0; i < this.cores.Length; i++)
            {
                this.cores[i] = new Core(i, cpuid[i], this, settings);
            }

            coreMaxClocks = new Sensor("CPU Max Clock", this.cores.Length + 1, SensorType.Clock, this, settings);
            ActivateSensor(coreMaxClocks);
        }

        protected override uint[] GetMSRs()
        {
            return new uint[] { MSR_P_STATE_0, MSR_FAMILY_17H_P_STATE,
                MSR_RAPL_PWR_UNIT, MSR_CORE_ENERGY_STAT, MSR_PKG_ENERGY_STAT };
        }

        private IList<uint> GetSmnRegisters()
        {
            var registers = new List<uint>
            {
                FAMILY_17H_M01H_THM_TCON_TEMP
            };

            for (uint i = 0; i < MAX_CCD_COUNT; i++)
            {
                registers.Add(FAMILY_17H_M70H_CCD_TEMP(i));
            }

            return registers;
        }

        public override string GetReport()
        {
            StringBuilder r = new StringBuilder();
            r.Append(base.GetReport());

            r.Append("Time Stamp Counter Multiplier: ");
            r.AppendLine(timeStampCounterMultiplier.ToString(
              CultureInfo.InvariantCulture));
            r.AppendLine();

            r.AppendLine("SMN Registers");
            r.AppendLine();
            r.AppendLine(" Register  Value");
            var registers = GetSmnRegisters();
            for (int i = 0; i < registers.Count; i++)
                if (ReadSmnRegister(registers[i], out uint value))
                {
                    r.Append(" ");
                    r.Append(registers[i].ToString("X8", CultureInfo.InvariantCulture));
                    r.Append("  ");
                    r.Append(value.ToString("X8", CultureInfo.InvariantCulture));
                    r.AppendLine();
                }
            r.AppendLine();

            return r.ToString();
        }

        private double GetTimeStampCounterMultiplier()
        {
            uint cpuDfsId = 0;
            uint cpuFid = 0;
            if (Ring0.Rdmsr(MSR_P_STATE_0, out uint eax, out _))
            {
                cpuDfsId = (eax >> 8) & 0x3f;
                cpuFid = eax & 0xff;
            }
            return 2.0 * cpuFid / cpuDfsId;
        }

        private bool ReadSmnRegister(uint address, out uint value)
        {
            if (!Ring0.WritePciConfig(0, 0x60, address))
            {
                value = 0;
                return false;
            }
            return Ring0.ReadPciConfig(0, 0x64, out value);
        }

        public override void Update()
        {
            base.Update();

            if (sensorConfig.GetSensorEvaluate(coreTemperature.IdentifierString))
            {
                if (ReadSmnRegister(FAMILY_17H_M01H_THM_TCON_TEMP, out uint value))
                {
                    float temperature = ((value >> 21) & 0x7FF) / 8.0f;
                    if ((value & FAMILY_17H_M01H_THM_TCON_TEMP_RANGE_SEL) != 0)
                        temperature -= 49;

                    if (tctlTemperature != null)
                    {
                        tctlTemperature.Value = temperature +
                          tctlTemperature.Parameters[0].Value;
                        ActivateSensor(tctlTemperature);
                    }

                    temperature -= tctlOffset;

                    coreTemperature.Value = temperature +
                      coreTemperature.Parameters[0].Value;
                    ActivateSensor(coreTemperature);
                }
            }

            if (ccdTemperatures.Any(sensor => sensorConfig.GetSensorEvaluate(sensor.IdentifierString))
                || sensorConfig.GetSensorEvaluate(ccdMaxTemperature.IdentifierString)
                || sensorConfig.GetSensorEvaluate(ccdAvgTemperature.IdentifierString))
            {
                float maxTemperature = float.MinValue;
                int ccdCount = 0;
                float ccdTemperatureSum = 0;
                for (uint i = 0; i < ccdTemperatures.Length; i++)
                {
                    if (ReadSmnRegister(FAMILY_17H_M70H_CCD_TEMP(i), out uint value))
                    {
                        if ((value & FAMILY_17H_M70H_CCD_TEMP_VALID) == 0)
                            break;

                        float temperature = (value & 0x7FF) / 8.0f - 49;
                        temperature += ccdTemperatures[i].Parameters[0].Value;

                        if (temperature > maxTemperature)
                            maxTemperature = temperature;
                        ccdCount++;
                        ccdTemperatureSum += temperature;

                        ccdTemperatures[i].Value = temperature;
                        ActivateSensor(ccdTemperatures[i]);
                    }
                }

                if (ccdCount > 1)
                {
                    ccdMaxTemperature.Value = maxTemperature;
                    ActivateSensor(ccdMaxTemperature);

                    ccdAvgTemperature.Value = ccdTemperatureSum / ccdCount;
                    ActivateSensor(ccdAvgTemperature);
                }
            }

            if (sensorConfig.GetSensorEvaluate(coreVoltage.IdentifierString)
                || sensorConfig.GetSensorEvaluate(socVoltage.IdentifierString))
            {
                GroupAffinity previousAffinity = ThreadAffinity.Set(cpuid[0][0].Affinity);

                if (Ring0.WaitPciBusMutex(10))
                {
                    // SVI0_TFN_PLANE0 [0]
                    // SVI0_TFN_PLANE1 [1]
                    Ring0.WritePciConfig(0x00, FAMILY_17H_PCI_CONTROL_REGISTER, F17H_M01H_SVI + 0x8);
                    Ring0.ReadPciConfig(0x00, FAMILY_17H_PCI_CONTROL_REGISTER + 4, out uint smuSvi0Tfn);

                    uint sviPlane0Offset;
                    uint sviPlane1Offset;

                    switch (model)
                    {
                        case 0x31: // Threadripper 3000.
                            {
                                sviPlane0Offset = F17H_M01H_SVI + 0x14;
                                sviPlane1Offset = F17H_M01H_SVI + 0x10;
                                break;
                            }
                        case 0x71: // Zen 2
                        case 0x40: // Rembrandt
                        case 0x21: // Zen 3
                        case 0x61: // Zen 4 + Raphael
                        case 0x75: // Phoenix
                            {
                                sviPlane0Offset = F17H_M01H_SVI + 0x10;
                                sviPlane1Offset = F17H_M01H_SVI + 0xC;

                                break;
                            }
                        default: // Zen and Zen+.
                            {
                                sviPlane0Offset = F17H_M01H_SVI + 0xC;
                                sviPlane1Offset = F17H_M01H_SVI + 0x10;
                                break;
                            }
                    }

                    // SVI0_PLANE0_VDDCOR [24:16]
                    // SVI0_PLANE0_IDDCOR [7:0]
                    Ring0.WritePciConfig(0x00, FAMILY_17H_PCI_CONTROL_REGISTER, sviPlane0Offset);
                    Ring0.ReadPciConfig(0x00, FAMILY_17H_PCI_CONTROL_REGISTER + 4, out uint smuSvi0TelPlane0);

                    // SVI0_PLANE1_VDDCOR [24:16]
                    // SVI0_PLANE1_IDDCOR [7:0]
                    Ring0.WritePciConfig(0x00, FAMILY_17H_PCI_CONTROL_REGISTER, sviPlane1Offset);
                    Ring0.ReadPciConfig(0x00, FAMILY_17H_PCI_CONTROL_REGISTER + 4, out uint smuSvi0TelPlane1);

                    ThreadAffinity.Set(previousAffinity);

                    const double vidStep = 0.00625;
                    double vcc;
                    uint svi0PlaneXVddCor;

                    if (model is 0x61)
                        smuSvi0Tfn |= 0x01 | 0x02;

                    // Core (0x01)
                    if ((smuSvi0Tfn & 0x01) == 0)
                    {
                        svi0PlaneXVddCor = (smuSvi0TelPlane0 >> 16) & 0xff;
                        vcc = 1.550 - vidStep * svi0PlaneXVddCor;
                        coreVoltage.Value = (float)vcc;
                        ActivateSensor(coreVoltage);
                    }

                    // SoC (0x02), not every Zen cpu has this voltage.
                    if (model == 0x11 || model == 0x21 || model == 0x71 || model == 0x31 || (smuSvi0Tfn & 0x02) == 0)
                    {
                        svi0PlaneXVddCor = (smuSvi0TelPlane1 >> 16) & 0xff;
                        vcc = 1.550 - vidStep * svi0PlaneXVddCor;
                        socVoltage.Value = (float)vcc;
                        ActivateSensor(socVoltage);
                    }

                    Ring0.ReleasePciBusMutex();
                }
            }

            if (sensorConfig.GetSensorEvaluate(packagePowerSensor.IdentifierString))
            {
                if (energyUnitMultiplier != 0 &&
                  Ring0.Rdmsr(MSR_PKG_ENERGY_STAT, out uint energyConsumed, out _))
                {
                    DateTime time = DateTime.UtcNow;
                    float deltaTime = (float)(time - lastEnergyTime).TotalSeconds;
                    if (deltaTime > 0.01)
                    {
                        packagePowerSensor.Value = energyUnitMultiplier * unchecked(
                          energyConsumed - lastEnergyConsumed) / deltaTime;
                        lastEnergyTime = time;
                        lastEnergyConsumed = energyConsumed;
                    }
                }
            }

            if (cores.Any(core => sensorConfig.GetSensorEvaluate(core.ClockSensor.IdentifierString))
                || cores.Any(core => sensorConfig.GetSensorEvaluate(core.PowerSensor.IdentifierString))
                || sensorConfig.GetSensorEvaluate(coresPowerSensor.IdentifierString)
                || cores.Any(core => sensorConfig.GetSensorEvaluate(core.VoltageSensor.IdentifierString))
                || sensorConfig.GetSensorEvaluate(coreMaxClocks.IdentifierString))
            {
                float? coresPower = 0f;
                for (int i = 0; i < cores.Length; i++)
                {
                    cores[i].Update();
                    coresPower += cores[i].Power;
                }

                coresPowerSensor.Value = coresPower;
                coreMaxClocks.Value = cores.Max(crs => crs.CoreClock);

                if (coresPower.HasValue)
                {
                    ActivateSensor(coresPowerSensor);
                }
            }
        }

        private class Core
        {
            private readonly AMD17CPU cpu;
            private readonly GroupAffinity affinity;

            private readonly Sensor powerSensor;
            private readonly Sensor voltageSensor;
            private readonly Sensor clockSensor;

            private DateTime lastEnergyTime;
            private uint lastEnergyConsumed;
            private float? power = null;

            public float? CoreClock => clockSensor.Value;

            public float? Power => power;

            public Sensor PowerSensor => powerSensor;

            public Sensor VoltageSensor => voltageSensor;

            public Sensor ClockSensor => clockSensor;

            public Core(int index, CPUID[] threads, AMD17CPU cpu, ISettings settings)
            {
                this.cpu = cpu;
                this.affinity = threads[0].Affinity;

                string coreString = cpu.CoreString(index);
                this.powerSensor =
                  new Sensor(coreString, index + 2, SensorType.Power, cpu, settings);
                this.voltageSensor =
                  new Sensor(coreString, index + 3, SensorType.Voltage, cpu, settings);
                this.clockSensor =
                  new Sensor(coreString, index + 1, SensorType.Clock, cpu, settings);

                if (cpu.energyUnitMultiplier != 0)
                {
                    if (Ring0.RdmsrTx(MSR_CORE_ENERGY_STAT, out uint energyConsumed,
                      out _, affinity))
                    {
                        lastEnergyTime = DateTime.UtcNow;
                        lastEnergyConsumed = energyConsumed;
                        this.cpu.ActivateSensor(powerSensor);
                    }
                }

                this.cpu.ActivateSensor(voltageSensor);
            }

            private double? GetMultiplier()
            {
                if (Ring0.Rdmsr(MSR_FAMILY_17H_P_STATE, out uint eax, out _))
                {
                    uint cpuDfsId = (eax >> 8) & 0x3f;
                    uint cpuFid = eax & 0xff;
                    return 2.0 * cpuFid / cpuDfsId;
                }
                else
                {
                    return null;
                }
            }

            public void Update()
            {
                DateTime energyTime = DateTime.MinValue;
                var previousAffinity = ThreadAffinity.Set(affinity);
                if (Ring0.Rdmsr(MSR_CORE_ENERGY_STAT, out uint energyConsumed, out _))
                {
                    energyTime = DateTime.UtcNow;
                }

                double? multiplier = GetMultiplier();

                int curCpuVid = 0;
                if (Ring0.Rdmsr(MSR_FAMILY_17H_P_STATE, out uint eax, out _))
                {
                    curCpuVid = (int)((eax >> 14) & 0xff);
                }

                ThreadAffinity.Set(previousAffinity);

                if (cpu.energyUnitMultiplier != 0)
                {
                    float deltaTime = (float)(energyTime - lastEnergyTime).TotalSeconds;
                    if (deltaTime > 0.01)
                    {
                        power = cpu.energyUnitMultiplier *
                          unchecked(energyConsumed - lastEnergyConsumed) / deltaTime;
                        powerSensor.Value = power;
                        lastEnergyTime = energyTime;
                        lastEnergyConsumed = energyConsumed;
                    }
                }

                if (multiplier.HasValue)
                {
                    float? clock = (float?)(multiplier * cpu.busClock.Value);
                    clockSensor.Value = clock;
                    if (clock.HasValue)
                    {
                        this.cpu.ActivateSensor(clockSensor);
                    }
                }

                // Voltage		
                voltageSensor.Value = GetVcc(curCpuVid);
            }

            private float GetVcc(int cpuVid)
            {
                const float vidStep = 0.00625f;
                float vcc;

                // Family 19h
                // Raphael: 0x61
                // Phoenix: 0x75
                // Rembrandt: 0x40
                if (cpu.family == 0x19 && (cpu.model == 0x61 || cpu.model == 0x75 || cpu.model == 0x40))
                {
                    vcc = vidStep * cpuVid;
                }
                // Family 17h
                // VanGogh: 0x90
                else if (cpu.family == 0x17 && cpu.model == 0x90)
                {
                    vcc = vidStep * cpuVid;
                }
                else
                {
                    vcc = 1.550f - vidStep * cpuVid;
                }

                return vcc;
            }
        }
    }
}
