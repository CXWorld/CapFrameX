using System;
using System.Diagnostics;
using PmcReader.Interop;
using System.Windows.Forms;
using System.Security.Policy;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using static PmcReader.Intel.ModernIntelCpu;

namespace PmcReader.Intel
{
    public class ModernIntelCpu : GenericMonitoringArea
    {
        public const uint IA32_APERF = 0xE8;
        public const uint IA32_MPERF = 0xE7;
        public const uint IA32_PERF_GLOBAL_CTRL = 0x38F;
        public const uint IA32_PERF_GLOBAL_STATUS = 0x38E;
        public const uint IA32_PERF_GLOBAL_OVF_CTRL = 0x390;
        public const uint IA32_FIXED_CTR_CTRL = 0x38D;
        public const uint IA32_FIXED_CTR0 = 0x309;
        public const uint IA32_FIXED_CTR1 = 0x30A;
        public const uint IA32_FIXED_CTR2 = 0x30B;
        public const uint IA32_FIXED_CTR3 = 0x30C;
        public static readonly uint[] IA32_FIXED_CTR = {  IA32_FIXED_CTR0 +  0, IA32_FIXED_CTR0 +  1, IA32_FIXED_CTR0 +  2,
                                                          IA32_FIXED_CTR0 +  3, IA32_FIXED_CTR0 +  4, IA32_FIXED_CTR0 +  5,
                                                          IA32_FIXED_CTR0 +  6, IA32_FIXED_CTR0 +  7, IA32_FIXED_CTR0 +  8,
                                                          IA32_FIXED_CTR0 +  9, IA32_FIXED_CTR0 + 10, IA32_FIXED_CTR0 +  5,
                                                          IA32_FIXED_CTR0 + 12, IA32_FIXED_CTR0 + 13, IA32_FIXED_CTR0 + 14,
                                                          IA32_FIXED_CTR0 + 15 };

        public const uint IA32_PERFEVTSEL0 = 0x186;
        public const uint IA32_PERFEVTSEL1 = 0x187;
        public const uint IA32_PERFEVTSEL2 = 0x188;
        public const uint IA32_PERFEVTSEL3 = 0x189;
        public static readonly uint[] IA32_PERFEVTSEL = {  IA32_PERFEVTSEL0 +  0, IA32_PERFEVTSEL0 +  1, IA32_PERFEVTSEL0 +  2,
                                                           IA32_PERFEVTSEL0 +  3, IA32_PERFEVTSEL0 +  4, IA32_PERFEVTSEL0 +  5,
                                                           IA32_PERFEVTSEL0 +  6, IA32_PERFEVTSEL0 +  7, IA32_PERFEVTSEL0 +  8,
                                                           IA32_PERFEVTSEL0 +  9, IA32_PERFEVTSEL0 + 10, IA32_PERFEVTSEL0 +  5,
                                                           IA32_PERFEVTSEL0 + 12, IA32_PERFEVTSEL0 + 13, IA32_PERFEVTSEL0 + 14,
                                                           IA32_PERFEVTSEL0 + 15 };

        public const uint IA32_A_PMC0 = 0x4C1;
        public const uint IA32_A_PMC1 = 0x4C2;
        public const uint IA32_A_PMC2 = 0x4C3;
        public const uint IA32_A_PMC3 = 0x4C4;
        public static readonly uint[] IA32_A_PMC = {  IA32_A_PMC0 +  0, IA32_A_PMC0 +  1, IA32_A_PMC0 +  2,
                                                      IA32_A_PMC0 +  3, IA32_A_PMC0 +  4, IA32_A_PMC0 +  5,
                                                      IA32_A_PMC0 +  6, IA32_A_PMC0 +  7, IA32_A_PMC0 +  8,
                                                      IA32_A_PMC0 +  9, IA32_A_PMC0 + 10, IA32_A_PMC0 +  5,
                                                      IA32_A_PMC0 + 12, IA32_A_PMC0 + 13, IA32_A_PMC0 + 14,
                                                      IA32_A_PMC0 + 15 };

        // RAPL only applies to sandy bridge and later
        public const uint MSR_RAPL_POWER_UNIT = 0x606;
        public const uint MSR_PKG_ENERGY_STATUS = 0x611;
        public const uint MSR_PP0_ENERGY_STATUS = 0x639;
        public const uint MSR_DRAM_ENERGY_STATUS = 0x619;

        // Hardware prefetch control
        public const uint MSR_PF_CTL = 0x1A4;

        // applies to Gracemont and Goldmont Plus
        public const uint MSR_OFFCORE_RSP0 = 0x1A6;
        public const uint MSR_OFFCORE_RSP1 = 0x1A7;
        public const byte OFFCORE_RESPONSE_EVENT = 0xB7;

        // applies to big cores, which don't use a unit mask to select
        // an offcore response register
        public const byte OFFCORE_RESPONSE_EVENT_1 = 0xBB;

        // Introduced in Skylake
        public const uint MSR_PEBS_FRONTEND = 0x3F7;

        public NormalizedCoreCounterData[] NormalizedThreadCounts;
        public NormalizedCoreCounterData NormalizedTotalCounts;
        public RawTotalCoreCounterData RawTotalCounts;

        private float energyStatusUnits = 0;
        private Stopwatch lastPkgPwrTime;
        private ulong lastPkgPwr = 0;
        private ulong lastPp0Pwr = 0;

        // Hybrid stuff
        public struct CoreType
        {
            public CoreType(string name, byte type, byte coreCount, ulong coreMask, byte pmcCounters = 4, uint fixedCounterMask = 0x7, byte allocWidth = 4)
            {
                Name = name;
                Type = type;
                CoreCount = coreCount;
                CoreMask = coreMask;
                PmcCounters = pmcCounters;
                FixedCounterMask = fixedCounterMask;
                AllocWidth = allocWidth;
            }
            public string Name;
            public byte Type;
            public byte CoreCount;
            public ulong CoreMask;

            /// <summary>
            /// Number of programmable performance monitoring counters
            /// </summary>
            public byte PmcCounters;
            public uint FixedCounterMask;
            public byte AllocWidth;
        }

        public CoreType[] coreTypes;
        public List<int>[] coreTypeNumbers; // index of coreType -> list of cores of that type. MTL doesn't have contiguous core numbering 

