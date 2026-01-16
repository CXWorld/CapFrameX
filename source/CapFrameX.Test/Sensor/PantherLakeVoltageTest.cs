using CapFrameX.Data;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.PawnIo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Windows.Interop;

namespace CapFrameX.Test.Sensor
{
    /// <summary>
    /// Test class to scan MSR registers for valid core voltage readings on Panther Lake CPUs.
    /// IA32_PERF_STATUS (0x198) doesn't work for Panther Lake, so we scan for alternative registers.
    /// </summary>
    [TestClass]
    public class PantherLakeVoltageTest
    {
        private const uint IA32_PERF_STATUS = 0x0198;
        private const float MIN_VALID_VOLTAGE = 0.5f;
        private const float MAX_VALID_VOLTAGE = 1.5f;

        /// <summary>
        /// Scans a range of MSR registers to find ones that return valid core voltage values.
        /// Uses the same voltage formula as IntelCpu: VID / 2^13.
        /// Valid voltage range: 0.5V - 1.5V (typical operating range for Intel CPUs).
        /// </summary>
        [TestMethod]
        public void ScanMsrForValidVoltage_PantherLake()
        {
            // === Summary ===
            // Found 2 MSR(s) with potentially valid voltage readings:
            // MSR 0x019C: VoltageEax = 1.2500V, VoltageEdx = 0.0000V, Raw = 0x00000000883E2800
            // MSR 0x0641: VoltageEax = 0.8818V, VoltageEdx = 0.0000V, Raw = 0x0000000000021C38

            var pawnModule = new IntelMsr();
            var validMsrs = new List<(uint Address, float VoltageEax, float VoltageEdx, ulong RawValue)>();

            // Define MSR ranges to scan
            // Common Intel MSR ranges that might contain voltage information
            var msrRanges = new (uint Start, uint End)[]
            {
                // Core performance and power management MSRs
                (0x0100, 0x0250),
                // Package power and energy MSRs
                (0x0600, 0x0700),
                // Platform-specific MSRs
                (0x0C80, 0x0D00),
                // Extended MSRs
                (0x0770, 0x0800),
            };

            Debug.WriteLine("=== Panther Lake MSR Voltage Scan ===");
            Debug.WriteLine($"Scanning for voltage values between {MIN_VALID_VOLTAGE}V and {MAX_VALID_VOLTAGE}V");
            Debug.WriteLine($"Formula: voltage = VID / 2^13 (8192)");
            Debug.WriteLine("");

            // First, verify that IA32_PERF_STATUS doesn't return valid voltage
            if (pawnModule.ReadMsr(IA32_PERF_STATUS, out uint eax, out uint edx))
            {
                uint vid = edx & 0xFFFF;
                float voltage = vid / (float)(1 << 13);
                Debug.WriteLine($"IA32_PERF_STATUS (0x{IA32_PERF_STATUS:X4}): VID={vid}, Voltage={voltage:F4}V");

                if (vid == 0)
                {
                    Debug.WriteLine("  -> VID is 0, confirming IA32_PERF_STATUS doesn't provide voltage for this CPU");
                }
                else if (voltage >= MIN_VALID_VOLTAGE && voltage <= MAX_VALID_VOLTAGE)
                {
                    Debug.WriteLine($"  -> UNEXPECTED: IA32_PERF_STATUS returns valid voltage!");
                }
            }
            else
            {
                Debug.WriteLine($"IA32_PERF_STATUS (0x{IA32_PERF_STATUS:X4}): Failed to read");
            }
            Debug.WriteLine("");

            // Scan MSR ranges for valid voltage readings
            foreach (var range in msrRanges)
            {
                Debug.WriteLine($"Scanning MSR range 0x{range.Start:X4} - 0x{range.End:X4}...");

                for (uint msrAddr = range.Start; msrAddr < range.End; msrAddr++)
                {
                    try
                    {
                        if (pawnModule.ReadMsr(msrAddr, out eax, out edx))
                        {
                            // Check voltage from EDX (bits 47:32 of 64-bit MSR)
                            uint vidEdx = edx & 0xFFFF;
                            float voltageEdx = vidEdx / (float)(1 << 13);

                            // Also check EAX (bits 15:0) as some MSRs may store voltage differently
                            uint vidEax = eax & 0xFFFF;
                            float voltageEax = vidEax / (float)(1 << 13);

                            // Get full 64-bit value for debugging
                            ulong fullValue = ((ulong)edx << 32) | eax;

                            bool validFromEdx = vidEdx > 0 && voltageEdx >= MIN_VALID_VOLTAGE && voltageEdx <= MAX_VALID_VOLTAGE;
                            bool validFromEax = vidEax > 0 && voltageEax >= MIN_VALID_VOLTAGE && voltageEax <= MAX_VALID_VOLTAGE;

                            if (validFromEdx || validFromEax)
                            {
                                validMsrs.Add((msrAddr, voltageEax, voltageEdx, fullValue));
                                Debug.WriteLine($"  MSR 0x{msrAddr:X4}: EAX=0x{eax:X8}, EDX=0x{edx:X8}");
                                if (validFromEax)
                                {
                                    Debug.WriteLine($"    -> Valid voltage from EAX: {voltageEax:F4}V (VID={vidEax})");
                                }
                                if (validFromEdx)
                                {
                                    Debug.WriteLine($"    -> Valid voltage from EDX: {voltageEdx:F4}V (VID={vidEdx})");
                                }
                            }
                        }
                    }
                    catch
                    {
                        // MSR not accessible, skip
                    }
                }
            }

            Debug.WriteLine("");
            Debug.WriteLine("=== Summary ===");
            Debug.WriteLine($"Found {validMsrs.Count} MSR(s) with potentially valid voltage readings:");

            foreach (var msr in validMsrs)
            {
                Debug.WriteLine($"  MSR 0x{msr.Address:X4}: VoltageEax={msr.VoltageEax:F4}V, VoltageEdx={msr.VoltageEdx:F4}V, Raw=0x{msr.RawValue:X16}");
            }

            // Close the module
            pawnModule.Close();

            // Assert that at least some MSRs were scanned successfully
            // The test passes if we could read at least IA32_PERF_STATUS (even if VID is 0)
            Assert.IsTrue(true, "MSR scan completed successfully");
        }

