using PmcReader.Interop;
using System;

namespace PmcReader.Intel
{
    /// <summary>
    /// The uncore from hell?
    /// </summary>
    public class SandyBridgeEL3 : ModernIntelCpu
    {
        // Sandy Bridge server uncore always has 8 CBos.
        // Even if some cache slices are disabled, the CBos are still 
        // active and take ring traffic (even if lookups/snoops count 0)
        // ok this manual is bs, those two disabled CBos give batshit insane counts
        public const uint CboCount = 6; // set to real number of CBos

        public const uint MSR_UNC_CBO_increment = 0x20;
        public const uint C0_MSR_PMON_CTR0 = 0xD16;
        public const uint C0_MSR_PMON_CTR1 = 0xD17;
        public const uint C0_MSR_PMON_CTR2 = 0xD18;
        public const uint C0_MSR_PMON_CTR3 = 0xD19;
        public const uint C0_MSR_PMON_BOX_FILTER = 0xD14;
        public const uint C0_MSR_PMON_CTL0 = 0xD10;
        public const uint C0_MSR_PMON_CTL1 = 0xD11;
        public const uint C0_MSR_PMON_CTL2 = 0xD12;
        public const uint C0_MSR_PMON_CTL3 = 0xD13;
        public const uint C0_MSR_PMON_BOX_CTL = 0xD04;

        // Constants for box filter register
        /// <summary>
        /// LLC lookup, line found in Invalid state (miss)
        /// </summary>
        public const byte LLC_LOOKUP_I = 0b00001;

        /// <summary>
        /// LLC lookup, line found in Shared state
        /// </summary>
        public const byte LLC_LOOKUP_S = 0b00010;

        /// <summary>
        /// LLC lookup, line found in Exclusive state
        /// </summary>
        public const byte LLC_LOOKUP_E = 0b00100;

        /// <summary>
        /// LLC lookup, line found in Modified state
        /// </summary>
        public const byte LLC_LOOKUP_M = 0b01000;

        /// <summary>
        /// LLC lookup, line found in Forward state
        /// </summary>
        public const byte LLC_LOOKUP_F = 0b10000;

        // Constants for ring counter umasks
        /// <summary>
        /// Traffic on up ring, even polarity
        /// </summary>
        public const byte RING_UP_EVEN = 0b0001;

        /// <summary>
        /// Traffic on up ring, odd polarity
        /// </summary>
        public const byte RING_UP_ODD  = 0b0010;

        /// <summary>
        /// Traffic on down ring, even polarity
        /// </summary>
        public const byte RING_DN_EVEN = 0b0100;

        /// <summary>
        /// Traffic on down ring, odd polarity
        /// </summary>
        public const byte RING_DN_ODD  = 0b1000;

        public NormalizedCboCounterData[] cboData;
        public NormalizedCboCounterData cboTotals;

        public SandyBridgeEL3()
        {
            architectureName = "Sandy Bridge E L3 Cache";
            cboTotals = new NormalizedCboCounterData();
            cboData = new NormalizedCboCounterData[CboCount];
            monitoringConfigs = new MonitoringConfig[8];
            monitoringConfigs[0] = new HitsBlConfig(this);
            monitoringConfigs[1] = new RxRConfig(this);
            monitoringConfigs[2] = new DataReadLatency(this);
            monitoringConfigs[3] = new MissesAdConfig(this);
            monitoringConfigs[4] = new LLCVictimsAndIvRing(this);
            monitoringConfigs[5] = new BouncesAndAkRing(this);
            monitoringConfigs[6] = new DataReadMissLatency(this);
            monitoringConfigs[7] = new ToR(this);
        }

        public class NormalizedCboCounterData
        {
            public float ctr0;
            public float ctr1;
            public float ctr2;
            public float ctr3;
        }

