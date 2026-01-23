using PmcReader.Interop;
using System;
using System.Windows.Forms;
using System.Drawing;

namespace PmcReader.AMD
{
    public class Amd16hCpu : GenericMonitoringArea
    {
        // someone else really likes resetting aperf
        public const uint MSR_APERF = 0x000000E8;
        public const uint MSR_MPERF = 0x000000E7;
        public const uint MSR_TSC = 0x00000010;

        public const uint MSR_PERF_CTL_0 = 0xC0010000;
        public const uint MSR_PERF_CTL_1 = 0xC0010001;
        public const uint MSR_PERF_CTL_2 = 0xC0010002;
        public const uint MSR_PERF_CTL_3 = 0xC0010003;
        public const uint MSR_PERF_CTR_0 = 0xC0010004;
        public const uint MSR_PERF_CTR_1 = 0xC0010005;
        public const uint MSR_PERF_CTR_2 = 0xC0010006;
        public const uint MSR_PERF_CTR_3 = 0xC0010007;

        public const uint MSR_L2I_PERF_CTL_0 = 0xC0010230;
        public const uint MSR_L2I_PERF_CTR_0 = 0xC0010231;
        public const uint MSR_L2I_PERF_CTL_1 = 0xC0010232;
        public const uint MSR_L2I_PERF_CTR_1 = 0xC0010233;
        public const uint MSR_L2I_PERF_CTL_2 = 0xC0010234;
        public const uint MSR_L2I_PERF_CTR_2 = 0xC0010235;
        public const uint MSR_L2I_PERF_CTL_3 = 0xC0010236;
        public const uint MSR_L2I_PERF_CTR_3 = 0xC0010237;

        public const uint MSR_NB_PERF_CTL_0 = 0xC0010240;
        public const uint MSR_NB_PERF_CTL_1 = 0xC0010242;
        public const uint MSR_NB_PERF_CTL_2 = 0xC0010244;
        public const uint MSR_NB_PERF_CTL_3 = 0xC0010246;
        public const uint MSR_NB_PERF_CTR_0 = 0xC0010241;
        public const uint MSR_NB_PERF_CTR_1 = 0xC0010243;
        public const uint MSR_NB_PERF_CTR_2 = 0xC0010245;
        public const uint MSR_NB_PERF_CTR_3 = 0xC0010247;

        public const uint HWCR = 0xC0010015;

        public NormalizedCounterData[] NormalizedThreadCounts;
        public NormalizedCounterData NormalizedTotalCounts;
        private ulong[] lastThreadAperf;
        private ulong[] lastThreadMperf;
        private ulong[] lastThreadTsc;

        public Amd16hCpu()
        {
            architectureName = "AMD 16h Family";
            lastThreadAperf = new ulong[GetThreadCount()];
            lastThreadMperf = new ulong[GetThreadCount()];
            lastThreadTsc = new ulong[GetThreadCount()];
        }

        /// <summary>
        /// Program core perf counters
        /// </summary>
        /// <param name="ctr0">Counter 0 event select</param>
        /// <param name="ctr1">Counter 1 event select</param>
        /// <param name="ctr2">Counter 2 event select</param>
        /// <param name="ctr3">Counter 3 event select</param>
        public void ProgramCorePerfCounters(ulong ctr0, ulong ctr1, ulong ctr2, ulong ctr3)
        {
            for (int threadIdx = 0; threadIdx < this.GetThreadCount(); threadIdx++)
            {
                ThreadAffinity.Set(1UL << threadIdx);
                Ring0.WriteMsr(MSR_PERF_CTL_0, ctr0);
                Ring0.WriteMsr(MSR_PERF_CTL_1, ctr1);
                Ring0.WriteMsr(MSR_PERF_CTL_2, ctr2);
                Ring0.WriteMsr(MSR_PERF_CTL_3, ctr3);
                Ring0.WriteMsr(MSR_PERF_CTR_0, 0);
                Ring0.WriteMsr(MSR_PERF_CTR_1, 0);
                Ring0.WriteMsr(MSR_PERF_CTR_2, 0);
                Ring0.WriteMsr(MSR_PERF_CTR_3, 0);
                if (this.NormalizedTotalCounts != null)
                {
                    // Clear totals
                    this.NormalizedTotalCounts.ctr0total = 0;
                    this.NormalizedTotalCounts.ctr1total = 0;
                    this.NormalizedTotalCounts.ctr2total = 0;
                    this.NormalizedTotalCounts.ctr3total = 0;
                }
            }
        }