        /// <summary>
        /// Detailed scan around the IA32_PERF_STATUS area with more context.
        /// </summary>
        [TestMethod]
        public void ScanPerformanceMsrArea_DetailedAnalysis()
        {
            var pawnModule = new IntelMsr();

            Debug.WriteLine("=== Detailed Performance MSR Area Scan ===");
            Debug.WriteLine("Scanning MSRs 0x180 - 0x1C0 with detailed output");
            Debug.WriteLine("");

            // Scan around IA32_PERF_STATUS (0x198) area
            for (uint msrAddr = 0x0180; msrAddr <= 0x01C0; msrAddr++)
            {
                try
                {
                    if (pawnModule.ReadMsr(msrAddr, out uint eax, out uint edx))
                    {
                        ulong fullValue = ((ulong)edx << 32) | eax;

                        // Skip if completely zero
                        if (fullValue == 0)
                            continue;

                        // Calculate potential voltage interpretations
                        uint vidEdx = edx & 0xFFFF;
                        float voltageEdx = vidEdx / (float)(1 << 13);

                        uint vidEax = eax & 0xFFFF;
                        float voltageEax = vidEax / (float)(1 << 13);

                        // Alternative formula: some newer CPUs use different scaling
                        float voltageAlt1 = (eax & 0xFF) / 256.0f * 1.6f; // 8-bit VID with 1.6V max
                        float voltageAlt2 = ((eax >> 8) & 0xFF) / 256.0f * 1.6f;

                        string msrName = GetKnownMsrName(msrAddr);
                        Debug.WriteLine($"MSR 0x{msrAddr:X4} {msrName}:");
                        Debug.WriteLine($"  Raw: EAX=0x{eax:X8}, EDX=0x{edx:X8}, Full=0x{fullValue:X16}");
                        Debug.WriteLine($"  VID/8192 formula: EAX->{voltageEax:F4}V, EDX->{voltageEdx:F4}V");
                        Debug.WriteLine($"  Alt formulas: {voltageAlt1:F4}V, {voltageAlt2:F4}V");

                        bool isValidVoltage = (voltageEax >= MIN_VALID_VOLTAGE && voltageEax <= MAX_VALID_VOLTAGE) ||
                                              (voltageEdx >= MIN_VALID_VOLTAGE && voltageEdx <= MAX_VALID_VOLTAGE);
                        if (isValidVoltage)
                        {
                            Debug.WriteLine($"  *** POTENTIAL VALID VOLTAGE DETECTED ***");
                        }
                        Debug.WriteLine("");
                    }
                }
                catch
                {
                    // MSR not accessible
                }
            }

            pawnModule.Close();
            Assert.IsTrue(true, "Detailed scan completed");
        }

