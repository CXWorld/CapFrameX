// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.

using LibreHardwareMonitor.PawnIo;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;

namespace LibreHardwareMonitor.Hardware.Cpu;

internal sealed class IntelCpu : GenericCpu
{
    private readonly Sensor _busClock;
    private readonly Sensor _coreAvg;
    private readonly Sensor[] _coreClocks;
    private readonly Sensor _coreEffectiveAvg;
    private readonly Sensor[][] _threadEffectiveClocks;
    private readonly Sensor _coreEffectiveMax;
    private readonly Sensor _maxClock;
    private readonly Sensor _coreMax;
    private readonly Sensor[] _coreTemperatures;
    private readonly Sensor[] _coreVIDs;
    private readonly Sensor _coreVoltage;
    private readonly Sensor[] _distToTjMaxTemperatures;

    private readonly uint[] _energyStatusMsrs = { MSR_PKG_ENERGY_STATUS, MSR_PP0_ENERGY_STATUS, MSR_PP1_ENERGY_STATUS, MSR_DRAM_ENERGY_STATUS, MSR_PLATFORM_ENERGY_STATUS };
    private readonly uint[] _lastEnergyConsumed;
    private readonly DateTime[] _lastEnergyTime;

    private readonly MicroArchitecture _microArchitecture;
    private readonly Sensor _packageTemperature;
    private readonly Sensor[] _powerSensors;
    private readonly double _timeStampCounterMultiplier;

    private readonly IntelMsr _msrModule;
    private readonly bool _hasAperfMperf;
    private readonly ulong[][] _lastAperf;
    private readonly ulong[][] _lastMperf;
    private readonly DateTime[][] _lastSampleTime;

    private readonly Sensor _uncoreClock;
    private readonly bool _hasUncoreClock;

    private readonly IntelImc _imcModule;
    private readonly Sensor _imcClock;
    private readonly Sensor _memoryDataRate;
    private readonly Sensor _dramFrequency;
    private readonly Sensor _memoryGear;
    private readonly bool _hasImc;
    private readonly bool _hasImcLive;

