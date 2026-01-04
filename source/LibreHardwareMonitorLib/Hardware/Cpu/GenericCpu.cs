// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace LibreHardwareMonitor.Hardware.Cpu;

/// <summary>
/// Class GenericCpu for basic CPU management
/// </summary>
public class GenericCpu : Hardware
{
    private const uint CPUID_CORE_MASK_STATUS = 0x1A;

    protected readonly int _coreCount;
    protected readonly CpuId[][] _cpuId;
    protected readonly uint _family;
    protected readonly uint _model;
    protected readonly uint _packageType;
    protected readonly uint _stepping;
    protected readonly int _threadCount;
    protected readonly bool _isHybrid;

    private readonly CpuLoad _cpuLoad;
    private readonly double _estimatedTimeStampCounterFrequency;
    private readonly double _estimatedTimeStampCounterFrequencyError;
    private readonly bool _isInvariantTimeStampCounter;
    private readonly Sensor[] _threadLoads;

    private readonly Sensor _totalLoad;
    private readonly Sensor _maxLoad;
    private readonly Vendor _vendor;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenericCpu"/> class.
    /// </summary>
    /// <param name="processorIndex">The zero-based processor index.</param>
    /// <param name="cpuId">The CPUID data for all cores and threads.</param>
    /// <param name="settings">The settings instance.</param>
    public GenericCpu(int processorIndex, CpuId[][] cpuId, ISettings settings) : base(cpuId[0][0].Name, CreateIdentifier(cpuId[0][0].Vendor, processorIndex), settings)
    {
        _cpuId = cpuId;
        _vendor = cpuId[0][0].Vendor;
        _family = cpuId[0][0].Family;
        _model = cpuId[0][0].Model;
        _stepping = cpuId[0][0].Stepping;
        _packageType = cpuId[0][0].PkgType;

        Index = processorIndex;
        _coreCount = cpuId.Length;
        _threadCount = cpuId.Sum(x => x.Length);
        _isHybrid = CpuArchitecture.IsHybridDesign([.. cpuId.Select(core => core[0])]);

        // Check if processor has MSRs.
        HasModelSpecificRegisters = cpuId[0][0].Data.GetLength(0) > 1 && (cpuId[0][0].Data[1, 3] & 0x20) != 0;

        // Check if processor has a TSC.
        HasTimeStampCounter = cpuId[0][0].Data.GetLength(0) > 1 && (cpuId[0][0].Data[1, 3] & 0x10) != 0;

        // Check if processor supports an invariant TSC.
        _isInvariantTimeStampCounter = cpuId[0][0].ExtData.GetLength(0) > 7 && (cpuId[0][0].ExtData[7, 3] & 0x100) != 0;

        _totalLoad = _coreCount > 1 ? new Sensor("CPU Total", 0, SensorType.Load, this, settings) { PresentationSortKey = "1_2_1" } : null;
        _maxLoad = _coreCount > 1 ? new Sensor("CPU Max", 1, SensorType.Load, this, settings) { PresentationSortKey = "1_2_2" } : null;

        _cpuLoad = new CpuLoad(cpuId);
        if (_cpuLoad.IsAvailable)
        {
            _threadLoads = new Sensor[_threadCount];
            for (int coreIdx = 0; coreIdx < cpuId.Length; coreIdx++)
            {
                for (int threadIdx = 0; threadIdx < cpuId[coreIdx].Length; threadIdx++)
                {
                    int thread = cpuId[coreIdx][threadIdx].Thread;
                    if (thread < _threadLoads.Length)
                    {
                        // Some cores may have 2 threads while others have only one (e.g. P-cores vs E-cores on Intel 12th gen).
                        string sensorName = CoreString(coreIdx) + (cpuId[coreIdx].Length > 1 ? $" Thread #{threadIdx + 1}" : string.Empty);
                        _threadLoads[thread] = new Sensor(sensorName, thread + 2, SensorType.Load, this, settings)
                        { IsPresentationDefault = true, PresentationSortKey = $"1_1_{coreIdx + threadIdx}" };

                        ActivateSensor(_threadLoads[thread]);
                    }
                }
            }

            if (_totalLoad != null)
            {
                ActivateSensor(_totalLoad);
            }

            if (_maxLoad != null)
            {
                ActivateSensor(_maxLoad);
            }
        }

        if (HasTimeStampCounter)
        {
            GroupAffinity previousAffinity = ThreadAffinity.Set(cpuId[0][0].Affinity);

            EstimateTimeStampCounterFrequency(
                out _estimatedTimeStampCounterFrequency,
                out _estimatedTimeStampCounterFrequencyError);

            ThreadAffinity.Set(previousAffinity);
        }
        else
        {
            _estimatedTimeStampCounterFrequency = 0;
        }

        TimeStampCounterFrequency = _estimatedTimeStampCounterFrequency;
    }