        /// <summary>
        /// Enable and set up Jaketown CBo counters
        /// </summary>
        /// <param name="ctr0">Counter 0 control</param>
        /// <param name="ctr1">Counter 1 control</param>
        /// <param name="ctr2">Counter 2 control</param>
        /// <param name="ctr3">Counter 3 control</param>
        /// <param name="filter">Box filter control</param>
        public void SetupMonitoringSession(ulong ctr0, ulong ctr1, ulong ctr2, ulong ctr3, ulong filter)
        {
            for (uint cboIdx = 0; cboIdx < 8; cboIdx++)
            {
                EnableBoxFreeze(cboIdx);
                FreezeBoxCounters(cboIdx);
                Ring0.WriteMsr(C0_MSR_PMON_CTL0 + MSR_UNC_CBO_increment * cboIdx, ctr0);
                Ring0.WriteMsr(C0_MSR_PMON_CTL1 + MSR_UNC_CBO_increment * cboIdx, ctr1);
                Ring0.WriteMsr(C0_MSR_PMON_CTL2 + MSR_UNC_CBO_increment * cboIdx, ctr2);
                Ring0.WriteMsr(C0_MSR_PMON_CTL3 + MSR_UNC_CBO_increment * cboIdx, ctr3);
                Ring0.WriteMsr(C0_MSR_PMON_BOX_FILTER + MSR_UNC_CBO_increment * cboIdx, filter);
                ClearBoxCounters(cboIdx);
                UnFreezeBoxCounters(cboIdx);
            }
        }

        public void InitializeCboTotals()
        {
            cboTotals.ctr0 = 0;
            cboTotals.ctr1 = 0;
            cboTotals.ctr2 = 0;
            cboTotals.ctr3 = 0;
        }

        /// <summary>
        /// Read Jaketown CBo counters
        /// </summary>
        /// <param name="cboIdx">CBo index</param>
        public void UpdateCboCounterData(uint cboIdx)
        {
            float normalizationFactor = GetNormalizationFactor((int)cboIdx);
            ulong ctr0, ctr1, ctr2, ctr3;
            FreezeBoxCounters(cboIdx);
            Ring0.ReadMsr(C0_MSR_PMON_CTR0 + MSR_UNC_CBO_increment * cboIdx, out ctr0);
            Ring0.ReadMsr(C0_MSR_PMON_CTR1 + MSR_UNC_CBO_increment * cboIdx, out ctr1);
            Ring0.ReadMsr(C0_MSR_PMON_CTR2 + MSR_UNC_CBO_increment * cboIdx, out ctr2);
            Ring0.ReadMsr(C0_MSR_PMON_CTR3 + MSR_UNC_CBO_increment * cboIdx, out ctr3);
            ClearBoxCounters(cboIdx);
            UnFreezeBoxCounters(cboIdx);

            if (cboData[cboIdx] == null)
            {
                cboData[cboIdx] = new NormalizedCboCounterData();
            }

            cboData[cboIdx].ctr0 = ctr0 * normalizationFactor;
            cboData[cboIdx].ctr1 = ctr1 * normalizationFactor;
            cboData[cboIdx].ctr2 = ctr2 * normalizationFactor;
            cboData[cboIdx].ctr3 = ctr3 * normalizationFactor;
            cboTotals.ctr0 += cboData[cboIdx].ctr0;
            cboTotals.ctr1 += cboData[cboIdx].ctr1;
            cboTotals.ctr2 += cboData[cboIdx].ctr2;
            cboTotals.ctr3 += cboData[cboIdx].ctr3;
        }

        /// <summary>
        /// Enable counter freeze signal for CBo
        /// </summary>
        /// <param name="cboIdx">CBo index</param>
        public void EnableBoxFreeze(uint cboIdx)
        {
            if (cboIdx >= CboCount) return;
            ulong freezeEnableValue = GetUncoreBoxCtlRegisterValue(false, false, false, true);
            Ring0.WriteMsr(C0_MSR_PMON_BOX_CTL + MSR_UNC_CBO_increment * cboIdx, freezeEnableValue);
        }

        public void FreezeBoxCounters(uint cboIdx)
        {
            if (cboIdx >= CboCount) return;
            ulong freezeValue = GetUncoreBoxCtlRegisterValue(false, false, true, true);
            Ring0.WriteMsr(C0_MSR_PMON_BOX_CTL + MSR_UNC_CBO_increment * cboIdx, freezeValue);
        }

        public void UnFreezeBoxCounters(uint cboIdx)
        {
            if (cboIdx >= CboCount) return;
            ulong freezeValue = GetUncoreBoxCtlRegisterValue(false, false, false, true);
            Ring0.WriteMsr(C0_MSR_PMON_BOX_CTL + MSR_UNC_CBO_increment * cboIdx, freezeValue);
        }

        public void ClearBoxCounters(uint cboIdx)
        {
            if (cboIdx >= CboCount) return;
            ulong freezeValue = GetUncoreBoxCtlRegisterValue(false, true, false, true);
            Ring0.WriteMsr(C0_MSR_PMON_BOX_CTL + MSR_UNC_CBO_increment * cboIdx, freezeValue);
        }

