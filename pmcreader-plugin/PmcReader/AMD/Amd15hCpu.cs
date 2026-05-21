using PmcReader.Interop;
using System;
using System.Windows.Forms;
using System.Drawing;

namespace PmcReader.AMD
{
    public class Amd15hCpu : GenericMonitoringArea
    {
        // someone else really likes resetting aperf
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
        public const uint MSR_NB_PERF_CTL_0 = 0xC0010240;
        public const uint MSR_NB_PERF_CTL_1 = 0xC0010242;
        public const uint MSR_NB_PERF_CTL_2 = 0xC0010244;
        public const uint MSR_NB_PERF_CTL_3 = 0xC0010246;
        public const uint MSR_NB_PERF_CTR_0 = 0xC0010241;
        public const uint MSR_NB_PERF_CTR_1 = 0xC0010243;
        public const uint MSR_NB_PERF_CTR_2 = 0xC0010245;
        public const uint MSR_NB_PERF_CTR_3 = 0xC0010247;

        public const uint HWCR = 0xC0010015;
        public const uint DC_CFG = 0xC0011022;
        public const uint CU_CFG = 0xC0011023;

        public NormalizedCoreCounterData[] NormalizedThreadCounts;
        public NormalizedCoreCounterData NormalizedTotalCounts;
        private ulong[] lastThreadAperf;
        private ulong[] lastThreadMperf;
        private ulong[] lastThreadTsc;

        public Amd15hCpu()
        {
            architectureName = "AMD 15h Family";
            lastThreadAperf = new ulong[GetThreadCount()];
            lastThreadMperf = new ulong[GetThreadCount()];
            lastThreadTsc = new ulong[GetThreadCount()];
        }

        public class FPU : MonitoringConfig
        {
            private Amd15hCpu cpu;
            public string GetConfigName() { return "FPU"; }

            private uint pipeNumber;
            private float[] lastPipeCounts;

            public FPU(Amd15hCpu amdCpu)
            {
                cpu = amdCpu;
                pipeNumber = 1;
                lastPipeCounts = new float[4 * cpu.GetThreadCount()];
            }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(
                    GetPerfCtlValue(0xC0, 0, false, 0, 0), // instructions retired
                    GetPerfCtlValue(0xCB, 0x4, false, 0, 0), // SSE/AVX instructions retired
                    GetPerfCtlValue(0xCB, 0x3, false, 0, 0), // MMX/x86 instructions retired
                    GetPerfCtlValue(0, 0x1, false, 0, 0), // perf_ctl_3, mux this
                    GetPerfCtlValue(0x1, 0, false, 0, 0), // FP scheduler empty
                    GetPerfCtlValue(0x5, 0xF, false, 0, 0));  // FP Serializing op
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[cpu.GetThreadCount()][];
                cpu.InitializeCoreTotals();
                byte nextPipeUmask = 0;
                if (pipeNumber == 0) nextPipeUmask = 0x2; // pipe 1
                else if (pipeNumber == 1) nextPipeUmask = 0x4; // pipe 2
                else if (pipeNumber == 2) nextPipeUmask = 0x8; // pipe 3
                else if (pipeNumber == 3) nextPipeUmask = 0x1; // pipe 0
                else throw new Exception("Bad Pipe");

                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    Ring0.WriteMsr(MSR_PERF_CTL_3, GetPerfCtlValue(0, nextPipeUmask, false, 0, 0));
                    lastPipeCounts[threadIdx * 4 + pipeNumber] = cpu.NormalizedThreadCounts[threadIdx].ctr3;
                    results.unitMetrics[threadIdx] = computeMetrics("Thread " + threadIdx, cpu.NormalizedThreadCounts[threadIdx], threadIdx);
                }

                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts, -1);

                NormalizedCoreCounterData dataToLog = cpu.NormalizedTotalCounts;
                if (cpu.targetLogCoreIndex >= 0)
                {
                    dataToLog = cpu.NormalizedThreadCounts[cpu.targetLogCoreIndex];
                }

