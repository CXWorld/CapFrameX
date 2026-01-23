using PmcReader.Interop;
using System;
using System.Collections.Generic;

namespace PmcReader.Intel
{
    /// <summary>
    /// The uncore from hell?
    /// </summary>
    public class SandyBridgeUncore : ModernIntelCpu
    {
        public const uint PCU_MSR_PMON_CTR0 = 0xC36;
        public const uint PCU_MSR_PMON_CTR1 = 0xC37;
        public const uint PCU_MSR_PMON_CTR2 = 0xC38;
        public const uint PCU_MSR_PMON_CTR3 = 0xC39;
        public const uint PCU_MSR_PMON_BOX_FILTER = 0xC34;
        public const uint PCU_MSR_PMON_CTL0 = 0xC30;
        public const uint PCU_MSR_PMON_CTL1 = 0xC31;
        public const uint PCU_MSR_PMON_CTL2 = 0xC32;
        public const uint PCU_MSR_PMON_CTL3 = 0xC33;
        public const uint PCU_MSR_CORE_C6_CTR = 0x3FD; // C6 state (deep sleep, power gated?)
        public const uint PCU_MSR_CORE_C3_CTR = 0x3FC; // C3 state (sleep, clock gated)
        public const uint PCU_MSR_PMON_BOX_CTL = 0xC24;

        // for event occupancy selection
        public const byte C0_OCCUPANCY = 0b01;
        public const byte C3_OCCUPANCY = 0b10;
        public const byte C6_OCCUPANCY = 0b11;

        // PCU runs at fixed 800 MHz
        public const ulong PcuFrequency = 8000000;

        public SandyBridgeUncore()
        {
            architectureName = "Sandy Bridge E Uncore";
            List<MonitoringConfig> configs = new List<MonitoringConfig>();
            configs.Add(new VoltageTransitions(this));
            configs.Add(new Limits(this));
            configs.Add(new ChangeAndPhaseShedding(this));
            //configs.Add(new MemoryBandwidth(this)); // does not work because writing pci config doesn't work
            monitoringConfigs = configs.ToArray();
        }

        /// <summary>
        /// Enable and set up power control unit box counters
        /// </summary>
        /// <param name="ctr0">Counter 0 control</param>
        /// <param name="ctr1">Counter 1 control</param>
        /// <param name="ctr2">Counter 2 control</param>
        /// <param name="ctr3">Counter 3 control</param>
        /// <param name="filter">Box filter control</param>
        public void SetupMonitoringSession(ulong ctr0, ulong ctr1, ulong ctr2, ulong ctr3, ulong filter)
        {
            EnableBoxFreeze();
            FreezeBoxCounters();
            Ring0.WriteMsr(PCU_MSR_PMON_CTL0, ctr0);
            Ring0.WriteMsr(PCU_MSR_PMON_CTL1, ctr1);
            Ring0.WriteMsr(PCU_MSR_PMON_CTL2, ctr2);
            Ring0.WriteMsr(PCU_MSR_PMON_CTL3, ctr3);
            Ring0.WriteMsr(PCU_MSR_PMON_BOX_FILTER, filter);
            ClearBoxCounters();
            Ring0.WriteMsr(PCU_MSR_CORE_C3_CTR, 0);
            Ring0.WriteMsr(PCU_MSR_CORE_C6_CTR, 0);
            UnFreezeBoxCounters();
        }

        public void EnableBoxFreeze()
        {
            ulong freezeEnableValue = GetUncoreBoxCtlRegisterValue(false, false, false, true);
            Ring0.WriteMsr(PCU_MSR_PMON_BOX_CTL, freezeEnableValue);
        }

        public void FreezeBoxCounters()
        {
            ulong freezeValue = GetUncoreBoxCtlRegisterValue(false, false, true, true);
            Ring0.WriteMsr(PCU_MSR_PMON_BOX_CTL, freezeValue);
        }

