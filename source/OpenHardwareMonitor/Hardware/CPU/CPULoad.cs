// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.

using System;
using System.Runtime.InteropServices;

namespace OpenHardwareMonitor.Hardware.CPU
{
    internal class CPULoad
    {
        private readonly CPUID[][] cpuid;

        private long[] idleTimes;
        private long[] totalTimes;

        private float totalLoad;
        private readonly float[] threadLoads;
        private float maxLoad;

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

        public CPULoad(CPUID[][] cpuid)
        {
            this.cpuid = cpuid;
            this.threadLoads = new float[cpuid.Length * cpuid[0].Length];
            this.totalLoad = 0;
            try
            {
                GetTimes(out idleTimes, out totalTimes);
            }
            catch (Exception)
            {
                this.idleTimes = null;
                this.totalTimes = null;
            }
            if (idleTimes != null)
                IsAvailable = true;
        }

        public bool IsAvailable { get; set; }

        public float GetMaxLoad()
        {
            return this.maxLoad;
        }

        public float GetTotalLoad()
        {
            return totalLoad;
        }

        public float GetThreadLoad(int thread)
        {
            return threadLoads[thread];
        }

        public void Update()
        {
            if (this.idleTimes == null)
                return;

            if (!GetTimes(out long[] newIdleTimes, out long[] newTotalTimes))
                return;

            if (newIdleTimes == null || newTotalTimes == null)
                return;

            for (int i = 0; i < Math.Min(newTotalTimes.Length, totalTimes.Length); i++)
                if (newTotalTimes[i] - this.totalTimes[i] < 100000)
                    return;

            float total = 0;
            this.maxLoad = 0;
            int count = 0;
            for (int i = 0; i < cpuid.Length; i++)
            {
                for (int j = 0; j < cpuid[i].Length; j++)
                {
                    float value = 0;
                    long index = cpuid[i][j].Thread;
                    if (index < newIdleTimes.Length && index < totalTimes.Length)
                    {
                        float idle = 0f;
                        if (index < newIdleTimes.Length && index < newTotalTimes.Length)
                        {
                            idle = (float)(newIdleTimes[index] - this.idleTimes[index]) /
                            (float)(newTotalTimes[index] - this.totalTimes[index]);
                        }

                        value = idle;
                        total += idle;
                        count++;
                    }
                    value = 1.0f - value;
                    value = value < 0 ? 0 : value;
                    threadLoads[index] = value * 100;

                    if (threadLoads[index] > this.maxLoad)
                        this.maxLoad = threadLoads[index];
                }
            }

            if (count > 0)
            {
                total = 1.0f - total / count;
                total = total < 0 ? 0 : total;
            }
            else
            {
                total = 0;
            }

            this.totalLoad = total * 100;
            this.totalTimes = newTotalTimes;
            this.idleTimes = newIdleTimes;
        }
    }
}