        public ModernIntelCpu()
        {
            this.architectureName = "Modern P6 Family CPU";
            monitoringConfigs = new MonitoringConfig[1];
            monitoringConfigs[0] = new ArchitecturalCounters(this);

            // Determine the number of different types of cores within the system
            List<byte> coreTypes = new List<byte>();
            for (byte threadIdx = 0; threadIdx < GetThreadCount(); threadIdx++)
            {
                ThreadAffinity.Set(1UL << threadIdx);
                OpCode.Cpuid(0x1A, 0, out uint eax, out _, out _, out _); // Core
                byte coreType = (byte)((eax >> 24) & 0xFF);
                bool newType = true;
                foreach (byte type in coreTypes)
                {
                    if (type == coreType)
                    {
                        newType = false;
                        break;
                    }
                }
                if (newType)
                    coreTypes.Add(coreType);
            }

            if (coreTypes.Count > 1)
                this.architectureName += " (Hybrid)";

            this.coreTypes = new CoreType[coreTypes.Count];
            this.coreTypeNumbers = new List<int>[coreTypes.Count];

            for (byte coreTypeIdx = 0; coreTypeIdx < coreTypes.Count; coreTypeIdx++)
            {
                byte type = coreTypes[coreTypeIdx];
                byte coreCount = 0;
                ulong coreMask = 0;
                uint fixedMask = 0;
                byte pmcCount = 4;
                this.coreTypeNumbers[coreTypeIdx] = new List<int>();

                // Query each logical processor to determine its type
                for (byte threadIdx = 0; threadIdx < threadCount; threadIdx++)
                {
                    ThreadAffinity.Set(1UL << threadIdx);
                    OpCode.Cpuid(0x1A, 0, out uint eax, out _, out _, out _);
                    byte coreType = (byte)((eax >> 24) & 0xFF);
                    if (type == coreType)
                    {
                        this.coreTypeNumbers[coreTypeIdx].Add(threadIdx);

                        // If this is the first core of this type, read the number of
                        // general and fixed counters that are present.
                        if (coreMask == 0)
                        {
                            OpCode.Cpuid(0x0A, 0, out eax, out _, out uint ecx, out uint edx);
                            pmcCount = (byte)((eax >> 8) & 0xFF);
                            if (pmcCount == 0)
                                pmcCount = 4; // Default if enumeration returns 0

                            byte fixedCounters = (byte)((edx >> 0) & 0x1F);
                            uint fixedCounterMask = (ecx & 0xFFFF);
                            for(int fixedIdx = 0; fixedIdx < 16; fixedIdx++)
                            {
                                // A fixed counter exists if it's within the identified total or if the mask says it exists
                                if ((fixedIdx < fixedCounters) || ((fixedCounterMask & 0x1) == 0x1))
                                    fixedMask |= (1U << fixedIdx);
                                fixedCounterMask >>= 1;
                            }
                            if (fixedCounterMask == 0)
                                fixedCounterMask = 0x3;  // Default if enumeration returns 0
                        }
                        coreCount++;
                        coreMask |= (1UL << threadIdx); // Track which threads are of this core type
                    }
                }
                // Console.WriteLine("Creating core type: ID {0} Type 0x{1,2:X2} coreMask 0x{2,8:X8} PMC {3} fixedMask 0x{4,4:X4}", coreIdx, type, coreMask, pmcCount, fixedMask);
                this.coreTypes[coreTypeIdx] = new CoreType("Type" + coreTypeIdx, type, coreCount, coreMask, pmcCount, fixedMask);
            }
        }

        ~ModernIntelCpu()
        {
            DisablePerformanceCounters();
        }

        /// <summary>
        /// Generate value to put in IA32_PERFEVTSELx MSR
        /// for programming PMCs
        /// </summary>
        /// <param name="perfEvent">Event selection</param>
        /// <param name="umask">Umask (more specific condition for event)</param>
        /// <param name="usr">Count user mode events</param>
        /// <param name="os">Count kernel mode events</param>
        /// <param name="edge">Edge detect</param>
        /// <param name="pc">Pin control (???)</param>
        /// <param name="interrupt">Trigger interrupt on counter overflow</param>
        /// <param name="anyThread">Count across all logical processors</param>
        /// <param name="enable">Enable the counter</param>
        /// <param name="invert">Invert cmask condition</param>
        /// <param name="cmask">if not zero, count when increment >= cmask</param>
        /// <returns>Value to put in performance event select register</returns>
        public static ulong GetPerfEvtSelRegisterValue(byte perfEvent,
                                           byte umask,
                                           bool usr = true,
                                           bool os = true,
                                           bool edge = false,
                                           bool pc = false,
                                           bool interrupt = false,
                                           bool anyThread = false,
                                           bool enable = true,
                                           bool invert = false,
                                           byte cmask = 0)
        {
            ulong value = (ulong)perfEvent |
                (ulong)umask << 8 |
                (usr ? 1UL : 0UL) << 16 |
                (os ? 1UL : 0UL) << 17 |
                (edge ? 1UL : 0UL) << 18 |
                (pc ? 1UL : 0UL) << 19 |
                (interrupt ? 1UL : 0UL) << 20 |
                (anyThread ? 1UL : 0UL) << 21 |
                (enable ? 1UL : 0UL) << 22 |
                (invert ? 1UL : 0UL) << 23 |
                (ulong)cmask << 24;
            return value;
        }

        /// <summary>
        /// Set up fixed counters and enable programmable ones. 
        /// Works on Sandy Bridge, Haswell, and Skylake
        /// </summary>
        public void EnablePerformanceCounters(byte type = 0xFF)
        {
            foreach (CoreType coreType in coreTypes)
            {
                if ((type != 0xFF) && (coreType.Type != type))
                    continue;

                // enable fixed performance counters (3) and programmable counters (4)
                ulong enablePMCsValue = 0;
                ulong fixedCounterConfigurationValue = 0;
                for (byte pmcIdx = 0; pmcIdx < coreType.PmcCounters; pmcIdx++)
                    enablePMCsValue |= (1UL << pmcIdx); // General purpose counters

                for (byte fixedIdx = 0; fixedIdx < 16; fixedIdx++)
                {
                    if (((coreType.FixedCounterMask >> fixedIdx) & 0x1) == 0x1)
                    {
                        enablePMCsValue |= (1UL << (fixedIdx + 32)); // Fixed counters
                        fixedCounterConfigurationValue |= (1UL << ((fixedIdx * 4) + 0)); // Kernel mode
                        fixedCounterConfigurationValue |= (1UL << ((fixedIdx * 4) + 1)); // User Mode
                    }
                }

                if (type == AlderLake.ADL_P_CORE_TYPE)
                {
                    enablePMCsValue |= (1UL << 48); // this is how linux enables MSR_PERF_METRICS
                    enablePMCsValue |= (1UL << 35); // does this enable fixed counter 3?
                }

                for (int threadIdx = 0; threadIdx < GetThreadCount(); threadIdx++)
                {
                    if (((coreType.CoreMask >> threadIdx) & 0x1) == 0x1)
                    {
                        ThreadAffinity.Set(1UL << threadIdx);
                        Ring0.WriteMsr(IA32_PERF_GLOBAL_CTRL, enablePMCsValue);
                        Ring0.WriteMsr(IA32_FIXED_CTR_CTRL, fixedCounterConfigurationValue);

                        // Zero counters to ensure we don't pollute totals
                        Ring0.WriteMsr(IA32_FIXED_CTR0, 0);
                        Ring0.WriteMsr(IA32_FIXED_CTR1, 0);
                        Ring0.WriteMsr(IA32_FIXED_CTR2, 0);

                        for (byte fixedIdx = 0; fixedIdx < 16; fixedIdx++)
                        {
                            if (((coreType.FixedCounterMask >> fixedIdx) & 0x1) == 0x1)
                            {
                                Ring0.WriteMsr(IA32_A_PMC[fixedIdx], 0);
                            }
                        }
                    }
                }
            }

            if (RawTotalCounts != null)
            {
                // Reset totals when enabling counters
                for (byte pmcIdx = 0; pmcIdx < RawTotalCounts.pmc.Length; pmcIdx++)
                    RawTotalCounts.pmc[pmcIdx] = 0;
            }
        }