    public IntelCpu(int processorIndex, CpuId[][] cpuId, ISettings settings) : base(processorIndex, cpuId, settings)
    {
        _msrModule = new IntelMsr();
        _imcModule = new IntelImc();

        uint eax;

        // set tjMax
        float[] tjMax;
        switch (_family)
        {
            case 0x06:
                {
                    switch (_model)
                    {
                        case 0x0F: // Intel Core 2 (65nm)
                            _microArchitecture = MicroArchitecture.Core;
                            tjMax = _stepping switch
                            {
                                // B2
                                0x06 => _coreCount switch
                                {
                                    2 => Floats(80 + 10),
                                    4 => Floats(90 + 10),
                                    _ => Floats(85 + 10)
                                },
                                // G0
                                0x0B => Floats(90 + 10),
                                // M0
                                0x0D => Floats(85 + 10),
                                _ => Floats(85 + 10)
                            };
                            break;

                        case 0x17: // Intel Core 2 (45nm)
                            _microArchitecture = MicroArchitecture.Core;
                            tjMax = Floats(100);
                            break;

                        case 0x1C: // Intel Atom (45nm)
                            _microArchitecture = MicroArchitecture.Atom;
                            tjMax = _stepping switch
                            {
                                // C0
                                0x02 => Floats(90),
                                // A0, B0
                                0x0A => Floats(100),
                                _ => Floats(90)
                            };
                            break;

                        case 0x1A: // Intel Core i7 LGA1366 (45nm)
                        case 0x1E: // Intel Core i5, i7 LGA1156 (45nm)
                        case 0x1F: // Intel Core i5, i7
                        case 0x25: // Intel Core i3, i5, i7 LGA1156 (32nm)
                        case 0x2C: // Intel Core i7 LGA1366 (32nm) 6 Core
                        case 0x2E: // Intel Xeon Processor 7500 series (45nm)
                        case 0x2F: // Intel Xeon Processor (32nm)
                            _microArchitecture = MicroArchitecture.Nehalem;
                            tjMax = GetTjMaxFromMsr();
                            break;

                        case 0x2A: // Intel Core i5, i7 2xxx LGA1155 (32nm)
                        case 0x2D: // Next Generation Intel Xeon, i7 3xxx LGA2011 (32nm)
                            _microArchitecture = MicroArchitecture.SandyBridge;
                            tjMax = GetTjMaxFromMsr();
                            break;

                        case 0x3A: // Intel Core i5, i7 3xxx LGA1155 (22nm)
                        case 0x3E: // Intel Core i7 4xxx LGA2011 (22nm)
                            _microArchitecture = MicroArchitecture.IvyBridge;
                            tjMax = GetTjMaxFromMsr();
                            break;

                        case 0x3C: // Intel Core i5, i7 4xxx LGA1150 (22nm)
                        case 0x3F: // Intel Xeon E5-2600/1600 v3, Core i7-59xx
                        // LGA2011-v3, Haswell-E (22nm)
                        case 0x45: // Intel Core i5, i7 4xxxU (22nm)
                        case 0x46:
                            _microArchitecture = MicroArchitecture.Haswell;
                            tjMax = GetTjMaxFromMsr();
                            break;

                        case 0x3D: // Intel Core M-5xxx (14nm)
                        case 0x47: // Intel i5, i7 5xxx, Xeon E3-1200 v4 (14nm)
                        case 0x4F: // Intel Xeon E5-26xx v4
                        case 0x56: // Intel Xeon D-15xx
                            _microArchitecture = MicroArchitecture.Broadwell;
                            tjMax = GetTjMaxFromMsr();
                            break;

                        case 0x36: // Intel Atom S1xxx, D2xxx, N2xxx (32nm)
                            _microArchitecture = MicroArchitecture.Atom;
                            tjMax = GetTjMaxFromMsr();
                            break;

                        case 0x37: // Intel Atom E3xxx, Z3xxx (22nm)
                        case 0x4A:
                        case 0x4D: // Intel Atom C2xxx (22nm)
                        case 0x5A:
                        case 0x5D:
                            _microArchitecture = MicroArchitecture.Silvermont;
                            tjMax = GetTjMaxFromMsr();
                            break;

                        case 0x4E:
                        case 0x5E: // Intel Core i5, i7 6xxxx LGA1151 (14nm)
                        case 0x55: // Intel Core X i7, i9 7xxx LGA2066 (14nm)
                            _microArchitecture = MicroArchitecture.Skylake;
                            tjMax = GetTjMaxFromMsr();
                            break;

                        case 0x4C: // Intel Airmont (Cherry Trail, Braswell)
                            _microArchitecture = MicroArchitecture.Airmont;
                            tjMax = GetTjMaxFromMsr();
                            break;

                        case 0x8E: // Intel Core i5, i7 7xxxx (14nm) (Kaby Lake) and 8xxxx (14nm++) (Coffee Lake)
                        case 0x9E:
                            _microArchitecture = MicroArchitecture.KabyLake;
                            tjMax = GetTjMaxFromMsr();
                            break;

                        case 0x5C: // Goldmont (Apollo Lake)
                        case 0x5F: // (Denverton)
                            _microArchitecture = MicroArchitecture.Goldmont;
                            tjMax = GetTjMaxFromMsr();
                            break;

                        case 0x7A: // Goldmont plus (Gemini Lake)
                            _microArchitecture = MicroArchitecture.GoldmontPlus;
                            tjMax = GetTjMaxFromMsr();
                            break;

                        case 0x66: // Intel Core i3 8xxx (10nm) (Cannon Lake)
                            _microArchitecture = MicroArchitecture.CannonLake;
                            tjMax = GetTjMaxFromMsr();
                            break;

                        case 0x7D: // Intel Core i3, i5, i7 10xxx (10nm) (Ice Lake)
                        case 0x7E:
                        case 0x6A: // Ice Lake server
                        case 0x6C:
                            _microArchitecture = MicroArchitecture.IceLake;
                            tjMax = GetTjMaxFromMsr();
                            break;

                        case 0xA5:
                        case 0xA6: // Intel Core i3, i5, i7 10xxxU (14nm)
                            _microArchitecture = MicroArchitecture.CometLake;
                            tjMax = GetTjMaxFromMsr();
                            break;

                        case 0x86: // Tremont (10nm) (Elkhart Lake, Skyhawk Lake)
                            _microArchitecture = MicroArchitecture.Tremont;
                            tjMax = GetTjMaxFromMsr();
                            break;

                        case 0x8C: // Tiger Lake (Intel 10 nm SuperFin, Gen. 11)
                        case 0x8D:
                            _microArchitecture = MicroArchitecture.TigerLake;
                            tjMax = GetTjMaxFromMsr();
                            break;

                        case 0x97: // Alder Lake (Intel 7 (10ESF), Gen. 12)
                        case 0x9A: // Alder Lake-L (Intel 7 (10ESF), Gen. 12)
                        case 0xBE: // Alder Lake-N (Intel 7 (10ESF), Gen. 12)
                            _microArchitecture = MicroArchitecture.AlderLake;
                            tjMax = GetTjMaxFromMsr();
                            break;

                        case 0xB7: // Raptor Lake (Intel 7 (10ESF), Gen. 13)
                        case 0xBA: // Raptor Lake-P (Intel 7 (10ESF), Gen. 13)
                        case 0xBF: // Raptor Lake-N (Intel 7 (10ESF), Gen. 13)
                            _microArchitecture = MicroArchitecture.RaptorLake;
                            tjMax = GetTjMaxFromMsr();
                            break;

                        case 0xAC: // Meteor Lake (Intel 4, TSMC N5/N6, Gen. 14)
                        case 0xAA: // Meteor Lake-L (Intel 4, TSMC N5/N6, Gen. 14)
                            _microArchitecture = MicroArchitecture.MeteorLake;
                            tjMax = GetTjMaxFromMsr();
                            break;

                        case 0x9C: // Jasper Lake (10nm)
                            _microArchitecture = MicroArchitecture.JasperLake;
                            tjMax = GetTjMaxFromMsr();
                            break;

                        case 0xA7: // Intel Core i5, i6, i7 11xxx (14nm) (Rocket Lake)
                            _microArchitecture = MicroArchitecture.RocketLake;
                            tjMax = GetTjMaxFromMsr();
                            break;

                        case 0xB5: // Arrow Lake-U (Linux: INTEL_ARROWLAKE_U)
                        case 0xC5: // Arrow Lake-H (Linux: INTEL_ARROWLAKE_H)
                        case 0xC6: // Arrow Lake (Linux: INTEL_ARROWLAKE)
                        case 0xC7: // Intel Core Ultra 200 Series Arrow Lake-S Refresh Reserved
                        case 0xC8: // Intel Core Ultra 200 Series Arrow Lake-S Refresh Reserved
                            _microArchitecture = MicroArchitecture.ArrowLake;
                            tjMax = GetTjMaxFromMsr();
                            break;

                        case 0xBD: // Intel Core Ultra 5/7 200 Series LunarLake
                            _microArchitecture = MicroArchitecture.LunarLake;
                            tjMax = GetTjMaxFromMsr();
                            break;

                        case 0xCC: // PantherLake-L (Linux: INTEL_PANTHERLAKE_L)
                            _microArchitecture = MicroArchitecture.PantherLake;
                            tjMax = GetTjMaxFromMsr();
                            break;

                        case 0xCF: // Emerald Rapids-X server (Linux: INTEL_EMERALDRAPIDS_X, Raptor Cove)
                            _microArchitecture = MicroArchitecture.EmeraldRapids;
                            tjMax = GetTjMaxFromMsr();
                            break;

                        case 0xD5: // Wildcat Lake-L (Linux: INTEL_WILDCATLAKE_L)
                            _microArchitecture = MicroArchitecture.WildcatLake;
                            tjMax = GetTjMaxFromMsr();
                            break;

                        case 0xD7: // Bartlett Lake-S (Linux: INTEL_BARTLETTLAKE, Raptor Cove)
                            _microArchitecture = MicroArchitecture.BartlettLake;
                            tjMax = GetTjMaxFromMsr();
                            break;

                        case 0x8F: // Intel Xeon W5-3435X SapphireRapids
                            _microArchitecture = MicroArchitecture.SapphireRapids;
                            tjMax = GetTjMaxFromMsr();
                            break;
                        case 0x96: // Intel Celeron ElkhartLake 
                            _microArchitecture = MicroArchitecture.ElkhartLake;
                            tjMax = GetTjMaxFromMsr();
                            break;

                        default:
                            _microArchitecture = MicroArchitecture.Unknown;
                            tjMax = Floats(100);
                            break;
                    }
                }

                break;
            case 0x12:
                // Nova Lake introduces a new CPUID display family (Family 18 / 0x12).
                // Per Linux kernel arch/x86/include/asm/intel-family.h the assigned
                // models are:
                //   INTEL_NOVALAKE   = IFM(18, 0x01) // desktop / mainstream
                //   INTEL_NOVALAKE_L = IFM(18, 0x03) // low-power / mobile
                // We accept any other Family 18 model (ES) as NovaLake too: Family 18 is
                // the new-generation envelope for the Coyote Cove / Arctic Wolf core
                // (no other architecture is expected to share it), and unassigned
                // models inside it are most likely future steppings or SKU variants.
                // Failing closed to Unknown would silently regress detection on
                // late-stepping silicon.
                _microArchitecture = MicroArchitecture.NovaLake;
                tjMax = GetTjMaxFromMsr();
                break;
            case 0x0F:
                switch (_model)
                {
                    case 0x00: // Pentium 4 (180nm)
                    case 0x01: // Pentium 4 (130nm)
                    case 0x02: // Pentium 4 (130nm)
                    case 0x03: // Pentium 4, Celeron D (90nm)
                    case 0x04: // Pentium 4, Pentium D, Celeron D (90nm)
                    case 0x06: // Pentium 4, Pentium D, Celeron D (65nm)
                        _microArchitecture = MicroArchitecture.NetBurst;
                        tjMax = Floats(100);
                        break;

                    default:
                        _microArchitecture = MicroArchitecture.Unknown;
                        tjMax = Floats(100);
                        break;
                }

                break;
            default:
                _microArchitecture = MicroArchitecture.Unknown;
                tjMax = Floats(100);
                break;
        }

        // set timeStampCounterMultiplier
        switch (_microArchitecture)
        {
            case MicroArchitecture.Atom:
            case MicroArchitecture.Core:
            case MicroArchitecture.NetBurst:
                if (_msrModule.ReadMsr(IA32_PERF_STATUS, out uint _, out uint edx))
                    _timeStampCounterMultiplier = ((edx >> 8) & 0x1f) + (0.5 * ((edx >> 14) & 1));
                break;
            case MicroArchitecture.Airmont:
            case MicroArchitecture.AlderLake:
            case MicroArchitecture.ArrowLake:
            case MicroArchitecture.BartlettLake:
            case MicroArchitecture.Broadwell:
            case MicroArchitecture.CannonLake:
            case MicroArchitecture.CometLake:
            case MicroArchitecture.EmeraldRapids:
            case MicroArchitecture.Goldmont:
            case MicroArchitecture.GoldmontPlus:
            case MicroArchitecture.Haswell:
            case MicroArchitecture.IceLake:
            case MicroArchitecture.IvyBridge:
            case MicroArchitecture.JasperLake:
            case MicroArchitecture.KabyLake:
            case MicroArchitecture.LunarLake:
            case MicroArchitecture.PantherLake:
            case MicroArchitecture.NovaLake:
            case MicroArchitecture.WildcatLake:
            case MicroArchitecture.Nehalem:
            case MicroArchitecture.MeteorLake:
            case MicroArchitecture.RaptorLake:
            case MicroArchitecture.RocketLake:
            case MicroArchitecture.SandyBridge:
            case MicroArchitecture.Silvermont:
            case MicroArchitecture.Skylake:
            case MicroArchitecture.TigerLake:
            case MicroArchitecture.SapphireRapids:
            case MicroArchitecture.ElkhartLake:
            case MicroArchitecture.Tremont:
                if (_msrModule.ReadMsr(MSR_PLATFORM_INFO, out eax, out uint _))
                    _timeStampCounterMultiplier = (eax >> 8) & 0xff;

                break;
            default:
                _timeStampCounterMultiplier = 0;
                break;
        }

        //Clock 0
        //Load 1
        //Power 2
        //Temperature 3
        //Voltage 4
        //Misc 5
        //PMC 6

        int coreSensorId = 0;

        //core temp avg and max value
        //is only available when the cpu has more than 1 core
        if (cpuId[0][0].Data.GetLength(0) > 6 && (cpuId[0][0].Data[6, 0] & 0x40) != 0 && _microArchitecture != MicroArchitecture.Unknown && _coreCount > 1)
        {
            _coreMax = new Sensor("Core Max", coreSensorId, SensorType.Temperature, this, settings)
            { PresentationSortKey = $"3_0" };
            ActivateSensor(_coreMax);
            coreSensorId++;

            _coreAvg = new Sensor("Core Average", coreSensorId, SensorType.Temperature, this, settings)
            { PresentationSortKey = $"3_1" };
            ActivateSensor(_coreAvg);
            coreSensorId++;
        }
        else
        {
            _coreMax = null;
            _coreAvg = null;
        }

        // check if processor supports a digital thermal sensor at core level
        if (cpuId[0][0].Data.GetLength(0) > 6 && (cpuId[0][0].Data[6, 0] & 1) != 0 && _microArchitecture != MicroArchitecture.Unknown)
        {
            _coreTemperatures = new Sensor[_coreCount];
            for (int i = 0; i < _coreTemperatures.Length; i++)
            {
                _coreTemperatures[i] = new Sensor(CoreString(i),
                    coreSensorId,
                    SensorType.Temperature,
                    this,
                    [
                        new ParameterDescription("TjMax [°C]", "TjMax temperature of the core sensor.\n" + "Temperature = TjMax - TSlope * Value.", tjMax[i]),
                        new ParameterDescription("TSlope [°C]", "Temperature slope of the digital thermal sensor.\n" + "Temperature = TjMax - TSlope * Value.", 1)
                    ],
                    settings)
                { PresentationSortKey = $"3_2_{i}" };

                ActivateSensor(_coreTemperatures[i]);
                coreSensorId++;
            }
        }
        else
            _coreTemperatures = Array.Empty<Sensor>();

        // check if processor supports a digital thermal sensor at package level
        if (cpuId[0][0].Data.GetLength(0) > 6 && (cpuId[0][0].Data[6, 0] & 0x40) != 0 && _microArchitecture != MicroArchitecture.Unknown)
        {
            _packageTemperature = new Sensor("CPU Package",
                coreSensorId,
                SensorType.Temperature,
                this,
                [
                    new ParameterDescription("TjMax [°C]", "TjMax temperature of the package sensor.\n" + "Temperature = TjMax - TSlope * Value.", tjMax[0]),
                    new ParameterDescription("TSlope [°C]", "Temperature slope of the digital thermal sensor.\n" + "Temperature = TjMax - TSlope * Value.", 1)
                ],
                settings)
            { IsPresentationDefault = true, PresentationSortKey = $"3_3" };

            ActivateSensor(_packageTemperature);
            coreSensorId++;
        }

        // dist to tjmax sensor
        if (cpuId[0][0].Data.GetLength(0) > 6 && (cpuId[0][0].Data[6, 0] & 1) != 0 && _microArchitecture != MicroArchitecture.Unknown)
        {
            _distToTjMaxTemperatures = new Sensor[_coreCount];
            for (int i = 0; i < _distToTjMaxTemperatures.Length; i++)
            {
                _distToTjMaxTemperatures[i] = new Sensor(CoreString(i) + " Distance to TjMax", coreSensorId, SensorType.Temperature, this, settings)
                { PresentationSortKey = $"3_4_{i}" };
                ActivateSensor(_distToTjMaxTemperatures[i]);
                coreSensorId++;
            }
        }
        else
            _distToTjMaxTemperatures = Array.Empty<Sensor>();

        _busClock = new Sensor("Bus Speed", 0, SensorType.Clock, this, settings)
        { PresentationSortKey = $"0_0" };
        _coreClocks = new Sensor[_coreCount];
        for (int i = 0; i < _coreClocks.Length; i++)
        {
            _coreClocks[i] = new Sensor(CoreString(i), i + 1, SensorType.Clock, this, settings)
            { IsPresentationDefault = true, PresentationSortKey = $"0_1_0_{i}" };
            if (HasTimeStampCounter && _microArchitecture != MicroArchitecture.Unknown)
                ActivateSensor(_coreClocks[i]);
        }

        _maxClock = new Sensor("CPU Max", _coreClocks.Length + 1, SensorType.Clock, this, settings)
        { PresentationSortKey = $"0_2" };
        ActivateSensor(_maxClock);

        if (HasModelSpecificRegisters &&
            TryReadPerfCounters(_cpuId[0][0].Affinity, out _, out _))
        {
            _hasAperfMperf = true;
            _threadEffectiveClocks = new Sensor[_coreCount][];
            _lastAperf = new ulong[_coreCount][];
            _lastMperf = new ulong[_coreCount][];
            _lastSampleTime = new DateTime[_coreCount][];

            int effectiveSensorIndex = _coreClocks.Length + 2;
            int totalThreadSensors = 0;
            for (int i = 0; i < _coreCount; i++)
            {
                int threadCount = _cpuId[i].Length;
                _threadEffectiveClocks[i] = new Sensor[threadCount];
                _lastAperf[i] = new ulong[threadCount];
                _lastMperf[i] = new ulong[threadCount];
                _lastSampleTime[i] = new DateTime[threadCount];

                for (int j = 0; j < threadCount; j++)
                {
                    _threadEffectiveClocks[i][j] = new Sensor($"Core #{i + 1} Thread #{j + 1} (Effective)", effectiveSensorIndex + totalThreadSensors, SensorType.Clock, this, settings)
                    { PresentationSortKey = $"0_1_1_{i}_{j}" };
                    ActivateSensor(_threadEffectiveClocks[i][j]);
                    totalThreadSensors++;
                }
            }

            _coreEffectiveAvg = new Sensor("CPU Effective", effectiveSensorIndex + totalThreadSensors, SensorType.Clock, this, settings)
            { PresentationSortKey = "0_1_2" };
            _coreEffectiveMax = new Sensor("CPU Max Effective", effectiveSensorIndex + totalThreadSensors + 1, SensorType.Clock, this, settings)
            { PresentationSortKey = "0_1_3" };

            ActivateSensor(_coreEffectiveAvg);
            ActivateSensor(_coreEffectiveMax);
        }
        else
        {
            _hasAperfMperf = false;
            _coreEffectiveAvg = null;
            _coreEffectiveMax = null;
            _threadEffectiveClocks = Array.Empty<Sensor[]>();
            _lastAperf = Array.Empty<ulong[]>();
            _lastMperf = Array.Empty<ulong[]>();
            _lastSampleTime = Array.Empty<DateTime[]>();
        }

        if (SupportsUncoreClock(_microArchitecture, _model) &&
            _msrModule.ReadMsr(MSR_UNCORE_PERF_STATUS, out uint uncoreEax, out uint _) &&
            (uncoreEax & 0x7F) > 0)
        {
            int uncoreClockIndex = _hasAperfMperf
                ? _coreClocks.Length + 2 + _threadEffectiveClocks.Sum(t => t.Length) + 2
                : _coreClocks.Length + 2;

            _uncoreClock = new Sensor("Uncore Ring Clock", uncoreClockIndex, SensorType.Clock, this, settings)
            { PresentationSortKey = "0_3" };
            ActivateSensor(_uncoreClock);
            _hasUncoreClock = true;
        }

        if (SupportsIntelImc(_microArchitecture) && _imcModule.ReadClock(out _))
        {
            int imcSensorBaseIndex = _hasAperfMperf
                ? _coreClocks.Length + 2 + _threadEffectiveClocks.Sum(t => t.Length) + 2
                : _coreClocks.Length + 2;
            if (_hasUncoreClock)
                imcSensorBaseIndex++;

            _imcClock = new Sensor("IMC Clock (QCLK)", imcSensorBaseIndex, SensorType.Clock, this, settings)
            { PresentationSortKey = "0_4_0" };
            _memoryDataRate = new Sensor("Memory Data Rate", imcSensorBaseIndex + 1, SensorType.Frequency, this, settings)
            { PresentationSortKey = "0_4_1" };
            _dramFrequency = new Sensor("DRAM Frequency", imcSensorBaseIndex + 2, SensorType.Clock, this, settings)
            { PresentationSortKey = "0_4_2" };
            _memoryGear = new Sensor("Memory Gear", imcSensorBaseIndex + 3, SensorType.Factor, this, settings)
            { PresentationSortKey = "0_4_3" };
            ActivateSensor(_imcClock);
            ActivateSensor(_memoryDataRate);
            ActivateSensor(_dramFrequency);
            ActivateSensor(_memoryGear);
            _hasImc = true;
            _hasImcLive = _imcModule.ReadLiveClock(out _);
        }

        if (_microArchitecture is MicroArchitecture.Airmont or
            MicroArchitecture.AlderLake or
            MicroArchitecture.ArrowLake or
            MicroArchitecture.BartlettLake or
            MicroArchitecture.Broadwell or
            MicroArchitecture.CannonLake or
            MicroArchitecture.CometLake or
            MicroArchitecture.EmeraldRapids or
            MicroArchitecture.Goldmont or
            MicroArchitecture.GoldmontPlus or
            MicroArchitecture.Haswell or
            MicroArchitecture.IceLake or
            MicroArchitecture.IvyBridge or
            MicroArchitecture.JasperLake or
            MicroArchitecture.KabyLake or
            MicroArchitecture.LunarLake or
            MicroArchitecture.PantherLake or
            MicroArchitecture.NovaLake or
            MicroArchitecture.WildcatLake or
            MicroArchitecture.MeteorLake or
            MicroArchitecture.RaptorLake or
            MicroArchitecture.RocketLake or
            MicroArchitecture.SandyBridge or
            MicroArchitecture.Silvermont or
            MicroArchitecture.Skylake or
            MicroArchitecture.TigerLake or
            MicroArchitecture.SapphireRapids or
            MicroArchitecture.ElkhartLake or
            MicroArchitecture.Tremont)
        {
            _powerSensors = new Sensor[_energyStatusMsrs.Length];
            _lastEnergyTime = new DateTime[_energyStatusMsrs.Length];
            _lastEnergyConsumed = new uint[_energyStatusMsrs.Length];

            if (_msrModule.ReadMsr(MSR_RAPL_POWER_UNIT, out eax, out uint _))
            {
                EnergyUnitsMultiplier = _microArchitecture switch
                {
                    MicroArchitecture.Silvermont or MicroArchitecture.Airmont => 1.0e-6f * (1 << (int)((eax >> 8) & 0x1F)),
                    _ => 1.0f / (1 << (int)((eax >> 8) & 0x1F))
                };
            }

            if (EnergyUnitsMultiplier != 0)
            {
                string[] powerSensorLabels = { "CPU Package", "CPU Cores", "CPU Graphics", "CPU Memory", "CPU Platform" };

                for (int i = 0; i < _energyStatusMsrs.Length; i++)
                {
                    if (!_msrModule.ReadMsr(_energyStatusMsrs[i], out eax, out uint _))
                        continue;

                    // Don't show the "GPU Graphics" sensor on windows, it will show up under the GPU instead.
                    if (i == 2 && !Software.OperatingSystem.IsUnix)
                        continue;

                    _lastEnergyTime[i] = DateTime.UtcNow;
                    _lastEnergyConsumed[i] = eax;
                    _powerSensors[i] = new Sensor(powerSensorLabels[i],
                        i,
                        SensorType.Power,
                        this,
                        settings)
                    { IsPresentationDefault = powerSensorLabels[i] == "CPU Package", PresentationSortKey = $"2_{i}" };

                    ActivateSensor(_powerSensors[i]);
                }
            }
        }

        if (_msrModule.ReadMsr(IA32_PERF_STATUS, out eax, out uint _) && ((eax >> 32) & 0xFFFF) > 0)
        {
            _coreVoltage = new Sensor("CPU Core", 0, SensorType.Voltage, this, settings)
            { PresentationSortKey = $"4_0" };
            ActivateSensor(_coreVoltage);
        }

        _coreVIDs = new Sensor[_coreCount];
        for (int i = 0; i < _coreVIDs.Length; i++)
        {
            _coreVIDs[i] = new Sensor(CoreString(i), i + 1, SensorType.Voltage, this, settings)
            { PresentationSortKey = $"4_1_{i}" };
            ActivateSensor(_coreVIDs[i]);
        }

        Update();
    }

