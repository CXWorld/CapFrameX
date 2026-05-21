using PmcReader.Interop;
using System;
using System.Collections.Generic;

namespace PmcReader.Intel
{
    /// <summary>
    /// Haswell E uncore
    /// </summary>
    public class HaswellEL3 : ModernIntelCpu
    {
        public const uint MSR_UNC_CBO_increment = 0x10;
        public const uint C0_MSR_PMON_CTR0 = 0xE08;
        public const uint C0_MSR_PMON_CTR1 = 0xE09;
        public const uint C0_MSR_PMON_CTR2 = 0xE0A;
        public const uint C0_MSR_PMON_CTR3 = 0xE0B;
        public const uint C0_MSR_PMON_BOX_FILTER0 = 0xE05;
        public const uint C0_MSR_PMON_BOX_FILTER1 = 0xE06;
        public const uint C0_MSR_PMON_CTL0 = 0xE01;
        public const uint C0_MSR_PMON_CTL1 = 0xE02;
        public const uint C0_MSR_PMON_CTL2 = 0xE03;
        public const uint C0_MSR_PMON_CTL3 = 0xE04;
        public const uint C0_MSR_PMON_BOX_STATUS = 0xE07;
        public const uint C0_MSR_PMON_BOX_CTL = 0xE00;

        // UBox PMON global conrol
        public const uint U_MSR_PMON_GLOBAL_CTL = 0x700;
        public const uint U_MSR_PMON_GLOBAL_STATUS = 0x701;
        public const uint U_MSR_PMON_GLOBAL_CONFIG = 0x702;

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
        public const byte LLC_LOOKUP_M = 0b101000;

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
        public static uint CboCount;