        /// <summary>
        /// Disable fixed/general programmable counters 
        /// </summary>
        public void DisablePerformanceCounters(byte type = 0xFF)
        {
            foreach (CoreType coreType in coreTypes)
            {
                if ((type != 0xFF) && (coreType.Type != type))
                    continue;

                for (int threadIdx = 0; threadIdx < GetThreadCount(); threadIdx++)
                {
                    if (((coreType.CoreMask >> threadIdx) & 0x1) == 0x1)
                    {
                        ThreadAffinity.Set(1UL << threadIdx);
                        Ring0.WriteMsr(IA32_PERF_GLOBAL_CTRL, 0);
                        Ring0.WriteMsr(IA32_FIXED_CTR_CTRL, 0);
                        for (byte pmcIdx = 0; pmcIdx < 16; pmcIdx++)
                        {
                            Ring0.WriteMsr(IA32_PERFEVTSEL[pmcIdx], 0);
                            Ring0.WriteMsr(IA32_A_PMC[pmcIdx], 0);
                        }
                        for (byte fixedIdx = 0; fixedIdx < 16; fixedIdx++)
                        {
                            Ring0.WriteMsr(IA32_FIXED_CTR[fixedIdx], 0);
                        }
                    }
                }
                if (type != 0xFF)
                    break;
            }
        }

        /// <summary>
        /// Set up programmable perf counters. All modern Intel CPUs have 4 perf counters.
        /// Ice Lake or >Haswell with SMT off have 8 programmable counters but too much trouble to do that detection
        /// </summary>
        /// <param name="pmc0">counter 0 config</param>
        /// <param name="pmc1">counter 1 config</param>
        /// <param name="pmc2">counter 2 config</param>
        /// <param name="pmc3">counter 3 config</param>
        public void ProgramPerfCounters(ulong pmc0, ulong pmc1, ulong pmc2, ulong pmc3)
        {
            ProgramPerfCounters(new ulong[] { pmc0, pmc1, pmc2, pmc3 });
        }

        /// <summary>
        /// Set up programmable perf counters. Supports a generic counter size.
        /// </summary>
        /// <param name="pmc">counter array config</param>
        public void ProgramPerfCounters(ulong[] pmc, byte type = 0xFF)
        {
            bool found = false;
            foreach (CoreType coreType in coreTypes)
            {             
                if ((type != 0xFF) && (coreType.Type != type))
                    continue;

                for (int threadIdx = 0; threadIdx < GetThreadCount(); threadIdx++)
                {
                    if (((coreType.CoreMask >> threadIdx) & 0x1) == 0x1)
                    {
                        found = true;
                        ProgramPerfCounters(pmc, threadIdx, (pmc.Length <= coreType.PmcCounters) ? (uint)pmc.Length : coreType.PmcCounters);
                    }
                }
                if (found)
                    EnablePerformanceCounters(coreType.Type);             
            }
        }

        public void ProgramPerfCounters(ulong[] pmc, List<int> threadIndices, byte coreType)
        {
            foreach (int threadIdx in threadIndices)
            {
                ProgramPerfCounters(pmc, threadIdx, (uint)pmc.Length);
            }

            EnablePerformanceCounters(coreType);
        }

        public void ProgramPerfCounters(ulong[] pmc, int threadIdx, uint pmcLen)
        {
            ThreadAffinity.Set(1UL << threadIdx);
            for (byte pmcIdx = 0; pmcIdx < pmcLen; pmcIdx++)
            {
                Ring0.WriteMsr(IA32_PERFEVTSEL[pmcIdx], pmc[pmcIdx]);
                Ring0.WriteMsr(IA32_A_PMC[pmcIdx], 0);
            }
        }
        
        /// <summary>
        /// Reset accumulated totals for core counter data
        /// </summary>
        public void InitializeCoreTotals()
        {
            if (NormalizedTotalCounts == null)
            {
                NormalizedTotalCounts = new NormalizedCoreCounterData();
            }

            NormalizedTotalCounts.activeCycles = 0;
            NormalizedTotalCounts.instr = 0;
            NormalizedTotalCounts.refTsc = 0;
            NormalizedTotalCounts.packagePower = 0;
            for (byte pmcIdx = 0; pmcIdx < NormalizedTotalCounts.pmc.Length; pmcIdx++)
                NormalizedTotalCounts.pmc[pmcIdx] = 0;

            if (RawTotalCounts == null)
            {
                RawTotalCounts = new RawTotalCoreCounterData();
            }

            // don't reset totals
        }

        /// <summary>
        /// Update counter values for thread, and add to totals
        /// Will set thread affinity
        /// </summary>
        /// <param name="threadIdx">thread in question</param>
        public void UpdateThreadCoreCounterData(int threadIdx)
        {
            if (NormalizedThreadCounts == null)
            {
                NormalizedThreadCounts = new NormalizedCoreCounterData[threadCount];
            }

            if (NormalizedThreadCounts[threadIdx] == null)
            {
                NormalizedThreadCounts[threadIdx] = new NormalizedCoreCounterData();
            }

            ThreadAffinity.Set(1UL << threadIdx);
            foreach (CoreType coreType in coreTypes)
            {
                // Find core type to determine many PMCs it supports
                if (((coreType.CoreMask >> threadIdx) & 0x1) == 0x0)
                    continue;

                ulong activeCycles, retiredInstructions, refTsc, slots;
                float normalizationFactor = GetNormalizationFactor(threadIdx);
                retiredInstructions = ReadAndClearMsr(IA32_FIXED_CTR0);

                // make sure this can freerun (don't clear it) so we don't fight with hwinfo
                Ring0.ReadMsr(IA32_FIXED_CTR1, out activeCycles);
                ulong adjustedActiveCycles = activeCycles;

                // handle wrap around case once I remember how wide the counter is
                if (NormalizedThreadCounts[threadIdx].lastActiveCycles > 0 && activeCycles > NormalizedThreadCounts[threadIdx].lastActiveCycles)
                {
                    adjustedActiveCycles = activeCycles - NormalizedThreadCounts[threadIdx].lastActiveCycles;
                }

                NormalizedThreadCounts[threadIdx].lastActiveCycles = activeCycles;
                activeCycles = adjustedActiveCycles;

                refTsc = ReadAndClearMsr(IA32_FIXED_CTR2);

                ulong[] pmc = new ulong[coreType.PmcCounters];
                for (byte pmcIdx = 0; pmcIdx < pmc.Length; pmcIdx++)
                    pmc[pmcIdx] = ReadAndClearMsr(IA32_A_PMC[pmcIdx]);

                NormalizedThreadCounts[threadIdx].activeCycles = activeCycles * normalizationFactor;
                NormalizedThreadCounts[threadIdx].instr = retiredInstructions * normalizationFactor;
                NormalizedThreadCounts[threadIdx].refTsc = refTsc * normalizationFactor;
                //NormalizedThreadCounts[threadIdx].slots = slots * normalizationFactor;
                for (byte pmcIdx = 0; pmcIdx < pmc.Length; pmcIdx++)
                    NormalizedThreadCounts[threadIdx].pmc[pmcIdx] = pmc[pmcIdx] * normalizationFactor;
                NormalizedThreadCounts[threadIdx].NormalizationFactor = normalizationFactor;
                NormalizedTotalCounts.activeCycles += NormalizedThreadCounts[threadIdx].activeCycles;
                NormalizedTotalCounts.instr += NormalizedThreadCounts[threadIdx].instr;
                NormalizedTotalCounts.refTsc += NormalizedThreadCounts[threadIdx].refTsc;
                for (byte pmcIdx = 0; pmcIdx < pmc.Length; pmcIdx++)
                    NormalizedTotalCounts.pmc[pmcIdx] += NormalizedThreadCounts[threadIdx].pmc[pmcIdx];

                RawTotalCounts.activeCycles += activeCycles;
                RawTotalCounts.instr += retiredInstructions;
                RawTotalCounts.refTsc += refTsc;
                for (byte pmcIdx = 0; pmcIdx < pmc.Length; pmcIdx++)
                    RawTotalCounts.pmc[pmcIdx] += pmc[pmcIdx];
                    
                break;
            }
        }