    public float EnergyUnitsMultiplier { get; }

    private float[] Floats(float f)
    {
        float[] result = new float[_coreCount];
        for (int i = 0; i < _coreCount; i++)
            result[i] = f;

        return result;
    }

    // MSR_UNCORE_PERF_STATUS (0x621) carries the current uncore ratio in bits [6:0]
    // on Intel client platforms where the local intel-perfmon data also documents
    // an uncore clock source (UNC_CLOCK.SOCKET / Info_System_Uncore_Frequency).
    // CUR_RATIO * BCLK is exposed as the instantaneous Uncore/Ring clock.
    //
    // This is intentionally not a memory-clock sensor. Newer client platforms have
    // a decoupled IMC clock, and server uncore PMUs expose their clocks through
    // different PMU domains/counters rather than this simple MSR-ratio path.
    private static bool SupportsUncoreClock(MicroArchitecture arch, uint model)
    {
        switch (arch)
        {
            case MicroArchitecture.SandyBridge:
                return model == 0x2A; // exclude SNB-EP/EX (0x2D)
            case MicroArchitecture.IvyBridge:
                return model == 0x3A; // exclude Ivy Town (0x3E)
            case MicroArchitecture.Haswell:
                return model is 0x3C or 0x45 or 0x46; // exclude HSX (0x3F)
            case MicroArchitecture.Broadwell:
                return model is 0x3D or 0x47; // exclude BDX (0x4F) and BDW-DE (0x56)
            case MicroArchitecture.Skylake:
                return model is 0x4E or 0x5E; // exclude SKX (0x55)
            case MicroArchitecture.KabyLake:
            case MicroArchitecture.CometLake:
                return true;
            case MicroArchitecture.IceLake:
                return model is 0x7D or 0x7E; // exclude Ice Lake server (0x6A/0x6C)
            case MicroArchitecture.RocketLake:
                return model == 0xA7;
            case MicroArchitecture.TigerLake:
                return model is 0x8C or 0x8D;
            case MicroArchitecture.AlderLake:
                return model is 0x97 or 0x9A or 0xBE;
            case MicroArchitecture.RaptorLake:
                return model is 0xB7 or 0xBA or 0xBF;
            case MicroArchitecture.MeteorLake:
                return model is 0xAA or 0xAC;
            case MicroArchitecture.LunarLake:
                return model == 0xBD;
            case MicroArchitecture.ArrowLake:
                return model is 0xB5 or 0xC5 or 0xC6; // exclude reserved 0xC7/0xC8
            case MicroArchitecture.BartlettLake:
                return model == 0xD7; // Linux: INTEL_BARTLETTLAKE (Raptor Cove core, client)
            case MicroArchitecture.PantherLake:
                return model == 0xCC; // Linux: INTEL_PANTHERLAKE_L
            case MicroArchitecture.NovaLake:
                // Family 18 (0x12). Linux: INTEL_NOVALAKE = IFM(18, 0x01),
                // INTEL_NOVALAKE_L = IFM(18, 0x03). MicroArchitecture.NovaLake is
                // only assigned for Family 18 (see family-switch above), so any
                // model that reaches here is a NovaLake variant — accept all.
                return true;
            case MicroArchitecture.WildcatLake:
                return model == 0xD5; // Linux: INTEL_WILDCATLAKE_L
            // EmeraldRapids (0xCF) is server silicon and uses the server uncore PMU
            // path, not the client MSR_UNCORE_RATIO_LIMIT ratio — intentionally not
            // listed here.
            default:
                return false;
        }
    }

