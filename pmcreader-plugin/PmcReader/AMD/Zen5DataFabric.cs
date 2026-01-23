using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using PmcReader.Interop;

namespace PmcReader.AMD
{
    public class Zen5DataFabric : Amd19hCpu
    {
        public enum DfType
        {
            Client = 0
        }

        public Zen5DataFabric(DfType dfType)
        {
            architectureName = "Zen 5 UMC";
            List<MonitoringConfig> monitoringConfigList = new List<MonitoringConfig>();
            monitoringConfigList.Add(new UMCConfig(this));
            monitoringConfigList.Add(new UMCSubtimingsConfig(this));
            monitoringConfigList.Add(new CSConfig(this));
            monitoringConfigList.Add(new CMConfig(this));
            monitoringConfigs = monitoringConfigList.ToArray();
        }

        public class CMConfig : MonitoringConfig
        {
            private Zen5DataFabric dataFabric;
            private long lastUpdateTime;
            private const int monitoringThread = 1;
            private ulong[] totals;

            public string[] columns = new string[] { "Item", "BW" };
            public string GetHelpText() { return ""; }
            public CMConfig(Zen5DataFabric dataFabric)
            {
                this.dataFabric = dataFabric;
            }

            public string GetConfigName() { return "CCM"; }
            public string[] GetColumns() { return columns; }
            public void Initialize()
            {
                ThreadAffinity.Set(1UL << monitoringThread);
                ulong evt0 = GetDFBandwidthPerfCtlValue(2, true);
                ulong evt1 = GetDFBandwidthPerfCtlValue(3, true);
                ulong evt2 = GetDFBandwidthPerfCtlValue(4, true);
                ulong evt3 = GetDFBandwidthPerfCtlValue(5, true);
                Ring0.WriteMsr(MSR_DF_PERF_CTL_0, evt0);
                Ring0.WriteMsr(MSR_DF_PERF_CTL_1, evt1);
                Ring0.WriteMsr(MSR_DF_PERF_CTL_2, evt2);
                Ring0.WriteMsr(MSR_DF_PERF_CTL_3, evt3);

                dataFabric.InitializeCoreTotals();
                lastUpdateTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            }

            public MonitoringUpdateResults Update()
            {
                float normalizationFactor = dataFabric.GetNormalizationFactor(ref lastUpdateTime);
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                ThreadAffinity.Set(1UL << monitoringThread);
                ulong ctr0 = ReadAndClearMsr(MSR_DF_PERF_CTR_0);
                ulong ctr1 = ReadAndClearMsr(MSR_DF_PERF_CTR_1);
                ulong ctr2 = ReadAndClearMsr(MSR_DF_PERF_CTR_2);
                ulong ctr3 = ReadAndClearMsr(MSR_DF_PERF_CTR_3);

                dataFabric.ReadPackagePowerCounter();
                results.unitMetrics = new string[4][];
                results.unitMetrics[0] = new string[] { "CCM0 Read", FormatLargeNumber(ctr0 * normalizationFactor * 64) + "B/s", FormatLargeNumber(ctr0 * normalizationFactor), "N/A" };
                results.unitMetrics[1] = new string[] { "CCM0 Write", FormatLargeNumber(ctr1 * normalizationFactor * 64) + "B/s", FormatLargeNumber(ctr1 * normalizationFactor), "N/A" };
                results.unitMetrics[2] = new string[] { "CCM1 Read", FormatLargeNumber(ctr2 * normalizationFactor * 64) + "B/s", FormatLargeNumber(ctr2 * normalizationFactor), "N/A" };
                results.unitMetrics[3] = new string[] { "CCM1 Write", FormatLargeNumber(ctr3 * normalizationFactor * 64) + "B/s", FormatLargeNumber(ctr3 * normalizationFactor), "N/A" };

                ulong total = ctr0 + ctr1 + ctr2 + ctr3;
                results.overallMetrics = new string[] { "Total",
                    FormatLargeNumber(total * normalizationFactor * 64) + "B/s",
                    FormatLargeNumber(total * normalizationFactor),
                    string.Format("{0:F2} W", dataFabric.NormalizedTotalCounts.watts)
                };

                results.overallCounterValues = new Tuple<string, float>[5];
                results.overallCounterValues[0] = new Tuple<string, float>("Package Power", dataFabric.NormalizedTotalCounts.watts);
                results.overallCounterValues[1] = new Tuple<string, float>("Ch 0 Read?", ctr0);
                results.overallCounterValues[2] = new Tuple<string, float>("Ch 0 Write?", ctr1);
                results.overallCounterValues[3] = new Tuple<string, float>("Ch 1 Read?", ctr2);
                results.overallCounterValues[4] = new Tuple<string, float>("Ch 1 Write?", ctr3);
                return results;
            }
        }