        /// <summary>
        /// Holds performance counter, read out from the three fixed counters
        /// and four programmable ones
        /// </summary>
        public class NormalizedCoreCounterData
        {
            public float activeCycles;
            public float instr;
            public float refTsc;
            public float slots;
            public float packagePower;
            public float pp0Power;
            public float[] pmc = new float[16];
            public float NormalizationFactor;

            // hwinfo uses this so let it freerun
            public ulong lastActiveCycles;
        }

        public class RawTotalCoreCounterData
        {
            public ulong activeCycles;
            public ulong instr;
            public ulong refTsc;
            public float packagePower;
            public ulong[] pmc = new ulong[16];
        }

        /// <summary>
        /// Read RAPL package power MSR. Should work on SNB and above
        /// </summary>
        /// <returns>Package power in watts</returns>
        public float ReadPackagePowerCounter()
        {
            if (energyStatusUnits == 0)
            {
                ulong raplPowerUnitRegister, energyStatusUnitsField;
                Ring0.ReadMsr(MSR_RAPL_POWER_UNIT, out raplPowerUnitRegister);
                // energy status units in bits 8-12
                energyStatusUnitsField = (raplPowerUnitRegister >> 8) & 0x1F;
                energyStatusUnits = (float)Math.Pow(0.5, (float)energyStatusUnitsField);
            }

            ulong pkgEnergyStatus, pp0EnergyStatus, elapsedPkgEnergy, elapsedPp0Energy;
            Ring0.ReadMsr(MSR_PKG_ENERGY_STATUS, out pkgEnergyStatus);
            Ring0.ReadMsr(MSR_PP0_ENERGY_STATUS, out pp0EnergyStatus);
            pkgEnergyStatus &= 0xFFFFFFFF;
            elapsedPkgEnergy = pkgEnergyStatus;
            if (pkgEnergyStatus > lastPkgPwr) elapsedPkgEnergy -= lastPkgPwr;
            else if (lastPkgPwr > 0) elapsedPkgEnergy += (0xFFFFFFFF - lastPkgPwr);
            lastPkgPwr = pkgEnergyStatus;

            pp0EnergyStatus &= 0xFFFFFFFF;
            elapsedPp0Energy = pp0EnergyStatus;
            if (pp0EnergyStatus > lastPp0Pwr) elapsedPp0Energy -= lastPp0Pwr;
            else if (lastPp0Pwr > 0) elapsedPp0Energy += (0xFFFFFFFF - lastPp0Pwr);
            lastPp0Pwr = pp0EnergyStatus;

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

            float packagePower = elapsedPkgEnergy * energyStatusUnits * normalizationFactor;
            float pp0Power = elapsedPp0Energy * energyStatusUnits * normalizationFactor;
            if (NormalizedTotalCounts != null)
            {
                NormalizedTotalCounts.packagePower = packagePower;
                NormalizedTotalCounts.pp0Power = pp0Power;
            }

            return packagePower;
        }

        /// <summary>
        /// Assemble overall counter values into a Tuple of string, float array.
        /// </summary>
        /// <param name="pmc0">Description for counter 0</param>
        /// <param name="pmc1">Description for counter 1</param>
        /// <param name="pmc2">Description for counter 2</param>
        /// <param name="pmc3">Description for counter 3</param>
        /// <returns>Array to put in results object</returns>
        public Tuple<string, float>[] GetOverallCounterValues(string pmc0, string pmc1, string pmc2, string pmc3)
        {
            return GetOverallCounterValues(new string[] {pmc0, pmc1, pmc2, pmc3});
        }

        /// <summary>
        /// Assemble overall counter values into a Tuple of string, float array.
        /// </summary>
        /// <param name="pmc">Description for counter array</param>
        /// <returns>Array to put in results object</returns>
        public Tuple<string, float>[] GetOverallCounterValues(string[] pmc)
        {
            int returnedPoints = 5 + pmc.Length;
            Tuple<string, float>[] retval = new Tuple<string, float>[returnedPoints];
            NormalizedCoreCounterData dataToLog = this.NormalizedTotalCounts;
            if (this.targetLogCoreIndex >= 0)
            {
                dataToLog = NormalizedThreadCounts[this.targetLogCoreIndex];
            }

            retval[0] = new Tuple<string, float>("Active Cycles", dataToLog.activeCycles);
            retval[1] = new Tuple<string, float>("REF_TSC", dataToLog.refTsc);
            retval[2] = new Tuple<string, float>("Instructions", dataToLog.instr);
            retval[3] = new Tuple<string, float>("Package Power", dataToLog.packagePower);
            retval[4] = new Tuple<string, float>("PP0 Power", dataToLog.pp0Power);
            for (byte pmcIdx = 0; pmcIdx < pmc.Length; pmcIdx++)
                retval[pmcIdx + 5] = new Tuple<string, float>(pmc[pmcIdx], dataToLog.pmc[pmcIdx]);

            return retval;
        }
        
        private Label errorLabel;
        public override void InitializeCrazyControls(FlowLayoutPanel flowLayoutPanel, Label errLabel)
        {
            flowLayoutPanel.Controls.Clear();

            Button enableL2HwPfButton = CreateButton("Enable L2 HW PF", EnableL2HwPf);
            Button disableL2HwPfButton = CreateButton("Disable L2 HW PF", DisableL2HwPf);
            Button enableL2AdjacentPfButton = CreateButton("Enable L2 Adj PF", EnableL2AdjPf);
            Button disableL2AdjacentPfButton = CreateButton("Disable L2 Adj PF", DisableL2AdjPf);
            Button enableDcuPf = CreateButton("Enable L1D Adj Pf", EnableDcuPf);
            Button disableDcuPf = CreateButton("Disable L1D Adj Pf", DisableDcuPf);
            Button enableDcuIpPf = CreateButton("Enable L1D IP Pf", EnableDcuIpPf);
            Button disableDcuIpPf = CreateButton("Disable L1D IP Pf", DisableDcuIpPf);


            flowLayoutPanel.Controls.Add(enableL2HwPfButton);
            flowLayoutPanel.Controls.Add(disableL2HwPfButton);
            flowLayoutPanel.Controls.Add(enableL2AdjacentPfButton);
            flowLayoutPanel.Controls.Add(disableL2AdjacentPfButton);
            flowLayoutPanel.Controls.Add(enableDcuPf);
            flowLayoutPanel.Controls.Add(disableDcuPf);
            flowLayoutPanel.Controls.Add(enableDcuIpPf);
            flowLayoutPanel.Controls.Add(disableDcuIpPf);
            errorLabel = errLabel;
        }

        private new Button CreateButton(string buttonText, EventHandler handler)
        {
            Button button = new Button();
            button.Text = buttonText;
            button.AutoSize = true;
            button.Click += handler;

            return button;
        }

        private void GetHwPfStatus(out bool l2, out bool l2Adj, out bool dcu, out bool dcuIp)
        {
            Ring0.ReadMsr(MSR_PF_CTL, out ulong pfCtl);
            l2 = (pfCtl & 0x1) == 0;
            l2Adj = (pfCtl & 0x2) == 0;
            dcu = (pfCtl & 0x4) == 0;
            dcuIp = (pfCtl & 0x8) == 0;
        }