        public HaswellEL3()
        {
            architectureName = "Haswell E L3 Cache";
            CboCount = (uint)coreCount;
            cboTotals = new NormalizedCboCounterData();
            cboData = new NormalizedCboCounterData[CboCount];
            List<MonitoringConfig> configs = new List<MonitoringConfig>();
            configs.Add(new HitsBlConfig(this));
            monitoringConfigs = configs.ToArray();
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
        /// <param name="filter0">Box filter 0 control</param>
        /// <param name="filter1">Box filter 1 control</param>
        public void SetupMonitoringSession(ulong ctr0, ulong ctr1, ulong ctr2, ulong ctr3, ulong filter0, ulong filter1)
        {
            FreezeAllCounters();

            for (uint cboIdx = 0; cboIdx < 8; cboIdx++)
            {
                Ring0.WriteMsr(C0_MSR_PMON_CTL0 + MSR_UNC_CBO_increment * cboIdx, ctr0);
                Ring0.WriteMsr(C0_MSR_PMON_CTL1 + MSR_UNC_CBO_increment * cboIdx, ctr1);
                Ring0.WriteMsr(C0_MSR_PMON_CTL2 + MSR_UNC_CBO_increment * cboIdx, ctr2);
                Ring0.WriteMsr(C0_MSR_PMON_CTL3 + MSR_UNC_CBO_increment * cboIdx, ctr3);
                Ring0.WriteMsr(C0_MSR_PMON_BOX_FILTER0 + MSR_UNC_CBO_increment * cboIdx, filter0);
                Ring0.WriteMsr(C0_MSR_PMON_BOX_FILTER1 + MSR_UNC_CBO_increment * cboIdx, filter1);
                ClearBoxCounters(cboIdx);
                UnFreezeBoxCounters(cboIdx);
            }

            UnfreezeAllCounters();
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
        /// Freeze all uncore performance monitors
        /// </summary>
        public void FreezeAllCounters()
        {
            ulong freeze = 1UL << 31;
            Ring0.WriteMsr(U_MSR_PMON_GLOBAL_CTL, freeze);
        }

        /// <summary>
        /// Unfreeze all uncore performance monitors
        /// </summary>
        public void UnfreezeAllCounters()
        {
            ulong unfreeze = 1UL << 29;
            Ring0.WriteMsr(U_MSR_PMON_GLOBAL_CTL, unfreeze);
        }

        public void FreezeBoxCounters(uint cboIdx)
        {
            if (cboIdx >= CboCount) return;
            ulong freezeValue = GetUncoreBoxCtlRegisterValue(false, false, freeze: true);
            Ring0.WriteMsr(C0_MSR_PMON_BOX_CTL + MSR_UNC_CBO_increment * cboIdx, freezeValue);
        }

        public void UnFreezeBoxCounters(uint cboIdx)
        {
            if (cboIdx >= CboCount) return;
            ulong freezeValue = GetUncoreBoxCtlRegisterValue(false, false, freeze: false);
            Ring0.WriteMsr(C0_MSR_PMON_BOX_CTL + MSR_UNC_CBO_increment * cboIdx, freezeValue);
        }

        public void ClearBoxCounters(uint cboIdx)
        {
            if (cboIdx >= CboCount) return;
            ulong freezeValue = GetUncoreBoxCtlRegisterValue(false, rstCtrs: true, false);
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
        /// <param name="cmask">Count mask (threshold)</param>
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
        /// Get value to put in cbo filter 0 register
        /// </summary>
        /// <param name="tid">If tid_en for counter ctl register is 1, bit 0 = thread 1/0, bits 1-3 = core id, 0x3F = non-associated reqs</param>
        /// <param name="state">For LLC lookups, line state</param>
        /// <param name="opcode">Match ingress request queue opcodes</param>
        /// <returns>Value to put in filter register</returns>
        public static ulong GetUncoreFilter0RegisterValue(byte tid, byte state)
        {
            return tid |
                (ulong)state << 17;
        }

        /// <summary>
        /// Get value to put in cbo filter 1 register
        /// </summary>
        /// <param name="nid">Node ID mask</param>
        /// <param name="opcode">Opcode match</param>
        /// <param name="nc">Match on non-coherent requests</param>
        /// <param name="isoc">Match on ISOC requests</param>
        /// <returns></returns>
        public static ulong GetUncoreFilter1RegisterValue(short nid, byte opcode, byte nc, byte isoc)
        {
            return (ulong)(byte)nid |
                ((ulong)opcode << 20) |
                (ulong)nc << 30 | 
                (ulong)isoc << 31;
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
            bool freeze)
        {
            // software must write 1 to bits 16, 17 or behavior is undefined?
            return (rstCtrl ? 1UL : 0UL) |
                (rstCtrs ? 1UL : 0UL) << 1 |
                (freeze ? 1UL : 0UL) << 8 |
                3UL << 16;
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
            private HaswellEL3 cpu;
            public string GetConfigName() { return "L3 Hits and Data Ring"; }

            public HitsBlConfig(HaswellEL3 intelCpu)
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

                // umask: any = 0b10001
                ulong llcLookup = GetUncorePerfEvtSelRegisterValue(0x34, 0b10001, false, false, false, true, false, 0);
                // 0x1D = BL ring (block/data ring) used cycles, 0b1 = up direction even polarity. 0b10 = up direction odd polarity. must go in ctr2 or ctr3
                ulong blRingUp = GetUncorePerfEvtSelRegisterValue(0x1D, RING_UP_EVEN | RING_UP_ODD, false, false, false, true, false, 0);
                // 0b100 = down direction even polarity, 0b1000 = down direction odd polarity
                ulong blRingDn = GetUncorePerfEvtSelRegisterValue(0x1D, RING_DN_EVEN | RING_DN_ODD, false, false, false, true, false, 0);
                ulong filter0 = GetUncoreFilter0RegisterValue(0, LLC_LOOKUP_E | LLC_LOOKUP_F | LLC_LOOKUP_M | LLC_LOOKUP_S);
                ulong filter1 = GetUncoreFilter1RegisterValue(0xFF, 0, 0, 0);
                cpu.SetupMonitoringSession(clockticks, llcLookup, blRingUp, blRingDn, filter0, filter1);
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
    }
}
