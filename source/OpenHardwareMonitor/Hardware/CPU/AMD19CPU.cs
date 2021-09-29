using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CapFrameX.Contracts.Sensor;
using Serilog;

namespace OpenHardwareMonitor.Hardware.CPU
{
    internal sealed class AMD19CPU : AMDCPU
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

        private readonly ISensorConfig sensorConfig;

        private const uint FAMILY_19H_M20H_THM_TCON_TEMP = 0x00059800;
        private const uint FAMILY_19H_M20H_THM_TCON_TEMP_RANGE_SEL = 0x80000;
        private uint FAMILY_19H_M20H_CCD_TEMP(uint i) { return 0x00059954 + i * 4; }
        private const uint FAMILY_19H_M20H_CCD_TEMP_VALID = 0x800;
        private const uint MAX_CCD_COUNT = 8;

        private const uint MSR_RAPL_PWR_UNIT = 0xC0010299;
        private const uint MSR_CORE_ENERGY_STAT = 0xC001029A;
        private const uint MSR_PKG_ENERGY_STAT = 0xC001029B;
        private const uint MSR_P_STATE_0 = 0xC0010064;
        private const uint MSR_FAMILY_19H_P_STATE = 0xc0010293;
        private const uint FAMILY_19H_PCI_CONTROL_REGISTER = 0x60;

        private readonly float energyUnitMultiplier = 0;
        private uint lastEnergyConsumed;
        private DateTime lastEnergyTime;

        private readonly double timeStampCounterMultiplier;

        public AMD19CPU(int processorIndex, CPUID[][] cpuid, ISettings settings, ISensorConfig config)
          : base(processorIndex, cpuid, settings, config)
        {
            this.sensorConfig = config;

            coreTemperature = new Sensor(
              "CPU Package", 0, SensorType.Temperature, this, new[] {
            new ParameterDescription("Offset [°C]", "Temperature offset.", 0)
                }, this.settings);

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
            else
            {
                Log.Logger.Error($"Failed getting 19h family energy unit multiplier.");
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
                else
                {
                    Log.Logger.Error($"Failed getting 19h family power sensor.");
                }
            }

            coresPowerSensor = new Sensor("CPU Cores", 1, SensorType.Power, this, settings);

            busClock = new Sensor("Bus Speed", 0, SensorType.Clock, this, settings);
            timeStampCounterMultiplier = GetTimeStampCounterMultiplier();
            if (timeStampCounterMultiplier > 0)
            {
                busClock.Value = (float)(TimeStampCounterFrequency /
                  timeStampCounterMultiplier);
                ActivateSensor(busClock);
            }
            else
            {
                Log.Logger.Error($"19h family invalid time stamp counter multiplier.");
            }

            this.cores = new Core[coreCount];
            for (int i = 0; i < this.cores.Length; i++)
            {
                this.cores[i] = new Core(i, cpuid[i], this, settings);
            }

            coreMaxClocks = new Sensor("CPU Max Clock", this.cores.Length + 1, SensorType.Clock, this, settings);
            ActivateSensor(coreMaxClocks);

            Log.Logger.Information($"Family 19h processor successfully initialized.");
        }

        protected override uint[] GetMSRs()
        {
            return new uint[] { MSR_P_STATE_0, MSR_FAMILY_19H_P_STATE,
                MSR_RAPL_PWR_UNIT, MSR_CORE_ENERGY_STAT, MSR_PKG_ENERGY_STAT };
        }

        private IList<uint> GetSmnRegisters()
        {
            var registers = new List<uint>
            {
                FAMILY_19H_M20H_THM_TCON_TEMP
            };
            for (uint i = 0; i < MAX_CCD_COUNT; i++)
            {
                registers.Add(FAMILY_19H_M20H_CCD_TEMP(i));
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

            Ring0.ReadPciConfig(0x00, FAMILY_19H_PCI_CONTROL_REGISTER + 4, out uint smuSvi0Tfn);
            Ring0.ReadPciConfig(0x00, FAMILY_19H_PCI_CONTROL_REGISTER + 4, out uint smuSvi0TelPlane0);
            Ring0.ReadPciConfig(0x00, FAMILY_19H_PCI_CONTROL_REGISTER + 4, out uint smuSvi0TelPlane1);

            if (sensorConfig.GetSensorEvaluate(coreTemperature.IdentifierString))
            {
                if (ReadSmnRegister(FAMILY_19H_M20H_THM_TCON_TEMP, out uint value))
                {
                    float temperature = ((value >> 21) & 0x7FF) / 8.0f;
                    if ((value & FAMILY_19H_M20H_THM_TCON_TEMP_RANGE_SEL) != 0)
                        temperature -= 49;

                    if (tctlTemperature != null)
                    {
                        tctlTemperature.Value = temperature +
                          tctlTemperature.Parameters[0].Value;
                        ActivateSensor(tctlTemperature);
                    }

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
                    if (ReadSmnRegister(FAMILY_19H_M20H_CCD_TEMP(i), out uint value))
                    {
                        if ((value & FAMILY_19H_M20H_CCD_TEMP_VALID) == 0)
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

            if(sensorConfig.GetSensorEvaluate(coreVoltage.IdentifierString) 
                || sensorConfig.GetSensorEvaluate(socVoltage.IdentifierString))
            {
                const double vidStep = 0.00625;
                double vcc;
                uint svi0PlaneXVddCor;

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
            private readonly AMD19CPU cpu;
            private readonly GroupAffinity affinity;

            private readonly Sensor powerSensor;
            private readonly Sensor clockSensor;

            private DateTime lastEnergyTime;
            private uint lastEnergyConsumed;
            private float? power = null;

            public float? CoreClock => clockSensor.Value;

            public Sensor PowerSensor => powerSensor;

            public Sensor ClockSensor => clockSensor;

            public Core(int index, CPUID[] threads, AMD19CPU cpu, ISettings settings)
            {
                this.cpu = cpu;
                this.affinity = threads[0].Affinity;

                string coreString = cpu.CoreString(index);
                this.powerSensor =
                  new Sensor(coreString, index + 2, SensorType.Power, cpu, settings);
                this.clockSensor =
                  new Sensor(coreString, index + 1, SensorType.Clock, cpu, settings);

                if (cpu.energyUnitMultiplier != 0)
                {
                    if (Ring0.RdmsrTx(MSR_CORE_ENERGY_STAT, out uint energyConsumed,
                      out _, affinity))
                    {
                        lastEnergyTime = DateTime.UtcNow;
                        lastEnergyConsumed = energyConsumed;
                        cpu.ActivateSensor(powerSensor);
                    }
                }
            }

            private double? GetMultiplier()
            {
                if (Ring0.Rdmsr(MSR_FAMILY_19H_P_STATE, out uint eax, out _))
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

            public float? Power { get { return power; } }

            public void Update()
            {
                DateTime energyTime = DateTime.MinValue;
                var previousAffinity = ThreadAffinity.Set(affinity);
                if (Ring0.Rdmsr(MSR_CORE_ENERGY_STAT, out uint energyConsumed, out _))
                {
                    energyTime = DateTime.UtcNow;
                }

                double? multiplier = GetMultiplier();
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
                        cpu.ActivateSensor(clockSensor);
                }
            }
        }
    }
}