        /// <summary>
        /// Get value to put in PERFEVTSEL register, for uncore counters
        /// </summary>
        /// <param name="perfEvent">Perf event</param>
        /// <param name="umask">Perf event qualification (umask)</param>
        /// <param name="reset">Reset counter to 0</param>
        /// <param name="edge">Edge detect</param>
        /// <param name="tid_en">Enable threadId filter</param>
        /// <param name="enable">Enable counter</param>
        /// <param name="invert">Invert cmask</param>
        /// <param name="cmask">Count mask</param>
        /// <returns>value to put in perfevtsel register</returns>
        public static ulong GetUncorePerfEvtSelRegisterValue(byte perfEvent,
            byte umask,
            bool reset,
            bool edge,
            bool tid_en,
            bool enable,
            bool invert,
            byte cmask)
        {
            return perfEvent |
                (ulong)umask << 8 |
                (reset ? 1UL : 0UL) << 17 | 
                (edge ? 1UL : 0UL) << 18 |
                (tid_en ? 1UL : 0UL) << 19 | 
                (enable ? 1UL : 0UL) << 22 |
                (invert ? 1UL : 0UL) << 23 |
                (ulong)(cmask) << 24;
        }

        /// <summary>
        /// Get value to put in PMON_BOX_FILTER register
        /// </summary>
        /// <param name="tid">If tid_en for counter ctl register is 1, bit 0 = thread 1/0, bits 1-3 = core id</param>
        /// <param name="nodeId">node mask. 0x1 = NID 0, 0x2 = NID 1, etc</param>
        /// <param name="state">For LLC lookups, line state</param>
        /// <param name="opcode">Match ingress request queue opcodes</param>
        /// <returns>Value to put in filter register</returns>
        public static ulong GetUncoreFilterRegisterValue(byte tid,
            byte nodeId,
            byte state,
            uint opcode)
        {
            return tid |
                (ulong)nodeId << 10 |
                (ulong)state << 18 |
                (ulong)opcode << 23;
        }

        /// <summary>
        /// Get value to put in PMON_BOX_CTL register
        /// </summary>
        /// <param name="rstCtrl">Reset all box control registers to 0</param>
        /// <param name="rstCtrs">Reset all box counter registers to 0</param>
        /// <param name="freeze">Freeze all box counters, if freeze enabled</param>
        /// <param name="freezeEnable">Allow freeze signal</param>
        /// <returns>Value to put in PMON_BOX_CTL register</returns>
        public static ulong GetUncoreBoxCtlRegisterValue(bool rstCtrl,
            bool rstCtrs,
            bool freeze,
            bool freezeEnable)
        {
            return (rstCtrl ? 1UL : 0UL) |
                (rstCtrs ? 1UL : 0UL) << 1 |
                (freeze ? 1UL : 0UL) << 8 |
                (freezeEnable ? 1UL : 0UL) << 16;
        }

        public Tuple<string, float>[] GetOverallL3CounterValues(string ctr0, string ctr1, string ctr2, string ctr3)
        {
            Tuple<string, float>[] retval = new Tuple<string, float>[4];
            retval[0] = new Tuple<string, float>(ctr0, cboTotals.ctr0);
            retval[1] = new Tuple<string, float>(ctr1, cboTotals.ctr1);
            retval[2] = new Tuple<string, float>(ctr2, cboTotals.ctr2);
            retval[3] = new Tuple<string, float>(ctr3, cboTotals.ctr3);
            return retval;
        }

        public class HitsBlConfig : MonitoringConfig
        {
            private SandyBridgeEL3 cpu;
            public string GetConfigName() { return "L3 Hits and Data Ring"; }