        public class CSConfig : MonitoringConfig
        {
            private Zen5DataFabric dataFabric;
            private long lastUpdateTime;
            private const int monitoringThread = 1;
            private ulong[] totals;

            public string[] columns = new string[] { "Item", "BW" };
            public string GetHelpText() { return ""; }
            public CSConfig(Zen5DataFabric dataFabric)
            {
                this.dataFabric = dataFabric;
            }

            public string GetConfigName() { return "CS"; }
            public string[] GetColumns() { return columns; }
            public void Initialize()
            {
                ThreadAffinity.Set(1UL << monitoringThread);
                ulong cs0Read = GetDFBandwidthPerfCtlValue(0, true);
                ulong cs0Write = GetDFBandwidthPerfCtlValue(0, false);
                ulong cs1Read = GetDFBandwidthPerfCtlValue(1, true);
                ulong cs1Write = GetDFBandwidthPerfCtlValue(1, false);
                Ring0.WriteMsr(MSR_DF_PERF_CTL_0, cs0Read);
                Ring0.WriteMsr(MSR_DF_PERF_CTL_1, cs0Write);
                Ring0.WriteMsr(MSR_DF_PERF_CTL_2, cs1Read);
                Ring0.WriteMsr(MSR_DF_PERF_CTL_3, cs1Write);

                dataFabric.InitializeCoreTotals();
                lastUpdateTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            }

            public MonitoringUpdateResults Update()
            {
                float normalizationFactor = dataFabric.GetNormalizationFactor(ref lastUpdateTime);
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                ThreadAffinity.Set(1UL << monitoringThread);
                ulong ctr0 = ReadAndClearMsr(MSR_DF_PERF_CTR_0);
                ulong ctr1 = ReadAndClearMsr(MSR_DF_PERF_CTR_1);
                ulong ctr2 = ReadAndClearMsr(MSR_DF_PERF_CTR_2);
                ulong ctr3 = ReadAndClearMsr(MSR_DF_PERF_CTR_3);

                dataFabric.ReadPackagePowerCounter();
                results.unitMetrics = new string[4][];
                results.unitMetrics[0] = new string[] { "CS0 Read", FormatLargeNumber(ctr0 * normalizationFactor * 64) + "B/s", FormatLargeNumber(ctr0 * normalizationFactor), "N/A" };
                results.unitMetrics[1] = new string[] { "CS0 Write", FormatLargeNumber(ctr1 * normalizationFactor * 64) + "B/s", FormatLargeNumber(ctr1 * normalizationFactor), "N/A" };
                results.unitMetrics[2] = new string[] { "CS1 Read", FormatLargeNumber(ctr2 * normalizationFactor * 64) + "B/s", FormatLargeNumber(ctr2 * normalizationFactor), "N/A" };
                results.unitMetrics[3] = new string[] { "CS1 Write", FormatLargeNumber(ctr3 * normalizationFactor * 64) + "B/s", FormatLargeNumber(ctr3 * normalizationFactor), "N/A" };

                ulong total = ctr0 + ctr1 + ctr2 + ctr3;
                results.overallMetrics = new string[] { "Total",
                    FormatLargeNumber(total * normalizationFactor * 64) + "B/s",
                    FormatLargeNumber(total * normalizationFactor),
                    string.Format("{0:F2} W", dataFabric.NormalizedTotalCounts.watts)
                };

                results.overallCounterValues = new Tuple<string, float>[5];
                results.overallCounterValues[0] = new Tuple<string, float>("Package Power", dataFabric.NormalizedTotalCounts.watts);
                results.overallCounterValues[1] = new Tuple<string, float>("Ch 0 Read?", ctr0);
                results.overallCounterValues[2] = new Tuple<string, float>("Ch 0 Write?", ctr1);
                results.overallCounterValues[3] = new Tuple<string, float>("Ch 1 Read?", ctr2);
                results.overallCounterValues[4] = new Tuple<string, float>("Ch 1 Write?", ctr3);
                return results;
            }
        }