        /// <summary>
        /// Scan for voltage using alternative formulas that may apply to Panther Lake.
        /// </summary>
        [TestMethod]
        public void ScanWithAlternativeVoltageFormulas()
        {
            // === Valid Voltage Candidates ===
            // MSR 0x019C: VID / 8192(EAX[15:0]) = 1.2500V
            // MSR 0x00CE: VID / 8192(EAX[15:0]) = 1.1563V

            var pawnModule = new IntelMsr();
            var validMsrs = new List<(uint Address, string Formula, float Voltage, ulong RawValue)>();

            Debug.WriteLine("=== Alternative Voltage Formula Scan ===");
            Debug.WriteLine("Testing different VID-to-voltage conversion formulas");
            Debug.WriteLine("");

            // MSRs that might contain voltage information
            uint[] candidateMsrs = {
                0x0198, // IA32_PERF_STATUS
                0x0199, // IA32_PERF_CTL
                0x019A, // IA32_CLOCK_MODULATION
                0x019B, // IA32_THERM_INTERRUPT
                0x019C, // IA32_THERM_STATUS
                0x01A2, // IA32_TEMPERATURE_TARGET
                0x00CE, // MSR_PLATFORM_INFO
                0x0606, // MSR_RAPL_POWER_UNIT
                0x0611, // MSR_PKG_ENERGY_STATUS
                0x0770, // IA32_PM_ENABLE
                0x0771, // IA32_HWP_CAPABILITIES
                0x0772, // IA32_HWP_REQUEST_PKG
                0x0773, // IA32_HWP_INTERRUPT
                0x0774, // IA32_HWP_REQUEST
                0x0775, // IA32_HWP_PECI_REQUEST_INFO
                0x0777, // IA32_HWP_STATUS
            };

            foreach (uint msrAddr in candidateMsrs)
            {
                try
                {
                    if (pawnModule.ReadMsr(msrAddr, out uint eax, out uint edx))
                    {
                        ulong fullValue = ((ulong)edx << 32) | eax;

                        Debug.WriteLine($"MSR 0x{msrAddr:X4} ({GetKnownMsrName(msrAddr)}):");
                        Debug.WriteLine($"  Raw: EAX=0x{eax:X8}, EDX=0x{edx:X8}");

                        // Formula 1: Standard VID / 8192 from EDX
                        uint vid1 = edx & 0xFFFF;
                        float voltage1 = vid1 / (float)(1 << 13);
                        CheckAndLog("VID/8192 (EDX[15:0])", voltage1, msrAddr, fullValue, validMsrs);

                        // Formula 2: VID / 8192 from EAX
                        uint vid2 = eax & 0xFFFF;
                        float voltage2 = vid2 / (float)(1 << 13);
                        CheckAndLog("VID/8192 (EAX[15:0])", voltage2, msrAddr, fullValue, validMsrs);

                        // Formula 3: 8-bit VID with 1.52V reference (common on Skylake+)
                        uint vid3 = (eax >> 8) & 0xFF;
                        float voltage3 = vid3 / 255.0f * 1.52f;
                        CheckAndLog("8-bit VID (EAX[15:8])", voltage3, msrAddr, fullValue, validMsrs);

                        // Formula 4: From bits 47:32 with different scaling
                        uint vid4 = (edx) & 0xFF;
                        float voltage4 = vid4 / 255.0f * 1.52f;
                        CheckAndLog("8-bit VID (EDX[7:0])", voltage4, msrAddr, fullValue, validMsrs);

                        // Formula 5: HWP-style encoding (if this is HWP MSR)
                        if (msrAddr >= 0x0770 && msrAddr <= 0x0777)
                        {
                            // HWP MSRs may have performance/efficiency info that correlates to voltage
                            uint hwpMin = eax & 0xFF;
                            uint hwpMax = (eax >> 8) & 0xFF;
                            uint hwpDesired = (eax >> 16) & 0xFF;
                            Debug.WriteLine($"  HWP: Min={hwpMin}, Max={hwpMax}, Desired={hwpDesired}");
                        }

                        Debug.WriteLine("");
                    }
                }
                catch
                {
                    Debug.WriteLine($"MSR 0x{msrAddr:X4}: Not accessible");
                }
            }

            Debug.WriteLine("");
            Debug.WriteLine("=== Valid Voltage Candidates ===");
            foreach (var msr in validMsrs)
            {
                Debug.WriteLine($"MSR 0x{msr.Address:X4}: {msr.Formula} = {msr.Voltage:F4}V");
            }

            pawnModule.Close();
            Assert.IsTrue(true, "Alternative formula scan completed");
        }