        private void ReportHwPfStatus()
        {
            bool l2, l2Adj, dcu, dcuIp;
            bool l2AllEnabled = true, l2AdjAllEnabled = true, dcuAllEnabled = true, dcuIpAllEnabled = true;
            for (int threadIdx = 0; threadIdx < GetThreadCount(); threadIdx++)
            {
                GetHwPfStatus(out l2, out l2Adj, out dcu, out dcuIp);
                l2AllEnabled &= l2;
                l2AdjAllEnabled &= l2Adj;
                dcuAllEnabled &= dcu;
                dcuIpAllEnabled &= dcuIp;
            }
            errorLabel.Text = string.Format($"L2 HW Prefetcher: {l2AllEnabled}, L2 Adjacent Line Pf: {l2AdjAllEnabled}, L1D Adjacent Line Pf: {dcuAllEnabled}, L1D IP Pf: {dcuIpAllEnabled}");
        }

        public void EnableL2HwPf(object sender, EventArgs e)
        {
            for (int threadIdx = 0; threadIdx < GetThreadCount(); threadIdx++)
            {
                ThreadAffinity.Set(1UL << threadIdx);
                Ring0.ReadMsr(MSR_PF_CTL, out ulong pfCtl);
                pfCtl &= ~(1UL);
                Ring0.WriteMsr(MSR_PF_CTL, pfCtl);
            }

            ReportHwPfStatus();
        }

        public void DisableL2HwPf(object sender, EventArgs e)
        {
            for (int threadIdx = 0; threadIdx < GetThreadCount(); threadIdx++)
            {
                ThreadAffinity.Set(1UL << threadIdx);
                Ring0.ReadMsr(MSR_PF_CTL, out ulong pfCtl);
                pfCtl |= 1UL;
                Ring0.WriteMsr(MSR_PF_CTL, pfCtl);
            }

            ReportHwPfStatus();
        }

        public void EnableL2AdjPf(object sender, EventArgs e)
        {
            for (int threadIdx = 0; threadIdx < GetThreadCount(); threadIdx++)
            {
                ThreadAffinity.Set(1UL << threadIdx);
                Ring0.ReadMsr(MSR_PF_CTL, out ulong pfCtl);
                pfCtl &= ~(1UL << 1);
                Ring0.WriteMsr(MSR_PF_CTL, pfCtl);
            }

            ReportHwPfStatus();
        }

        public void DisableL2AdjPf(object sender, EventArgs e)
        {
            for (int threadIdx = 0; threadIdx < GetThreadCount(); threadIdx++)
            {
                ThreadAffinity.Set(1UL << threadIdx);
                Ring0.ReadMsr(MSR_PF_CTL, out ulong pfCtl);
                pfCtl |= (1UL << 1);
                Ring0.WriteMsr(MSR_PF_CTL, pfCtl);
            }

            ReportHwPfStatus();
        }

        public void EnableDcuPf(object sender, EventArgs e)
        {
            for (int threadIdx = 0; threadIdx < GetThreadCount(); threadIdx++)
            {
                ThreadAffinity.Set(1UL << threadIdx);
                Ring0.ReadMsr(MSR_PF_CTL, out ulong pfCtl);
                pfCtl &= ~(1UL << 2);
                Ring0.WriteMsr(MSR_PF_CTL, pfCtl);
            }

            ReportHwPfStatus();
        }

        public void DisableDcuPf(object sender, EventArgs e)
        {
            for (int threadIdx = 0; threadIdx < GetThreadCount(); threadIdx++)
            {
                ThreadAffinity.Set(1UL << threadIdx);
                Ring0.ReadMsr(MSR_PF_CTL, out ulong pfCtl);
                pfCtl |= (1UL << 2);
                Ring0.WriteMsr(MSR_PF_CTL, pfCtl);
            }

            ReportHwPfStatus();
        }

        public void EnableDcuIpPf(object sender, EventArgs e)
        {
            for (int threadIdx = 0; threadIdx < GetThreadCount(); threadIdx++)
            {
                ThreadAffinity.Set(1UL << threadIdx);
                Ring0.ReadMsr(MSR_PF_CTL, out ulong pfCtl);
                pfCtl &= ~(1UL << 3);
                Ring0.WriteMsr(MSR_PF_CTL, pfCtl);
            }

            ReportHwPfStatus();
        }

        public void DisableDcuIpPf(object sender, EventArgs e)
        {
            for (int threadIdx = 0; threadIdx < GetThreadCount(); threadIdx++)
            {
                ThreadAffinity.Set(1UL << threadIdx);
                Ring0.ReadMsr(MSR_PF_CTL, out ulong pfCtl);
                pfCtl |= (1UL << 3);
                Ring0.WriteMsr(MSR_PF_CTL, pfCtl);
            }

            ReportHwPfStatus();
        }

        /// <summary>
        /// Monitor branch prediction. Retired branch instructions mispredicted / retired branches
        /// are architectural events so it should be the same across modern Intel chips 
        /// not sure about baclears, but that's at least consistent across SKL/HSW/SNB
        /// </summary>
        public class BpuMonitoringConfig : MonitoringConfig
        {
            private ModernIntelCpu cpu;
            public string GetConfigName() { return "Branch Prediction"; }

            public BpuMonitoringConfig(ModernIntelCpu intelCpu)
            {
                cpu = intelCpu;
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                cpu.EnablePerformanceCounters();

                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    ThreadAffinity.Set(1UL << threadIdx);
                    // Set PMC0 to count all retired branches
                    ulong retiredBranches = GetPerfEvtSelRegisterValue(0xC4, 0x00, true, true, false, false, false, false, true, false, 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL0, retiredBranches);

                    // Set PMC1 to count mispredicted branches
                    ulong retiredMispredictedBranches = GetPerfEvtSelRegisterValue(0xC5, 0x00, true, true, false, false, false, false, true, false, 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL1, retiredMispredictedBranches);

                    // Set PMC2 to count BACLEARS, or frontend re-steers due to BPU misprediction
                    ulong branchResteers = GetPerfEvtSelRegisterValue(0xE6, 0x1F, true, true, false, false, false, false, true, false, 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL2, branchResteers);

                    // Set PMC3 to count all executed branches
                    ulong notTakenBranches = GetPerfEvtSelRegisterValue(0x88, 0xFF, true, true, false, false, false, false, true, false, 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL3, notTakenBranches);
                }
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
                cpu.ReadPackagePowerCounter();
                results.overallCounterValues = cpu.GetOverallCounterValues("Retired Branches", "Retired Mispredicted Branches", "BAClears", "Executed Branches");
                return results;
            }

            public string[] columns = new string[] { "Item", "Norm", "REF_TSC", "Active Cycles", "Instructions", "IPC", "BPU Accuracy", "Branch MPKI", "BTB Hitrate", "% Branches" };

            public string GetHelpText() { return ""; }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float bpuAccuracy = (1 - counterData.pmc[1] / counterData.pmc[0]) * 100;
                float ipc = counterData.instr / counterData.activeCycles;
                float branchMpki = counterData.pmc[1] / counterData.instr * 1000;
                float btbHitrate = (1 - counterData.pmc[2] / counterData.pmc[0]) * 100;
                float branchRate = counterData.pmc[0] / counterData.instr * 100;