    /// <summary>
    /// Gets the CPUID.
    /// </summary>
    public CpuId[][] CpuId => _cpuId;

    /// <inheritdoc />
    public override HardwareType HardwareType => HardwareType.Cpu;

    /// <summary>
    /// Gets a value indicating whether the processor supports model-specific registers (MSRs).
    /// </summary>
    public bool HasModelSpecificRegisters { get; }

    /// <summary>
    /// Gets a value indicating whether the processor has a time stamp counter (TSC).
    /// </summary>
    public bool HasTimeStampCounter { get; }

    /// <summary>
    /// Gets the CPU index.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Gets the time stamp counter frequency in MHz.
    /// </summary>
    public double TimeStampCounterFrequency { get; private set; }

    protected string CoreString(int i)
    {
        if (_coreCount == 1)
        {
            return $"Core {GetCoreLabel(i)}";
        }

        return $"Core #{i + 1} {GetCoreLabel(i)}";
    }

    // https://github.com/InstLatx64/InstLatX64_Demo/commit/e149a972655aff9c41f3eac66ad51fcfac1262b5
    private string GetCoreLabel(int i)
    {
        string corelabel = string.Empty;
        if (_isHybrid)
        {
            if (_cpuId[i][0].Vendor == Vendor.Intel)
            {
                var previousAffinity = ThreadAffinity.Set(_cpuId[i][0].Affinity);

                if (OpCode.CpuId(CPUID_CORE_MASK_STATUS, 0, out uint eax, out uint ebx, out uint ecx, out uint edx))
                {
                    uint coreType = eax >> 24;

                    switch (coreType)
                    {
                        case 0x20: // Efficient core
                                   // Check for L3 cache to distinguish E from LP-E
                            bool hasL3 = false;
                            for (uint sub = 0; sub < 10; sub++)
                            {
                                if (OpCode.CpuId(0x4, sub, out uint eax4, out _, out _, out _))
                                {
                                    uint cacheType = eax4 & 0x1F;
                                    if (cacheType == 0) break;

                                    uint cacheLevel = (eax4 >> 5) & 0x7;
                                    if (cacheLevel == 3)
                                    {
                                        hasL3 = true;
                                        break;
                                    }
                                }
                            }
                            corelabel = hasL3 ? "E" : "LPE";
                            break;
                        case 0x40:
                            corelabel = "P";
                            break;
                        default:
                            break;
                    }
                }

                ThreadAffinity.Set(previousAffinity);
            }
            else if (_cpuId[i][0].Vendor == Vendor.AMD)
            {
                var previousAffinity = ThreadAffinity.Set(_cpuId[i][0].Affinity);
                if (OpCode.CpuId(0x80000026, 0, out uint eax, out uint ebx, out uint ecx, out uint edx))
                {
                    // Heterogeneous core topology supported
                    if ((eax & (1u << 30)) != 0)
                    {
                        uint coreType = (ebx >> 28) & 0xF;
                        switch (coreType)
                        {
                            case 0: corelabel = "P"; break;
                            case 1: corelabel = "D"; break;
                            default: break;
                        }
                    }
                }

                ThreadAffinity.Set(previousAffinity);
            }
        }

        return corelabel;
    }

    /// <summary>
    /// Gets the core type for the specified core index.
    /// </summary>
    /// <param name="coreIndex">The zero-based core index.</param>
    /// <returns>The <see cref="CpuCoreType"/> of the specified core.</returns>
    public CpuCoreType GetCoreType(int coreIndex)
    {
        if (coreIndex < 0 || coreIndex >= _coreCount)
            return CpuCoreType.Standard;

        if (!_isHybrid)
            return CpuCoreType.Standard;

        if (_cpuId[coreIndex][0].Vendor == Vendor.Intel)
        {
            var previousAffinity = ThreadAffinity.Set(_cpuId[coreIndex][0].Affinity);

            try
            {
                if (OpCode.CpuId(CPUID_CORE_MASK_STATUS, 0, out uint eax, out uint ebx, out uint ecx, out uint edx))
                {
                    uint coreType = eax >> 24;

                    switch (coreType)
                    {
                        case 0x20: // Efficient core
                            // Check for L3 cache to distinguish E from LP-E
                            bool hasL3 = false;
                            for (uint sub = 0; sub < 10; sub++)
                            {
                                if (OpCode.CpuId(0x4, sub, out uint eax4, out _, out _, out _))
                                {
                                    uint cacheType = eax4 & 0x1F;
                                    if (cacheType == 0) break;

                                    uint cacheLevel = (eax4 >> 5) & 0x7;
                                    if (cacheLevel == 3)
                                    {
                                        hasL3 = true;
                                        break;
                                    }
                                }
                            }
                            return hasL3 ? CpuCoreType.EfficiencyCore : CpuCoreType.LowPowerEfficiencyCore;
                        case 0x40:
                            return CpuCoreType.PerformanceCore;
                        default:
                            return CpuCoreType.Standard;
                    }
                }
            }
            finally
            {
                ThreadAffinity.Set(previousAffinity);
            }
        }
        else if (_cpuId[coreIndex][0].Vendor == Vendor.AMD)
        {
            var previousAffinity = ThreadAffinity.Set(_cpuId[coreIndex][0].Affinity);

            try
            {
                if (OpCode.CpuId(0x80000026, 0, out uint eax, out uint ebx, out uint ecx, out uint edx))
                {
                    // Heterogeneous core topology supported
                    if ((eax & (1u << 30)) != 0)
                    {
                        uint coreType = (ebx >> 28) & 0xF;
                        switch (coreType)
                        {
                            case 0: return CpuCoreType.PerformanceCore;
                            case 1: return CpuCoreType.DenseCore;
                            default: return CpuCoreType.Standard;
                        }
                    }
                }
            }
            finally
            {
                ThreadAffinity.Set(previousAffinity);
            }
        }

        return CpuCoreType.Standard;
    }