        public void ProgramL2IPerfCounters(ulong ctr0, ulong ctr1, ulong ctr2, ulong ctr3)
        {
            Ring0.WriteMsr(MSR_L2I_PERF_CTL_0, ctr0);
            Ring0.WriteMsr(MSR_L2I_PERF_CTL_1, ctr1);
            Ring0.WriteMsr(MSR_L2I_PERF_CTL_2, ctr2);
            Ring0.WriteMsr(MSR_L2I_PERF_CTL_3, ctr3);
            Ring0.WriteMsr(MSR_L2I_PERF_CTR_0, 0);
            Ring0.WriteMsr(MSR_L2I_PERF_CTR_1, 0);
            Ring0.WriteMsr(MSR_L2I_PERF_CTR_2, 0);
            Ring0.WriteMsr(MSR_L2I_PERF_CTR_3, 0);

            // L2 will only use the total counts as a shortcut
            // Because there's only one Jaguar core cluster
            if (this.NormalizedTotalCounts != null)
            {
                // Clear totals
                this.NormalizedTotalCounts.ctr0total = 0;
                this.NormalizedTotalCounts.ctr1total = 0;
                this.NormalizedTotalCounts.ctr2total = 0;
                this.NormalizedTotalCounts.ctr3total = 0;
            }
            else
            {
                this.NormalizedTotalCounts = new NormalizedCounterData();
            }
        }

        public void ProgramNbPerfCounters(ulong ctr0, ulong ctr1, ulong ctr2, ulong ctr3)
        {
            Ring0.WriteMsr(MSR_NB_PERF_CTL_0, ctr0);
            Ring0.WriteMsr(MSR_NB_PERF_CTL_1, ctr1);
            Ring0.WriteMsr(MSR_NB_PERF_CTL_2, ctr2);
            Ring0.WriteMsr(MSR_NB_PERF_CTL_3, ctr3);

            Ring0.WriteMsr(MSR_NB_PERF_CTR_0, 0);
            Ring0.WriteMsr(MSR_NB_PERF_CTR_1, 0);
            Ring0.WriteMsr(MSR_NB_PERF_CTR_2, 0);
            Ring0.WriteMsr(MSR_NB_PERF_CTR_3, 0);

            if (this.NormalizedTotalCounts != null)
            {
                // Clear totals
                this.NormalizedTotalCounts.ctr0total = 0;
                this.NormalizedTotalCounts.ctr1total = 0;
                this.NormalizedTotalCounts.ctr2total = 0;
                this.NormalizedTotalCounts.ctr3total = 0;
            }
            else
            {
                this.NormalizedTotalCounts = new NormalizedCounterData();
            }
        }

        /// <summary>
        /// Update fixed counters for thread, affinity must be set going in
        /// </summary>
        /// <param name="threadIdx">thread to update fixed counters for</param>
        public void ReadFixedCounters(int threadIdx, out ulong elapsedAperf, out ulong elapsedTsc, out ulong elapsedMperf)
        {
            ulong aperf, tsc, mperf;
            Ring0.ReadMsr(MSR_APERF, out aperf);
            Ring0.ReadMsr(MSR_TSC, out tsc);
            Ring0.ReadMsr(MSR_MPERF, out mperf);

            elapsedAperf = aperf;
            elapsedTsc = tsc;
            elapsedMperf = mperf;
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
        }

        /// <summary>
        /// initialize/reset accumulated totals for core counter data
        /// </summary>
        public void InitializeCoreTotals()
        {
            if (NormalizedTotalCounts == null)
            {
                NormalizedTotalCounts = new NormalizedCounterData();
            }

            NormalizedTotalCounts.aperf = 0;
            NormalizedTotalCounts.mperf = 0;
            NormalizedTotalCounts.tsc = 0;
            NormalizedTotalCounts.ctr0 = 0;
            NormalizedTotalCounts.ctr1 = 0;
            NormalizedTotalCounts.ctr2 = 0;
            NormalizedTotalCounts.ctr3 = 0;
        }