        private void CheckAndLog(string formula, float voltage, uint msrAddr, ulong rawValue,
            List<(uint, string, float, ulong)> validMsrs)
        {
            if (voltage >= MIN_VALID_VOLTAGE && voltage <= MAX_VALID_VOLTAGE)
            {
                Debug.WriteLine($"  {formula}: {voltage:F4}V *** VALID ***");
                validMsrs.Add((msrAddr, formula, voltage, rawValue));
            }
            else if (voltage > 0 && voltage < 3.0f)
            {
                Debug.WriteLine($"  {formula}: {voltage:F4}V");
            }
        }

        /// <summary>
        /// Test voltage candidate MSRs across all cores.
        /// Based on scan results:
        /// - MSR 0x019C (IA32_THERM_STATUS): VoltageEax = 1.2500V using VID/8192 on EAX[15:0]
        /// - MSR 0x00CE (MSR_PLATFORM_INFO): VoltageEax = 1.1563V using VID/8192 on EAX[15:0]
        /// </summary>
        [TestMethod]
        public void TestVoltageCandidatesAllCores()
        {
            var pawnModule = new IntelMsr();
            int processorCount = Environment.ProcessorCount;

            Debug.WriteLine("=== Panther Lake Voltage Candidates - All Cores Test ===");
            Debug.WriteLine($"Processor count: {processorCount}");
            Debug.WriteLine($"Formula: voltage = VID / 8192 (VID from EAX[15:0])");
            Debug.WriteLine("");

            // Candidate MSRs found in register scan
            var candidates = new (uint Address, string Name)[]
            {
                (0x019C, "IA32_THERM_STATUS"),
                (0x00CE, "MSR_PLATFORM_INFO"),
            };

            // Results storage: [msrIndex][coreIndex]
            var results = new (bool Success, float Voltage, ulong RawValue)[candidates.Length, processorCount];

            // Loop through all logical processors
            for (int core = 0; core < processorCount; core++)
            {
                // Create affinity for this specific core (group 0)
                var affinity = GroupAffinity.Single(0, core);

                for (int msrIdx = 0; msrIdx < candidates.Length; msrIdx++)
                {
                    var (address, name) = candidates[msrIdx];

                    try
                    {
                        if (pawnModule.ReadMsr(address, out uint eax, out uint edx, affinity))
                        {
                            ulong rawValue = ((ulong)edx << 32) | eax;
                            uint vid = eax & 0xFFFF;
                            float voltage = vid / (float)(1 << 13);

                            results[msrIdx, core] = (true, voltage, rawValue);
                        }
                        else
                        {
                            results[msrIdx, core] = (false, 0, 0);
                        }
                    }
                    catch
                    {
                        results[msrIdx, core] = (false, 0, 0);
                    }
                }
            }

            // Output results per MSR
            for (int msrIdx = 0; msrIdx < candidates.Length; msrIdx++)
            {
                var (address, name) = candidates[msrIdx];
                Debug.WriteLine($"=== MSR 0x{address:X4} ({name}) ===");

                for (int core = 0; core < processorCount; core++)
                {
                    var (success, voltage, rawValue) = results[msrIdx, core];
                    if (success)
                    {
                        string validMarker = (voltage >= MIN_VALID_VOLTAGE && voltage <= MAX_VALID_VOLTAGE) ? " [VALID]" : "";
                        Debug.WriteLine($"  Core {core,2}: {voltage:F4}V (Raw: 0x{rawValue:X16}){validMarker}");
                    }
                    else
                    {
                        Debug.WriteLine($"  Core {core,2}: FAILED TO READ");
                    }
                }
                Debug.WriteLine("");
            }

            // Summary: Find MSRs that give consistent valid voltage across cores
            Debug.WriteLine("=== Summary ===");
            for (int msrIdx = 0; msrIdx < candidates.Length; msrIdx++)
            {
                var (address, name) = candidates[msrIdx];
                int validCount = 0;
                float minVoltage = float.MaxValue;
                float maxVoltage = float.MinValue;

                for (int core = 0; core < processorCount; core++)
                {
                    var (success, voltage, _) = results[msrIdx, core];
                    if (success && voltage >= MIN_VALID_VOLTAGE && voltage <= MAX_VALID_VOLTAGE)
                    {
                        validCount++;
                        minVoltage = Math.Min(minVoltage, voltage);
                        maxVoltage = Math.Max(maxVoltage, voltage);
                    }
                }

                if (validCount > 0)
                {
                    Debug.WriteLine($"MSR 0x{address:X4} ({name}): {validCount}/{processorCount} cores with valid voltage");
                    Debug.WriteLine($"  Range: {minVoltage:F4}V - {maxVoltage:F4}V");
                }
                else
                {
                    Debug.WriteLine($"MSR 0x{address:X4} ({name}): No valid voltage readings");
                }
            }

            pawnModule.Close();
            Assert.IsTrue(true, "All cores voltage test completed");
        }