            public HitsBlConfig(SandyBridgeEL3 intelCpu)
            {
                cpu = intelCpu;
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                // umask 0b1 = filter (mandatory), 0b10 = data read, 0b100 = write, 0b1000 = remote snoop. LLC lookup must go in ctr0 or ctr1
                ulong clockticks = GetUncorePerfEvtSelRegisterValue(0, 0, false, false, false, true, false, 0);
                ulong llcLookup = GetUncorePerfEvtSelRegisterValue(0x34, 0xF, false, false, false, true, false, 0);
                // 0x1D = BL ring (block/data ring) used cycles, 0b1 = up direction even polarity. 0b10 = up direction odd polarity. must go in ctr2 or ctr3
                ulong blRingUp = GetUncorePerfEvtSelRegisterValue(0x1D, RING_UP_EVEN | RING_UP_ODD, false, false, false, true, false, 0);
                // 0b100 = down direction even polarity, 0b1000 = down direction odd polarity
                ulong blRingDn = GetUncorePerfEvtSelRegisterValue(0x1D, RING_DN_EVEN | RING_DN_ODD, false, false, false, true, false, 0);
                ulong filter = GetUncoreFilterRegisterValue(0, 0x1, LLC_LOOKUP_E | LLC_LOOKUP_F | LLC_LOOKUP_M | LLC_LOOKUP_S, 0);
                cpu.SetupMonitoringSession(clockticks, llcLookup, blRingUp, blRingDn, filter);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[CboCount][];
                cpu.InitializeCboTotals();
                for (uint cboIdx = 0; cboIdx < CboCount; cboIdx++)
                {
                    cpu.UpdateCboCounterData(cboIdx);
                    results.unitMetrics[cboIdx] = computeMetrics("CBo " + cboIdx, cpu.cboData[cboIdx]);
                }

                results.overallMetrics = computeMetrics("Overall", cpu.cboTotals);
                results.overallCounterValues = cpu.GetOverallL3CounterValues("Clockticks", "LLC Hit", "BL Up Cycles", "BL Down Cycles");
                return results;
            }

            public string[] columns = new string[] { "Item", "Clk", "Hit BW", "MESF state", "Ring Stop Traffic", "BL Up Cycles", "BL Dn Cycles" };

            public string GetHelpText() { return ""; }

            private string[] computeMetrics(string label, NormalizedCboCounterData counterData)
            {
                return new string[] { label,
                    FormatLargeNumber(counterData.ctr0),
                    FormatLargeNumber(counterData.ctr1 * 64) + "B/s",
                    FormatLargeNumber(counterData.ctr1),
                    FormatLargeNumber((counterData.ctr2 + counterData.ctr3) * 32) + "B/s", 
                    string.Format("{0:F2}%", 100* counterData.ctr2 / counterData.ctr0),
                    string.Format("{0:F2}%", 100* counterData.ctr3 / counterData.ctr0),
                };
            }
        }

        public class RxRConfig : MonitoringConfig
        {
            private SandyBridgeEL3 cpu;
            public string GetConfigName() { return "Ingress Queue"; }

            public RxRConfig(SandyBridgeEL3 intelCpu)
            {
                cpu = intelCpu;
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                // no counter restrictions for clockticks
                ulong clockticks = GetUncorePerfEvtSelRegisterValue(0, 0, false, false, false, true, false, 0);
                // 0x11 = ingress occupancy. umask 1 = ingress request queue (core requests). must be in ctr0
                ulong rxrOccupancy = GetUncorePerfEvtSelRegisterValue(0x11, 1, false, false, false, true, false, 0);
                // 0x13 = ingress allocations. umask = 1 = irq (Ingress Request Queue = core requests). must be in ctr0 or ctr1
                ulong rxrInserts = GetUncorePerfEvtSelRegisterValue(0x13, 1, false, false, false, true, false, 0);
                // 0x1F = counter 0 occupancy. cmask = 1 to count cycles when ingress queue isn't empty
                ulong rxrEntryPresent = GetUncorePerfEvtSelRegisterValue(0x1F, 0xFF, false, false, false, true, false, 1);
                ulong filter = GetUncoreFilterRegisterValue(0, 0x1, LLC_LOOKUP_E | LLC_LOOKUP_F | LLC_LOOKUP_M | LLC_LOOKUP_S, 0);
                cpu.SetupMonitoringSession(rxrOccupancy, rxrInserts, clockticks, rxrEntryPresent, filter);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[CboCount][];
                cpu.InitializeCboTotals();
                for (uint cboIdx = 0; cboIdx < CboCount; cboIdx++)
                {
                    cpu.UpdateCboCounterData(cboIdx);
                    results.unitMetrics[cboIdx] = computeMetrics("CBo " + cboIdx, cpu.cboData[cboIdx]);
                }

                results.overallMetrics = computeMetrics("Overall", cpu.cboTotals);
                results.overallCounterValues = cpu.GetOverallL3CounterValues("Ingress Occupancy", "Ingress Inserts", "Clockticks", "Ingress Entry Present");
                return results;
            }