        public class UMCConfig : MonitoringConfig
        {
            private Zen5DataFabric dataFabric;
            private long lastUpdateTime;
            private const int monitoringThread = 1;
            private ulong[] totals;

            public string[] columns = new string[] { "Item", "BW", "Busy", "Total Data", "Pkg Pwr" };
            public string GetHelpText() { return ""; }
            public UMCConfig(Zen5DataFabric dataFabric)
            {
                this.dataFabric = dataFabric;
            }

            public string GetConfigName() { return "UMC"; }
            public string[] GetColumns() { return columns; }
            public void Initialize()
            {
                ThreadAffinity.Set(1UL << monitoringThread);

                ulong hwcrValue;
                Ring0.ReadMsr(HWCR, out hwcrValue);
                hwcrValue |= 1UL << 30; // instructions retired counter
                hwcrValue |= 1UL << 31; // enable UMC counters
                Ring0.WriteMsr(HWCR, hwcrValue);
                Ring0.ReadMsr(HWCR, out hwcrValue);

                ulong clkEvt = GetUmcPerfCtlValue(0, false, false); // clk
                /*OpCode.CpuidTx(0x80000022, 0, out uint extPerfMonAndDbgEax, out uint extPerfMonAndDbgEbx, out uint extPerfMonAndDbgEcx, out uint _, 1);

                // does not work, everything returns 0
                uint umcPerfCtrCount = (extPerfMonAndDbgEbx >> 16) & 0xFF;
                uint umcPerfCtrMask = extPerfMonAndDbgEcx;
                Console.WriteLine(string.Format("{0} UMC PMCs, active mask {1:X}", umcPerfCtrCount, umcPerfCtrMask));
                // From brute force experimentation, the 9900X has eight usable UMC perf counters
                // likely split 4+4

                for (uint i = 0; i < 16; i++)
                {
                    Ring0.WriteMsr(MSR_UMC_PERF_CTL_base + MSR_UMC_PERF_increment * i, clkEvt);
                }*/
                
                ulong casReads = GetUmcPerfCtlValue(0xa, maskReads: false, maskWrites: true); // cas, exclude writes
                ulong casWrites = GetUmcPerfCtlValue(0xa, maskReads: true, maskWrites: false);
                ulong busUtil = GetUmcPerfCtlValue(0x14, maskReads: false, maskWrites: false);
                Ring0.WriteMsr(MSR_UMC_PERF_CTL_base, casReads);
                Ring0.WriteMsr(MSR_UMC_PERF_CTL_base + MSR_UMC_PERF_increment, casWrites);
                Ring0.WriteMsr(MSR_UMC_PERF_CTL_base + MSR_UMC_PERF_increment * 2, clkEvt);
                Ring0.WriteMsr(MSR_UMC_PERF_CTL_base + MSR_UMC_PERF_increment * 3, busUtil);
                Ring0.WriteMsr(MSR_UMC_PERF_CTL_base + MSR_UMC_PERF_increment * 4, casReads);
                Ring0.WriteMsr(MSR_UMC_PERF_CTL_base + MSR_UMC_PERF_increment * 5, casWrites);
                Ring0.WriteMsr(MSR_UMC_PERF_CTL_base + MSR_UMC_PERF_increment * 6, clkEvt);
                Ring0.WriteMsr(MSR_UMC_PERF_CTL_base + MSR_UMC_PERF_increment * 7, busUtil);

                for (uint i = 0; i < 8; i++) Ring0.WriteMsr(MSR_UMC_PERF_CTR_base + MSR_UMC_PERF_increment * i, 0);

                dataFabric.InitializeCoreTotals();
                totals = new ulong[4];
                lastUpdateTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            }

