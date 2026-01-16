using PmcReader.Interop;
using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace PmcReader.AMD
{
    public class Amd19hCpu : GenericMonitoringArea
    {
        public const uint HWCR = 0xC0010015;
        public const uint MSR_INSTR_RETIRED = 0xC00000E9;
        public const uint MSR_APERF_READONLY = 0xC00000E8;
        public const uint MSR_MPERF_READONLY = 0xC00000E7;
        public const uint MSR_TSC = 0x00000010;
        public const uint MSR_PERF_CTR_0 = 0xC0010201;
        public const uint MSR_PERF_CTR_1 = 0xC0010203;
        public const uint MSR_PERF_CTR_2 = 0xC0010205;
        public const uint MSR_PERF_CTR_3 = 0xC0010207;
        public const uint MSR_PERF_CTR_4 = 0xC0010209;
        public const uint MSR_PERF_CTR_5 = 0xC001020B;
        public const uint MSR_PERF_CTL_0 = 0xC0010200;
        public const uint MSR_PERF_CTL_1 = 0xC0010202;
        public const uint MSR_PERF_CTL_2 = 0xC0010204;
        public const uint MSR_PERF_CTL_3 = 0xC0010206;
        public const uint MSR_PERF_CTL_4 = 0xC0010208;
        public const uint MSR_PERF_CTL_5 = 0xC001020A;
        public const uint MSR_L3_PERF_CTL_0 = 0xC0010230;
        public const uint MSR_L3_PERF_CTL_1 = 0xC0010232;
        public const uint MSR_L3_PERF_CTL_2 = 0xC0010234;
        public const uint MSR_L3_PERF_CTL_3 = 0xC0010236;
        public const uint MSR_L3_PERF_CTL_4 = 0xC0010238;
        public const uint MSR_L3_PERF_CTL_5 = 0xC001023A;
        public const uint MSR_L3_PERF_CTR_0 = 0xC0010231;
        public const uint MSR_L3_PERF_CTR_1 = 0xC0010233;
        public const uint MSR_L3_PERF_CTR_2 = 0xC0010235;
        public const uint MSR_L3_PERF_CTR_3 = 0xC0010237;
        public const uint MSR_L3_PERF_CTR_4 = 0xC0010239;
        public const uint MSR_L3_PERF_CTR_5 = 0xC001023B;
        public const uint MSR_DF_PERF_CTL_0 = 0xC0010240;
        public const uint MSR_DF_PERF_CTL_1 = 0xC0010242;
        public const uint MSR_DF_PERF_CTL_2 = 0xC0010244;
        public const uint MSR_DF_PERF_CTL_3 = 0xC0010246;
        public const uint MSR_DF_PERF_CTR_0 = 0xC0010241;
        public const uint MSR_DF_PERF_CTR_1 = 0xC0010243;
        public const uint MSR_DF_PERF_CTR_2 = 0xC0010245;
        public const uint MSR_DF_PERF_CTR_3 = 0xC0010247;
        public const uint MSR_UMC_PERF_CTL_base = 0xC0010800;
        public const uint MSR_UMC_PERF_increment = 2;
        public const uint MSR_UMC_PERF_CTR_base = 0xC0010801;

        public const uint MSR_RAPL_PWR_UNIT = 0xC0010299;
        public const uint MSR_CORE_ENERGY_STAT = 0xC001029A;
        public const uint MSR_PKG_ENERGY_STAT = 0xC001029B;

        public const uint MSR_LS_CFG = 0xC0011020; // bit 4 = zen 1 lock errata 
        public const uint MSR_IC_CFG = 0xC0011021; // bit 5 = disable OC. 0x800 = disable IC sequential prefetch on Athlon
        public const uint MSR_DC_CFG = 0xC0011022; // data cache config? bit 16 = disable L1D stream prefetcher
        public const uint MSR_FP_CFG = 0xC0011028; // bit 4 = zen 1 FCMOV errata
        public const uint MSR_DE_CFG = 0xC0011029; // bit 13 = zen 1 stale store forward errata
        public const uint MSR_L2_PF_CFG = 0xC001102B; // bit 0 = enable L2 stream prefetcher
        public const uint MSR_SPEC_CTRL = 0x48; // bit 7 = disable predictive store forwarding
        public const uint MSR_PrefetchControl = 0xC0000108; 
        public const uint MSR_ProcNameStringBase = 0xC0010030;
        public const uint ProcNameStringMsrCount = 6;

        public NormalizedCoreCounterData[] NormalizedThreadCounts;
        public NormalizedCoreCounterData NormalizedTotalCounts;

        private ulong[] lastThreadAperf;
        private ulong[] lastThreadRetiredInstructions;
        private ulong[] lastThreadMperf;
        private ulong[] lastThreadTsc;
        private ulong[] lastThreadPwr;
        private ulong lastPkgPwr;
        private Stopwatch lastPkgPwrTime;

        private float energyStatusUnits;

        public Amd19hCpu()
        {
            architectureName = "AMD 19h Family";
            lastThreadAperf = new ulong[GetThreadCount()];
            lastThreadRetiredInstructions = new ulong[GetThreadCount()];
            lastThreadMperf = new ulong[GetThreadCount()];
            lastThreadTsc = new ulong[GetThreadCount()];
            lastThreadPwr = new ulong[GetThreadCount()];
            lastPkgPwr = 0;

            ulong raplPwrUnit;
            Ring0.ReadMsr(MSR_RAPL_PWR_UNIT, out raplPwrUnit);
            ulong energyUnits = (raplPwrUnit >> 8) & 0x1F; // bits 8-12 = energy status units
            energyStatusUnits = (float)Math.Pow(0.5, (double)energyUnits); // 1/2 ^ (value)
        }

        /// <summary>
        /// Get core perf ctl value
        /// </summary>
        /// <param name="perfEvent">Low 8 bits of performance event</param>
        /// <param name="umask">perf event umask</param>
        /// <param name="usr">count user events?</param>
        /// <param name="os">count os events?</param>
        /// <param name="edge">only increment on transition</param>
        /// <param name="interrupt">generate apic interrupt on overflow</param>
        /// <param name="enable">enable perf ctr</param>
        /// <param name="invert">invert cmask</param>
        /// <param name="cmask">0 = increment by event count. >0 = increment by 1 if event count in clock cycle >= cmask</param>
        /// <param name="perfEventHi">high 4 bits of performance event</param>
        /// <param name="guest">count guest events if virtualization enabled</param>
        /// <param name="host">count host events if virtualization enabled</param>
        /// <returns>value for perf ctl msr</returns>
        public static ulong GetPerfCtlValue(byte perfEvent, byte umask, bool usr, bool os, bool edge, bool interrupt, bool enable, bool invert, byte cmask, byte perfEventHi, bool guest, bool host)
        {
            return perfEvent |
                (ulong)umask << 8 |
                (usr ? 1UL : 0UL) << 16 |
                (os ? 1UL : 0UL) << 17 |
                (edge ? 1UL : 0UL) << 18 |
                (interrupt ? 1UL : 0UL) << 20 |
                (enable ? 1UL : 0UL) << 22 |
                (invert ? 1UL : 0UL) << 23 |
                (ulong)cmask << 24 |
                (ulong)perfEventHi << 32 |
                (guest ? 1UL : 0UL) << 40 |
                (host ? 1UL : 0UL) << 41;
        }

        /// <summary>
        /// Get L3 perf ctl value
        /// </summary>
        /// <param name="perfEvent">Event select</param>
        /// <param name="umask">unit mask</param>
        /// <param name="enable">enable perf counter</param>
        /// <param name="sliceMask">L3 slice select. bit 0 = slice 0, etc. 4 slices in ccx</param>
        /// <param name="threadMask">Thread select. bit 0 = c0t0, bit 1 = c0t1, bit 2 = c1t0, etc. Up to 8 threads in ccx</param>
        /// <returns>value to put in ChL3PmcCfg</returns>
        public static ulong GetL3PerfCtlValue(byte perfEvent, byte umask, bool enable, byte sliceMask, byte threadMask)
        {
            return perfEvent |
                (ulong)umask << 8 |
                (enable ? 1UL : 0UL) << 22 |
                (ulong)sliceMask << 48 |
                (ulong)threadMask << 56;
        }

        /// <summary>
        /// Gets L3 perf ctl value for 19h CPUs.
        /// About the only thing different for 19h (Zen 3) vs 17h (Zen 1/2)
        /// </summary>
        /// <param name="perfEvent">Event select</param>
        /// <param name="umask">unit mask</param>
        /// <param name="enable">enable perf counter</param>
        /// <param name="coreId">Core select</param>
        /// <param name="enableAllCores">Count for all cores</param>
        /// <param name="enableAllSlices">Count for all slices</param>
        /// <param name="sliceId">Slice select</param>
        /// <param name="threadMask">Which SMT thread to count for</param>
        /// <returns></returns>
        public static ulong Get19hL3PerfCtlValue(byte perfEvent, 
            byte umask, bool enable, byte coreId, bool enableAllCores, bool enableAllSlices, byte sliceId, byte threadMask)
        {
            return perfEvent |
                (ulong)umask << 8 |
                (enable ? 1UL : 0UL) << 22 |
                (ulong)(coreId & 0x7) << 42 |
                (enableAllSlices ? 1UL : 0UL) << 46 |
                (enableAllCores ? 1UL : 0UL) << 47 |
                (ulong)(sliceId & 0x7) << 48 |
                (ulong)threadMask << 56;
        }

        /// <summary>
        /// Gets L3 perf ctl value for 19h CPUs.
        /// About the only thing different for 19h (Zen 3) vs 17h (Zen 1/2)
        /// </summary>
        /// <param name="perfEvent">Event select</param>
        /// <param name="umask">unit mask</param>
        /// <param name="enable">enable perf counter</param>
        /// <param name="coreId">Core select</param>
        /// <param name="enableAllCores">Count for all cores</param>
        /// <param name="enableAllSources">Count for all slices</param>
        /// <param name="sourceId">For L3 PMC events, controls L3 slice. For Xi, controls CCX interface</param>
        /// <param name="threadMask">Which SMT thread to count for</param>
        /// <returns></returns>
        public static ulong Get1AhL3PerfCtlValue(byte perfEvent,
            byte umask, bool enable, byte coreId, bool enableAllCores, bool enableAllSources, byte sourceId, byte threadMask)
        {
            return perfEvent |
                (ulong)umask << 8 |
                (enable ? 1UL : 0UL) << 22 |
                (ulong)(coreId & 0xF) << 42 |
                (enableAllSources ? 1UL : 0UL) << 46 |
                (enableAllCores ? 1UL : 0UL) << 47 |
                (ulong)(sourceId & 0x7) << 48 |
                (ulong)threadMask << 56;
        }


        /// <summary>
        /// Get data fabric performance event select MSR value
        /// </summary>
        /// <param name="perfEventLow">Low 8 bits of performance event select</param>
        /// <param name="perfEventHi">Bits 8-13 of performance event (high 6 bits)</param>
        /// <param name="umaskLow">Low 8 bits of unit mask</param>
        /// <param name="umaskHi">Bits 8-11 of unit mask (high 4 bits)</param>
        /// <param name="enable">enable perf counter</param>
        /// <returns>value to put in DF_PERF_CTL</returns>
        public static ulong GetDFPerfCtlValue(byte perfEventLow, byte perfEventHi, byte umaskLow, byte umaskHi, bool enable)
        {
            return perfEventLow |
                (ulong)umaskLow << 8 |
                (enable ? 1UL : 0UL) << 22 |
                (ulong)umaskHi << 24 |
                (ulong)perfEventHi << 32;
        }

        /// <summary>
        /// Get DF bandwidth event select MSR value
        /// </summary>
        /// <param name="instanceId">0-11: CS (UMC), 12-15: CS (CXL), 0x10-0x17: CCM0-7, 0x35-0x3A: LINK</param>
        /// <param name="read">true = count read, false = count write</param>
        /// <returns>perf ctl value</returns>
        public static ulong GetDFBandwidthPerfCtlValue(byte instanceId, bool read)
        {
            // event is split 0:7, 32:38, for 14 bits total
            // bits 0:5 = event encoding, only DATA_BW (0x1F) is documented
            ulong ctlValue = 0x1F;

            // 6:13 = instance id. first two bits fit in 0:7 low section
            ulong instanceIdLo = (ulong)instanceId & 3;
            ulong instanceIdHi = (ulong)(instanceId >> 2);
            ctlValue |= instanceIdLo << 6;
            ctlValue |= instanceIdHi << 32;

            // unit mask is split 8:15, 24:27
            ulong umask = read ? 0UL : 1; // read request = 0, write request = 1
            umask |= 0x1FF << 1; // 9 reserved bits
            umask |= 1 << 10; // 1 = same node, 2 = remote node, 3 = count all (src = same or remote die)
            ctlValue |= (umask & 0xFF) << 8; // low bits
            ctlValue |= (umask >> 8) << 24; // high 4 bits

            ctlValue |= 1UL << 22; // enable
            return ctlValue;
        }

        /// <summary>
        /// Get UMC perf counter control value
        /// </summary>
        /// <param name="perfEvent">Event select</param>
        /// <param name="maskReads">Don't count reads</param>
        /// <param name="maskWrites">Don't count writes</param>
        /// <returns>Value to put in UMC_PERF_CTL</returns>
        public static ulong GetUmcPerfCtlValue(byte perfEvent, bool maskReads, bool maskWrites)
        {
            return perfEvent |
                (maskReads ? 2UL << 8 : 0) |
                (maskWrites ? 1UL << 8 : 0) |
                1UL << 31;
        }

        /// <summary>
        /// Enable fixed instructions retired counter on Zen
        /// </summary>
        public void EnablePerformanceCounters()
        {
            for (int threadIdx = 0; threadIdx < GetThreadCount(); threadIdx++)
            {
                // Enable instructions retired counter
                ThreadAffinity.Set(1UL << threadIdx);
                ulong hwcrValue;
                Ring0.ReadMsr(HWCR, out hwcrValue);
                hwcrValue |= 1UL << 30;
                Ring0.WriteMsr(HWCR, hwcrValue);

                // Initialize fixed counter values
                Ring0.ReadMsr(MSR_APERF_READONLY, out lastThreadAperf[threadIdx]);
                Ring0.ReadMsr(MSR_INSTR_RETIRED, out lastThreadRetiredInstructions[threadIdx]);
                Ring0.ReadMsr(MSR_TSC, out lastThreadTsc[threadIdx]);
                Ring0.ReadMsr(MSR_MPERF_READONLY, out lastThreadMperf[threadIdx]);
            }
        }

        public void ProgramPerfCounters(ulong ctr0, ulong ctr1, ulong ctr2, ulong ctr3, ulong ctr4, ulong ctr5)
        {
            for (int threadIdx = 0; threadIdx < this.GetThreadCount(); threadIdx++)
            {
                ThreadAffinity.Set(1UL << threadIdx);
                ulong hwcrValue;
                Ring0.ReadMsr(HWCR, out hwcrValue);
                hwcrValue |= 1UL << 30; // instructions retired counter
                hwcrValue |= 1UL << 31; // enable UMC counters
                Ring0.WriteMsr(HWCR, hwcrValue);

                // Initialize fixed counter values
                Ring0.ReadMsr(MSR_APERF_READONLY, out lastThreadAperf[threadIdx]);
                Ring0.ReadMsr(MSR_INSTR_RETIRED, out lastThreadRetiredInstructions[threadIdx]);
                Ring0.ReadMsr(MSR_TSC, out lastThreadTsc[threadIdx]);
                Ring0.ReadMsr(MSR_MPERF_READONLY, out lastThreadMperf[threadIdx]);

                Ring0.WriteMsr(MSR_PERF_CTL_0, ctr0);
                Ring0.WriteMsr(MSR_PERF_CTL_1, ctr1);
                Ring0.WriteMsr(MSR_PERF_CTL_2, ctr2);
                Ring0.WriteMsr(MSR_PERF_CTL_3, ctr3);
                Ring0.WriteMsr(MSR_PERF_CTL_4, ctr4);
                Ring0.WriteMsr(MSR_PERF_CTL_5, ctr5);
            }
        }

        /// <summary>
        /// Get a thread's LLC/CCX ID 
        /// </summary>
        /// <param name="threadId">thread ID</param>
        /// <returns>CCX ID</returns>
        public int GetCcxId(int threadId)
        {
            // linux arch/x86/kernel/cpu/cacheinfo.c:666 does this and it seems to work?
            /*uint extendedApicId, ecx, edx, ebx;
            OpCode.CpuidTx(0x8000001E, 0, out extendedApicId, out ebx, out ecx, out edx, 1UL << threadId);
            return (int)(extendedApicId >> 3);*/

            // this is a hack. windows numbers cores/threads like (0,1) = core 1, (2,3) = core 2, etc
            if (coreCount * 2 == threadCount) return threadId / 8;
            else return threadId / 4;
        }

        /// <summary>
        /// Get thread CCX ID
        /// </summary>
        /// <param name="threadId"></param>
        /// <returns></returns>
        public int Get19hCcxId(int threadId)
        {
            // placeholder until I figure this out. Again just how windows assigns thread IDs
            if (coreCount * 2 == threadCount) return threadId / 16;
            else return threadId / 8;
        }

        public int Get1AhCcxId(int threadId)
        {
            // placeholder until I figure this out. only works on the 9900X
            if (coreCount * 2 == threadCount) return threadId / 12;
            else return threadId / 6;
        }

        public void GetUmcPerfmonInfo(out uint umcCount, out uint umcPerfcounterCount)
        {
            OpCode.CpuidTx(0x80000022, 0, out uint eax, out uint ebx, out uint ecx, out uint edx, 1);
            umcPerfcounterCount = (ebx >> 16) & 0x3F;

            umcCount = 0;
            for (int i = 0; i < 31; i++)
            {
                if ((ecx & (1UL << i)) > 0)
                {
                    umcCount++;
                }
            }
        }

        /// <summary>
        /// Update fixed counters for thread, affinity must be set going in
        /// </summary>
        /// <param name="threadIdx">thread to update fixed counters for</param>
        public void ReadFixedCounters(int threadIdx, out ulong elapsedAperf, out ulong elapsedInstr, out ulong elapsedTsc, out ulong elapsedMperf)
        {
            ulong aperf, instr, tsc, mperf;
            Ring0.ReadMsr(MSR_APERF_READONLY, out aperf);
            Ring0.ReadMsr(MSR_INSTR_RETIRED, out instr);
            Ring0.ReadMsr(MSR_TSC, out tsc);
            Ring0.ReadMsr(MSR_MPERF_READONLY, out mperf);

            elapsedAperf = aperf;
            elapsedInstr = instr;
            elapsedTsc = tsc;
            elapsedMperf = mperf;
            if (instr > lastThreadRetiredInstructions[threadIdx])
                elapsedInstr = instr - lastThreadRetiredInstructions[threadIdx];
            else if (lastThreadRetiredInstructions[threadIdx] > 0)
                elapsedInstr = instr + (0xFFFFFFFFFFFFFFFF - lastThreadRetiredInstructions[threadIdx]);
            if (aperf > lastThreadAperf[threadIdx])
                elapsedAperf = aperf - lastThreadAperf[threadIdx];
            else if (lastThreadAperf[threadIdx] > 0)
                elapsedAperf = aperf + (0xFFFFFFFFFFFFFFFF - lastThreadAperf[threadIdx]);
            if (mperf > lastThreadMperf[threadIdx])
                elapsedMperf = mperf - lastThreadMperf[threadIdx];
            else if (lastThreadMperf[threadIdx] > 0)
                elapsedMperf = mperf + (0xFFFFFFFFFFFFFFFF - lastThreadMperf[threadIdx]);
            if (tsc > lastThreadTsc[threadIdx])
                elapsedTsc = tsc - lastThreadTsc[threadIdx];
            else if (lastThreadTsc[threadIdx] > 0)
                elapsedTsc = tsc + (0xFFFFFFFFFFFFFFFF - lastThreadTsc[threadIdx]);

            lastThreadAperf[threadIdx] = aperf;
            lastThreadMperf[threadIdx] = mperf;
            lastThreadTsc[threadIdx] = tsc;
            lastThreadRetiredInstructions[threadIdx] = instr;
        }

        /// <summary>
        /// Read core energy consumed counter. Affinity must be set going in
        /// </summary>
        /// <param name="threadIdx">thread</param>
        /// <param name="joulesConsumed">energy consumed</param>
        public void ReadCorePowerCounter(int threadIdx, out float joulesConsumed)
        {
            ulong coreEnergyStat, elapsedEnergyStat;
            Ring0.ReadMsr(MSR_CORE_ENERGY_STAT, out coreEnergyStat);
            coreEnergyStat &= 0xFFFFFFFF; // bits 0-31 = total energy consumed. other bits reserved

            elapsedEnergyStat = coreEnergyStat;
            if (lastThreadPwr[threadIdx] < coreEnergyStat) elapsedEnergyStat = coreEnergyStat - lastThreadPwr[threadIdx];
            lastThreadPwr[threadIdx] = coreEnergyStat;
            joulesConsumed = elapsedEnergyStat * energyStatusUnits;
        }

        /// <summary>
        /// Read package energy consumed counter
        /// </summary>
        /// <returns>Watts consumed</returns>
        public float ReadPackagePowerCounter()
        {
            ulong pkgEnergyStat, elapsedEnergyStat;
            float normalizationFactor = 1;
            if (lastPkgPwrTime == null)
            {
                lastPkgPwrTime = new Stopwatch();
                lastPkgPwrTime.Start();
            }
            else
            {
                lastPkgPwrTime.Stop();
                normalizationFactor = 1000 / (float)lastPkgPwrTime.ElapsedMilliseconds;
                lastPkgPwrTime.Restart();
            }

            Ring0.ReadMsr(MSR_PKG_ENERGY_STAT, out pkgEnergyStat);
            elapsedEnergyStat = pkgEnergyStat;
            if (lastPkgPwr < pkgEnergyStat) elapsedEnergyStat = pkgEnergyStat - lastPkgPwr;
            lastPkgPwr = pkgEnergyStat;

            float watts = elapsedEnergyStat * energyStatusUnits * normalizationFactor;
            if (NormalizedTotalCounts != null)
            {
                NormalizedTotalCounts.watts = watts;
            }

            return watts;
        }

        /// <summary>
        /// initialize/reset accumulated totals for core counter data
        /// </summary>
        public void InitializeCoreTotals()
        {
            if (NormalizedTotalCounts == null)
            {
                NormalizedTotalCounts = new NormalizedCoreCounterData();
            }

            NormalizedTotalCounts.aperf = 0;
            NormalizedTotalCounts.mperf = 0;
            NormalizedTotalCounts.tsc = 0;
            NormalizedTotalCounts.instr = 0;
            NormalizedTotalCounts.ctr0 = 0;
            NormalizedTotalCounts.ctr1 = 0;
            NormalizedTotalCounts.ctr2 = 0;
            NormalizedTotalCounts.ctr3 = 0;
            NormalizedTotalCounts.ctr4 = 0;
            NormalizedTotalCounts.ctr5 = 0;
            NormalizedTotalCounts.watts = 0;
            NormalizedTotalCounts.totalCoreWatts = 0;
        }

        /// <summary>
        /// Update counter values for thread, and add to totals
        /// Will set thread affinity
        /// </summary>
        /// <param name="threadIdx">thread in question</param>
        public void UpdateThreadCoreCounterData(int threadIdx)
        {
            ThreadAffinity.Set(1UL << threadIdx);
            float normalizationFactor = GetNormalizationFactor(threadIdx);
            float joules;
            ulong aperf, mperf, tsc, instr;
            ulong ctr0, ctr1, ctr2, ctr3, ctr4, ctr5;
            ReadFixedCounters(threadIdx, out aperf, out instr, out tsc, out mperf);
            ctr0 = ReadAndClearMsr(MSR_PERF_CTR_0);
            ctr1 = ReadAndClearMsr(MSR_PERF_CTR_1);
            ctr2 = ReadAndClearMsr(MSR_PERF_CTR_2);
            ctr3 = ReadAndClearMsr(MSR_PERF_CTR_3);
            ctr4 = ReadAndClearMsr(MSR_PERF_CTR_4);
            ctr5 = ReadAndClearMsr(MSR_PERF_CTR_5);
            ReadCorePowerCounter(threadIdx, out joules);

            if (NormalizedThreadCounts == null)
            {
                NormalizedThreadCounts = new NormalizedCoreCounterData[threadCount];
            }

            if (NormalizedThreadCounts[threadIdx] == null)
            {
                NormalizedThreadCounts[threadIdx] = new NormalizedCoreCounterData();
            }

            if (NormalizedThreadCounts[threadIdx].NormalizationFactor != 0.0f)
            {
                NormalizedThreadCounts[threadIdx].totalInstructions += instr;
                NormalizedThreadCounts[threadIdx].totalctr0 += ctr0;
                NormalizedThreadCounts[threadIdx].totalctr1 += ctr1;
                NormalizedThreadCounts[threadIdx].totalctr2 += ctr2;
                NormalizedThreadCounts[threadIdx].totalctr3 += ctr3;
                NormalizedThreadCounts[threadIdx].totalctr4 += ctr4;
                NormalizedThreadCounts[threadIdx].totalctr5 += ctr5;
                NormalizedTotalCounts.totalInstructions += instr;
                NormalizedTotalCounts.totalctr0 += ctr0;
                NormalizedTotalCounts.totalctr1 += ctr1;
                NormalizedTotalCounts.totalctr2 += ctr2;
                NormalizedTotalCounts.totalctr3 += ctr3;
                NormalizedTotalCounts.totalctr4 += ctr4;
                NormalizedTotalCounts.totalctr5 += ctr5;
            }

            NormalizedThreadCounts[threadIdx].aperf = aperf * normalizationFactor;
            NormalizedThreadCounts[threadIdx].mperf = mperf * normalizationFactor;
            NormalizedThreadCounts[threadIdx].instr = instr * normalizationFactor;
            NormalizedThreadCounts[threadIdx].tsc = tsc * normalizationFactor;
            NormalizedThreadCounts[threadIdx].ctr0 = ctr0 * normalizationFactor;
            NormalizedThreadCounts[threadIdx].ctr1 = ctr1 * normalizationFactor;
            NormalizedThreadCounts[threadIdx].ctr2 = ctr2 * normalizationFactor;
            NormalizedThreadCounts[threadIdx].ctr3 = ctr3 * normalizationFactor;
            NormalizedThreadCounts[threadIdx].ctr4 = ctr4 * normalizationFactor;
            NormalizedThreadCounts[threadIdx].ctr5 = ctr5 * normalizationFactor;
            NormalizedThreadCounts[threadIdx].watts = joules * normalizationFactor;
            NormalizedThreadCounts[threadIdx].NormalizationFactor = normalizationFactor;
            NormalizedTotalCounts.aperf += NormalizedThreadCounts[threadIdx].aperf;
            NormalizedTotalCounts.mperf += NormalizedThreadCounts[threadIdx].mperf;
            NormalizedTotalCounts.instr += NormalizedThreadCounts[threadIdx].instr;
            NormalizedTotalCounts.tsc += NormalizedThreadCounts[threadIdx].tsc;
            NormalizedTotalCounts.ctr0 += NormalizedThreadCounts[threadIdx].ctr0;
            NormalizedTotalCounts.ctr1 += NormalizedThreadCounts[threadIdx].ctr1;
            NormalizedTotalCounts.ctr2 += NormalizedThreadCounts[threadIdx].ctr2;
            NormalizedTotalCounts.ctr3 += NormalizedThreadCounts[threadIdx].ctr3;
            NormalizedTotalCounts.ctr4 += NormalizedThreadCounts[threadIdx].ctr4;
            NormalizedTotalCounts.ctr5 += NormalizedThreadCounts[threadIdx].ctr5;

            // only add core power once per core. don't count it per-SMT thread
            // and always add if SMT is off (thread count == core count)
            if (threadCount == coreCount || (threadCount == coreCount * 2 && threadIdx % 2 == 0))
            {
                NormalizedTotalCounts.totalCoreWatts += NormalizedThreadCounts[threadIdx].watts;
            } 
        }

        /// <summary>
        /// Assemble overall counter values into a Tuple of string, float array.
        /// </summary>
        /// <param name="ctr0">Description for counter 0 value</param>
        /// <param name="ctr1">Description for counter 1 value</param>
        /// <param name="ctr2">Description for counter 2 value</param>
        /// <param name="ctr3">Description for counter 3 value</param>
        /// <param name="ctr4">Description for counter 4 value</param>
        /// <param name="ctr5">Description for counter 5 value</param>
        /// <returns>Array to put in results object</returns>
        public Tuple<string, float>[] GetOverallCounterValues(string ctr0, string ctr1, string ctr2, string ctr3, string ctr4, string ctr5)
        {
            NormalizedCoreCounterData dataToLog = this.NormalizedTotalCounts;
            if (this.targetLogCoreIndex >= 0)
            {
                dataToLog = NormalizedThreadCounts[this.targetLogCoreIndex];
            }

            Tuple<string, float>[] retval = new Tuple<string, float>[12];
            retval[0] = new Tuple<string, float>("APERF", dataToLog.aperf);
            retval[1] = new Tuple<string, float>("MPERF", dataToLog.mperf);
            retval[2] = new Tuple<string, float>("TSC", dataToLog.tsc);
            retval[3] = new Tuple<string, float>("IRPerfCount", dataToLog.instr);
            retval[4] = new Tuple<string, float>("Watts", dataToLog.watts);
            retval[5] = new Tuple<string, float>("CoreWatts", dataToLog.totalCoreWatts);
            retval[6] = new Tuple<string, float>(ctr0, dataToLog.ctr0);
            retval[7] = new Tuple<string, float>(ctr1, dataToLog.ctr1);
            retval[8] = new Tuple<string, float>(ctr2, dataToLog.ctr2);
            retval[9] = new Tuple<string, float>(ctr3, dataToLog.ctr3);
            retval[10] = new Tuple<string, float>(ctr4, dataToLog.ctr4);
            retval[11] = new Tuple<string, float>(ctr5, dataToLog.ctr5);
            return retval;
        }

        public Label errorLabel; // ugly whatever
        private TextBox procNameTextBox;

        public override void InitializeCrazyControls(FlowLayoutPanel flowLayoutPanel, Label errLabel)
        {
            flowLayoutPanel.Controls.Clear();
            CheckBox opCacheCheckbox = new CheckBox();
            opCacheCheckbox.Text = "Op Cache";
            opCacheCheckbox.Checked = GetOpCacheEnabledStatus();
            opCacheCheckbox.CheckedChanged += HandleOpCacheCheckbox;
            opCacheCheckbox.Width = TextRenderer.MeasureText(opCacheCheckbox.Text, opCacheCheckbox.Font).Width + 20;
            flowLayoutPanel.Controls.Add(opCacheCheckbox);

            CheckBox boostCheckbox = new CheckBox();
            boostCheckbox.Text = "Core Performance Boost";
            boostCheckbox.Checked = GetCpbEnabled();
            boostCheckbox.CheckedChanged += HandleCorePerformanceBoostCheckbox;
            boostCheckbox.Width = TextRenderer.MeasureText(boostCheckbox.Text, boostCheckbox.Font).Width + 20;
            flowLayoutPanel.Controls.Add(boostCheckbox);
            //flowLayoutPanel.SetFlowBreak(boostCheckbox, true);

            CheckBox psfCheckbox = new CheckBox();
            psfCheckbox.Text = "Predictive STLF";
            psfCheckbox.Checked = GetPSFEnabled();
            psfCheckbox.CheckedChanged += HandlePsfCheckbox;
            psfCheckbox.Width = TextRenderer.MeasureText(psfCheckbox.Text, psfCheckbox.Font).Width + 20;
            flowLayoutPanel.Controls.Add(psfCheckbox);
            flowLayoutPanel.SetFlowBreak(psfCheckbox, true);

            CheckBox l1dStreamPrefetchCheckbox = new CheckBox();
            l1dStreamPrefetchCheckbox.Text = "L1D Stream Pf";
            l1dStreamPrefetchCheckbox.Checked = GetPrefetcherStatus(Zen4Prefetcher.L1Stream);
            l1dStreamPrefetchCheckbox.CheckedChanged += HandleL1dStreamCheckbox;
            l1dStreamPrefetchCheckbox.Width = TextRenderer.MeasureText(l1dStreamPrefetchCheckbox.Text, l1dStreamPrefetchCheckbox.Font).Width + 20;
            flowLayoutPanel.Controls.Add(l1dStreamPrefetchCheckbox);

            CheckBox l1dStridePrefetchCheckbox = new CheckBox();
            l1dStridePrefetchCheckbox.Text = "L1D Stride Pf";
            l1dStridePrefetchCheckbox.Checked = GetPrefetcherStatus(Zen4Prefetcher.L1Stride);
            l1dStridePrefetchCheckbox.CheckedChanged += HandleL1dStrideCheckbox;
            l1dStridePrefetchCheckbox.Width = TextRenderer.MeasureText(l1dStridePrefetchCheckbox.Text, l1dStridePrefetchCheckbox.Font).Width + 20;
            flowLayoutPanel.Controls.Add(l1dStridePrefetchCheckbox);

            CheckBox l1dRegionPrefetchCheckbox = new CheckBox();
            l1dRegionPrefetchCheckbox.Text = "L1D Region Pf";
            l1dRegionPrefetchCheckbox.Checked = GetPrefetcherStatus(Zen4Prefetcher.L1Region);
            l1dRegionPrefetchCheckbox.CheckedChanged += HandleL1dRegionCheckbox;
            l1dRegionPrefetchCheckbox.Width = TextRenderer.MeasureText(l1dRegionPrefetchCheckbox.Text, l1dRegionPrefetchCheckbox.Font).Width + 20;
            flowLayoutPanel.Controls.Add(l1dRegionPrefetchCheckbox);

            CheckBox l2StreamPrefetchCheckbox = new CheckBox();
            l2StreamPrefetchCheckbox.Text = "L2 Stream Prefetcher";
            l2StreamPrefetchCheckbox.Checked = GetPrefetcherStatus(Zen4Prefetcher.L2Stream);
            l2StreamPrefetchCheckbox.CheckedChanged += HandleL2StreamCheckbox;
            l2StreamPrefetchCheckbox.Width = TextRenderer.MeasureText(l2StreamPrefetchCheckbox.Text, l2StreamPrefetchCheckbox.Font).Width + 20;
            flowLayoutPanel.Controls.Add(l2StreamPrefetchCheckbox);

            CheckBox l2UpDownPrefetchCheckbox = new CheckBox();
            l2UpDownPrefetchCheckbox.Text = "L2 Up/Down Pf";
            l2UpDownPrefetchCheckbox.Checked = GetPrefetcherStatus(Zen4Prefetcher.L2UpDown);
            l2UpDownPrefetchCheckbox.CheckedChanged += HandleL2UpDownCheckbox;
            l2UpDownPrefetchCheckbox.Width = TextRenderer.MeasureText(l2UpDownPrefetchCheckbox.Text, l2UpDownPrefetchCheckbox.Font).Width + 20;
            flowLayoutPanel.Controls.Add(l2UpDownPrefetchCheckbox);

            /*Button setProcNameButton = CreateButton("Set CPU Name String", SetCpuNameString);
            procNameTextBox = new TextBox();
            procNameTextBox.Width = 325;
            procNameTextBox.MaxLength = 47;
            flowLayoutPanel.Controls.Add(procNameTextBox);
            flowLayoutPanel.Controls.Add(setProcNameButton);*/

            errorLabel = errLabel;
        }

        private void HandleL2StreamCheckbox(object sender, EventArgs e)
        {
            CheckBox checkbox = (CheckBox)sender;
            bool L2PfStatus = HandlePrefetcherCheckbox(Zen4Prefetcher.L2Stream, checkbox.Checked);
            if (L2PfStatus) errorLabel.Text = "L2 Stream Prefetcher enabled";
            else errorLabel.Text = "L2 Stream Prefetcher disabled";
        }

        private void HandleL2UpDownCheckbox(object sender, EventArgs e)
        {
            CheckBox checkbox = (CheckBox)sender;
            bool L2PfStatus = HandlePrefetcherCheckbox(Zen4Prefetcher.L2UpDown, checkbox.Checked);
            if (L2PfStatus) errorLabel.Text = "L2 Up/Down Prefetcher enabled";
            else errorLabel.Text = "L2 Up/Down Prefetcher disabled";
        }

        private void HandleL1dStreamCheckbox(object sender, EventArgs e)
        {
            CheckBox checkbox = (CheckBox)sender;
            bool L1DPfStatus = HandlePrefetcherCheckbox(Zen4Prefetcher.L1Stream, checkbox.Checked);
            if (L1DPfStatus) errorLabel.Text = "L1D Stream Prefetcher enabled";
            else errorLabel.Text = "L1D Stream Prefetcher disabled";
        }

        private void HandleL1dStrideCheckbox(object sender, EventArgs e)
        {
            CheckBox checkbox = (CheckBox)sender;
            bool L1DPfStatus = HandlePrefetcherCheckbox(Zen4Prefetcher.L1Stride, checkbox.Checked);
            if (L1DPfStatus) errorLabel.Text = "L1D Stride Prefetcher enabled";
            else errorLabel.Text = "L1D Stride Prefetcher disabled";
        }

        private void HandleL1dRegionCheckbox(object sender, EventArgs e)
        {
            CheckBox checkbox = (CheckBox)sender;
            bool L1DPfStatus = HandlePrefetcherCheckbox(Zen4Prefetcher.L1Region, checkbox.Checked);
            if (L1DPfStatus) errorLabel.Text = "L1D Region Prefetcher enabled";
            else errorLabel.Text = "L1D Region Prefetcher disabled";
        }

        private bool HandlePrefetcherCheckbox(Zen4Prefetcher pf, bool enabled)
        {
            bool prefetcherEnabled = enabled;
            for (int threadIdx = 0; threadIdx < GetThreadCount(); threadIdx++)
            {
                SetPrefetcherStatus(Zen4Prefetcher.L1Stream, enabled);
                bool threadStatus = GetPrefetcherStatus(Zen4Prefetcher.L1Stream);
                if (enabled) prefetcherEnabled &= threadStatus;
                else prefetcherEnabled |= threadStatus;
            }

            return prefetcherEnabled;
        }

        private enum Zen4Prefetcher
        {
            L1Stream,
            L1Stride,
            L1Region,
            L2Stream,
            L2UpDown
        }

        private void SetPrefetcherStatus(Zen4Prefetcher pf, bool enabled)
        {
            ulong controlBit = 0;
            if (pf == Zen4Prefetcher.L1Stream) controlBit = 1;
            else if (pf == Zen4Prefetcher.L1Stride) controlBit = 2;
            else if (pf == Zen4Prefetcher.L1Region) controlBit = 4;
            else if (pf == Zen4Prefetcher.L2Stream) controlBit = 8;
            else if (pf == Zen4Prefetcher.L2UpDown) controlBit = (1UL << 5);

            Ring0.ReadMsr(MSR_PrefetchControl, out ulong prefetchControl);

            // set bit to disable prefetcher, clear it to enable
            if (!enabled) prefetchControl |= controlBit;
            else prefetchControl &= ~controlBit;
            Ring0.WriteMsr(MSR_PrefetchControl, prefetchControl);
        }

        private bool GetPrefetcherStatus(Zen4Prefetcher pf)
        {
            ulong controlBit = 0;
            if (pf == Zen4Prefetcher.L1Stream) controlBit = 1;
            else if (pf == Zen4Prefetcher.L1Stride) controlBit = 2;
            else if (pf == Zen4Prefetcher.L1Region) controlBit = 4;
            else if (pf == Zen4Prefetcher.L2Stream) controlBit = 8;
            else if (pf == Zen4Prefetcher.L2UpDown) controlBit = (1UL << 5);

            Ring0.ReadMsr(MSR_PrefetchControl, out ulong prefetchControl);

            // Setting the MSR bit disables the prefetcher
            return !((prefetchControl & controlBit) > 0);
        }

        public bool GetPSFEnabled()
        {
            Ring0.ReadMsr(MSR_SPEC_CTRL, out ulong specCtrl);
            return !((specCtrl & (1UL << 7)) > 0); // bit 7 = disable predictive store forwarding
        }

        public void SetPsf(bool enabled)
        {
            Ring0.ReadMsr(MSR_SPEC_CTRL, out ulong specCtrl);
            if (!enabled) specCtrl |= (1UL << 7);
            else specCtrl &= ~(1UL << 7);
            Ring0.WriteMsr(MSR_SPEC_CTRL, specCtrl);
        }

        public void HandlePsfCheckbox(object sender, EventArgs e)
        {
            CheckBox checkbox = (CheckBox)sender;
            bool psfEnabled = checkbox.Checked;
            for (int threadIdx = 0; threadIdx < GetThreadCount(); threadIdx++)
            {
                SetPsf(checkbox.Checked);
                bool threadStatus = GetPSFEnabled();
                if (checkbox.Checked) psfEnabled &= threadStatus;
                else psfEnabled |= threadStatus;
            }

            if (psfEnabled) errorLabel.Text = "Predictive store forwarding enabled";
            else errorLabel.Text = "Predictive store forwarding disabled";
            checkbox.Checked = psfEnabled;
        }

        private bool GetOpCacheEnabledStatus()
        {
            Ring0.ReadMsr(MSR_IC_CFG, out ulong icCfg);
            return (icCfg & (1UL << 5)) == 0;
        }

        public void HandleOpCacheCheckbox(object sender, EventArgs e)
        {
            CheckBox checkbox = (CheckBox)sender;
            if (checkbox.Checked) EnableOpCache();
            else DisableOpCache();
        }

        /// <summary>
        /// Disable the op cache
        /// </summary>
        public void DisableOpCache()
        {
            bool allOpCachesDisabled = true;
            for (int threadIdx = 0; threadIdx < GetThreadCount(); threadIdx++)
            {
                ThreadAffinity.Set(1UL << threadIdx);
                Ring0.ReadMsr(MSR_IC_CFG, out ulong icCfg);
                icCfg |= (1UL << 5);
                Ring0.WriteMsr(MSR_IC_CFG, icCfg);
                allOpCachesDisabled &= !GetOpCacheEnabledStatus();
            }

            if (!allOpCachesDisabled)
            {
                errorLabel.Text = "Failed to disable op caches";
            }
            else
            {
                errorLabel.Text = "Op caches disabled";
            }
        }

        /// <summary>
        /// Enable the op cache
        /// </summary>
        public void EnableOpCache()
        {
            bool allOpCachesEnabled = true;
            for (int threadIdx = 0; threadIdx < GetThreadCount(); threadIdx++)
            {
                ThreadAffinity.Set(1UL << threadIdx);
                Ring0.ReadMsr(MSR_IC_CFG, out ulong icCfg);
                icCfg &= ~(1UL << 5);
                Ring0.WriteMsr(MSR_IC_CFG, icCfg);
                allOpCachesEnabled &= GetOpCacheEnabledStatus();
            }

            if (!allOpCachesEnabled)
            {
                errorLabel.Text = "Failed to enable op caches";
            }
            else
            {
                errorLabel.Text = "Op caches enabled";
            }
        }

        public void HandleCorePerformanceBoostCheckbox(object sender, EventArgs e)
        {
            CheckBox checkbox = (CheckBox)sender;
            if (checkbox.Checked) EnableCorePerformanceBoost();
            else DisableCorePerformanceBoost();
        }

        private bool GetCpbEnabled()
        {
            Ring0.ReadMsr(HWCR, out ulong hwcr);
            return (hwcr & (1UL << 25)) == 0;
        }

        public void EnableCorePerformanceBoost()
        {
            bool allCpbEnabled = true;
            for (int threadIdx = 0; threadIdx < GetThreadCount(); threadIdx++)
            {
                ThreadAffinity.Set(1UL << threadIdx);
                Ring0.ReadMsr(HWCR, out ulong hwcr);
                hwcr &= ~(1UL << 25);
                Ring0.WriteMsr(HWCR, hwcr);
                allCpbEnabled &= GetCpbEnabled();
            }

            if (!allCpbEnabled)
            {
                errorLabel.Text = "Failed to enable Core Performance Boost";
            }
            else
            {
                errorLabel.Text = "Core Performance Boost enabled";
            }
        }

        public void DisableCorePerformanceBoost()
        {
            bool allCpbDisabled = true;
            for (int threadIdx = 0; threadIdx < GetThreadCount(); threadIdx++)
            {
                ThreadAffinity.Set(1UL << threadIdx);
                Ring0.ReadMsr(HWCR, out ulong hwcr);
                hwcr |= (1UL << 25);
                Ring0.WriteMsr(HWCR, hwcr);
                allCpbDisabled &= !GetCpbEnabled();
            }

            if (!allCpbDisabled)
            {
                errorLabel.Text = "Failed to disable Core Performance Boost";
            }
            else
            {
                errorLabel.Text = "Core Performance Boost disabled";
            }
        }

        /// <summary>
        /// name better be in ascii
        /// </summary>
        public void SetCpuNameString(object sender, EventArgs e)
        {
            if (procNameTextBox == null) return;
            char[] nameArr = procNameTextBox.Text.ToCharArray();
            for (int threadIdx = 0; threadIdx < GetThreadCount(); threadIdx++)
            {
                ThreadAffinity.Set(1UL << threadIdx);
                for (int blockIdx = 0; blockIdx < ProcNameStringMsrCount; blockIdx++)
                {
                    ulong blockValue = 0;
                    for (int charIdx = blockIdx * 8, i = 0; charIdx < nameArr.Length && charIdx < 47 && i < 8; charIdx++, i++)
                    {
                        ulong charValue = nameArr[charIdx];
                        blockValue |= charValue << (8 * i);
                    }

                    Ring0.WriteMsr(MSR_ProcNameStringBase + (uint)blockIdx, blockValue);
                }
            }
        }

        /// <summary>
        /// Holds performance counter, read out from the three fixed counters
        /// and four programmable ones
        /// </summary>
        public class NormalizedCoreCounterData
        {
            /// <summary>
            /// Actual performance frequency clock count
            /// Counts actual number of C0 cycles
            /// </summary>
            public float aperf;

            /// <summary>
            /// Max performance frequency clock count
            /// Increments at P0 frequency while core is in C0
            /// </summary>
            public float mperf;

            /// <summary>
            /// Time stamp counter
            /// Increments at P0 frequency
            /// </summary>
            public float tsc;

            /// <summary>
            /// Retired instructions (IRPerfCount)
            /// </summary>
            public float instr;

            /// <summary>
            /// Programmable performance counter 0 value
            /// </summary>
            public float ctr0;
            public float ctr1;
            public float ctr2;
            public float ctr3;
            public float ctr4;
            public float ctr5;

            /// <summary>
            /// Total raw counts
            /// </summary>
            public ulong totalInstructions;
            public ulong totalctr0;
            public ulong totalctr1;
            public ulong totalctr2;
            public ulong totalctr3;
            public ulong totalctr4;
            public ulong totalctr5;

            /// <summary>
            /// Power consumed. Can be per-core, or whole package (for total)
            /// </summary>
            public float watts;

            /// <summary>
            /// Power consumed, total across all cores
            /// </summary>
            public float totalCoreWatts;
            public float NormalizationFactor;
        }

        public class TopDown : MonitoringConfig
        {
            private Amd19hCpu cpu;
            public string GetConfigName() { return "Top Down?"; }

            public TopDown(Amd19hCpu amdCpu) { cpu = amdCpu; }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                ulong decoderOps = GetPerfCtlValue(0xAA, 0b11, true, true, false, false, enable: false, false, 0, 0, false, false);
                cpu.ProgramPerfCounters(GetPerfCtlValue(0xAE, 0b11110111, true, true, false, false, true, false, 0, 0, false, false), // Dispatch stall 1
                    GetPerfCtlValue(0xAF, 0b101111, true, true, false, false, true, false, 0, 0, false, false),  // Dispatch stall 2
                    GetPerfCtlValue(0xA9, 0, true, true, false, false, true, false, 0, 0, false, false),  // mop queue empty
                    decoderOps,  // ops dispatched from decoder
                    GetPerfCtlValue(0xAA, 0b01, true, true, false, false, enable: false, false, cmask: 1, 0, false, false),  // decoder cycles
                    GetPerfCtlValue(0xAA, 0b10, true, true, false, false, enable: false, false, cmask: 1, 0, false, false)); // op cache cycles
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[cpu.GetThreadCount()][];
                cpu.InitializeCoreTotals();
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    results.unitMetrics[threadIdx] = computeMetrics("Thread " + threadIdx, cpu.NormalizedThreadCounts[threadIdx]);
                }

                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues("Dispatch stall 1", "Dispatch stall 2", "Op Queue Empty?", "Ops from Decoder", "Decoder cycles", "Op cache cycles");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC",
                "Dispatch Stall (sum)", "Dispatch Stall 1", "Dispatch Stall 2", "Op Queue Empty", "Ops/C from Decoder", "Decoder Cycles", "Op Cache Cycles" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.aperf),
                        FormatPercentage(counterData.ctr0 + counterData.ctr1, counterData.aperf),
                        FormatPercentage(counterData.ctr0, counterData.aperf),
                        FormatPercentage(counterData.ctr1, counterData.aperf),
                        FormatPercentage(counterData.ctr2, counterData.aperf),
                        string.Format("{0:F2}", counterData.ctr3 / counterData.aperf),
                        FormatPercentage(counterData.ctr4, counterData.aperf),
                        FormatPercentage(counterData.ctr5, counterData.aperf),
                         };    // fused branches
            }
        }

        public class Zen4TopDown : MonitoringConfig
        {
            private int pipelineSlots;
            private Amd19hCpu cpu;
            public string GetConfigName() { return "Top Down, Dispatch"; }

            public Zen4TopDown(Amd19hCpu amdCpu, int pipelineSlots)
            {
                this.cpu = amdCpu;
                this.pipelineSlots = pipelineSlots;
            }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                ulong frontendBoundSlots = 0x1004301A0;
                ulong decoderOps = 0x4307AA;
                ulong retiredOps = 0x4300C1;
                ulong backendBoundSlots = 0x100431EA0;
                ulong smtContentionSlots = 0x1004360A0;
                ulong microcodeOps = 0x1004300C2;
                cpu.ProgramPerfCounters(frontendBoundSlots, decoderOps, retiredOps, backendBoundSlots, smtContentionSlots, microcodeOps);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[cpu.GetThreadCount()][];
                cpu.InitializeCoreTotals();
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    results.unitMetrics[threadIdx] = computeMetrics("Thread " + threadIdx, cpu.NormalizedThreadCounts[threadIdx]);
                }

                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues("Frontend Bound Slots", "Decoder Ops", "Retired Ops", "Backend Bound Slots", "SMT Contention Slots", "Retired Microcode Ops");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC",
                "Bad Speculation", "Frontend Bound", "Backend Bound", "SMT Contention", "Microcoded Ops" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float slots = counterData.aperf * pipelineSlots;
                float frontendBoundSlots = counterData.ctr0;
                float decoderOps = counterData.ctr1;
                float retiredOps = counterData.ctr2;
                float backendBoundSlots = counterData.ctr3;
                float smtContentionSlots = counterData.ctr4;
                float microcodeOps = counterData.ctr5;
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.aperf),
                        FormatPercentage(decoderOps - retiredOps, slots),
                        FormatPercentage(frontendBoundSlots, slots),
                        FormatPercentage(backendBoundSlots, slots),
                        FormatPercentage(smtContentionSlots, slots),
                        FormatPercentage(microcodeOps, retiredOps)};
            }
        }

        public class Zen4TDFrontend : MonitoringConfig
        {
            private Amd19hCpu cpu;
            private int pipelineSlots;
            public string GetConfigName() { return "Top Down, Frontend"; }

            public Zen4TDFrontend(Amd19hCpu amdCpu, int slots) 
            { 
                cpu = amdCpu; 
                this.pipelineSlots = slots;
            }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                ulong frontendBoundSlots = 0x1004301A0;
                ulong frontendBoundSlotsCmask6 = 0x1064301A0;
                ulong mispredictedBranches = 0x4300C3;
                ulong resyncs = 0x430096;
                ulong decoderOps = 0x4307AA;
                ulong retiredOps = 0x4300C1;
                cpu.ProgramPerfCounters(frontendBoundSlots, decoderOps, retiredOps, frontendBoundSlotsCmask6, mispredictedBranches, resyncs);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[cpu.GetThreadCount()][];
                cpu.InitializeCoreTotals();
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    results.unitMetrics[threadIdx] = computeMetrics("Thread " + threadIdx, cpu.NormalizedThreadCounts[threadIdx]);
                }

                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues("Frontend Bound Slots", "Decoder Ops", "Retired Ops", "Frontend Bound Slots Cmask 6", "Mispredicted Branches", "Resyncs");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC",
                "Frontend Bound", "Frontend Latency", "Frontend BW", "Bad Speculation", "Bad Spec: Mispredicts", "Bad Spec: Resyncs" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float slots = counterData.aperf * pipelineSlots;
                float frontendBoundSlots = counterData.ctr0;
                float decoderOps = counterData.ctr1;
                float retiredOps = counterData.ctr2;
                float frontendBoundCmask6 = counterData.ctr3;
                float mispredictedBranches = counterData.ctr4;
                float resyncs = counterData.ctr5;
                float badSpec = (decoderOps - retiredOps) / slots;
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.aperf),
                        FormatPercentage(frontendBoundSlots, slots),
                        FormatPercentage(6 * frontendBoundCmask6, slots),
                        FormatPercentage(frontendBoundSlots - pipelineSlots * frontendBoundCmask6, slots),
                        FormatPercentage(decoderOps - retiredOps, slots),
                        FormatPercentage(badSpec * mispredictedBranches, mispredictedBranches + resyncs),
                        FormatPercentage(badSpec * resyncs, mispredictedBranches + resyncs)};
            }
        }

        public class Zen4TDBackend : MonitoringConfig
        {
            private Amd19hCpu cpu;
            private int pipelineSlots;
            public string GetConfigName() { return "Top Down, Backend Retire"; }

            public Zen4TDBackend(Amd19hCpu amdCpu, int slots) 
            { 
                cpu = amdCpu; 
                pipelineSlots = slots;
            }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                ulong retireCycles = GetPerfCtlValue(0xC1, 1, true, true, false, false, true, false, cmask: 1, 0, false, false);
                ulong backendBoundSlots = 0x100431EA0;
                ulong noRetireBlockedLoad = 0x43A2D6;
                ulong noRetireAnyBlocked = 0x4302D6;
                ulong noRetireEmpty = GetPerfCtlValue(0xD6, 1, true, true, false, false, true, false, 0, 0, false, false);
                ulong noRetireNotSelected = GetPerfCtlValue(0xD6, 2, true, true, false, false, true, false, 0, 0, false, false);
                cpu.ProgramPerfCounters(retireCycles, backendBoundSlots, noRetireBlockedLoad, noRetireAnyBlocked, noRetireEmpty, noRetireNotSelected);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[cpu.GetThreadCount()][];
                cpu.InitializeCoreTotals();
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    results.unitMetrics[threadIdx] = computeMetrics("Thread " + threadIdx, cpu.NormalizedThreadCounts[threadIdx]);
                }

                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues("Retired Ops Cmask 1", "Backend Bound Slots", "No retire: blocked load", "No retire: Not complete", "No retire: ROB empty", "No retire: Not selected");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC",
                "Backend Bound", "Memory Bound", "Core Bound", "Retire SMT Contention", "ROB Empty", "Retire Active" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float slots = counterData.aperf * pipelineSlots;
                float retiredOpsCmask1 = counterData.ctr0;
                float backendBoundSlots = counterData.ctr1;
                float backendBoundPercent = backendBoundSlots / slots;
                float blockedLoad = counterData.ctr2;
                float anyBlocked = counterData.ctr3;
                float robEmpty = counterData.ctr4;
                float notSelected = counterData.ctr5;
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.aperf),
                        string.Format("{0:F2}%", backendBoundPercent * 100),
                        string.Format("{0:F2}%", 100 * backendBoundPercent * (blockedLoad / anyBlocked)),  // backend bound, memory
                        string.Format("{0:F2}%", 100 * backendBoundPercent * (1 - (blockedLoad / anyBlocked))), // backend bound, core
                        FormatPercentage(notSelected, counterData.aperf),
                        FormatPercentage(robEmpty, counterData.aperf),
                        FormatPercentage(retiredOpsCmask1, counterData.aperf)
                };
            }
        }

        public class L2Config : MonitoringConfig
        {
            private Amd19hCpu cpu;
            public string GetConfigName() { return "L2 Cache"; }

            public L2Config(Amd19hCpu amdCpu) { cpu = amdCpu; }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(
                    GetPerfCtlValue(0x64, 0b111, true, true, false, false, true, false, 0, 0, false, false), // IC fill reqs
                    GetPerfCtlValue(0x64, 0b1, true, true, false, false, true, false, 0, 0, false, false), // IC fill miss
                    GetPerfCtlValue(0x64, 0b11111000, true, true, false, false, true, false, 0, 0, false, false),  // LS read
                    GetPerfCtlValue(0x64, 0b1000, true, true, false, false, true, false, 0, 0, false, false),  // LS read miss
                    GetPerfCtlValue(0x70, 0x1F, true, true, false, false, true, false, 0, 0, false, false),  // L2 Prefetch Hit from L2
                    GetPerfCtlValue(0x70, 0xE0, true, true, false, false, true, false, 0, 0, false, false)); // L2 Prefetch Hit from DC prefetcher
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[cpu.GetThreadCount()][];
                cpu.InitializeCoreTotals();
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    results.unitMetrics[threadIdx] = computeMetrics("Thread " + threadIdx, cpu.NormalizedThreadCounts[threadIdx]);
                }

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues("L2 Code Read", "L2 Code Miss", "L2 Data Read", "L2 Data Miss", "L2 Prefetcher Hits L2", "L1 Prefetcher Hits L2");
                return results;
            }

            public string[] columns = new string[] {
                "Item", "Active Cycles", "Instructions", "IPC",
                "Total L2 BW", "L2 Code Hitrate", "L2 Code Hit BW", "L2 Data Hitrate", "L2 Data Hit BW", "DC PF Hit BW", "L2 PF Hit BW",
                "Total L2 Data", "Total Instructions"};

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float totalHits = counterData.ctr5 + (counterData.ctr0 - counterData.ctr1) + (counterData.ctr2 - counterData.ctr3);
                ulong totalHitData = 64 * ((counterData.totalctr0 - counterData.totalctr1) + (counterData.totalctr2 - counterData.totalctr3));
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.aperf),
                        FormatLargeNumber(64 * totalHits) + "B/s",
                        string.Format("{0:F2}%", 100 * (1 - counterData.ctr1 / counterData.ctr0)),
                        FormatLargeNumber(64 * (counterData.ctr0 - counterData.ctr1)) + "B/s",
                        string.Format("{0:F2}%", 100 * (1 - counterData.ctr3 / counterData.ctr2)),
                        FormatLargeNumber(64 * (counterData.ctr2 - counterData.ctr3)) + "B/s",
                        FormatLargeNumber(counterData.ctr5 * 64) + "B/s",
                        FormatLargeNumber(counterData.ctr4 * 64) + "B/s",
                        FormatLargeNumber(totalHitData) + "B",
                        FormatLargeNumber(counterData.totalInstructions)
                        };
            }
        }

        public class FetchConfig : MonitoringConfig
        {
            private Amd19hCpu cpu;
            public string GetConfigName() { return "Instruction Fetch"; }

            public FetchConfig(Amd19hCpu amdCpu) { cpu = amdCpu; }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(GetPerfCtlValue(0x8E, 0x1F, true, true, false, false, true, false, 0, 0x1, false, false), // IC access
                    GetPerfCtlValue(0x8E, 0x18, true, true, false, false, true, false, 0, 0x1, false, false),  // IC Miss
                    GetPerfCtlValue(0x8F, 0x7, true, true, false, false, true, false, 0, 0x2, false, false),  // OC Access
                    GetPerfCtlValue(0x8F, 0x4, true, true, false, false, true, false, 0, 0x2, false, false),  // OC Miss
                    GetPerfCtlValue(0x84, 0, true, true, false, false, true, false, 0, 0, false, false),  // iTLB miss, L2 iTLB hit
                    GetPerfCtlValue(0x85, 0xF, true, true, false, false, true, false, 0, 0, false, false)); // L2 iTLB miss (page walk)
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[cpu.GetThreadCount()][];
                cpu.InitializeCoreTotals();
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    results.unitMetrics[threadIdx] = computeMetrics("Thread " + threadIdx, cpu.NormalizedThreadCounts[threadIdx]);
                }

                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues("IC Access", "IC Miss", "OC Access", "OC Miss", "iTLB Miss L2 iTLB Hit", "Instr Page Walk");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC",
                "Op$ Hitrate", "Op$ MPKI", "L1i Hitrate", "L1i MPKI", "iTLB MPKI", "L2 iTLB Hitrate", "L2 iTLB MPKI" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.aperf),
                        string.Format("{0:F2}%", 100 * (1 - counterData.ctr3 / counterData.ctr2)),
                        string.Format("{0:F2}", 1000 * counterData.ctr3 / counterData.instr),
                        string.Format("{0:F2}%", 100 * (1 - counterData.ctr1 / counterData.ctr0)),
                        string.Format("{0:F2}", 1000 * counterData.ctr1 / counterData.instr),
                        string.Format("{0:F2}", 1000 * (counterData.ctr4 + counterData.ctr5) / counterData.instr),
                        FormatPercentage(counterData.ctr4, counterData.ctr4 + counterData.ctr5),
                        string.Format("{0:F2}", 1000 * counterData.ctr5 / counterData.instr),
                        };
            }
        }

        public class PmcMonitoringConfig : MonitoringConfig
        {
            private Amd17hCpu cpu;
            public string GetConfigName() { return "Read the events"; }

            public PmcMonitoringConfig(Amd17hCpu amdCpu) { cpu = amdCpu; }

            public string[] GetColumns() { return columns; }

            public void Initialize() { }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[cpu.GetThreadCount()][];
                ulong ctl0 = 0, ctl1 = 0, ctl2 = 0, ctl3 = 0, ctl4 = 0, ctl5 = 0;
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    ThreadAffinity.Set(1UL << threadIdx);
                    Ring0.ReadMsr(MSR_PERF_CTL_0, out ctl0);
                    Ring0.ReadMsr(MSR_PERF_CTL_1, out ctl1);
                    Ring0.ReadMsr(MSR_PERF_CTL_2, out ctl2);
                    Ring0.ReadMsr(MSR_PERF_CTL_3, out ctl3);
                    Ring0.ReadMsr(MSR_PERF_CTL_4, out ctl4);
                    Ring0.ReadMsr(MSR_PERF_CTL_5, out ctl5);
                    results.unitMetrics[threadIdx] = new string[] { "Thread " + threadIdx,
                        string.Format("{0:X}", ctl0),
                        string.Format("{0:X}", ctl1),
                        string.Format("{0:X}", ctl2),
                        string.Format("{0:X}", ctl3),
                        string.Format("{0:X}", ctl4),
                        string.Format("{0:X}", ctl5)};
                }

                results.overallMetrics = new string[] { "Overall (ignore) ",
                        string.Format("{0:X}", ctl0),
                        string.Format("{0:X}", ctl1),
                        string.Format("{0:X}", ctl2),
                        string.Format("{0:X}", ctl3),
                        string.Format("{0:X}", ctl4),
                        string.Format("{0:X}", ctl5)};
                results.overallCounterValues = new Tuple<string, float>[6];
                results.overallCounterValues[0] = new Tuple<string, float>("Ctl0", ctl0);
                results.overallCounterValues[1] = new Tuple<string, float>("Ctl1", ctl1);
                results.overallCounterValues[2] = new Tuple<string, float>("Ctl2", ctl2);
                results.overallCounterValues[3] = new Tuple<string, float>("Ctl3", ctl3);
                results.overallCounterValues[4] = new Tuple<string, float>("Ctl4", ctl4);
                results.overallCounterValues[5] = new Tuple<string, float>("Ctl5", ctl5);
                return results;
            }

            public string[] columns = new string[] { "Thread", "Ctl0", "Ctl1", "Ctl2", "Ctl3", "Ctl4", "Ctl5" };

            public string GetHelpText() { return ""; }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.aperf),
                        string.Format("{0:F2}%", 100 * (1 - counterData.ctr1 / counterData.ctr0)), // bpu acc
                        string.Format("{0:F2}", counterData.ctr1 / counterData.aperf * 1000),      // branch mpki
                        string.Format("{0:F2}%", 100 * counterData.ctr0 / counterData.instr),      // % branches
                        string.Format("{0:F2}", 1000 * counterData.ctr2 / counterData.instr),     // l2 btb overrides
                        string.Format("{0:F2}", 1000 * counterData.ctr3 / counterData.instr),     // ita overrides
                        string.Format("{0:F2}", 1000 * counterData.ctr4 / counterData.instr),     // decoder overrides
                        string.Format("{0:F2}%", counterData.ctr5 / counterData.ctr0 * 100) };    // fused branches
            }
        }
    }
}