        public void UnFreezeBoxCounters()
        {
            ulong freezeValue = GetUncoreBoxCtlRegisterValue(false, false, false, true);
            Ring0.WriteMsr(PCU_MSR_PMON_BOX_CTL, freezeValue);
        }

        public void ClearBoxCounters()
        {
            ulong freezeValue = GetUncoreBoxCtlRegisterValue(false, true, false, true);
            Ring0.WriteMsr(PCU_MSR_PMON_BOX_CTL, freezeValue);
        }

        /// <summary>
        /// Get value to put in PCU perf counter control registers
        /// </summary>
        /// <param name="perfEvent">PCU event. Bit 7 = use occupancy subcounter?</param>
        /// <param name="occ_sel">Occupancy counter to use</param>
        /// <param name="reset">Reset counter to 0</param>
        /// <param name="edge">Edge detect, must set cmask to >= 1</param>
        /// <param name="extra_select">Extra select bit, undocumented...?</param>
        /// <param name="enable">Enable counter</param>
        /// <param name="invert">Invert cmask, must set cmask to >= 1</param>
        /// <param name="cmask">Counter comparison threshold</param>
        /// <param name="occ_invert">Invert cmask for occupancy events, must set cmask >= 1</param>
        /// <param name="occ_edge">Edge detect for occupancy events, must set cmask >= 1</param>
        /// <returns>Value to put in PCU_MSR_PMON_CTLn</returns>
        public static ulong GetPCUPerfEvtSelRegisterValue(byte perfEvent,
            byte occ_sel,
            bool reset,
            bool edge,
            bool extra_select,
            bool enable,
            bool invert,
            byte cmask,
            bool occ_invert,
            bool occ_edge)
        {
            return perfEvent |
                (ulong)(occ_sel & 0x7) << 14 |
                (reset ? 1UL : 0UL) << 17 |
                (edge ? 1UL : 0UL) << 18 |
                (extra_select ? 1UL : 0UL) << 21 |
                (enable ? 1UL : 0UL) << 22 |
                (invert ? 1UL : 0UL) << 23 |
                (ulong)(cmask & 0xF) << 24 |
                (occ_invert ? 1UL : 0UL) << 30 |
                (occ_edge ? 1UL : 0UL) << 31;
        }

        /// <summary>
        /// Get value to put in PCU_MSR_PMON_BOX_FILTER register
        /// </summary>
        /// <param name="filt7_0">band 0</param>
        /// <param name="filt15_8">band 1</param>
        /// <param name="filt23_16">band 2</param>
        /// <param name="filt31_24">band 3</param>
        /// <returns>PCU box filter register vallue</returns>
        public static ulong GetPCUFilterRegisterValue(byte filt7_0,
            byte filt15_8,
            byte filt23_16,
            byte filt31_24)
        {
            return filt7_0 |
                (ulong)filt15_8 << 8 |
                (ulong)filt23_16 << 16 |
                (ulong)filt31_24 << 24;
        }

        public PcuCounterData ReadPcuCounterData()
        {
            float normalizationFactor = GetNormalizationFactor(0);
            PcuCounterData rc = new PcuCounterData();
            FreezeBoxCounters();
            ulong ctr0, ctr1, ctr2, ctr3;
            Ring0.ReadMsr(PCU_MSR_PMON_CTR0, out ctr0);
            Ring0.ReadMsr(PCU_MSR_PMON_CTR1, out ctr1);
            Ring0.ReadMsr(PCU_MSR_PMON_CTR2, out ctr2);
            Ring0.ReadMsr(PCU_MSR_PMON_CTR3, out ctr3);
            rc.ctr0 = ctr0 * normalizationFactor;
            rc.ctr1 = ctr1 * normalizationFactor;
            rc.ctr2 = ctr2 * normalizationFactor;
            rc.ctr3 = ctr3 * normalizationFactor;
            rc.c3 = ReadAndClearMsr(PCU_MSR_CORE_C3_CTR);
            rc.c6 = ReadAndClearMsr(PCU_MSR_CORE_C6_CTR);
            ClearBoxCounters();
            UnFreezeBoxCounters();
            return rc;
        }