                Tuple<string, float>[] retval = new Tuple<string, float>[10];
                retval[0] = new Tuple<string, float>("APERF", dataToLog.aperf);
                retval[1] = new Tuple<string, float>("MPERF", dataToLog.mperf);
                retval[2] = new Tuple<string, float>("TSC", dataToLog.tsc);
                retval[3] = new Tuple<string, float>("instr", dataToLog.ctr0);
                retval[4] = new Tuple<string, float>("sse/avx instr", dataToLog.ctr1);
                retval[5] = new Tuple<string, float>("mmx/x87 instr", dataToLog.ctr2);
                retval[6] = new Tuple<string, float>("FP Mux", dataToLog.ctr3);
                retval[7] = new Tuple<string, float>("FP Sch Empty", dataToLog.ctr4);
                retval[8] = new Tuple<string, float>("FP Serializing Op", dataToLog.ctr5);
                retval[9] = new Tuple<string, float>("FP Pipe Logged", pipeNumber);
                results.overallCounterValues = retval;

                pipeNumber = (pipeNumber + 1) % 4;
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "SSE/AVX Instr", "MMX/x87 Instr",
                "FP0", "FP1", "FP2", "FP3", "FP RS Empty", "FP Serializing Ops" };

            public string GetHelpText()
            {
                return "aaaaaa";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData, int threadId)
            {
                float fp0 = 0, fp1 = 0, fp2 = 0, fp3 = 0;
                if (threadId < 0)
                {
                    for (int i = 0; i < cpu.GetThreadCount(); i++)
                    {
                        fp0 += lastPipeCounts[i * 4];
                        fp1 += lastPipeCounts[i * 4 + 1];
                        fp2 += lastPipeCounts[i * 4 + 2];
                        fp3 += lastPipeCounts[i * 4 + 3];
                    }
                }
                else
                {
                    fp0 = lastPipeCounts[threadId * 4];
                    fp1 = lastPipeCounts[threadId * 4 + 1];
                    fp2 = lastPipeCounts[threadId * 4 + 2];
                    fp3 = lastPipeCounts[threadId * 4 + 3];
                }

                float instr = counterData.ctr0;
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf),
                        FormatLargeNumber(instr),
                        string.Format("{0:F2}", instr / counterData.aperf),
                        FormatLargeNumber(counterData.ctr1),
                        FormatLargeNumber(counterData.ctr2),
                        FormatPercentage(fp0, counterData.aperf),
                        FormatPercentage(fp1, counterData.aperf),
                        FormatPercentage(fp2, counterData.aperf),
                        FormatPercentage(fp3, counterData.aperf),
                        FormatPercentage(counterData.ctr4, counterData.aperf),
                        FormatLargeNumber(counterData.ctr5)
                        };
            }
        }

        /// <summary>
        /// Program core perf counters
        /// </summary>
        /// <param name="ctr0">Counter 0 event select</param>
        /// <param name="ctr1">Counter 1 event select</param>
        /// <param name="ctr2">Counter 2 event select</param>
        /// <param name="ctr3">Counter 3 event select</param>
        /// <param name="ctr4">Counter 4 event select</param>
        /// <param name="ctr5">Counter 5 event select</param>
        public void ProgramPerfCounters(ulong ctr0, ulong ctr1, ulong ctr2, ulong ctr3, ulong ctr4, ulong ctr5)
        {
            for (int threadIdx = 0; threadIdx < this.GetThreadCount(); threadIdx++)
            {
                ThreadAffinity.Set(1UL << threadIdx);
                Ring0.WriteMsr(MSR_PERF_CTL_0, ctr0);
                Ring0.WriteMsr(MSR_PERF_CTL_1, ctr1);
                Ring0.WriteMsr(MSR_PERF_CTL_2, ctr2);
                Ring0.WriteMsr(MSR_PERF_CTL_3, ctr3);
                Ring0.WriteMsr(MSR_PERF_CTL_4, ctr4);
                Ring0.WriteMsr(MSR_PERF_CTL_5, ctr5);
                Ring0.WriteMsr(MSR_PERF_CTR_0, 0);
                Ring0.WriteMsr(MSR_PERF_CTR_1, 0);
                Ring0.WriteMsr(MSR_PERF_CTR_2, 0);
                Ring0.WriteMsr(MSR_PERF_CTR_3, 0);
                Ring0.WriteMsr(MSR_PERF_CTR_4, 0);
                Ring0.WriteMsr(MSR_PERF_CTR_5, 0);
                if (this.NormalizedTotalCounts != null)
                {
                    // Clear totals
                    this.NormalizedTotalCounts.ctr0total = 0;
                    this.NormalizedTotalCounts.ctr1total = 0;
                    this.NormalizedTotalCounts.ctr2total = 0;
                    this.NormalizedTotalCounts.ctr3total = 0;
                    this.NormalizedTotalCounts.ctr4total = 0;
                    this.NormalizedTotalCounts.ctr5total = 0;
                }
            }
        }

        /// <summary>
        /// Update fixed counters for thread, affinity must be set going in
        /// </summary>
        /// <param name="threadIdx">thread to update fixed counters for</param>
        public void ReadFixedCounters(int threadIdx, out ulong elapsedAperf, out ulong elapsedTsc, out ulong elapsedMperf)
        {
            ulong aperf, tsc, mperf;
            Ring0.ReadMsr(MSR_APERF_READONLY, out aperf);
            Ring0.ReadMsr(MSR_TSC, out tsc);
            Ring0.ReadMsr(MSR_MPERF_READONLY, out mperf);

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
                NormalizedTotalCounts = new NormalizedCoreCounterData();
            }

            NormalizedTotalCounts.aperf = 0;
            NormalizedTotalCounts.mperf = 0;
            NormalizedTotalCounts.tsc = 0;
            NormalizedTotalCounts.ctr0 = 0;
            NormalizedTotalCounts.ctr1 = 0;
            NormalizedTotalCounts.ctr2 = 0;
            NormalizedTotalCounts.ctr3 = 0;
            NormalizedTotalCounts.ctr4 = 0;
            NormalizedTotalCounts.ctr5 = 0;
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
            ulong ctr0, ctr1, ctr2, ctr3, ctr4, ctr5;
            ReadFixedCounters(threadIdx, out aperf, out tsc, out mperf);
            ctr0 = ReadAndClearMsr(MSR_PERF_CTR_0);
            ctr1 = ReadAndClearMsr(MSR_PERF_CTR_1);
            ctr2 = ReadAndClearMsr(MSR_PERF_CTR_2);
            ctr3 = ReadAndClearMsr(MSR_PERF_CTR_3);
            ctr4 = ReadAndClearMsr(MSR_PERF_CTR_4);
            ctr5 = ReadAndClearMsr(MSR_PERF_CTR_5);

            if (NormalizedThreadCounts == null) NormalizedThreadCounts = new NormalizedCoreCounterData[threadCount];
            if (NormalizedThreadCounts[threadIdx] == null) NormalizedThreadCounts[threadIdx] = new NormalizedCoreCounterData();

            NormalizedThreadCounts[threadIdx].aperf = aperf * normalizationFactor;
            NormalizedThreadCounts[threadIdx].mperf = mperf * normalizationFactor;
            NormalizedThreadCounts[threadIdx].tsc = tsc * normalizationFactor;
            NormalizedThreadCounts[threadIdx].ctr0 = ctr0 * normalizationFactor;
            NormalizedThreadCounts[threadIdx].ctr1 = ctr1 * normalizationFactor;
            NormalizedThreadCounts[threadIdx].ctr2 = ctr2 * normalizationFactor;
            NormalizedThreadCounts[threadIdx].ctr3 = ctr3 * normalizationFactor;
            NormalizedThreadCounts[threadIdx].ctr4 = ctr4 * normalizationFactor;
            NormalizedThreadCounts[threadIdx].ctr5 = ctr5 * normalizationFactor;
            NormalizedThreadCounts[threadIdx].NormalizationFactor = normalizationFactor;
            NormalizedThreadCounts[threadIdx].ctr0total += ctr0;
            NormalizedThreadCounts[threadIdx].ctr1total += ctr1;
            NormalizedThreadCounts[threadIdx].ctr2total += ctr2;
            NormalizedThreadCounts[threadIdx].ctr3total += ctr3;
            NormalizedThreadCounts[threadIdx].ctr4total += ctr4;
            NormalizedThreadCounts[threadIdx].ctr5total += ctr5;

            NormalizedTotalCounts.aperf += NormalizedThreadCounts[threadIdx].aperf;
            NormalizedTotalCounts.mperf += NormalizedThreadCounts[threadIdx].mperf;
            NormalizedTotalCounts.tsc += NormalizedThreadCounts[threadIdx].tsc;
            NormalizedTotalCounts.ctr0 += NormalizedThreadCounts[threadIdx].ctr0;
            NormalizedTotalCounts.ctr1 += NormalizedThreadCounts[threadIdx].ctr1;
            NormalizedTotalCounts.ctr2 += NormalizedThreadCounts[threadIdx].ctr2;
            NormalizedTotalCounts.ctr3 += NormalizedThreadCounts[threadIdx].ctr3;
            NormalizedTotalCounts.ctr4 += NormalizedThreadCounts[threadIdx].ctr4;
            NormalizedTotalCounts.ctr5 += NormalizedThreadCounts[threadIdx].ctr5;
            NormalizedTotalCounts.ctr0total += ctr0;
            NormalizedTotalCounts.ctr1total += ctr1;
            NormalizedTotalCounts.ctr2total += ctr2;
            NormalizedTotalCounts.ctr3total += ctr3;
            NormalizedTotalCounts.ctr4total += ctr4;
            NormalizedTotalCounts.ctr5total += ctr5;
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

            Tuple<string, float>[] retval = new Tuple<string, float>[9];
            retval[0] = new Tuple<string, float>("APERF", dataToLog.aperf);
            retval[1] = new Tuple<string, float>("MPERF", dataToLog.mperf);
            retval[2] = new Tuple<string, float>("TSC", dataToLog.tsc);
            retval[3] = new Tuple<string, float>(ctr0, dataToLog.ctr0);
            retval[4] = new Tuple<string, float>(ctr1, dataToLog.ctr1);
            retval[5] = new Tuple<string, float>(ctr2, dataToLog.ctr2);
            retval[6] = new Tuple<string, float>(ctr3,  dataToLog.ctr3);
            retval[7] = new Tuple<string, float>(ctr4, dataToLog.ctr4);
            retval[8] = new Tuple<string, float>(ctr5, dataToLog.ctr5);
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
            /// Programmable performance counter values
            /// </summary>
            public float ctr0;
            public float ctr1;
            public float ctr2;
            public float ctr3;
            public float ctr4;
            public float ctr5;

            public ulong ctr0total;
            public ulong ctr1total;
            public ulong ctr2total;
            public ulong ctr3total;
            public ulong ctr4total;
            public ulong ctr5total;

            public float NormalizationFactor;
        }

        private Label errorLabel; // ugly whatever
        RadioButton L2DcPriorityRadioButton, L2FairnessRadioButton;

        public override void InitializeCrazyControls(FlowLayoutPanel flowLayoutPanel, Label errLabel)
        {
            flowLayoutPanel.Controls.Clear();

            CheckBox boostCheckbox = new CheckBox();
            boostCheckbox.Text = "Core Performance Boost";
            boostCheckbox.Checked = GetCpbEnabled();
            boostCheckbox.CheckedChanged += HandleCorePerformanceBoostCheckbox;
            boostCheckbox.Width = TextRenderer.MeasureText(boostCheckbox.Text, boostCheckbox.Font).Width + 20;
            flowLayoutPanel.Controls.Add(boostCheckbox);

            CheckBox disableDcPrefetcherCheckbox = new CheckBox();
            disableDcPrefetcherCheckbox.Text = "DC Prefetcher";
            disableDcPrefetcherCheckbox.Checked = GetDcPrefetcherState();
            disableDcPrefetcherCheckbox.CheckedChanged += HandleDisableDcPrefetcherCheckbox;
            disableDcPrefetcherCheckbox.Width = TextRenderer.MeasureText(disableDcPrefetcherCheckbox.Text, disableDcPrefetcherCheckbox.Font).Width + 20;
            flowLayoutPanel.Controls.Add(disableDcPrefetcherCheckbox);
            flowLayoutPanel.SetFlowBreak(disableDcPrefetcherCheckbox, true);

            L2DcPriorityRadioButton = new RadioButton();
            L2DcPriorityRadioButton.Text = "Aggressively prioritize data reqs";
            L2DcPriorityRadioButton.Checked = GetL2PrioritizationState();
            L2DcPriorityRadioButton.CheckedChanged += HandleL2PrioritzationStateChange;
            L2DcPriorityRadioButton.Location = new Point(7, 20);
            L2DcPriorityRadioButton.Width = TextRenderer.MeasureText(L2DcPriorityRadioButton.Text, L2DcPriorityRadioButton.Font).Width + 20;

            L2FairnessRadioButton = new RadioButton();
            L2FairnessRadioButton.Text = "Ensure instruction/data fairness";
            L2FairnessRadioButton.Checked = !GetL2PrioritizationState();
            L2FairnessRadioButton.Location = new Point(7, 44);
            L2FairnessRadioButton.Width = TextRenderer.MeasureText(L2FairnessRadioButton.Text, L2FairnessRadioButton.Font).Width + 20;

            GroupBox L2AccessPriorityGroupbox = new GroupBox();
            L2AccessPriorityGroupbox.Text = "L2 Cache Access Prioritization";
            L2AccessPriorityGroupbox.Width = 200;
            L2AccessPriorityGroupbox.Height = 70;
            L2AccessPriorityGroupbox.Controls.Add(L2DcPriorityRadioButton);
            L2AccessPriorityGroupbox.Controls.Add(L2FairnessRadioButton);
            flowLayoutPanel.Controls.Add(L2AccessPriorityGroupbox);

            errorLabel = errLabel;
        }

        /// <summary>
        /// Get DcacheAggressivePriority state
        /// </summary>
        /// <returns>false = fairness, true = prioritize DC</returns>
        private bool GetL2PrioritizationState()
        {
            Ring0.ReadMsr(CU_CFG, out ulong cuCfg);
            return (cuCfg & (1UL << 10)) > 1; // if set, DC is given priority
        }

        public void HandleL2PrioritzationStateChange(object sender, EventArgs e)
        {
            RadioButton radioButton = (RadioButton)sender;

            // Both should send the event right?
            if (radioButton.Checked && radioButton == L2DcPriorityRadioButton ||
                (radioButton.Checked) && radioButton == L2FairnessRadioButton)
            {
                SetL2Prioritization(false);
            }
            
            if (radioButton.Checked && radioButton == L2FairnessRadioButton ||
                (!radioButton.Checked) && radioButton == L2DcPriorityRadioButton)
            {
                SetL2Prioritization(true);
            }
        }

        public void SetL2Prioritization(bool fairness)
        {
            bool prioritizeDcSet = true;
            for (int threadIdx = 0; threadIdx < GetThreadCount(); threadIdx++)
            {
                ThreadAffinity.Set(1UL << threadIdx);
                Ring0.ReadMsr(CU_CFG, out ulong cuCfg);
                if (fairness) cuCfg &= ~(1UL << 10);   // unset bit 10 = ic requests ensured fairness
                else cuCfg |= (1UL << 10);             // set bit 10 to aggressively prioritize DC
                Ring0.WriteMsr(CU_CFG, cuCfg);
                prioritizeDcSet &= GetL2PrioritizationState();
            }

            if (prioritizeDcSet)
            {
                errorLabel.Text = "L2 will prioritize handling L1D misses over L1i misses";
            }
            else
            {
                errorLabel.Text = "L2 will ensure fairness when handling L1D and L1i misses";
            }
        }

        public void HandleDisableDcPrefetcherCheckbox(object sender, EventArgs e)
        {
            CheckBox checkbox = (CheckBox)sender;
            SetDcPrefetcher(checkbox.Checked);
        }

        private bool GetDcPrefetcherState()
        {
            Ring0.ReadMsr(DC_CFG, out ulong dcCfg);
            return (dcCfg & (1UL << 13)) == 0; // if set, DC hardware prefetcher is disabled
        }

        public void SetDcPrefetcher(bool enable)
        {
            bool allDcPfEnabled = true;
            for (int threadIdx = 0; threadIdx < GetThreadCount(); threadIdx++)
            {
                ThreadAffinity.Set(1UL << threadIdx);
                Ring0.ReadMsr(DC_CFG, out ulong dcCfg);
                if (enable) dcCfg &= ~(1UL << 13);
                else dcCfg |= (1UL << 13);             // set bit 13 to disable hardware prefetcher
                Ring0.WriteMsr(DC_CFG, dcCfg);
                allDcPfEnabled &= GetDcPrefetcherState();
            }

            if (!allDcPfEnabled)
            {
                errorLabel.Text = "Data cache hardware prefetcher disabled";
            }
            else
            {
                errorLabel.Text = "Data cache hardware prefetcher enabled";
            }
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