    // Architectures whose IMC clock is exposed by the IntelIMC PawnIO module.
    // The kernel module enforces its own per-CPUID allowlist (MTL/ARL/LNL/PTL
    // via MEMSS_PMA, ADL/RPL via SA_PERF_STATUS), so this gate only filters
    // out the broad architecture buckets the module never targets. The
    // sensor registration site additionally probes the static IOCTL once
    // and only activates the sensors when the kernel actually returns data.
    // Validation status (per IntelIMC.p): ARL/LNL/PTL are validated end-to-end;
    // MTL/ADL/RPL are accepted by the module but flagged EXPERIMENTAL until
    // hardware cross-validation lands. The sensors are exposed for all six
    // and hidden from the default presentation (IsPresentationDefault stays
    // false) so consumers opt in explicitly.
    private static bool SupportsIntelImc(MicroArchitecture arch)
    {
        // NovaLake is included on the speculative assumption that it inherits the
        // Core-Ultra MEMSS_PMA / SA_PERF_STATUS register layout from PTL/ARL/LNL.
        // BartlettLake is included because it shares the Raptor Cove core with RPL
        // and is expected to follow the same SA_PERF_STATUS path; the kernel
        // module still gates on its own CPUID allowlist.
        // WildcatLake is intentionally not listed — its IMC topology (low-power
        // consumer SoC) is unknown and may not follow the MEMSS_PMA pattern.
        // EmeraldRapids is intentionally not listed — it is server silicon and
        // exposes memory controller telemetry through the server uncore PMU,
        // not the client MCHBAR path the module targets.
        return arch is MicroArchitecture.AlderLake or
            MicroArchitecture.RaptorLake or
            MicroArchitecture.MeteorLake or
            MicroArchitecture.ArrowLake or
            MicroArchitecture.LunarLake or
            MicroArchitecture.PantherLake or
            MicroArchitecture.NovaLake or
            MicroArchitecture.BartlettLake;
    }