        public class PcuCounterData
        {
            public float ctr0;
            public float ctr1;
            public float ctr2;
            public float ctr3;

            /// <summary>
            /// Cycles some core was in C3 state
            /// </summary>
            public float c3;

            /// <summary>
            /// Cycles some core was in C6 state
            /// </summary>
            public float c6;
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

        public class VoltageTransitions : MonitoringConfig
        {
            private SandyBridgeUncore cpu;
            public string GetConfigName() { return "PCU: Voltage Transitions"; }

            public VoltageTransitions(SandyBridgeUncore intelCpu)
            {
                cpu = intelCpu;
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                ulong voltageIncreaseCycles = GetPCUPerfEvtSelRegisterValue(0x1, 0, reset: false, edge: false, extra_select: false, enable: true, invert: false, cmask: 0, occ_invert: false, occ_edge: false);
                ulong voltageIncreaseCount = GetPCUPerfEvtSelRegisterValue(0x1, 0, reset: false, edge: true, extra_select: false, enable: true, invert: false, cmask: 1, occ_invert: false, occ_edge: false);
                ulong voltageDecreaseCycles = GetPCUPerfEvtSelRegisterValue(0x2, 0, reset: false, edge: false, extra_select: false, enable: true, invert: false, cmask: 0, occ_invert: false, occ_edge: false);;
                ulong voltageDecreaseCount = GetPCUPerfEvtSelRegisterValue(0x2, 0, reset: false, edge: true, extra_select: false, enable: true, invert: false, cmask: 1, occ_invert: false, occ_edge: false);
                ulong filter = 0;
                cpu.SetupMonitoringSession(voltageIncreaseCycles, voltageIncreaseCount, voltageDecreaseCycles, voltageDecreaseCount, filter);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[1][];
                PcuCounterData counterData = cpu.ReadPcuCounterData();

                float increaseLatency = counterData.ctr0 / counterData.ctr1;
                float decreaseLatency = counterData.ctr2 / counterData.ctr3;

                // abuse the form code because there's only one unit and uh, there's vertical space
                results.overallMetrics = new string[] { "Voltage Increase",
                    FormatLargeNumber(counterData.ctr0),
                    FormatLargeNumber(counterData.ctr1),
                    string.Format("{0:F1} clk", increaseLatency),
                    string.Format("{0:F2} ms", increaseLatency * (1000 / (float)PcuFrequency))};
                results.unitMetrics[0] = new string[] { "Voltage Decrease",
                    FormatLargeNumber(counterData.ctr2),
                    FormatLargeNumber(counterData.ctr3),
                    string.Format("{0:F1} clk", decreaseLatency),
                    string.Format("{0:F2} ms", increaseLatency * (1000 / (float)PcuFrequency))};
                return results;
            }

            public string[] columns = new string[] { "Item", "Cycles", "Count", "Latency", "Latency" };
            public string GetHelpText() { return ""; }
        }

        public class Limits : MonitoringConfig
        {
            private SandyBridgeUncore cpu;
            public string GetConfigName() { return "PCU: Limits"; }