            public MonitoringUpdateResults Update()
            {
                float normalizationFactor = dataFabric.GetNormalizationFactor(ref lastUpdateTime);
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                ThreadAffinity.Set(1UL << monitoringThread);
                ulong ch0Read = ReadAndClearMsr(MSR_UMC_PERF_CTR_base);
                ulong ch0Write = ReadAndClearMsr(MSR_UMC_PERF_CTR_base + MSR_UMC_PERF_increment);
                ulong ch0Clk = ReadAndClearMsr(MSR_UMC_PERF_CTR_base + MSR_UMC_PERF_increment * 2);
                ulong ch0BusUtil = ReadAndClearMsr(MSR_UMC_PERF_CTR_base + MSR_UMC_PERF_increment * 3);
                ulong ch1Read = ReadAndClearMsr(MSR_UMC_PERF_CTR_base + MSR_UMC_PERF_increment * 4);
                ulong ch1Write = ReadAndClearMsr(MSR_UMC_PERF_CTR_base + MSR_UMC_PERF_increment * 5);
                ulong ch1Clk = ReadAndClearMsr(MSR_UMC_PERF_CTR_base + MSR_UMC_PERF_increment * 6);
                ulong ch1BusUtil = ReadAndClearMsr(MSR_UMC_PERF_CTR_base + MSR_UMC_PERF_increment * 7);

                totals[0] += ch0Read;
                totals[1] += ch0Write;
                totals[2] += ch1Read;
                totals[3] += ch1Write;

                dataFabric.ReadPackagePowerCounter();

                // Bus utilization is DATASLOTCLKS so it seems to count at the data rate, not the UMC clock, which is half of the data clock
                results.unitMetrics = new string[4][];
                results.unitMetrics[0] = new string[] { "UMC0 Rd", FormatLargeNumber(ch0Read * normalizationFactor * 64) + "B/s", FormatPercentage(ch0BusUtil / 2, ch0Clk), FormatLargeNumber(totals[0] * 64) + "B", string.Empty };
                results.unitMetrics[1] = new string[] { "UMC0 Wr", FormatLargeNumber(ch0Write * normalizationFactor * 64) + "B/s", FormatPercentage(ch0BusUtil / 2, ch0Clk), FormatLargeNumber(totals[1] * 64) + "B", string.Empty };
                results.unitMetrics[2] = new string[] { "UMC1 Rd", FormatLargeNumber(ch1Read * normalizationFactor * 64) + "B/s", FormatPercentage(ch1BusUtil / 2, ch1Clk), FormatLargeNumber(totals[2] * 64) + "B", string.Empty };
                results.unitMetrics[3] = new string[] { "UMC1 Wr", FormatLargeNumber(ch1Write * normalizationFactor * 64) + "B/s", FormatPercentage(ch1BusUtil / 2, ch1Clk), FormatLargeNumber(totals[3] * 64) + "B", string.Empty };

                ulong accumulatedCas = totals[0] + totals[1] + totals[2] + totals[3];
                ulong totalCas = ch0Read + ch1Read + ch0Write + ch1Write;
                results.overallMetrics = new string[] { "Total",
                    FormatLargeNumber(totalCas * normalizationFactor * 64) + "B/s",
                    FormatPercentage((ch0BusUtil + ch1BusUtil) / 2, ch0Clk + ch1Clk),
                    FormatLargeNumber(accumulatedCas * 64) + "B",
                    string.Format("{0:F2} W", dataFabric.NormalizedTotalCounts.watts)
                };

                List<Tuple<string, float>> overallCounterList = new List<Tuple<string, float>>();
                overallCounterList.Add(new Tuple<string, float>("Package Power", dataFabric.NormalizedTotalCounts.watts));
                overallCounterList.Add(new Tuple<string, float>("UMC0 CAS Read", ch0Read));
                overallCounterList.Add(new Tuple<string, float>("UMC0 CAS Write", ch0Write));
                overallCounterList.Add(new Tuple<string, float>("UMC0 Clk", ch0Clk));
                overallCounterList.Add(new Tuple<string, float>("UMC0 Data Bus Utilized Clk", ch0BusUtil));
                overallCounterList.Add(new Tuple<string, float>("UMC1 CAS Read", ch1Read));
                overallCounterList.Add(new Tuple<string, float>("UMC1 CAS Write", ch1Write));
                overallCounterList.Add(new Tuple<string, float>("UMC1 Clk", ch1Clk));
                overallCounterList.Add(new Tuple<string, float>("UMC1 Data Bus Utilized Clk", ch1BusUtil));
                results.overallCounterValues = overallCounterList.ToArray();
                return results;
            }
        }