    private float[] GetTjMaxFromMsr()
    {
        float[] result = new float[_coreCount];
        for (int i = 0; i < _coreCount; i++)
        {
            if (_msrModule.ReadMsr(IA32_TEMPERATURE_TARGET, out uint eax, out uint _, _cpuId[i][0].Affinity))
                result[i] = (eax >> 16) & 0xFF;
            else
                result[i] = 100;
        }

        return result;
    }

    private string ReportCoreInfo(int coreIndex)
    {
        StringBuilder r = new();
        var previousAffinity = ThreadAffinity.Set(_cpuId[coreIndex][0].Affinity);

        r.AppendLine($"=== Core {coreIndex} ===");

        // Leaf 0x1A - Hybrid info
        if (OpCode.CpuId(0x1A, 0, out uint eax, out uint ebx, out uint ecx, out uint edx))
        {
            r.AppendLine($"Leaf 0x1A: EAX=0x{eax:X8} EBX=0x{ebx:X8} ECX=0x{ecx:X8} EDX=0x{edx:X8}");
        }

        // Leaf 0x1F - V2 Extended Topology
        r.AppendLine("Leaf 0x1F:");
        for (uint sub = 0; sub < 10; sub++)
        {
            if (OpCode.CpuId(0x1F, sub, out eax, out ebx, out ecx, out edx))
            {
                uint levelType = (ecx >> 8) & 0xFF;
                r.AppendLine($"  Sub {sub}: EAX=0x{eax:X8} EBX=0x{ebx:X8} ECX=0x{ecx:X8} EDX=0x{edx:X8} (LevelType={levelType})");
                if (levelType == 0) break;
            }
        }

        // Leaf 0x4 - Cache topology
        r.AppendLine("Leaf 0x4:");
        for (uint sub = 0; sub < 6; sub++)
        {
            if (OpCode.CpuId(0x4, sub, out eax, out ebx, out ecx, out edx))
            {
                uint cacheType = eax & 0x1F;
                if (cacheType == 0) break;
                uint cacheLevel = (eax >> 5) & 0x7;
                r.AppendLine($"  Sub {sub}: L{cacheLevel} Type={cacheType} EAX=0x{eax:X8}");
            }
        }

        ThreadAffinity.Set(previousAffinity);
        return r.ToString();
    }