            public Limits(SandyBridgeUncore intelCpu)
            {
                cpu = intelCpu;
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                ulong thermalLimitCycles = GetPCUPerfEvtSelRegisterValue(0x4, 0, false, false, false, true, false, 0, false, false);
                ulong currentLimitCycles = GetPCUPerfEvtSelRegisterValue(0x7, 0, false, false, false, true, false, 0, false, false);
                ulong osLimitCycles = GetPCUPerfEvtSelRegisterValue(0x6, 0, false, false, false, true, false, 0, false, false);
                ulong powerLimitCycles = GetPCUPerfEvtSelRegisterValue(0x5, 0, false, false, false, true, false, 0, false, false);
                ulong filter = 0;
                cpu.SetupMonitoringSession(thermalLimitCycles, currentLimitCycles, osLimitCycles, powerLimitCycles, filter);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[3][];
                PcuCounterData counterData = cpu.ReadPcuCounterData();

                // abuse the form code because there's only one unit and uh, there's vertical space
                results.overallMetrics = new string[] { "Thermal", string.Format("{0:F2}%", counterData.ctr0 / PcuFrequency) };
                results.unitMetrics[0] = new string[] { "Current", string.Format("{0:F2}%", counterData.ctr1 / PcuFrequency) };
                results.unitMetrics[1] = new string[] { "OS", string.Format("{0:F2}%", counterData.ctr2 / PcuFrequency) };
                results.unitMetrics[2] = new string[] { "Power", string.Format("{0:F2}%", counterData.ctr3 / PcuFrequency) };
                return results;
            }

            public string[] columns = new string[] { "Freq Limit", "Cycles" };
            public string GetHelpText() { return ""; }
        }

        public class ChangeAndPhaseShedding : MonitoringConfig
        {
            private SandyBridgeUncore cpu;
            public string GetConfigName() { return "PCU: Transition Cycles/Phase Shedding"; }

            public ChangeAndPhaseShedding(SandyBridgeUncore intelCpu)
            {
                cpu = intelCpu;
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                ulong voltTransCycles = GetPCUPerfEvtSelRegisterValue(0x3, 0, false, false, false, true, false, 0, false, false);
                ulong freqTransCycles = GetPCUPerfEvtSelRegisterValue(0, 0, false, false, extra_select: true, true, false, 0, false, false);
                ulong phaseSheddingCycles = GetPCUPerfEvtSelRegisterValue(0x2F, 0, false, false, false, true, false, 0, false, false);
                ulong cstateTransCycles = GetPCUPerfEvtSelRegisterValue(0xB, 0, false, false, extra_select: true, true, false, 0, false, false);
                ulong filter = 0;
                cpu.SetupMonitoringSession(voltTransCycles, freqTransCycles, cstateTransCycles, phaseSheddingCycles, filter);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[3][];
                PcuCounterData counterData = cpu.ReadPcuCounterData();

                // abuse the form code because there's only one unit and uh, there's vertical space
                results.overallMetrics = new string[] { "Voltage Transition", string.Format("{0:F2}%", counterData.ctr0 / PcuFrequency) };
                results.unitMetrics[0] = new string[] { "Freq Transition", string.Format("{0:F2}%", counterData.ctr1 / PcuFrequency) };
                results.unitMetrics[1] = new string[] { "C State Transition", string.Format("{0:F2}%", counterData.ctr2 / PcuFrequency) };
                results.unitMetrics[2] = new string[] { "Memory Phase Shedding", string.Format("{0:F2}%", counterData.ctr3 / PcuFrequency) };
                return results;
            }

            public string[] columns = new string[] { "Item", "Cycles" };
            public string GetHelpText() { return ""; }
        }

        public const uint MC_CH_PCI_PMON_BOX_CTL = 0xF4;
        public const uint MC_CH_PCI_PMON_FIXED_CTL = 0xF0; 
        public const uint MC_CH_PCI_PMON_FIXED_CTR = 0xD0; // Fixed counter, counts IMC cycles
        public const uint MC_CH_PCI_PMON_CTL0 = 0xD8;
        public const uint MC_CH_PCI_PMON_CTL1 = 0xDC;
        public const uint MC_CH_PCI_PMON_CTL2 = 0xE0;
        public const uint MC_CH_PCI_PMON_CTL3 = 0xE4;
        public const uint MC_CH_PCI_PMON_CTR0 = 0xA0;
        public const uint MC_CH_PCI_PMON_CTR1 = 0xA8;
        public const uint MC_CH_PCI_PMON_CTR2 = 0xB0;
        public const uint MC_CH_PCI_PMON_CTR3 = 0xB8;