        /// <summary>
        /// Read and update counter data for thread
        /// </summary>
        /// <param name="threadIdx">Thread to set affinity to</param>
        public void UpdateThreadCoreCounterData(int threadIdx)
        {
            ThreadAffinity.Set(1UL << threadIdx);
            float normalizationFactor = GetNormalizationFactor(threadIdx);
            ulong aperf, mperf, tsc;
            ulong ctr0, ctr1, ctr2, ctr3;
            ReadFixedCounters(threadIdx, out aperf, out tsc, out mperf);
            ctr0 = ReadAndClearMsr(MSR_PERF_CTR_0);
            ctr1 = ReadAndClearMsr(MSR_PERF_CTR_1);
            ctr2 = ReadAndClearMsr(MSR_PERF_CTR_2);
            ctr3 = ReadAndClearMsr(MSR_PERF_CTR_3);

            if (NormalizedThreadCounts == null) NormalizedThreadCounts = new NormalizedCounterData[threadCount];
            if (NormalizedThreadCounts[threadIdx] == null) NormalizedThreadCounts[threadIdx] = new NormalizedCounterData();

            NormalizedThreadCounts[threadIdx].aperf = aperf * normalizationFactor;
            NormalizedThreadCounts[threadIdx].mperf = mperf * normalizationFactor;
            NormalizedThreadCounts[threadIdx].tsc = tsc * normalizationFactor;
            NormalizedThreadCounts[threadIdx].ctr0 = ctr0 * normalizationFactor;
            NormalizedThreadCounts[threadIdx].ctr1 = ctr1 * normalizationFactor;
            NormalizedThreadCounts[threadIdx].ctr2 = ctr2 * normalizationFactor;
            NormalizedThreadCounts[threadIdx].ctr3 = ctr3 * normalizationFactor;
            NormalizedThreadCounts[threadIdx].NormalizationFactor = normalizationFactor;
            NormalizedThreadCounts[threadIdx].ctr0total += ctr0;
            NormalizedThreadCounts[threadIdx].ctr1total += ctr1;
            NormalizedThreadCounts[threadIdx].ctr2total += ctr2;
            NormalizedThreadCounts[threadIdx].ctr3total += ctr3;

            NormalizedTotalCounts.aperf += NormalizedThreadCounts[threadIdx].aperf;
            NormalizedTotalCounts.mperf += NormalizedThreadCounts[threadIdx].mperf;
            NormalizedTotalCounts.tsc += NormalizedThreadCounts[threadIdx].tsc;
            NormalizedTotalCounts.ctr0 += NormalizedThreadCounts[threadIdx].ctr0;
            NormalizedTotalCounts.ctr1 += NormalizedThreadCounts[threadIdx].ctr1;
            NormalizedTotalCounts.ctr2 += NormalizedThreadCounts[threadIdx].ctr2;
            NormalizedTotalCounts.ctr3 += NormalizedThreadCounts[threadIdx].ctr3;
            NormalizedTotalCounts.ctr0total += ctr0;
            NormalizedTotalCounts.ctr1total += ctr1;
            NormalizedTotalCounts.ctr2total += ctr2;
            NormalizedTotalCounts.ctr3total += ctr3;
        }

        public void UpdateL2ICounterData()
        {
            const int JaguarL2Index = 99;
            float normalizationFactor = GetNormalizationFactor(JaguarL2Index);
            ulong ctr0, ctr1, ctr2, ctr3;
            ctr0 = ReadAndClearMsr(MSR_L2I_PERF_CTR_0);
            ctr1 = ReadAndClearMsr(MSR_L2I_PERF_CTR_1);
            ctr2 = ReadAndClearMsr(MSR_L2I_PERF_CTR_2);
            ctr3 = ReadAndClearMsr(MSR_L2I_PERF_CTR_3);

            NormalizedTotalCounts.ctr0 = ctr0 * normalizationFactor;
            NormalizedTotalCounts.ctr1 = ctr1 * normalizationFactor;
            NormalizedTotalCounts.ctr2 = ctr2 * normalizationFactor;
            NormalizedTotalCounts.ctr3 = ctr3 * normalizationFactor;
            NormalizedTotalCounts.ctr0total += ctr0;
            NormalizedTotalCounts.ctr1total += ctr1;
            NormalizedTotalCounts.ctr2total += ctr2;
            NormalizedTotalCounts.ctr3total += ctr3;
        }