            public string[] columns = new string[] { "Item", "Clk", "IngressQ Occupancy", "IngressQ Alloc", "IngressQ Latency", "IngressQ not empty" };

            public string GetHelpText() { return ""; }

            private string[] computeMetrics(string label, NormalizedCboCounterData counterData)
            {
                return new string[] { label,
                    FormatLargeNumber(counterData.ctr2),
                    string.Format("{0:F2}", counterData.ctr0 / counterData.ctr3),
                    FormatLargeNumber(counterData.ctr1),
                    string.Format("{0:F2} clk", counterData.ctr0 / counterData.ctr1),
                    string.Format("{0:F2}%", 100 * counterData.ctr3 / counterData.ctr2)
                };
            }
        }

        public class DataReadLatency : MonitoringConfig
        {
            private SandyBridgeEL3 cpu;
            public string GetConfigName() { return "ToR, Data Read Latency"; }

            public DataReadLatency(SandyBridgeEL3 intelCpu)
            {
                cpu = intelCpu;
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                // no counter restrictions for clockticks
                ulong clockticks = GetUncorePerfEvtSelRegisterValue(0, 0, false, false, false, true, false, 0);
                // 0x36 = tor occupancy, 0x1 = use opcode filter. 
                ulong torOccupancy = GetUncorePerfEvtSelRegisterValue(0x36, 1, false, false, false, true, false, 0);
                // 0x35 = tor inserts, 0x1 = use opcode filter
                ulong torInserts = GetUncorePerfEvtSelRegisterValue(0x35, 1, false, false, false, true, false, 0);
                // 0x1F = counter 0 occupancy. cmask = 1 to count cycles when data read is present
                ulong drdPresent = GetUncorePerfEvtSelRegisterValue(0x1F, 0xFF, false, false, false, true, false, 1);
                // opcode 0x182 = demand data read, but opcode field is only 8 bits wide. wtf.
                // try with just lower 8 bits
                ulong filter = GetUncoreFilterRegisterValue(0, 0x1, LLC_LOOKUP_E | LLC_LOOKUP_F | LLC_LOOKUP_M | LLC_LOOKUP_S, 0x182);
                cpu.SetupMonitoringSession(torOccupancy, torInserts, clockticks, drdPresent, filter);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[CboCount][];
                cpu.InitializeCboTotals();
                for (uint cboIdx = 0; cboIdx < CboCount; cboIdx++)
                {
                    cpu.UpdateCboCounterData(cboIdx);
                    results.unitMetrics[cboIdx] = computeMetrics("CBo " + cboIdx, cpu.cboData[cboIdx], false);
                }

                results.overallMetrics = computeMetrics("Overall", cpu.cboTotals, true);
                results.overallCounterValues = cpu.GetOverallL3CounterValues("ToR Occupancy", "ToR Inserts", "Clockticks", "DRD Present");
                return results;
            }

            public string[] columns = new string[] { "Item", "Clk", "ToR DRD Occupancy", "DRD Latency", "DRD Latency", "DRD Present", "DRD ToR Insert" };
            public string GetHelpText() { return ""; }

            private string[] computeMetrics(string label, NormalizedCboCounterData counterData, bool total)
            {
                float avgClock = total ? counterData.ctr2 / CboCount : counterData.ctr2;
                float missLatency = counterData.ctr0 / counterData.ctr1;
                return new string[] { label,
                    FormatLargeNumber(counterData.ctr2),
                    string.Format("{0:F2}", counterData.ctr0 / (total ? counterData.ctr2 / CboCount : counterData.ctr2)),
                    string.Format("{0:F2} clk", missLatency),
                    string.Format("{0:F2} ns", (1000000000 / avgClock) * missLatency),
                    string.Format("{0:F2}%", 100 * counterData.ctr3 / counterData.ctr2),
                    FormatLargeNumber(counterData.ctr1)
                };
            }
        }

        public class DataReadMissLatency : MonitoringConfig
        {
            private SandyBridgeEL3 cpu;
            public string GetConfigName() { return "ToR, Data Read Miss Latency"; }