    public override string GetReport()
    {
        StringBuilder r = new();
        r.Append(base.GetReport());
        r.Append("MicroArchitecture: ");
        r.AppendLine(_microArchitecture.ToString());
        r.Append("Time Stamp Counter Multiplier: ");
        r.AppendLine(_timeStampCounterMultiplier.ToString(CultureInfo.InvariantCulture));
        r.AppendLine();
        for (int i = 0; i < _coreCount; i++)
        {
            r.Append(ReportCoreInfo(i));
        }
        return r.ToString();
    }

    /// <inheritdoc />
    public override void Close()
    {
        base.Close();
        _msrModule.Close();
        _imcModule.Close();
    }

    public override void Update()
    {
        base.Update();

        float coreMax = float.MinValue;
        float coreAvg = 0;
        uint eax;

        for (int i = 0; i < _coreTemperatures.Length; i++)
        {
            // if reading is valid
            if (_msrModule.ReadMsr(IA32_THERM_STATUS_MSR, out eax, out _, _cpuId[i][0].Affinity) && (eax & 0x80000000) != 0)
            {
                // get the dist from tjMax from bits 22:16
                float deltaT = (eax & 0x007F0000) >> 16;
                float tjMax = _coreTemperatures[i].Parameters[0].Value;
                float tSlope = _coreTemperatures[i].Parameters[1].Value;
                _coreTemperatures[i].Value = tjMax - (tSlope * deltaT);

                coreAvg += (float)_coreTemperatures[i].Value;
                if (coreMax < _coreTemperatures[i].Value)
                    coreMax = (float)_coreTemperatures[i].Value;

                _distToTjMaxTemperatures[i].Value = deltaT;
            }
            else
            {
                _coreTemperatures[i].Value = null;
                _distToTjMaxTemperatures[i].Value = null;
            }
        }

        //calculate average cpu temperature over all cores
        if (_coreMax != null && coreMax != float.MinValue)
        {
            _coreMax.Value = coreMax;
            coreAvg /= _coreTemperatures.Length;
            _coreAvg.Value = coreAvg;
        }

        if (_packageTemperature != null)
        {
            // if reading is valid
            if (_msrModule.ReadMsr(IA32_PACKAGE_THERM_STATUS, out eax, out _, _cpuId[0][0].Affinity) && (eax & 0x80000000) != 0)
            {
                // get the dist from tjMax from bits 22:16
                float deltaT = (eax & 0x007F0000) >> 16;
                float tjMax = _packageTemperature.Parameters[0].Value;
                float tSlope = _packageTemperature.Parameters[1].Value;
                _packageTemperature.Value = tjMax - (tSlope * deltaT);
            }
            else
            {
                _packageTemperature.Value = null;
            }
        }

        if (HasTimeStampCounter && _timeStampCounterMultiplier > 0)
        {
            double newBusClock = 0;
            for (int i = 0; i < _coreClocks.Length; i++)
            {
                System.Threading.Thread.Sleep(1);
                if (_msrModule.ReadMsr(IA32_PERF_STATUS, out eax, out _, _cpuId[i][0].Affinity))
                {
                    newBusClock = TimeStampCounterFrequency / _timeStampCounterMultiplier;
                    switch (_microArchitecture)
                    {
                        case MicroArchitecture.Nehalem:
                            _coreClocks[i].Value = (float)((eax & 0xff) * newBusClock);
                            break;
                        case MicroArchitecture.Airmont:
                        case MicroArchitecture.AlderLake:
                        case MicroArchitecture.ArrowLake:
                        case MicroArchitecture.BartlettLake:
                        case MicroArchitecture.Broadwell:
                        case MicroArchitecture.CannonLake:
                        case MicroArchitecture.CometLake:
                        case MicroArchitecture.EmeraldRapids:
                        case MicroArchitecture.Goldmont:
                        case MicroArchitecture.GoldmontPlus:
                        case MicroArchitecture.Haswell:
                        case MicroArchitecture.IceLake:
                        case MicroArchitecture.IvyBridge:
                        case MicroArchitecture.JasperLake:
                        case MicroArchitecture.KabyLake:
                        case MicroArchitecture.LunarLake:
                        case MicroArchitecture.PantherLake:
                        case MicroArchitecture.NovaLake:
                        case MicroArchitecture.WildcatLake:
                        case MicroArchitecture.MeteorLake:
                        case MicroArchitecture.RaptorLake:
                        case MicroArchitecture.RocketLake:
                        case MicroArchitecture.SandyBridge:
                        case MicroArchitecture.Silvermont:
                        case MicroArchitecture.Skylake:
                        case MicroArchitecture.TigerLake:
                        case MicroArchitecture.SapphireRapids:
                        case MicroArchitecture.ElkhartLake:
                        case MicroArchitecture.Tremont:
                            _coreClocks[i].Value = (float)(((eax >> 8) & 0xff) * newBusClock);
                            break;
                        default:
                            _coreClocks[i].Value = (float)((((eax >> 8) & 0x1f) + (0.5 * ((eax >> 14) & 1))) * newBusClock);
                            break;
                    }
                }
                else
                {
                    // if IA32_PERF_STATUS is not available, assume TSC frequency
                    _coreClocks[i].Value = (float)TimeStampCounterFrequency;
                }
            }

            _maxClock.Value = _coreClocks.Max(clk => clk.Value);

            if (newBusClock > 0)
            {
                _busClock.Value = (float)newBusClock;
                ActivateSensor(_busClock);
            }

            if (_hasUncoreClock && newBusClock > 0 &&
                _msrModule.ReadMsr(MSR_UNCORE_PERF_STATUS, out uint uncoreEax, out _))
            {
                // CUR_RATIO (bits [6:0]) * BCLK is the instantaneous uncore/ring
                // clock in MHz.
                uint curRatio = uncoreEax & 0x7F;
                if (curRatio > 0)
                    _uncoreClock.Value = (float)(curRatio * newBusClock);
                else
                    _uncoreClock.Value = null;
            }

            if (_hasImc && newBusClock > 0)
            {
                // Prefer the live SA_PERF workpoint where the kernel module
                // exposes one (Core Ultra MTL/LNL/PTL). On Arrow Lake the live
                // IOCTL is short-circuited (SA_PERF reads as zero, see
                // Deploy/VALIDATION-ARL.md) and on Alder/Raptor Lake the
                // static IOCTL already returns a live value via SA_PERF, so
                // we fall back to the static read in both cases.
                bool gotImc = _hasImcLive
                    ? _imcModule.ReadLiveClock(out IntelImc.ImcClock imcClock)
                    : _imcModule.ReadClock(out imcClock);
                if (!gotImc && _hasImcLive)
                    gotImc = _imcModule.ReadClock(out imcClock);

                if (gotImc)
                {
                    double refMHz = IntelImc.GetReferenceMHz(imcClock.ReferenceClock, newBusClock);
                    if (refMHz > 0 && imcClock.Ratio > 0)
                    {
                        // QCLK = ratio * reference. The data rate (MT/s) and
                        // IO clock (MHz) follow the IntelIMC.p computation but
                        // require a known gear. SA_PERF_STATUS does not encode
                        // gear on ADL/RPL, and on Core Ultra the live IOCTL
                        // can return Gear=Unknown when MEMSS_PMA's strict
                        // reserved-bits check rejects on a future stepping;
                        // in those cases we still surface QCLK but suppress
                        // the dependent sensors rather than report a wrong
                        // (typically half-rate) data rate.
                        double qclk = imcClock.Ratio * refMHz;
                        _imcClock.Value = (float)qclk;
                        if (imcClock.Gear != IntelImc.ImcGear.Unknown)
                        {
                            double dataRate = qclk * (int)imcClock.Gear;
                            _memoryDataRate.Value = (float)dataRate;
                            _dramFrequency.Value = (float)(dataRate / 2.0);
                            _memoryGear.Value = (int)imcClock.Gear;
                        }
                        else
                        {
                            _memoryDataRate.Value = null;
                            _dramFrequency.Value = null;
                            _memoryGear.Value = null;
                        }
                    }
                    else
                    {
                        _imcClock.Value = null;
                        _memoryDataRate.Value = null;
                        _dramFrequency.Value = null;
                        _memoryGear.Value = null;
                    }
                }
                else
                {
                    _imcClock.Value = null;
                    _memoryDataRate.Value = null;
                    _dramFrequency.Value = null;
                    _memoryGear.Value = null;
                }
            }
        }

        if (_hasAperfMperf)
        {
            double effectiveSum = 0;
            double effectiveMax = 0;
            int effectiveCount = 0;

            for (int i = 0; i < _threadEffectiveClocks.Length; i++)
            {
                for (int j = 0; j < _threadEffectiveClocks[i].Length; j++)
                {
                    DateTime sampleTime = DateTime.UtcNow;

                    if (!TryReadPerfCounters(_cpuId[i][j].Affinity, out ulong aperf, out ulong mperf))
                    {
                        _threadEffectiveClocks[i][j].Value = null;
                        continue;
                    }

                    if (aperf < _lastAperf[i][j] ||
                        mperf < _lastMperf[i][j])
                    {
                        // Counter overflow - reset
                        _lastAperf[i][j] = aperf;
                        _lastMperf[i][j] = mperf;
                        _lastSampleTime[i][j] = sampleTime;
                        _threadEffectiveClocks[i][j].Value = null;
                        continue;
                    }

                    TimeSpan sampleDuration = sampleTime - _lastSampleTime[i][j];
                    ulong aperfDelta = aperf - _lastAperf[i][j];
                    ulong mperfDelta = mperf - _lastMperf[i][j];
                    _lastAperf[i][j] = aperf;
                    _lastMperf[i][j] = mperf;
                    _lastSampleTime[i][j] = sampleTime;

                    if (aperfDelta == 0 || mperfDelta == 0 || sampleDuration.Ticks == 0)
                    {
                        _threadEffectiveClocks[i][j].Value = null;
                        continue;
                    }

                    // Effective clock = APERF cycles / elapsed time (same approach as AMD)
                    double freq = aperfDelta / (sampleDuration.TotalMilliseconds * 1000.0);

                    // Clamp effective clock between 0 and core clock
                    float maxClock = _coreClocks[i].Value ?? (float)TimeStampCounterFrequency;

                    // Clamping must consider BCLK oc (max 10%) and Spread Spectrum Clocking (SSC) (1-2%)
                    freq = Math.Max(0, Math.Min(freq, maxClock * 1.12));
                    _threadEffectiveClocks[i][j].Value = (float)Math.Round(freq, 0);

                    effectiveSum += freq;
                    effectiveCount++;
                    if (freq > effectiveMax)
                        effectiveMax = freq;
                }
            }

            if (effectiveCount > 0)
            {
                _coreEffectiveAvg.Value = (float)Math.Round(effectiveSum / effectiveCount, 0);
                _coreEffectiveMax.Value = (float)Math.Round(effectiveMax, 0);
            }
            else
            {
                _coreEffectiveAvg.Value = null;
                _coreEffectiveMax.Value = null;
            }
        }

        if (_powerSensors != null)
        {
            foreach (Sensor sensor in _powerSensors)
            {
                if (sensor == null)
                    continue;

                if (!_msrModule.ReadMsr(_energyStatusMsrs[sensor.Index], out eax, out _))
                    continue;

                DateTime time = DateTime.UtcNow;
                uint energyConsumed = eax;
                float deltaTime = (float)(time - _lastEnergyTime[sensor.Index]).TotalSeconds;
                if (deltaTime < 0.01)
                    continue;

                sensor.Value = EnergyUnitsMultiplier * unchecked(energyConsumed - _lastEnergyConsumed[sensor.Index]) / deltaTime;
                _lastEnergyTime[sensor.Index] = time;
                _lastEnergyConsumed[sensor.Index] = energyConsumed;
            }
        }

        // Read package-level core voltage
        if (_coreVoltage != null && _msrModule.ReadMsr(IA32_PERF_STATUS, out eax, out uint edx))
        {
            // Voltage is in bits 47:32 of the 64-bit MSR (IA32_PERF_STATUS)
            // ReadMsr returns: eax = bits 31:0, edx = bits 63:32
            // Therefore, voltage (bits 47:32) is in bits 15:0 of edx
            uint vid = edx & 0xFFFF;

            if (vid > 0)
            {
                // Voltage formula: VID / 2^13 (documented for Sandy Bridge and some later CPUs)
                _coreVoltage.Value = vid / (float)(1 << 13);
            }
            else
            {
                float voltageEax = (eax & 0xFFFF) / (float)(1 << 13);

                if (voltageEax > 0)
                {
                    _coreVoltage.Value = voltageEax;
                }
                else
                {
                    // VID field is 0 on modern HWP-enabled CPUs (Skylake and newer)
                    // Voltage is managed internally by hardware and not exposed via this MSR
                    DeactivateSensor(_coreVoltage);
                }
            }
        }

        // Read per-core VIDs
        for (int i = 0; i < _coreVIDs.Length; i++)
        {
            if (_msrModule.ReadMsr(IA32_PERF_STATUS, out eax, out edx, _cpuId[i][0].Affinity))
            {
                uint vid = edx & 0xFFFF;

                if (vid > 0)
                {
                    _coreVIDs[i].Value = vid / (float)(1 << 13);
                    ActivateSensor(_coreVIDs[i]);
                }
                else
                {
                    float voltageEax = (eax & 0xFFFF) / (float)(1 << 13);
                    if (voltageEax > 0)
                    {
                        _coreVIDs[i].Value = voltageEax;
                        ActivateSensor(_coreVIDs[i]);
                    }
                    else
                    {
                        DeactivateSensor(_coreVIDs[i]);
                    }
                }
            }
            else
            {
                DeactivateSensor(_coreVIDs[i]);
            }
        }
    }