        public class UMCSubtimingsConfig : MonitoringConfig
        {
            private Zen5DataFabric dataFabric;
            private long lastUpdateTime;
            private const int monitoringThread = 1;
            private ulong[] totals;

            public string[] columns = new string[] { "Item", "Item*64B", "Pkg Pwr" };
            public string GetHelpText() { return ""; }
            public UMCSubtimingsConfig(Zen5DataFabric dataFabric)
            {
                this.dataFabric = dataFabric;
            }

            public string GetConfigName() { return "Subtimings"; }
            public string[] GetColumns() { return columns; }
            public void Initialize()
            {
                ThreadAffinity.Set(1UL << monitoringThread);

                ulong hwcrValue;
                Ring0.ReadMsr(HWCR, out hwcrValue);
                hwcrValue |= 1UL << 30; // instructions retired counter
                hwcrValue |= 1UL << 31; // enable UMC counters
                Ring0.WriteMsr(HWCR, hwcrValue);
                Ring0.ReadMsr(HWCR, out hwcrValue);

                ulong clkEvt = GetUmcPerfCtlValue(0, false, false); // clk
                /*OpCode.CpuidTx(0x80000022, 0, out uint extPerfMonAndDbgEax, out uint extPerfMonAndDbgEbx, out uint extPerfMonAndDbgEcx, out uint _, 1);

                // does not work, everything returns 0
                uint umcPerfCtrCount = (extPerfMonAndDbgEbx >> 16) & 0xFF;
                uint umcPerfCtrMask = extPerfMonAndDbgEcx;
                Console.WriteLine(string.Format("{0} UMC PMCs, active mask {1:X}", umcPerfCtrCount, umcPerfCtrMask));
                // From brute force experimentation, the 9900X has eight usable UMC perf counters
                // likely split 4+4

                for (uint i = 0; i < 16; i++)
                {
                    Ring0.WriteMsr(MSR_UMC_PERF_CTL_base + MSR_UMC_PERF_increment * i, clkEvt);
                }*/

                ulong cas = GetUmcPerfCtlValue(0xa, maskReads: false, maskWrites: false);
                ulong activate = GetUmcPerfCtlValue(5, maskReads: false, maskWrites: false);
                ulong precharge = GetUmcPerfCtlValue(0x6, maskReads: false, maskWrites: false);
                ulong busUtil = GetUmcPerfCtlValue(0x14, maskReads: false, maskWrites: false);
                Ring0.WriteMsr(MSR_UMC_PERF_CTL_base, cas);
                Ring0.WriteMsr(MSR_UMC_PERF_CTL_base + MSR_UMC_PERF_increment, activate);
                Ring0.WriteMsr(MSR_UMC_PERF_CTL_base + MSR_UMC_PERF_increment * 2, precharge);
                Ring0.WriteMsr(MSR_UMC_PERF_CTL_base + MSR_UMC_PERF_increment * 3, busUtil);
                Ring0.WriteMsr(MSR_UMC_PERF_CTL_base + MSR_UMC_PERF_increment * 4, cas);
                Ring0.WriteMsr(MSR_UMC_PERF_CTL_base + MSR_UMC_PERF_increment * 5, activate);
                Ring0.WriteMsr(MSR_UMC_PERF_CTL_base + MSR_UMC_PERF_increment * 6, precharge);
                Ring0.WriteMsr(MSR_UMC_PERF_CTL_base + MSR_UMC_PERF_increment * 7, busUtil);

                for (uint i = 0; i < 8; i++) Ring0.WriteMsr(MSR_UMC_PERF_CTR_base + MSR_UMC_PERF_increment * i, 0);

                dataFabric.InitializeCoreTotals();
                totals = new ulong[4];
                lastUpdateTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            }