            public DataReadMissLatency(SandyBridgeEL3 intelCpu)
            {
                cpu = intelCpu;
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                // no counter restrictions for clockticks
                ulong clockticks = GetUncorePerfEvtSelRegisterValue(0, 0, false, false, false, true, false, 0);
                // 0x36 = tor occupancy, 0b11 = miss transactions, and use opcode filter
                ulong torOccupancy = GetUncorePerfEvtSelRegisterValue(0x36, 0b11, false, false, false, true, false, 0);
                // 0x35 = tor inserts, 0b11 = miss transactions, use opcode filter
                ulong torInserts = GetUncorePerfEvtSelRegisterValue(0x35, 0b11, false, false, false, true, false, 0);
                // 0x1F = counter 0 occupancy. cmask = 1 to count cycles when data read is present
                ulong missPresent = GetUncorePerfEvtSelRegisterValue(0x1F, 0xFF, false, false, false, true, false, 1);
                // opcode 0x182 = demand data read, but opcode field is only 8 bits wide. wtf.
                // try with just lower 8 bits
                ulong filter = GetUncoreFilterRegisterValue(0, 0x1, LLC_LOOKUP_E | LLC_LOOKUP_F | LLC_LOOKUP_M | LLC_LOOKUP_S, 0x182);
                cpu.SetupMonitoringSession(torOccupancy, torInserts, clockticks, missPresent, filter);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[CboCount][];
                cpu.InitializeCboTotals();
                for (uint cboIdx = 0; cboIdx < CboCount; cboIdx++)
                {
                    cpu.UpdateCboCounterData(cboIdx);
                    results.unitMetrics[cboIdx] = computeMetrics("CBo " + cboIdx, cpu.cboData[cboIdx], false);
                }

                results.overallMetrics = computeMetrics("Overall", cpu.cboTotals, true);
                results.overallCounterValues = cpu.GetOverallL3CounterValues("ToR Occupancy", "ToR Inserts", "Clockticks", "Miss Present");
                return results;
            }

            public string GetHelpText() { return ""; }

            public string[] columns = new string[] { "Item", "Clk", "ToR DRD Miss Occupancy", "DRD Miss Latency", "DRD Miss Latency", "DRD Miss Present", "DRD Miss ToR Insert" };

            private string[] computeMetrics(string label, NormalizedCboCounterData counterData, bool total)
            {
                float avgClock = total ? counterData.ctr2 / CboCount : counterData.ctr2;
                float missLatency = counterData.ctr0 / counterData.ctr1;
                return new string[] { label,
                    FormatLargeNumber(counterData.ctr2),
                    string.Format("{0:F2}", counterData.ctr0 / (total ? counterData.ctr2 / CboCount : counterData.ctr2)),
                    string.Format("{0:F2} clk", missLatency),
                    string.Format("{0:F2} ns", (1000000000 / avgClock) * missLatency),
                    string.Format("{0:F2}%", 100 * counterData.ctr3 / counterData.ctr2),
                    FormatLargeNumber(counterData.ctr1)
                };
            }
        }

        public class ToR : MonitoringConfig
        {
            private SandyBridgeEL3 cpu;
            public string GetConfigName() { return "ToR, All Requests"; }

            public ToR(SandyBridgeEL3 intelCpu)
            {
                cpu = intelCpu;
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                // no counter restrictions for clockticks
                ulong clockticks = GetUncorePerfEvtSelRegisterValue(0, 0, false, false, false, true, false, 0);
                // 0x36 = tor occupancy, 0b1000 = all valid ToR entries
                ulong torOccupancy = GetUncorePerfEvtSelRegisterValue(0x36, 0b1000, false, false, false, true, false, 0);
                // 0x35 = tor inserts. 0b1000 not documented but other umasks are the same so let's try
                ulong torInserts = GetUncorePerfEvtSelRegisterValue(0x35, 0b1000, false, false, false, true, false, 0);
                // 0x1F = counter 0 occupancy. cmask = 1 to count cycles when there are valid entries in the ToR
                ulong missPresent = GetUncorePerfEvtSelRegisterValue(0x1D, 0xFF, false, false, false, true, false, 1);
                // filter not used, low bit of umask not set for ToR events
                ulong filter = GetUncoreFilterRegisterValue(0, 0x1, LLC_LOOKUP_E | LLC_LOOKUP_F | LLC_LOOKUP_M | LLC_LOOKUP_S, 0x182);
                cpu.SetupMonitoringSession(torOccupancy, torInserts, clockticks, missPresent, filter);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[CboCount][];
                cpu.InitializeCboTotals();
                for (uint cboIdx = 0; cboIdx < CboCount; cboIdx++)
                {
                    cpu.UpdateCboCounterData(cboIdx);
                    results.unitMetrics[cboIdx] = computeMetrics("CBo " + cboIdx, cpu.cboData[cboIdx], false);
                }

                results.overallMetrics = computeMetrics("Overall", cpu.cboTotals, true);
                results.overallCounterValues = cpu.GetOverallL3CounterValues("ToR Occupancy", "ToR Inserts", "Clockticks", "ToR Entry Present");
                return results;
            }