    [SuppressMessage("ReSharper", "IdentifierTypo")]
    private enum MicroArchitecture
    {
        Airmont,
        AlderLake,
        Atom,
        ArrowLake,     // Family 6 model 0xC5 (-H), 0xC6 (vanilla), 0xB5 (-U)
        BartlettLake,  // Family 6 model 0xD7 (Raptor Cove core)
        Broadwell,
        CannonLake,
        CometLake,
        Core,
        EmeraldRapids, // Family 6 model 0xCF (server, Raptor Cove core)
        Goldmont,
        GoldmontPlus,
        Haswell,
        IceLake,
        IvyBridge,
        JasperLake,
        KabyLake,
        LunarLake,     // Family 6 model 0xBD (Lion Cove / Skymont)
        PantherLake,   // Family 6 model 0xCC (Cougar Cove / Darkmont)
        NovaLake,      // Family 18 model 0x01 (vanilla) and 0x03 (-L mobile),
                       // Coyote Cove / Arctic Wolf cores
        WildcatLake,   // Family 6 model 0xD5 (-L)
        Nehalem,
        NetBurst,
        MeteorLake,
        RocketLake,
        SandyBridge,
        Silvermont,
        Skylake,
        TigerLake,
        Tremont,
        RaptorLake,
        SapphireRapids,
        ElkhartLake,
        Unknown
    }