        public void UpdateNBCounterData()
        {
            const int JaguarNbIndex = 100;
            float normalizationFactor = GetNormalizationFactor(JaguarNbIndex);
            ulong ctr0, ctr1, ctr2, ctr3;
            ctr0 = ReadAndClearMsr(MSR_NB_PERF_CTR_0);
            ctr1 = ReadAndClearMsr(MSR_NB_PERF_CTR_1);
            ctr2 = ReadAndClearMsr(MSR_NB_PERF_CTR_2);
            ctr3 = ReadAndClearMsr(MSR_NB_PERF_CTR_3);

            NormalizedTotalCounts.ctr0 = ctr0 * normalizationFactor;
            NormalizedTotalCounts.ctr1 = ctr1 * normalizationFactor;
            NormalizedTotalCounts.ctr2 = ctr2 * normalizationFactor;
            NormalizedTotalCounts.ctr3 = ctr3 * normalizationFactor;
            NormalizedTotalCounts.ctr0total += ctr0;
            NormalizedTotalCounts.ctr1total += ctr1;
            NormalizedTotalCounts.ctr2total += ctr2;
            NormalizedTotalCounts.ctr3total += ctr3;
        }

        /// <summary>
        /// Assemble overall counter values into a Tuple of string, float array.
        /// </summary>
        /// <param name="ctr0">Description for counter 0 value</param>
        /// <param name="ctr1">Description for counter 1 value</param>
        /// <param name="ctr2">Description for counter 2 value</param>
        /// <param name="ctr3">Description for counter 3 value</param>
        /// <returns>Array to put in results object</returns>
        public Tuple<string, float>[] GetOverallCounterValues(string ctr0, string ctr1, string ctr2, string ctr3)
        {
            NormalizedCounterData dataToLog = this.NormalizedTotalCounts;
            if (this.targetLogCoreIndex >= 0)
            {
                dataToLog = NormalizedThreadCounts[this.targetLogCoreIndex];
            }

            Tuple<string, float>[] retval = new Tuple<string, float>[7];
            retval[0] = new Tuple<string, float>("APERF", dataToLog.aperf);
            retval[1] = new Tuple<string, float>("MPERF", dataToLog.mperf);
            retval[2] = new Tuple<string, float>("TSC", dataToLog.tsc);
            retval[3] = new Tuple<string, float>(ctr0, dataToLog.ctr0);
            retval[4] = new Tuple<string, float>(ctr1, dataToLog.ctr1);
            retval[5] = new Tuple<string, float>(ctr2, dataToLog.ctr2);
            retval[6] = new Tuple<string, float>(ctr3,  dataToLog.ctr3);
            return retval;
        }

        /// <summary>
        /// Get perf ctl value assuming default values for stupid stuff
        /// </summary>
        /// <param name="perfEvent">Perf event, low 16 bits</param>
        /// <param name="umask">Unit mask</param>
        /// <param name="edge">only increment on transition</param>
        /// <param name="cmask">count mask</param>
        /// <param name="perfEventHi">Perf event, high 8 bits</param>
        /// <returns></returns>
        public static ulong GetPerfCtlValue(byte perfEvent, byte umask, bool edge, byte cmask, byte perfEventHi)
        {
            return GetPerfCtlValue(perfEvent, 
                umask, 
                OsUsrMode.All, 
                edge, 
                interrupt: false, 
                enable: true, 
                invert: false, 
                cmask, 
                perfEventHi, 
                HostGuestOnly.All);
        }

        /// <summary>
        /// Get core perf ctl value
        /// </summary>
        /// <param name="perfEvent">Low 16 bits of performance event</param>
        /// <param name="umask">perf event umask</param>
        /// <param name="osUsrMode">Count in os or user mode</param>
        /// <param name="edge">only increment on transition</param>
        /// <param name="interrupt">generate apic interrupt on overflow</param>
        /// <param name="enable">enable perf ctr</param>
        /// <param name="invert">invert cmask</param>
        /// <param name="cmask">0 = increment by event count. >0 = increment by 1 if event count in clock cycle >= cmask</param>
        /// <param name="perfEventHi">high 4 bits of performance event</param>
        /// <param name="hostGuestOnly">Count host or guest events</param>
        /// <returns>value for perf ctl msr</returns>
        public static ulong GetPerfCtlValue(byte perfEvent, byte umask, OsUsrMode osUsrMode, bool edge, bool interrupt, bool enable, bool invert, byte cmask, byte perfEventHi, HostGuestOnly hostGuestOnly)
        {
            return perfEvent |
                (ulong)umask << 8 |
                ((ulong)osUsrMode) << 16 |
                (edge ? 1UL : 0UL) << 18 |
                (interrupt ? 1UL : 0UL) << 20 |
                (enable ? 1UL : 0UL) << 22 |
                (invert ? 1UL : 0UL) << 23 |
                (ulong)cmask << 24 |
                (ulong)perfEventHi << 32 |
                ((ulong)hostGuestOnly) << 40;
        }