            public string[] columns = new string[] { "Item", "Clk", "ToR Occupancy", "Req Latency", "Req Latency", "Req Present", "ToR Insert" };

            public string GetHelpText() { return ""; }

            private string[] computeMetrics(string label, NormalizedCboCounterData counterData, bool total)
            {
                float avgClock = total ? counterData.ctr2 / CboCount : counterData.ctr2;
                float missLatency = counterData.ctr0 / counterData.ctr1;
                return new string[] { label,
                    FormatLargeNumber(counterData.ctr2),
                    string.Format("{0:F2}", counterData.ctr0 / counterData.ctr3),
                    string.Format("{0:F2} clk", missLatency),
                    string.Format("{0:F2} ns", (1000000000 / avgClock) * missLatency),
                    string.Format("{0:F2}%", 100 * counterData.ctr3 / counterData.ctr2),
                    FormatLargeNumber(counterData.ctr1)
                };
            }
        }

        public class MissesAdConfig : MonitoringConfig
        {
            private SandyBridgeEL3 cpu;
            public string GetConfigName() { return "L3 Miss and Address Ring"; }

            public MissesAdConfig(SandyBridgeEL3 intelCpu)
            {
                cpu = intelCpu;
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                ulong clockticks = GetUncorePerfEvtSelRegisterValue(0, 0, false, false, false, true, false, 0);
                ulong llcLookup = GetUncorePerfEvtSelRegisterValue(0x34, 0xF, false, false, false, true, false, 0);
                // event 0x1B = AD ring
                ulong adRingUp = GetUncorePerfEvtSelRegisterValue(0x1B, RING_UP_EVEN | RING_UP_ODD, false, false, false, true, false, 0);
                ulong adRingDn = GetUncorePerfEvtSelRegisterValue(0x1B, RING_DN_EVEN | RING_DN_ODD, false, false, false, true, false, 0);
                ulong filter = GetUncoreFilterRegisterValue(0, 0x1, LLC_LOOKUP_I, 0);
                cpu.SetupMonitoringSession(llcLookup, clockticks, adRingUp, adRingDn, filter);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[CboCount][];
                cpu.InitializeCboTotals();
                for (uint cboIdx = 0; cboIdx < CboCount; cboIdx++)
                {
                    cpu.UpdateCboCounterData(cboIdx);
                    results.unitMetrics[cboIdx] = computeMetrics("CBo " + cboIdx, cpu.cboData[cboIdx]);
                }

                results.overallMetrics = computeMetrics("Overall", cpu.cboTotals);
                return results;
            }

            public string[] columns = new string[] { "Item", "Clk", "L3 Miss BW", "I State", "AD Ring Total", "AD Up Cycles", "AD Dn Cycles" };

            public string GetHelpText() { return ""; }

            private string[] computeMetrics(string label, NormalizedCboCounterData counterData)
            {
                return new string[] { label,
                    FormatLargeNumber(counterData.ctr1),
                    FormatLargeNumber(counterData.ctr0 * 64) + "B/s",
                    FormatLargeNumber(counterData.ctr0),
                    FormatLargeNumber(counterData.ctr2 + counterData.ctr3),
                    string.Format("{0:F2}%", 100 * counterData.ctr2 / counterData.ctr1),
                    string.Format("{0:F2}%", 100 * counterData.ctr3 / counterData.ctr1)
                };
            }
        }

        public class LLCVictimsAndIvRing : MonitoringConfig
        {
            private SandyBridgeEL3 cpu;
            public string GetConfigName() { return "LLC Writebacks and Invalidate Ring"; }