        /// <summary>
        /// Holds counter data for all four IMC channels
        /// </summary>
        public class ImcCounterData
        {
            public float[] cycles;
            public float[] ctr0;
            public float[] ctr1;
            public float[] ctr2;
            public float[] ctr3;
        }


        private ImcCounterData imcCounterData;

        /// <summary>
        /// Get the PCICFG base address of the memory controller
        /// </summary>
        /// <param name="channel">Memory controller channel</param>
        /// <returns>PCICFG base address</returns>
        public uint GetImcPciAddress(uint channel)
        {
            if (channel == 0) return Ring0.GetPciAddress(0, 0x16, 0xF0);
            else if (channel == 1) return Ring0.GetPciAddress(0, 0x16, 0xF1);
            else if (channel == 2) return Ring0.GetPciAddress(0, 0x16, 0xF4);
            else if (channel == 3) return Ring0.GetPciAddress(0, 0x16, 0xF5);
            throw new Exception("Sandy Bridge E only has four memory channels");
        }

        /// <summary>
        /// Write an IMC PMON register across all four channels
        /// </summary>
        /// <param name="register">PCICFG register</param>
        /// <param name="value">Register to write</param>
        public void WriteImcRegister(uint register, uint value)
        {
            for (uint channel = 0; channel < 4; channel++)
            {
                uint imcAddress = GetImcPciAddress(channel);
                bool writeSuccess = Ring0.WritePciConfigPcm(imcAddress, register, value);
                Ring0.ReadPciConfig(imcAddress, register, out uint readValue);
                if (!writeSuccess)
                {
                    Console.WriteLine("Write failed");
                }

                if (readValue != value)
                {
                    Console.WriteLine("Wrote {0:X} but got {1:X}", value, readValue);
                }
            }
        }

        /// <summary>
        /// Enable fixed IMC counter, which tracks number of DRAM clocks = half DDR speed. Also reset it
        /// </summary>
        public void EnableImcFixedCounter()
        {
            uint enableFixedCounterValue = 1U << 22;
            WriteImcRegister(MC_CH_PCI_PMON_FIXED_CTL, enableFixedCounterValue);
        }

        /// <summary>
        /// Get value to program IMC perf counter with
        /// </summary>
        /// <param name="evt">Event</param>
        /// <param name="umask">Unit mask</param>
        /// <param name="edge">Edge detect (increment on change from 0 to 1)</param>
        /// <param name="invert">Invert cmask</param>
        /// <param name="cmask">Increment counter only if count exceeds cmask in that cycle</param>
        /// <returns>Perf counter value</returns>
        public static uint GetImcPerfControlValue(byte evt, byte umask, bool edge, bool invert, byte cmask)
        {
            return evt |
                (uint)umask << 8 |
                (edge ? 1U << 18 : 0) |
                1U << 22 | // enable
                (invert ? 1U << 23 : 0) |
                (uint)cmask << 31;
        }

        /// <summary>
        /// Program performance counters across all four IMC channels
        /// </summary>
        public void ProgramImcPerfCounters(uint ctr0, uint ctr1, uint ctr2, uint ctr3)
        {
            this.ClearAndInitImcCounterData();
            if (Ring0.WaitPciBusMutex(10))
            {
                EnableImcFixedCounter();
                WriteImcRegister(MC_CH_PCI_PMON_CTL0, ctr0);
                WriteImcRegister(MC_CH_PCI_PMON_CTL1, ctr1);
                WriteImcRegister(MC_CH_PCI_PMON_CTL2, ctr2);
                WriteImcRegister(MC_CH_PCI_PMON_CTL3, ctr3);
                Ring0.ReleasePciBusMutex();
            }
        }