        public static ulong GetL2iPerfCtlValue(byte perfEvent, byte umask, bool invert, byte cmask, byte perfEventHi, byte bankMask, byte threadMask)
        {
            return perfEvent |
                (ulong)umask << 8 |
                1UL << 22 |
                (invert ? 1UL : 0UL) << 23 |
                (ulong)cmask << 24 |
                (ulong)(perfEventHi & 0xF) << 32 |
                (ulong)bankMask << 48 |
                (ulong)threadMask << 56;
        }

        /// <summary>
        /// Selects what ring(s) events are counted for
        /// </summary>
        public enum OsUsrMode
        {
            None = 0b00,
            Usr = 0b01,
            OS = 0b10,
            All = 0b11
        }

        /// <summary>
        /// Whether to count events for guest (VM) or host
        /// </summary>
        public enum HostGuestOnly
        {
            All = 0b00,
            Guest = 0b01,
            Host = 0b10,
            AllSvme = 0b11
        }

        /// <summary>
        /// Get northbridge performance event select MSR value
        /// </summary>
        /// <param name="perfEventLow">Low 8 bits of performance event select</param>
        /// <param name="umask">unit mask</param>
        /// <param name="enable">enable perf counter</param>
        /// <param name="perfEventHi">bits 8-11 of performance event select</param>
        /// <returns>value to put in DF_PERF_CTL</returns>
        public static ulong GetNBPerfCtlValue(byte perfEventLow, byte umask, bool enable, byte perfEventHi)
        {
            // bit 20 enables interrupt on overflow, bit 36 enables interrupt to a core, and bits 37-40 select a core, but we don't care about that
            return perfEventLow |
                (ulong)umask << 8 |
                (enable ? 1UL : 0UL) << 22 |
                (ulong)perfEventHi << 32;
        }

        public class NormalizedCounterData
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
            /// Programmable performance counter values
            /// </summary>
            public float ctr0;
            public float ctr1;
            public float ctr2;
            public float ctr3;

            public ulong ctr0total;
            public ulong ctr1total;
            public ulong ctr2total;
            public ulong ctr3total;

            public float NormalizationFactor;
        }

        private Label errorLabel; // ugly whatever

        public override void InitializeCrazyControls(FlowLayoutPanel flowLayoutPanel, Label errLabel)
        {
            flowLayoutPanel.Controls.Clear();

            CheckBox boostCheckbox = new CheckBox();
            boostCheckbox.Text = "Core Performance Boost";
            boostCheckbox.Checked = GetCpbEnabled();
            boostCheckbox.CheckedChanged += HandleCorePerformanceBoostCheckbox;
            boostCheckbox.Width = TextRenderer.MeasureText(boostCheckbox.Text, boostCheckbox.Font).Width + 20;
            flowLayoutPanel.Controls.Add(boostCheckbox);

            errorLabel = errLabel;
        }

        public void HandleCorePerformanceBoostCheckbox(object sender, EventArgs e)
        {
            CheckBox checkbox = (CheckBox)sender;
            SetCorePerformanceBoost(checkbox.Checked);
        }

        private bool GetCpbEnabled()
        {
            Ring0.ReadMsr(HWCR, out ulong hwcr);
            return (hwcr & (1UL << 25)) == 0;
        }

        public void SetCorePerformanceBoost(bool enable)
        {
            bool allCpbEnabled = true;
            for (int threadIdx = 0; threadIdx < GetThreadCount(); threadIdx++)
            {
                ThreadAffinity.Set(1UL << threadIdx);
                Ring0.ReadMsr(HWCR, out ulong hwcr);
                if (enable) hwcr &= ~(1UL << 25);     // unset to not disable CPB
                else hwcr |= (1UL << 25);             // set bit 25 to disable CPB
                Ring0.WriteMsr(HWCR, hwcr);
                allCpbEnabled &= GetCpbEnabled();
            }

            if (!allCpbEnabled)
            {
                errorLabel.Text = "Core Performance Boost disabled";
            }
            else
            {
                errorLabel.Text = "Core Performance Boost enabled";
            }
        }
    }
}