            public LLCVictimsAndIvRing(SandyBridgeEL3 intelCpu)
            {
                cpu = intelCpu;
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                ulong clockticks = GetUncorePerfEvtSelRegisterValue(0, 0, false, false, false, true, false, 0);
                // LLC victim in M (modified) state = 64B writeback. ctr0 or ctr1
                ulong llcWbVictims = GetUncorePerfEvtSelRegisterValue(0x37, 1, false, false, false, true, false, 0); ;
                // event 0x1E = IV ring
                ulong ivRingOdd = GetUncorePerfEvtSelRegisterValue(0x1E, RING_UP_ODD | RING_DN_ODD, false, false, false, true, false, 0);
                ulong ivRingEven = GetUncorePerfEvtSelRegisterValue(0x1E, RING_UP_EVEN | RING_DN_ODD, false, false, false, true, false, 0);
                ulong filter = GetUncoreFilterRegisterValue(0, 0x1, LLC_LOOKUP_I, 0); // doesn't matter
                cpu.SetupMonitoringSession(llcWbVictims, clockticks, ivRingOdd, ivRingEven, filter);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[CboCount][];
                cpu.InitializeCboTotals();
                for (uint cboIdx = 0; cboIdx < CboCount; cboIdx++)
                {
                    cpu.UpdateCboCounterData(cboIdx);
                    results.unitMetrics[cboIdx] = computeMetrics("CBo " + cboIdx, cpu.cboData[cboIdx]);
                }

                results.overallMetrics = computeMetrics("Overall", cpu.cboTotals);
                return results;
            }

            public string[] columns = new string[] { "Item", "Clk", "L3 Writeback BW", "L3 Writebacks", "IV Ring Total", "IV Odd Polarity", "IV Even Polarity" };

            public string GetHelpText() { return ""; }

            private string[] computeMetrics(string label, NormalizedCboCounterData counterData)
            {
                return new string[] { label,
                    FormatLargeNumber(counterData.ctr1),
                    FormatLargeNumber(counterData.ctr0 * 64) + "B/s",
                    FormatLargeNumber(counterData.ctr0),
                    FormatLargeNumber(counterData.ctr2 + counterData.ctr3),
                    string.Format("{0:F2}%", 100 * counterData.ctr2 / counterData.ctr1),
                    string.Format("{0:F2}%", 100 * counterData.ctr3 / counterData.ctr1)
                };
            }
        }

        public class BouncesAndAkRing : MonitoringConfig
        {
            private SandyBridgeEL3 cpu;
            public string GetConfigName() { return "Bounces and Acknowledge Ring"; }

            public BouncesAndAkRing(SandyBridgeEL3 intelCpu)
            {
                cpu = intelCpu;
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                ulong clockticks = GetUncorePerfEvtSelRegisterValue(0, 0, false, false, false, true, false, 0);
                // Ring response bounces, include AK/BL/IV
                ulong bounces = GetUncorePerfEvtSelRegisterValue(0x5, 0b111, false, false, false, true, false, 0); ;
                // event 0x1C = AK ring
                ulong akRingUp = GetUncorePerfEvtSelRegisterValue(0x1C, RING_UP_EVEN | RING_UP_ODD, false, false, false, true, false, 0);
                ulong akRingDn = GetUncorePerfEvtSelRegisterValue(0x1C, RING_DN_EVEN | RING_DN_ODD, false, false, false, true, false, 0);
                ulong filter = GetUncoreFilterRegisterValue(0, 0x1, 0x1F, 0); // doesn't matter
                cpu.SetupMonitoringSession(bounces, clockticks, akRingUp, akRingDn, filter);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[CboCount][];
                cpu.InitializeCboTotals();
                for (uint cboIdx = 0; cboIdx < CboCount; cboIdx++)
                {
                    cpu.UpdateCboCounterData(cboIdx);
                    results.unitMetrics[cboIdx] = computeMetrics("CBo " + cboIdx, cpu.cboData[cboIdx]);
                }

                results.overallMetrics = computeMetrics("Overall", cpu.cboTotals);
                return results;
            }

            public string[] columns = new string[] { "Item", "Clk", "Response Bounces", "AK Ring Total", "AK Up Cycles", "AK Dn Cycles" };

            public string GetHelpText() { return ""; }

            private string[] computeMetrics(string label, NormalizedCboCounterData counterData)
            {
                return new string[] { label,
                    FormatLargeNumber(counterData.ctr1),
                    FormatLargeNumber(counterData.ctr0),
                    FormatLargeNumber(counterData.ctr2 + counterData.ctr3),
                    string.Format("{0:F2}%", 100 * counterData.ctr2 / counterData.ctr1),
                    string.Format("{0:F2}%", 100 * counterData.ctr3 / counterData.ctr1)
                };
            }
        }
    }
}