        /// <summary>
        /// Set IMC performance monitoring control box to allow freezing counters,
        /// across all four channels
        /// </summary>
        public void EnableImcBoxFreeze()
        {
            for (uint channel = 0; channel < 4; channel++)
            {
                uint imcAddress = GetImcPciAddress(channel);
                uint boxControlValue = 0;
                Ring0.ReadPciConfig(imcAddress, MC_CH_PCI_PMON_BOX_CTL, out boxControlValue);
                boxControlValue |= 1 << 16; // bit 16 = freeze enable
                Ring0.WritePciConfigPcm(imcAddress, MC_CH_PCI_PMON_BOX_CTL, boxControlValue);
            }
        }

        public void FreezeImcBox()
        {
            for (uint channel = 0; channel < 4; channel++)
            {
                uint imcAddress = GetImcPciAddress(channel);
                uint boxControlValue = 0;
                Ring0.ReadPciConfig(imcAddress, MC_CH_PCI_PMON_BOX_CTL, out boxControlValue);
                boxControlValue |= 1 << 8; // bit 8 = freeze counters
                Ring0.WritePciConfigPcm(imcAddress, MC_CH_PCI_PMON_BOX_CTL, boxControlValue);
            }
        }

        public void UnfreezeImcBox()
        {
            for (uint channel = 0; channel < 4; channel++)
            {
                uint imcAddress = GetImcPciAddress(channel);
                uint boxControlValue = 0;
                Ring0.ReadPciConfig(imcAddress, MC_CH_PCI_PMON_BOX_CTL, out boxControlValue);
                boxControlValue &= ~(1U << 8); // bit 8 = freeze counters
                Ring0.WritePciConfigPcm(imcAddress, MC_CH_PCI_PMON_BOX_CTL, boxControlValue);
            }
        }

        /// <summary>
        /// Clear and initialize IMC counter data
        /// </summary>
        public void ClearAndInitImcCounterData()
        {
            if (this.imcCounterData == null) this.imcCounterData = new ImcCounterData();
            if (this.imcCounterData.cycles == null) this.imcCounterData.cycles = new float[4];
            if (this.imcCounterData.ctr0 == null) this.imcCounterData.ctr0 = new float[4];
            if (this.imcCounterData.ctr1 == null) this.imcCounterData.ctr1 = new float[4];
            if (this.imcCounterData.ctr2 == null) this.imcCounterData.ctr2 = new float[4];
            if (this.imcCounterData.ctr3 == null) this.imcCounterData.ctr3 = new float[4];

            for (uint i = 0; i < 4; i++)
            {
                this.imcCounterData.cycles[i] = 0;
                this.imcCounterData.ctr0[i] = 0;
                this.imcCounterData.ctr1[i] = 0;
                this.imcCounterData.ctr2[i] = 0;
                this.imcCounterData.ctr3[i] = 0;
            }
        }

        /// <summary>
        /// Since ReadPciConfig only reads 32 bits, and counters are 2x32, this one reads both at once, and clears both at once
        /// </summary>
        /// <param name="address">PCICFG base address</param>
        /// <param name="register">PCICFG register base</param>
        /// <returns>64-bit counter value</returns>
        public ulong ReadAndClear64BitCtr(uint address, uint register)
        {
            Ring0.ReadPciConfig(address, register, out uint ctrlo);
            Ring0.ReadPciConfig(address, register + 4, out uint ctrhi);
            ulong rc = ctrlo + ((ulong)ctrhi << 32);
            Ring0.WritePciConfigPcm(address, register, 0);
            Ring0.WritePciConfigPcm(address, register + 4, 0);
            return rc;
        }