            public MonitoringUpdateResults Update()
            {
                float normalizationFactor = dataFabric.GetNormalizationFactor(ref lastUpdateTime);
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                ThreadAffinity.Set(1UL << monitoringThread);
                ulong ch0Cas = ReadAndClearMsr(MSR_UMC_PERF_CTR_base);
                ulong ch0Activate = ReadAndClearMsr(MSR_UMC_PERF_CTR_base + MSR_UMC_PERF_increment);
                ulong ch0Precharge = ReadAndClearMsr(MSR_UMC_PERF_CTR_base + MSR_UMC_PERF_increment * 2);
                ulong ch0BusUtil = ReadAndClearMsr(MSR_UMC_PERF_CTR_base + MSR_UMC_PERF_increment * 3);
                ulong ch1Cas = ReadAndClearMsr(MSR_UMC_PERF_CTR_base + MSR_UMC_PERF_increment * 4);
                ulong ch1Activate = ReadAndClearMsr(MSR_UMC_PERF_CTR_base + MSR_UMC_PERF_increment * 5);
                ulong ch1Precharge = ReadAndClearMsr(MSR_UMC_PERF_CTR_base + MSR_UMC_PERF_increment * 6);
                ulong ch1BusUtil = ReadAndClearMsr(MSR_UMC_PERF_CTR_base + MSR_UMC_PERF_increment * 7);

                totals[0] += ch0Cas;
                totals[1] += ch0Activate;
                totals[2] += ch1Cas;
                totals[3] += ch1Activate;

                dataFabric.ReadPackagePowerCounter();

                results.unitMetrics = new string[6][];
                results.unitMetrics[0] = new string[] { "UMC0 CAS", FormatLargeNumber(ch0Cas * normalizationFactor * 64) + "B/s", string.Empty };
                results.unitMetrics[1] = new string[] { "UMC0 ACT", FormatLargeNumber(ch0Activate * normalizationFactor * 64) + "B/s", string.Empty };
                results.unitMetrics[2] = new string[] { "UMC0 Precharge", FormatLargeNumber(ch0Precharge * normalizationFactor * 64) + "B/s", string.Empty };
                results.unitMetrics[3] = new string[] { "UMC1 CAS", FormatLargeNumber(ch1Cas * normalizationFactor * 64) + "B/s", string.Empty };
                results.unitMetrics[4] = new string[] { "UMC1 ACT", FormatLargeNumber(ch1Activate * normalizationFactor * 64) + "B/s", string.Empty };
                results.unitMetrics[5] = new string[] { "UMC1 Precharge", FormatLargeNumber(ch1Precharge * normalizationFactor * 64) + "B/s", string.Empty };

                ulong accumulatedCas = totals[0] + totals[2];
                ulong totalCas = ch0Cas + ch1Cas;
                results.overallMetrics = new string[] { "Total",
                    FormatLargeNumber(totalCas * normalizationFactor * 64) + "B/s",
                    string.Format("{0:F2} W", dataFabric.NormalizedTotalCounts.watts)
                };

                List<Tuple<string, float>> overallCounterList = new List<Tuple<string, float>>();
                overallCounterList.Add(new Tuple<string, float>("Package Power", dataFabric.NormalizedTotalCounts.watts));
                overallCounterList.Add(new Tuple<string, float>("UMC0 CAS", ch0Cas));
                overallCounterList.Add(new Tuple<string, float>("UMC0 Activate", ch0Activate));
                overallCounterList.Add(new Tuple<string, float>("UMC0 Precharge", ch0Precharge));
                overallCounterList.Add(new Tuple<string, float>("UMC0 Data Bus Utilized Clk", ch0BusUtil));
                overallCounterList.Add(new Tuple<string, float>("UMC1 CAS", ch1Cas));
                overallCounterList.Add(new Tuple<string, float>("UMC1 Activate", ch1Activate));
                overallCounterList.Add(new Tuple<string, float>("UMC1 Precharge", ch1Precharge));
                overallCounterList.Add(new Tuple<string, float>("UMC1 Data Bus Utilized Clk", ch1BusUtil));
                results.overallCounterValues = overallCounterList.ToArray();
                return results;
            }
        }
    }
}