    /// <summary>
    /// Gets the number of cores in this CPU.
    /// </summary>
    public int CoreCount => _coreCount;

    /// <summary>
    /// Gets whether this CPU has a hybrid architecture with different core types.
    /// </summary>
    public bool IsHybrid => _isHybrid;

    private static Identifier CreateIdentifier(Vendor vendor, int processorIndex)
    {
        string s = vendor switch
        {
            Vendor.AMD => "amdcpu",
            Vendor.Intel => "intelcpu",
            _ => "genericcpu"
        };

        return new Identifier(s, processorIndex.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Gets the time stamp counter frequency from the native HWInfo library.
    /// </summary>
    /// <returns>The time stamp counter frequency in Hz.</returns>
    [DllImport("CapFrameX.Hwinfo.dll")]
    public static extern long GetTimeStampCounterFrequency();

    private static void EstimateTimeStampCounterFrequency(out double frequency, out double error)
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

    /// <inheritdoc />
    public override string GetReport()
    {
        StringBuilder r = new();

        switch (_vendor)
        {
            case Vendor.AMD:
                r.AppendLine("AMD CPU");
                break;
            case Vendor.Intel:
                r.AppendLine("Intel CPU");
                break;
            default:
                r.AppendLine("Generic CPU");
                break;
        }

        r.AppendLine();
        r.AppendFormat("Name: {0}{1}", _name, Environment.NewLine);
        r.AppendFormat("Number of Cores: {0}{1}", _coreCount, Environment.NewLine);
        r.AppendFormat("Threads per Core: {0}{1}", _cpuId[0].Length, Environment.NewLine);
        r.AppendLine(string.Format(CultureInfo.InvariantCulture, "Timer Frequency: {0} MHz", Stopwatch.Frequency * 1e-6));
        r.AppendLine("Time Stamp Counter: " + (HasTimeStampCounter ? _isInvariantTimeStampCounter ? "Invariant" : "Not Invariant" : "None"));
        r.AppendLine(string.Format(CultureInfo.InvariantCulture, "Estimated Time Stamp Counter Frequency: {0} MHz", Math.Round(_estimatedTimeStampCounterFrequency * 100) * 0.01));
        r.AppendLine(string.Format(CultureInfo.InvariantCulture,
            "Estimated Time Stamp Counter Frequency Error: {0} Mhz",
            Math.Round(_estimatedTimeStampCounterFrequency * _estimatedTimeStampCounterFrequencyError * 1e5) * 1e-5));

        r.AppendLine(string.Format(CultureInfo.InvariantCulture, "Time Stamp Counter Frequency: {0} MHz", Math.Round(TimeStampCounterFrequency * 100) * 0.01));
        r.AppendLine();

        return r.ToString();
    }

    /// <inheritdoc />
    public override void Update()
    {
        if (_cpuLoad.IsAvailable)
        {
            _cpuLoad.Update();

            float maxLoad = 0;
            if (_threadLoads != null)
            {
                for (int i = 0; i < _threadLoads.Length; i++)
                {
                    if (_threadLoads[i] != null)
                    {
                        _threadLoads[i].Value = (float)_cpuLoad.GetThreadLoad(i);
                        maxLoad = Math.Max(maxLoad, _threadLoads[i].Value ?? 0);
                    }
                }
            }

            if (_totalLoad != null)
                _totalLoad.Value = (float)_cpuLoad.GetTotalLoad();

            if (_maxLoad != null)
                _maxLoad.Value = maxLoad;
        }
    }
}