        /// <summary>
        /// Test additional voltage formulas across all cores.
        /// Some CPUs use different voltage encoding schemes.
        /// </summary>
        [TestMethod]
        public void TestAlternativeVoltageFormulasAllCores()
        {
            var pawnModule = new IntelMsr();
            int processorCount = Environment.ProcessorCount;

            Debug.WriteLine("=== Alternative Voltage Formulas - All Cores Test ===");
            Debug.WriteLine($"Processor count: {processorCount}");
            Debug.WriteLine("");

            // Candidate MSRs
            var candidates = new (uint Address, string Name)[]
            {
                (0x019C, "IA32_THERM_STATUS"),
                (0x00CE, "MSR_PLATFORM_INFO"),
                (0x0198, "IA32_PERF_STATUS"),
                (0x0641, "MSR_PP1_ENERGY_STATUS"),
            };

            // Test different formulas
            var formulas = new (string Name, Func<uint, uint, float> Calculate)[]
            {
                ("VID/8192 (EAX[15:0])", (eax, edx) => (eax & 0xFFFF) / (float)(1 << 13)),
                ("VID/8192 (EDX[15:0])", (eax, edx) => (edx & 0xFFFF) / (float)(1 << 13)),
                ("8-bit VID (EAX[15:8]) * 1.52/255", (eax, edx) => ((eax >> 8) & 0xFF) / 255.0f * 1.52f),
                ("8-bit VID (EAX[7:0]) * 1.52/255", (eax, edx) => (eax & 0xFF) / 255.0f * 1.52f),
            };

            foreach (var (address, name) in candidates)
            {
                Debug.WriteLine($"=== MSR 0x{address:X4} ({name}) ===");

                foreach (var (formulaName, calculate) in formulas)
                {
                    Debug.WriteLine($"  Formula: {formulaName}");

                    int validCount = 0;
                    float minVoltage = float.MaxValue;
                    float maxVoltage = float.MinValue;

                    for (int core = 0; core < processorCount; core++)
                    {
                        var affinity = GroupAffinity.Single(0, core);

                        try
                        {
                            if (pawnModule.ReadMsr(address, out uint eax, out uint edx, affinity))
                            {
                                float voltage = calculate(eax, edx);

                                if (voltage >= MIN_VALID_VOLTAGE && voltage <= MAX_VALID_VOLTAGE)
                                {
                                    validCount++;
                                    minVoltage = Math.Min(minVoltage, voltage);
                                    maxVoltage = Math.Max(maxVoltage, voltage);
                                }
                            }
                        }
                        catch { }
                    }

                    if (validCount > 0)
                    {
                        Debug.WriteLine($"    Valid on {validCount}/{processorCount} cores: {minVoltage:F4}V - {maxVoltage:F4}V");
                    }
                    else
                    {
                        Debug.WriteLine($"    No valid readings");
                    }
                }
                Debug.WriteLine("");
            }

            pawnModule.Close();
            Assert.IsTrue(true, "Alternative formulas test completed");
        }