                return new string[] { label,
                    string.Format("{0:F2}", counterData.NormalizationFactor),
                    FormatLargeNumber(counterData.refTsc),
                    FormatLargeNumber(counterData.activeCycles),
                    FormatLargeNumber(counterData.instr),
                    string.Format("{0:F2}", ipc),
                    string.Format("{0:F2}%", bpuAccuracy),
                    string.Format("{0:F2}", branchMpki),
                    string.Format("{0:F2}%", btbHitrate),
                    string.Format("{0:F2}%", branchRate)};
            }
        }

        /// <summary>
        /// Op Cache, events happen to be commmon across SKL/HSW/SNB
        /// </summary>
        public class OpCachePerformance : MonitoringConfig
        {
            private ModernIntelCpu cpu;
            public string GetConfigName() { return "Op Cache Performance"; }

            public OpCachePerformance(ModernIntelCpu intelCpu)
            {
                cpu = intelCpu;
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                cpu.EnablePerformanceCounters();

                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    ThreadAffinity.Set(1UL << threadIdx);
                    // Set PMC0 to count DSB (decoded stream buffer = op cache) uops
                    ulong retiredBranches = GetPerfEvtSelRegisterValue(0x79, 0x08, true, true, false, false, false, false, true, false, 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL0, retiredBranches);

                    // Set PMC1 to count cycles when the DSB's delivering to IDQ (cmask=1)
                    ulong retiredMispredictedBranches = GetPerfEvtSelRegisterValue(0x79, 0x08, true, true, false, false, false, false, true, false, 1);
                    Ring0.WriteMsr(IA32_PERFEVTSEL1, retiredMispredictedBranches);

                    // Set PMC2 to count MITE (micro instruction translation engine = decoder) uops
                    ulong branchResteers = GetPerfEvtSelRegisterValue(0x79, 0x04, true, true, false, false, false, false, true, false, 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL2, branchResteers);

                    // Set PMC3 to count MITE cycles (cmask=1)
                    ulong notTakenBranches = GetPerfEvtSelRegisterValue(0x79, 0x04, true, true, false, false, false, false, true, false, 1);
                    Ring0.WriteMsr(IA32_PERFEVTSEL3, notTakenBranches);
                }
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
                results.overallCounterValues = cpu.GetOverallCounterValues("DSB Uops", "DSB Uops cmask=1", "MITE Uops", "MITE Uops cmask=1");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "Op$ Hitrate", "Op$ Ops/C", "Op$ Active", "Decoder Ops/C", "Decoder Active", "Op$ Ops", "Decoder Ops" };

            public string GetHelpText() { return ""; }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float ipc = counterData.instr / counterData.activeCycles;
                return new string[] { label,
                    FormatLargeNumber(counterData.activeCycles),
                    FormatLargeNumber(counterData.instr),
                    string.Format("{0:F2}", ipc),
                    string.Format("{0:F2}%", 100 * counterData.pmc[0] / (counterData.pmc[0] + counterData.pmc[2])),
                    string.Format("{0:F2}", counterData.pmc[0] / counterData.pmc[1]),
                    string.Format("{0:F2}%", 100 * counterData.pmc[1] / counterData.activeCycles),
                    string.Format("{0:F2}", counterData.pmc[2] / counterData.pmc[3]),
                    string.Format("{0:F2}%", 100 * counterData.pmc[3] / counterData.activeCycles),
                    FormatLargeNumber(counterData.pmc[0]),
                    FormatLargeNumber(counterData.pmc[2])};
            }
        }

        public class OpDelivery : MonitoringConfig
        {
            private ModernIntelCpu cpu;
            public string GetConfigName() { return "Frontend Op Delivery"; }

            public OpDelivery(ModernIntelCpu intelCpu)
            {
                cpu = intelCpu;
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                cpu.EnablePerformanceCounters();

                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    ThreadAffinity.Set(1UL << threadIdx);
                    // Set PMC0 to count LSD uops
                    Ring0.WriteMsr(IA32_PERFEVTSEL0, GetPerfEvtSelRegisterValue(0xA8, 0x1, true, true, false, false, false, false, true, false, 0));

                    // Set PMC1 to count DSB uops
                    Ring0.WriteMsr(IA32_PERFEVTSEL1, GetPerfEvtSelRegisterValue(0x79, 0x8, true, true, false, false, false, false, true, false, 0));

                    // Set PMC2 to count MITE uops
                    Ring0.WriteMsr(IA32_PERFEVTSEL2, GetPerfEvtSelRegisterValue(0x79, 0x4, true, true, false, false, false, false, true, false, 0));

                    // Set PMC3 to count MS uops
                    Ring0.WriteMsr(IA32_PERFEVTSEL3, GetPerfEvtSelRegisterValue(0x79, 0x30, true, true, false, false, false, false, true, false, 0));
                }
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
                results.overallCounterValues = cpu.GetOverallCounterValues("LSD Ops", "DSB Ops", "MITE Ops", "MS Ops");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "LSD Ops", "LSD %", "Op$ Ops", "Op$ %", "Decoder Ops", "Decoder %", "MS Ops", "MS %" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float totalOps = counterData.pmc[0] + counterData.pmc[1] + counterData.pmc[2] + counterData.pmc[3];
                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        FormatLargeNumber(counterData.pmc[0]),
                        FormatPercentage(counterData.pmc[0], totalOps),
                        FormatLargeNumber(counterData.pmc[1]),
                        FormatPercentage(counterData.pmc[1], totalOps),
                        FormatLargeNumber(counterData.pmc[2]),
                        FormatPercentage(counterData.pmc[2], totalOps),
                        FormatLargeNumber(counterData.pmc[3]),
                        FormatPercentage(counterData.pmc[3], totalOps)
                };
            }
        }

        public class DecodeHistogram : MonitoringConfig
        {
            private ModernIntelCpu cpu;
            public string GetConfigName() { return "Decode Histogram"; }

            public DecodeHistogram(ModernIntelCpu intelCpu)
            {
                cpu = intelCpu;
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                cpu.EnablePerformanceCounters();

                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    ThreadAffinity.Set(1UL << threadIdx);
                    // MITE cmask 1
                    Ring0.WriteMsr(IA32_PERFEVTSEL0, GetPerfEvtSelRegisterValue(0x79, 0x4, true, true, false, false, false, false, true, false, 1));

                    // MITE cmask 2
                    Ring0.WriteMsr(IA32_PERFEVTSEL1, GetPerfEvtSelRegisterValue(0x79, 0x4, true, true, false, false, false, false, true, false, 2));

                    // MITE cmask 3
                    Ring0.WriteMsr(IA32_PERFEVTSEL2, GetPerfEvtSelRegisterValue(0x79, 0x4, true, true, false, false, false, false, true, false, 3));

                    // MITE cmaks 4
                    Ring0.WriteMsr(IA32_PERFEVTSEL3, GetPerfEvtSelRegisterValue(0x79, 0x4, true, true, false, false, false, false, true, false, 4));
                }
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[cpu.GetThreadCount()][];
                cpu.InitializeCoreTotals();
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    results.unitMetrics[threadIdx] = computeMetrics("Thread " + threadIdx, cpu.NormalizedThreadCounts[threadIdx], false);
                }

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts, true);
                results.overallCounterValues = cpu.GetOverallCounterValues("MITE cmask 1", "MITE cmask 2", "MITE cmask 3", "MITE cmask 4");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "Pkg Pwr", "Instr/Watt", "Decode Active", "Decode Ops", "Decode Ops/C", "1 Op", "2 Ops", "3 Ops", "4 Ops" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData, bool overall)
            {
                float oneOp = counterData.pmc[0] - counterData.pmc[1];
                float twoOps = counterData.pmc[1] - counterData.pmc[2];
                float threeOps = counterData.pmc[2] - counterData.pmc[3];
                float decoderOps = oneOp + 2 * twoOps + 3 * threeOps + 4 * counterData.pmc[3];
                return new string[] { label,
                       FormatLargeNumber(counterData.activeCycles),
                       FormatLargeNumber(counterData.instr),
                       string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                       overall ? string.Format("{0:F2} W", counterData.packagePower) : "N/A",
                       overall ? FormatLargeNumber(counterData.instr / counterData.packagePower) : "N/A",
                       FormatPercentage(counterData.pmc[0], counterData.activeCycles),
                       FormatLargeNumber(decoderOps),
                       string.Format("{0:F2}", decoderOps / counterData.pmc[0]),
                       FormatPercentage(oneOp, counterData.pmc[0]),
                       FormatPercentage(twoOps, counterData.pmc[0]),
                       FormatPercentage(threeOps, counterData.pmc[0]),
                       FormatPercentage(counterData.pmc[3], counterData.pmc[0]),
                };
            }
        }

        public class OCHistogram : MonitoringConfig
        {
            private ModernIntelCpu cpu;
            public string GetConfigName() { return "Op Cache Histogram"; }

            public OCHistogram(ModernIntelCpu intelCpu)
            {
                cpu = intelCpu;
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                cpu.EnablePerformanceCounters();

                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    ThreadAffinity.Set(1UL << threadIdx);
                    // DSB cmask 1
                    Ring0.WriteMsr(IA32_PERFEVTSEL0, GetPerfEvtSelRegisterValue(0x79, 0x8, true, true, false, false, false, false, true, false, 1));

                    // DSB cmask 2
                    Ring0.WriteMsr(IA32_PERFEVTSEL1, GetPerfEvtSelRegisterValue(0x79, 0x8, true, true, false, false, false, false, true, false, 2));

                    // DSB cmask 3
                    Ring0.WriteMsr(IA32_PERFEVTSEL2, GetPerfEvtSelRegisterValue(0x79, 0x8, true, true, false, false, false, false, true, false, 3));

                    // DSB cmaks 4
                    Ring0.WriteMsr(IA32_PERFEVTSEL3, GetPerfEvtSelRegisterValue(0x79, 0x8, true, true, false, false, false, false, true, false, 4));
                }
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[cpu.GetThreadCount()][];
                cpu.InitializeCoreTotals();
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    results.unitMetrics[threadIdx] = computeMetrics("Thread " + threadIdx, cpu.NormalizedThreadCounts[threadIdx], false);
                }

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts, true);
                results.overallCounterValues = cpu.GetOverallCounterValues("DSB cmask 1", "DSB cmask 2", "DSB cmask 3", "DSB cmask 4");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "Pkg Pwr", "Instr/Watt", "OC Active", "OC Ops", "OC Ops/C", "1 Op", "2 Ops", "3 Ops", "4 Ops" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData, bool overall)
            {
                float oneOp = counterData.pmc[0] - counterData.pmc[1];
                float twoOps = counterData.pmc[1] - counterData.pmc[2];
                float threeOps = counterData.pmc[2] - counterData.pmc[3];
                float opCacheOps = oneOp + 2 * twoOps + 3 * threeOps + 4 * counterData.pmc[3];
                return new string[] { label,
                       FormatLargeNumber(counterData.activeCycles),
                       FormatLargeNumber(counterData.instr),
                       string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                       overall ? string.Format("{0:F2} W", counterData.packagePower) : "N/A",
                       overall ? FormatLargeNumber(counterData.instr / counterData.packagePower) : "N/A",
                       FormatPercentage(counterData.pmc[0], counterData.activeCycles),
                       FormatLargeNumber(opCacheOps),
                       string.Format("{0:F2}", opCacheOps / counterData.pmc[0]),
                       FormatPercentage(oneOp, counterData.pmc[0]),
                       FormatPercentage(twoOps, counterData.pmc[0]),
                       FormatPercentage(threeOps, counterData.pmc[0]),
                       FormatPercentage(counterData.pmc[3], counterData.pmc[0]),
                };
            }
        }

        public class L1DFill : MonitoringConfig
        {
            private ModernIntelCpu cpu;
            public string GetConfigName() { return "L1D Fill"; }

            public L1DFill(ModernIntelCpu intelCpu)
            {
                cpu = intelCpu;
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                cpu.EnablePerformanceCounters();

                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    ThreadAffinity.Set(1UL << threadIdx);
                    // PMC0 - L1D Replacements
                    Ring0.WriteMsr(IA32_PERFEVTSEL0, GetPerfEvtSelRegisterValue(0x51, 0x1, true, true, false, false, false, false, true, false, 0));

                    // PMC1 - fb full
                    Ring0.WriteMsr(IA32_PERFEVTSEL1, GetPerfEvtSelRegisterValue(0x48, 0x2, true, true, false, false, false, false, true, false, 0));

                    // PMC2 - pending misses (ctr2 only)
                    Ring0.WriteMsr(IA32_PERFEVTSEL2, GetPerfEvtSelRegisterValue(0x48, 0x1, true, true, false, false, false, false, true, false, 0));

                    // PMC2 - fb full cycles (cmask=1)
                    Ring0.WriteMsr(IA32_PERFEVTSEL3, GetPerfEvtSelRegisterValue(0x48, 0x2, true, true, false, false, false, false, true, false, 1));
                }
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
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "L1D Fill BW", "Pending Misses", "FB Full", "FB Full Cycles", "L1D Fill Latency" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float totalOps = counterData.pmc[0] + counterData.pmc[1] + counterData.pmc[2] + counterData.pmc[3];
                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        FormatLargeNumber(counterData.pmc[0] * 64) + "B",
                        string.Format("{0:F2}", counterData.pmc[2] / counterData.activeCycles),
                        FormatLargeNumber(counterData.pmc[1]),
                        string.Format("{0:F2}%", 100 * counterData.pmc[3] / counterData.activeCycles),
                        string.Format("{0:F2} clk", counterData.pmc[2] / counterData.pmc[0])
                };
            }
        }

        // undocumented performance counters for Haswell and Skylake
        public class ResourceStalls1 : MonitoringConfig
        {
            private ModernIntelCpu cpu;
            public string GetConfigName() { return "Dispatch Stalls 1"; }

            public ResourceStalls1(ModernIntelCpu intelCpu)
            {
                cpu = intelCpu;
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(GetPerfEvtSelRegisterValue(0xA2, 0x2, true, true, false, false, false, false, true, false, 0), // LB full
                    GetPerfEvtSelRegisterValue(0xA2, 0x40, true, true, false, false, false, false, true, false, 0),  // mem rs full
                    GetPerfEvtSelRegisterValue(0x5B, 0x4, true, true, false, false, false, false, true, false, 0),   // integer RF
                    GetPerfEvtSelRegisterValue(0x5B, 0x8, true, true, false, false, false, false, true, false, 0));
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
                results.overallCounterValues = cpu.GetOverallCounterValues("LB Full", "Mem RS Full?", "INT RF Full?", "FP RF Full?");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "Pkg Pwr", "Instr/Watt", "LB Full", "Mem RS Full?", "INT RF Full?", "FP RF Full?" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2} W", counterData.packagePower),
                        FormatLargeNumber(counterData.instr / counterData.packagePower),
                        string.Format("{0:F2}%", 100 * counterData.pmc[0] / counterData.activeCycles),
                        string.Format("{0:F2}%", 100 * counterData.pmc[1] / counterData.activeCycles),
                        string.Format("{0:F2}%", 100 * counterData.pmc[2] / counterData.activeCycles),
                        string.Format("{0:F2}%", 100 * counterData.pmc[3] / counterData.activeCycles)
                };
            }
        }

        public class OffcoreBw : MonitoringConfig
        {
            private ModernIntelCpu cpu;
            public string GetConfigName() { return "Offcore BW (Burst)"; }

            public OffcoreBw(ModernIntelCpu intelCpu)
            {
                cpu = intelCpu;
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                cpu.EnablePerformanceCounters();

                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    ThreadAffinity.Set(1UL << threadIdx);
                    // cmask 4
                    Ring0.WriteMsr(IA32_PERFEVTSEL0, GetPerfEvtSelRegisterValue(0x60, 0x8 | 0x2, true, true, false, false, false, false, true, false, cmask: 4));

                    // cmask 8
                    Ring0.WriteMsr(IA32_PERFEVTSEL1, GetPerfEvtSelRegisterValue(0x60, 0x8 | 0x2, true, true, false, false, false, false, true, false, cmask: 8));

                    // cmask 12
                    Ring0.WriteMsr(IA32_PERFEVTSEL2, GetPerfEvtSelRegisterValue(0x60, 0x8 | 0x2, true, true, false, false, false, false, true, false, cmask: 12));

                    // cmask 16
                    Ring0.WriteMsr(IA32_PERFEVTSEL3, GetPerfEvtSelRegisterValue(0x60, 0x8 | 0x2, true, true, false, false, false, false, true, false, cmask: 16));
                }
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[cpu.GetThreadCount()][];
                cpu.InitializeCoreTotals();
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    results.unitMetrics[threadIdx] = computeMetrics("Thread " + threadIdx, cpu.NormalizedThreadCounts[threadIdx], false);
                }

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts, true);
                results.overallCounterValues = cpu.GetOverallCounterValues("offcore req cmask 4", "offcore req cmask 8", "offcore req cmask 12", "offcore req cmask 16");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "Pkg Pwr", "Instr/Watt", "offcore req cmask 4", "offcore req cmask 8", "offcore req cmask 12", "offcore req cmask 16" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData, bool overall)
            {
                float oneOp = counterData.pmc[0] - counterData.pmc[1];
                float twoOps = counterData.pmc[1] - counterData.pmc[2];
                float threeOps = counterData.pmc[2] - counterData.pmc[3];
                float opCacheOps = oneOp + 2 * twoOps + 3 * threeOps + 4 * counterData.pmc[3];
                return new string[] { label,
                       FormatLargeNumber(counterData.activeCycles),
                       FormatLargeNumber(counterData.instr),
                       string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                       overall ? string.Format("{0:F2} W", counterData.packagePower) : "N/A",
                       overall ? FormatLargeNumber(counterData.instr / counterData.packagePower) : "N/A",
                       FormatPercentage(counterData.pmc[0], counterData.activeCycles),
                       FormatPercentage(counterData.pmc[1], counterData.activeCycles),
                       FormatPercentage(counterData.pmc[2], counterData.activeCycles),
                       FormatPercentage(counterData.pmc[3], counterData.activeCycles),
                };
            }
        }

        public class ArchitecturalCounters : MonitoringConfig
        {
            private ModernIntelCpu cpu;
            public string GetConfigName() { return "Arch Counters"; }

            public ArchitecturalCounters(ModernIntelCpu intelCpu)
            {
                cpu = intelCpu;
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                ulong[] pmc = new ulong[4];
                pmc[0] = GetPerfEvtSelRegisterValue(0xC4, 0x00); // Retired Branches
                pmc[1] = GetPerfEvtSelRegisterValue(0xC5, 0x00); // Mispredicted Branches
                pmc[2] = GetPerfEvtSelRegisterValue(0x2E, 0x4F); // LLC References
                pmc[3] = GetPerfEvtSelRegisterValue(0x2E, 0x41); // LLC Misses
                cpu.ProgramPerfCounters(pmc);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[cpu.GetThreadCount()][];
                cpu.InitializeCoreTotals();
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    results.unitMetrics[threadIdx] = computeMetrics("Thread " + threadIdx, cpu.NormalizedThreadCounts[threadIdx], null);
                }

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts, cpu.RawTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues("Retired Branches", "Retired Misp Branches", "LLC References", "LLC Misses");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "BPU Accuracy", "Branch MPKI", "% Branches", "LLC Hitrate", "LLC MPKI", "LLC References", "LLC Hit BW", "Total Instructions" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData, RawTotalCoreCounterData totals)
            {
                float totalOps = counterData.pmc[0] + counterData.pmc[1] + counterData.pmc[2] + counterData.pmc[3];
                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2}%", 100 * (1 - counterData.pmc[1] / counterData.pmc[0])),
                        string.Format("{0:F2}", 1000 * counterData.pmc[1] / counterData.instr),
                        string.Format("{0:F2}%", 100 * counterData.pmc[0] / counterData.instr),
                        FormatPercentage((counterData.pmc[2] - counterData.pmc[3]), counterData.pmc[2]),
                        string.Format("{0:F2}", 1000 * counterData.pmc[3] / counterData.instr),
                        FormatLargeNumber(counterData.pmc[3]),
                        FormatLargeNumber(64 * (counterData.pmc[2] - counterData.pmc[3])) + "B/s",
                        totals == null ? "-" : FormatLargeNumber(totals.instr)
                };
            }
        }

        public class RetireHistogram : MonitoringConfig
        {
            private ModernIntelCpu cpu;
            public string GetConfigName() { return "Instr Retire Histogram"; }

            public RetireHistogram(ModernIntelCpu intelCpu)
            {
                cpu = intelCpu;
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                cpu.EnablePerformanceCounters();

                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    ThreadAffinity.Set(1UL << threadIdx);
                    // Retired instructions, cmask 1,2,3,4
                    Ring0.WriteMsr(IA32_PERFEVTSEL0, GetPerfEvtSelRegisterValue(0xC0, 0, true, true, false, false, false, false, true, false, 1));
                    Ring0.WriteMsr(IA32_PERFEVTSEL1, GetPerfEvtSelRegisterValue(0xC0, 0, true, true, false, false, false, false, true, false, 2));
                    Ring0.WriteMsr(IA32_PERFEVTSEL2, GetPerfEvtSelRegisterValue(0xC0, 0, true, true, false, false, false, false, true, false, 3));
                    Ring0.WriteMsr(IA32_PERFEVTSEL3, GetPerfEvtSelRegisterValue(0xC0, 0, true, true, false, false, false, false, true, false, 4));
                }
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
                results.overallCounterValues = cpu.GetOverallCounterValues("Instructions Retired cmask 1", "Instructions retired cmask 2", "Instructions retired cmask 3", "Instructions retired cmask 4");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "Retire Active", "1 Instr", "2 Instrs", "3 Instrs", ">= 4 Instrs" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2}%", 100 * counterData.pmc[0] / counterData.activeCycles),
                        string.Format("{0:F2}%", 100 * (counterData.pmc[0] - counterData.pmc[1]) / counterData.activeCycles),
                        string.Format("{0:F2}%", 100 * (counterData.pmc[1] - counterData.pmc[2]) / counterData.activeCycles),
                        string.Format("{0:F2}%", 100 * (counterData.pmc[2] - counterData.pmc[3]) / counterData.activeCycles),
                        string.Format("{0:F2}%", 100 * counterData.pmc[3] / counterData.activeCycles)
                };
            }
        }
    }
}