    // ReSharper disable InconsistentNaming
    private const uint IA32_PACKAGE_THERM_STATUS = 0x1B1;
    private const uint IA32_PERF_STATUS = 0x0198;
    private const uint IA32_TEMPERATURE_TARGET = 0x01A2;
    private const uint IA32_THERM_STATUS_MSR = 0x019C;
    private const uint IA32_MPERF = 0xE7;
    private const uint IA32_APERF = 0xE8;

    private const uint MSR_DRAM_ENERGY_STATUS = 0x619;
    private const uint MSR_PKG_ENERGY_STATUS = 0x611;
    private const uint MSR_PLATFORM_INFO = 0xCE;
    private const uint MSR_PP0_ENERGY_STATUS = 0x639;
    private const uint MSR_PP1_ENERGY_STATUS = 0x641;
    private const uint MSR_PLATFORM_ENERGY_STATUS = 0x64D;

    private const uint MSR_RAPL_POWER_UNIT = 0x606;

    // MSR_UNCORE_PERF_STATUS (Intel client, Sandy Bridge and later) - bits [6:0] hold
    // CUR_RATIO, the current uncore ratio in 100 MHz units. PawnIO's IntelMSR module
    // exposes it under the alternative name MSR_UNC_PERF_GLOBAL_STATUS at the same
    // address (0x621). On server uncore PMUs the same address has a different
    // meaning, so callers must restrict use to client microarchitectures.
    private const uint MSR_UNCORE_PERF_STATUS = 0x621;
    // ReSharper restore InconsistentNaming

    private bool TryReadPerfCounters(GroupAffinity affinity, out ulong aperf, out ulong mperf)
    {
        GroupAffinity previousAffinity = ThreadAffinity.Set(affinity);
        bool readAperf = _msrModule.ReadMsr(IA32_APERF, out uint aperfEax, out uint aperfEdx);
        bool readMperf = _msrModule.ReadMsr(IA32_MPERF, out uint mperfEax, out uint mperfEdx);

        if (readAperf && readMperf && aperfEax == 0 && aperfEdx == 0 && mperfEax == 0 && mperfEdx == 0)
        {
            System.Threading.Thread.SpinWait(10000);
            readAperf = _msrModule.ReadMsr(IA32_APERF, out aperfEax, out aperfEdx);
            readMperf = _msrModule.ReadMsr(IA32_MPERF, out mperfEax, out mperfEdx);
        }
        ThreadAffinity.Set(previousAffinity);

        aperf = ((ulong)aperfEdx << 32) | aperfEax;
        mperf = ((ulong)mperfEdx << 32) | mperfEax;
        return readAperf && readMperf && (aperf != 0 || mperf != 0);
    }
}