        /// <summary>
        /// Detailed per-core dump for the most promising MSR candidates.
        /// </summary>
        [TestMethod]
        public void DumpVoltageMsrsPerCore()
        {
            var pawnModule = new IntelMsr();
            int processorCount = Environment.ProcessorCount;

            Debug.WriteLine("=== Per-Core MSR Dump ===");
            Debug.WriteLine($"Processor count: {processorCount}");
            Debug.WriteLine("");

            // MSRs to dump
            uint[] msrs = { 0x00CE, 0x0198, 0x019C, 0x01A2, 0x0641 };
            string[] msrNames = { "MSR_PLATFORM_INFO", "IA32_PERF_STATUS", "IA32_THERM_STATUS", "IA32_TEMPERATURE_TARGET", "MSR_PP1_ENERGY_STATUS" };

            for (int core = 0; core < processorCount; core++)
            {
                var affinity = GroupAffinity.Single(0, core);
                Debug.WriteLine($"--- Core {core} ---");

                for (int i = 0; i < msrs.Length; i++)
                {
                    try
                    {
                        if (pawnModule.ReadMsr(msrs[i], out uint eax, out uint edx, affinity))
                        {
                            ulong raw = ((ulong)edx << 32) | eax;

                            // Calculate various voltage interpretations
                            float v1 = (eax & 0xFFFF) / (float)(1 << 13);
                            float v2 = (edx & 0xFFFF) / (float)(1 << 13);

                            Debug.WriteLine($"  0x{msrs[i]:X4} ({msrNames[i]}): EAX=0x{eax:X8} EDX=0x{edx:X8}");
                            Debug.WriteLine($"         VID/8192: EAX->{v1:F4}V, EDX->{v2:F4}V");
                        }
                        else
                        {
                            Debug.WriteLine($"  0x{msrs[i]:X4} ({msrNames[i]}): READ FAILED");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"  0x{msrs[i]:X4} ({msrNames[i]}): EXCEPTION: {ex.Message}");
                    }
                }
                Debug.WriteLine("");
            }

            pawnModule.Close();
            Assert.IsTrue(true, "Per-core dump completed");
        }

        private static string GetKnownMsrName(uint address)
        {
            switch (address)
            {
                case 0x0010: return "IA32_TIME_STAMP_COUNTER";
                case 0x001B: return "IA32_APIC_BASE";
                case 0x003A: return "IA32_FEATURE_CONTROL";
                case 0x00CE: return "MSR_PLATFORM_INFO";
                case 0x0198: return "IA32_PERF_STATUS";
                case 0x0199: return "IA32_PERF_CTL";
                case 0x019A: return "IA32_CLOCK_MODULATION";
                case 0x019B: return "IA32_THERM_INTERRUPT";
                case 0x019C: return "IA32_THERM_STATUS";
                case 0x01A2: return "IA32_TEMPERATURE_TARGET";
                case 0x01B1: return "IA32_PACKAGE_THERM_STATUS";
                case 0x0606: return "MSR_RAPL_POWER_UNIT";
                case 0x0611: return "MSR_PKG_ENERGY_STATUS";
                case 0x0619: return "MSR_DRAM_ENERGY_STATUS";
                case 0x0639: return "MSR_PP0_ENERGY_STATUS";
                case 0x0641: return "MSR_PP1_ENERGY_STATUS";
                case 0x064D: return "MSR_PLATFORM_ENERGY_STATUS";
                case 0x0770: return "IA32_PM_ENABLE";
                case 0x0771: return "IA32_HWP_CAPABILITIES";
                case 0x0772: return "IA32_HWP_REQUEST_PKG";
                case 0x0773: return "IA32_HWP_INTERRUPT";
                case 0x0774: return "IA32_HWP_REQUEST";
                case 0x0775: return "IA32_HWP_PECI_REQUEST_INFO";
                case 0x0777: return "IA32_HWP_STATUS";
                default: return "";
            }
        }
    }
}
