// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.

using CapFrameX.Extensions;
using System;
using System.Runtime.InteropServices;

namespace LibreHardwareMonitor.Hardware.Cpu;

internal class CpuLoad
{
    private readonly CpuId[][] _cpuid;

    private readonly double[] _threadLoads;
    private long[] _idleTimes;
    private double _totalLoad;
    private long[] _totalTimes;
    private double _maxLoad;

    public CpuLoad(CpuId[][] cpuid)
    {
        _cpuid = cpuid;

        int threadCount = 0;
        for (int i = 0; i < cpuid.Length; i++)
        {
            threadCount += cpuid[i].Length;
        }

        _threadLoads = new double[threadCount];
        _totalLoad = 0;

        // check thread indices
        for (int i = 0; i < cpuid.Length; i++)
        {
            for (int j = 0; j < cpuid[i].Length; j++)
            {
                long index = cpuid[i][j].Thread;

                if (index >= _threadLoads.Length)
                {
                    break;
                }
            }
        }

        try
        {
            GetTimes(out _idleTimes, out _totalTimes);
        }
        catch (Exception)
        {
            _idleTimes = null;
            _totalTimes = null;
        }

        if (_idleTimes != null)
            IsAvailable = true;
    }

    private static bool GetTimes(out long[] idle, out long[] total)
    {
        idle = null;
        total = null;

        //Query processor idle information
        Interop.NtDll.SYSTEM_PROCESSOR_IDLE_INFORMATION[] idleInformation = new Interop.NtDll.SYSTEM_PROCESSOR_IDLE_INFORMATION[64];
        int idleSize = Marshal.SizeOf(typeof(Interop.NtDll.SYSTEM_PROCESSOR_IDLE_INFORMATION));
        if (Interop.NtDll.NtQuerySystemInformation(Interop.NtDll.SYSTEM_INFORMATION_CLASS.SystemProcessorIdleInformation, idleInformation, idleInformation.Length * idleSize, out int idleReturn) != 0)
        {
            return false;
        }

        //Query processor performance information
        Interop.NtDll.SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION[] perfInformation = new Interop.NtDll.SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION[64];
        int perfSize = Marshal.SizeOf(typeof(Interop.NtDll.SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION));
        if (Interop.NtDll.NtQuerySystemInformation(Interop.NtDll.SYSTEM_INFORMATION_CLASS.SystemProcessorPerformanceInformation, perfInformation, perfInformation.Length * perfSize, out int perfReturn) != 0)
        {
            return false;
        }

        idle = new long[idleReturn / idleSize];
        for (int i = 0; i < idle.Length; i++)
        {
            idle[i] = idleInformation[i].IdleTime;
        }

        total = new long[perfReturn / perfSize];
        for (int i = 0; i < total.Length; i++)
        {
            total[i] = perfInformation[i].KernelTime + perfInformation[i].UserTime;
        }

        return true;
    }

    public bool IsAvailable { get; }


    public double GetTotalLoad()
    {
        return _totalLoad;
    }

    public double GetThreadLoad(int thread)
    {
        double threadLoad = 0.0;
        if (thread < _threadLoads.Length)
        {
            threadLoad = _threadLoads[thread];
        }

        return threadLoad;
    }

    public void Update()
    {
        if (_idleTimes == null)
            return;

        try
        {
            if (!GetTimes(out long[] newIdleTimes, out long[] newTotalTimes))
                return;

            if (newIdleTimes.IsNullOrEmpty() || newTotalTimes.IsNullOrEmpty())
                return;

            for (int i = 0; i < Math.Min(newTotalTimes.Length, _totalTimes.Length); i++)
                if (newTotalTimes[i] - _totalTimes[i] < 100000)
                    return;

            double total = 0;
            _maxLoad = 0;
            int count = 0;
            for (int i = 0; i < _cpuid.Length; i++)
            {
                for (int j = 0; j < _cpuid[i].Length; j++)
                {
                    double value = 0.0;
                    long index = _cpuid[i][j].Thread;
                    if (index < newIdleTimes.Length && index < _totalTimes.Length)
                    {
                        double idle = 0.0;
                        if (index < newIdleTimes.Length && index < newTotalTimes.Length)
                        {
                            idle = (double)(newIdleTimes[index] - _idleTimes[index]) /
                                (newTotalTimes[index] - _totalTimes[index]);
                        }

                        value = idle;
                        total += idle;
                        count++;
                    }
                    value = 1.0 - value;
                    value = value < 0 ? 0 : value;

                    if (index < _threadLoads.Length)
                    {
                        _threadLoads[index] = value * 100;

                        if (_threadLoads[index] > _maxLoad)
                            _maxLoad = _threadLoads[index];
                    }
                }
            }

            if (count > 0)
            {
                total = 1.0 - total / (double)count;
                total = total < 0 ? 0 : total;
            }
            else
            {
                total = 0;
            }

            _totalLoad = total * 100.0;
            _totalTimes = newTotalTimes;
            _idleTimes = newIdleTimes;
        }
        catch
        {
            _totalLoad = 0.0;
            _totalTimes = new long[Environment.ProcessorCount]; ;
            _idleTimes = new long[Environment.ProcessorCount]; ;
        }
    }
}