        public void UpdateImcCounters()
        {
            float normalizationFactor = GetNormalizationFactor(0);
            ImcCounterData rc = new ImcCounterData();
            ulong ctr0, ctr1, ctr2, ctr3, fixedCtr;

            for (uint channel = 0; channel < 4; channel++)
            {
                uint baseAddress = GetImcPciAddress(channel);
                fixedCtr = ReadAndClear64BitCtr(baseAddress, MC_CH_PCI_PMON_FIXED_CTR);
                ctr0 = ReadAndClear64BitCtr(baseAddress, MC_CH_PCI_PMON_CTR0);
                ctr1 = ReadAndClear64BitCtr(baseAddress, MC_CH_PCI_PMON_CTR1);
                ctr2 = ReadAndClear64BitCtr(baseAddress, MC_CH_PCI_PMON_CTR2);
                ctr3 = ReadAndClear64BitCtr(baseAddress, MC_CH_PCI_PMON_CTR3);
                this.imcCounterData.cycles[channel] = fixedCtr * normalizationFactor;
                this.imcCounterData.ctr0[channel] = ctr0 * normalizationFactor;
                this.imcCounterData.ctr1[channel] = ctr1 * normalizationFactor;
                this.imcCounterData.ctr2[channel] = ctr2 * normalizationFactor;
                this.imcCounterData.ctr3[channel] = ctr3 * normalizationFactor;
            }
        }

        public class MemoryBandwidth : MonitoringConfig
        {
            private SandyBridgeUncore cpu;
            public string GetConfigName() { return "IMC: Bandwidth"; }

            public MemoryBandwidth(SandyBridgeUncore intelCpu)
            {
                cpu = intelCpu;
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                uint casRd = GetImcPerfControlValue(0x4, 0b11, false, false, 0);
                uint casWr = GetImcPerfControlValue(0x4, 0b1100, false, false, 0);
                uint prechargePageMiss = GetImcPerfControlValue(0x2, 1, false, false, 0);
                uint prechargePageClose = GetImcPerfControlValue(0x2, 0b10, false, false, 0);
                cpu.ProgramImcPerfCounters(casRd, casWr, prechargePageMiss, prechargePageClose);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                cpu.UpdateImcCounters();
                results.unitMetrics = new string[4][];

                float avgClk = 0, totalReads = 0, totalWrites = 0, totalPageMiss = 0, totalPageClose = 0;
                for (uint ch = 0; ch < 4; ch++)
                {
                    float clk = cpu.imcCounterData.cycles[ch];
                    float reads = cpu.imcCounterData.ctr0[ch];
                    float writes = cpu.imcCounterData.ctr1[ch];
                    float pageMiss = cpu.imcCounterData.ctr2[ch];
                    float pageClose = cpu.imcCounterData.ctr3[ch];

                    results.unitMetrics[ch] = new string[]
                    {
                        "Ch" + ch,
                        FormatLargeNumber(clk),
                        FormatLargeNumber(reads * 64) + "B/s",
                        FormatLargeNumber(writes * 64) + "B/s",
                        FormatLargeNumber((reads + writes) * 64) + "B/s",
                        FormatPercentage(reads + writes, pageMiss),
                        FormatLargeNumber(pageClose)
                    };

                    avgClk += clk;
                    totalReads += reads;
                    totalWrites += writes;
                    totalPageMiss += pageMiss;
                    totalPageClose += pageClose;
                }

                avgClk /= 4;
                results.overallMetrics = new string[] 
                { 
                    "Total", FormatLargeNumber(avgClk), 
                    FormatLargeNumber(totalReads * 64) + "B/s",
                    FormatLargeNumber(totalWrites * 64) + "B/s",
                    FormatLargeNumber((totalReads + totalWrites) * 64) + "B/s",
                    FormatPercentage(totalReads + totalWrites, totalPageMiss),
                    FormatLargeNumber(totalPageClose)
                };

                return results;
            }

            public string[] columns = new string[] { "Channel", "Clk", "Read BW", "Write BW", "Total BW", "Page Miss", "Page Close" };
            public string GetHelpText() { return ""; }
        }
    }
}
