using System;
using System.Collections.Generic;
using PmcReader.Interop;

namespace PmcReader.AMD
{
    public class Zen2DataFabric : Amd17hCpu
    {
        public enum DfType
        {
            Client = 0,
            DestkopThreadripper = 1,
            Server = 2
        }
        public Zen2DataFabric(DfType dfType)
        {
            architectureName = "Zen 2 Data Fabric";
            List<MonitoringConfig> monitoringConfigList = new List<MonitoringConfig>();
            if (dfType == DfType.Client) monitoringConfigList.Add(new ClientBwConfig(this));
            else if (dfType == DfType.DestkopThreadripper) monitoringConfigList.Add(new TrDramBwConfig(this));

            monitoringConfigList.Add(new Zen2DfTest(this));
            monitoringConfigs = monitoringConfigList.ToArray();
        }

        public class TrDramBwConfig : MonitoringConfig
        {
            private Zen2DataFabric dataFabric;
            private long lastUpdateTime;
            private const int monitoringThread = 1;

            public string[] columns = new string[] { "Item", "DRAM BW" };
            public string GetHelpText() { return ""; }
            public TrDramBwConfig(Zen2DataFabric dataFabric)
            {
                this.dataFabric = dataFabric;
            }

            public string GetConfigName() { return "TR DRAM Bandwidth?"; }
            public string[] GetColumns() { return columns; }
            public void Initialize()
            {
                // Undocumented data fabric mentioned in prelimary PPR, but removed in the latest one
                // prelimary PPR suggests calculating DRAM bandwidth by adding up all these events and
                // multiplying by 64
                // These four are always zero on the 3950X. Possibly for quad channel?
                /*ulong mysteryDramBytes7 = 0x00000001004038C7;
                ulong mysteryDramBytes6 = 0x0000000100403887; */
                // ulong mysteryDramBytes1 = 0x0000000000403847;
                //ulong mysteryDramBytes0 = 0x0000000000403807;

                // Nemes says these four return counts on her TR
                ulong mysteryDramBytes5 = 0x0000000100403847;
                ulong mysteryDramBytes4 = 0x0000000100403807;
                ulong mysteryDramBytes3 = 0x00000000004038C7;
                ulong mysteryDramBytes2 = 0x0000000000403887;

                ThreadAffinity.Set(1UL << monitoringThread);
                Ring0.WriteMsr(MSR_DF_PERF_CTL_0, mysteryDramBytes4);
                Ring0.WriteMsr(MSR_DF_PERF_CTL_1, mysteryDramBytes5);
                Ring0.WriteMsr(MSR_DF_PERF_CTL_2, mysteryDramBytes2);
                Ring0.WriteMsr(MSR_DF_PERF_CTL_3, mysteryDramBytes3);

                lastUpdateTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            }

            public MonitoringUpdateResults Update()
            {
                float normalizationFactor = dataFabric.GetNormalizationFactor(ref lastUpdateTime);
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[4][];
                ThreadAffinity.Set(1UL << monitoringThread);
                ulong mysteryDramBytes4 = ReadAndClearMsr(MSR_DF_PERF_CTR_0) * 64;
                ulong mysteryDramBytes5 = ReadAndClearMsr(MSR_DF_PERF_CTR_1) * 64;
                ulong mysteryDramBytes2 = ReadAndClearMsr(MSR_DF_PERF_CTR_2) * 64;
                ulong mysteryDramBytes3 = ReadAndClearMsr(MSR_DF_PERF_CTR_3) * 64;

                results.unitMetrics[0] = new string[] { "DF Evt 0x87 Umask 0x38", FormatLargeNumber(mysteryDramBytes2 * normalizationFactor) + "B/s" };
                results.unitMetrics[1] = new string[] { "DF Evt 0xC7 Umask 0x38", FormatLargeNumber(mysteryDramBytes3 * normalizationFactor) + "B/s" };
                results.unitMetrics[2] = new string[] { "DF Evt 0x107 Umask 0x38", FormatLargeNumber(mysteryDramBytes4 * normalizationFactor) + "B/s" };
                results.unitMetrics[3] = new string[] { "DF Evt 0x147 Umask 0x38", FormatLargeNumber(mysteryDramBytes5 * normalizationFactor) + "B/s" };

                results.overallMetrics = new string[] { "Overall",
                    FormatLargeNumber((mysteryDramBytes4 + mysteryDramBytes5 + mysteryDramBytes2 + mysteryDramBytes3) * normalizationFactor) + "B/s" };
                return results;
            }
        }

        public class ClientBwConfig : MonitoringConfig
        {
            private Zen2DataFabric dataFabric;
            private long lastUpdateTime;
            private const int monitoringThread = 1;
            private ulong[] totals;

            public string[] columns = new string[] { "Item", "Bandwidth", "Total Data", "Pkg Pwr" };
            public string GetHelpText() { return ""; }
            public ClientBwConfig(Zen2DataFabric dataFabric)
            {
                this.dataFabric = dataFabric;
            }

            public string GetConfigName() { return "MTS/RNR DRAM Bandwidth??"; }
            public string[] GetColumns() { return columns; }
            public void Initialize()
            {
                ThreadAffinity.Set(1UL << monitoringThread);
                /* From experimentation, the umask seems to be laid out as:
                 * bit 0: include NT writes, but requires bit 3 to be set???
                 * bit 1: unknown (very low counts)
                 * bit 2: unknown (very low counts)
                 * bit 3: writes
                 * bit 4: unknown (very low counts for normal reads/writes, zero counts for NT write)
                 * bit 5: reads
                 * bit 6: unknown (zero)
                 * bit 7: unknown (zero)
                 * Unit masks tested on a 3950X and 4800H
                 * These work for events 0x7 and 0x47, which seem to correspond to the two memory channels
                 * based on one of them reading zero if the DIMMs for one channel are pulled
                 */
                Ring0.WriteMsr(MSR_DF_PERF_CTL_0, GetDFPerfCtlValue(0x7, 0x20, true, 0, 0)); // ch0 read?
                Ring0.WriteMsr(MSR_DF_PERF_CTL_1, GetDFPerfCtlValue(0x7, 0x8, true, 0, 0));  // ch0 write?
                Ring0.WriteMsr(MSR_DF_PERF_CTL_2, GetDFPerfCtlValue(0x47, 0x20, true, 0, 0));// ch1 read?
                Ring0.WriteMsr(MSR_DF_PERF_CTL_3, GetDFPerfCtlValue(0x47, 0x8, true, 0, 0)); // ch1 write?

                Ring0.WriteMsr(MSR_DF_PERF_CTR_0, 0);
                Ring0.WriteMsr(MSR_DF_PERF_CTR_1, 0);
                Ring0.WriteMsr(MSR_DF_PERF_CTR_2, 0);
                Ring0.WriteMsr(MSR_DF_PERF_CTR_3, 0);

                dataFabric.InitializeCoreTotals();
                lastUpdateTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                totals = new ulong[4];
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
                totals[0] += ctr0;
                totals[1] += ctr1;
                totals[2] += ctr2;
                totals[3] += ctr3;
                ulong allData = totals[0] + totals[1] + totals[2] + totals[3];

                dataFabric.ReadPackagePowerCounter();
                results.unitMetrics = new string[4][];
                results.unitMetrics[0] = new string[] { "Ch 0 Read?", FormatLargeNumber(ctr0 * normalizationFactor * 64) + "B/s", FormatLargeNumber(totals[0] * 64) + "B", "N/A" };
                results.unitMetrics[1] = new string[] { "Ch 0 Write?", FormatLargeNumber(ctr1 * normalizationFactor * 64) + "B/s", FormatLargeNumber(totals[1] * 64) + "B", "N/A" };
                results.unitMetrics[2] = new string[] { "Ch 1 Read?", FormatLargeNumber(ctr2 * normalizationFactor * 64) + "B/s", FormatLargeNumber(totals[2] * 64) + "B", "N/A" };
                results.unitMetrics[3] = new string[] { "Ch 1 Write?", FormatLargeNumber(ctr3 * normalizationFactor * 64) + "B/s", FormatLargeNumber(totals[3] * 64) + "B", "N/A" };

                ulong total = ctr0 + ctr1 + ctr2 + ctr3;
                results.overallMetrics = new string[] { "Total",
                    FormatLargeNumber(total * normalizationFactor * 64) + "B/s",
                    FormatLargeNumber(allData * 64) + "B",
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

        public class Zen2DfTest : MonitoringConfig
        {
            private Zen2DataFabric dataFabric;
            private long lastUpdateTime;
            private const int monitoringThread = 1;

            public string[] columns = new string[] { "Item", "Data BW" };
            public string GetHelpText() { return ""; }
            public Zen2DfTest(Zen2DataFabric dataFabric)
            {
                this.dataFabric = dataFabric;
            }

            public string GetConfigName() { return "DF Test"; }
            public string[] GetColumns() { return columns; }
            public void Initialize()
            {
                // 0x38 = requests with data (documented for Milan)
                // instances 0,1 = CS0,1 tied to memory controllers
                // instances 4,5 count up when testing copy bw to iGPU, but undercount at larger copy sizes
                // 4,5 also count up during SSD read
                Ring0.WriteMsr(MSR_DF_PERF_CTL_0, GetDFPerfCtlValue(0x07, 0x38, true, 1, 0)); // ch0 read?
                Ring0.WriteMsr(MSR_DF_PERF_CTL_1, GetDFPerfCtlValue(0x47, 0x38, true, 1, 0));  // ch0 write?
                Ring0.WriteMsr(MSR_DF_PERF_CTL_2, GetDFPerfCtlValue(0x7, 0x38, true, 4, 0));// ch1 read?
                Ring0.WriteMsr(MSR_DF_PERF_CTL_3, GetDFPerfCtlValue(0x47, 0x38, true, 4, 0)); // ch1 write?

                Ring0.WriteMsr(MSR_DF_PERF_CTR_0, 0);
                Ring0.WriteMsr(MSR_DF_PERF_CTR_1, 0);
                Ring0.WriteMsr(MSR_DF_PERF_CTR_2, 0);
                Ring0.WriteMsr(MSR_DF_PERF_CTR_3, 0);

                lastUpdateTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            }

            public MonitoringUpdateResults Update()
            {
                float normalizationFactor = dataFabric.GetNormalizationFactor(ref lastUpdateTime);
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[4][];
                ThreadAffinity.Set(1UL << monitoringThread);
                ulong mysteryOutboundBytes0 = ReadAndClearMsr(MSR_DF_PERF_CTR_0) * 64;
                ulong mysteryOutboundBytes1 = ReadAndClearMsr(MSR_DF_PERF_CTR_1) * 64;
                ulong mysteryOutboundBytes2 = ReadAndClearMsr(MSR_DF_PERF_CTR_2) * 64;
                ulong mysteryOutboundBytes3 = ReadAndClearMsr(MSR_DF_PERF_CTR_3) * 64;

                results.unitMetrics[0] = new string[] { "DF Data Inst 4", FormatLargeNumber(mysteryOutboundBytes0 * normalizationFactor) + "B/s" };
                results.unitMetrics[1] = new string[] { "DF Data Inst 5", FormatLargeNumber(mysteryOutboundBytes1 * normalizationFactor) + "B/s" };
                results.unitMetrics[2] = new string[] { "DF Data Inst 16", FormatLargeNumber(mysteryOutboundBytes2 * normalizationFactor) + "B/s" };
                results.unitMetrics[3] = new string[] { "DF Data Inst 17", FormatLargeNumber(mysteryOutboundBytes3 * normalizationFactor) + "B/s" };

                results.overallMetrics = new string[] { "Overall",
                    FormatLargeNumber((mysteryOutboundBytes0 + mysteryOutboundBytes1 + mysteryOutboundBytes2 + mysteryOutboundBytes3) * normalizationFactor) + "B/s" };
                return results;
            }
        }

        public class OutboundDataConfig : MonitoringConfig
        {
            private Zen2DataFabric dataFabric;
            private long lastUpdateTime;
            private const int monitoringThread = 1;

            public string[] columns = new string[] { "Item", "Outbound Data BW" };
            public string GetHelpText() { return ""; }
            public OutboundDataConfig(Zen2DataFabric dataFabric)
            {
                this.dataFabric = dataFabric;
            }

            public string GetConfigName() { return "Remote Outbound Data???"; }
            public string[] GetColumns() { return columns; }
            public void Initialize()
            {
                /* from preliminary PPR */
                ulong mysteryOutboundBytes3 = 0x800400247;
                ulong mysteryOutboundBytes2 = 0x800400247; // yes the same event is mentioned twice
                ulong mysteryOutboundBytes1 = 0x800400207;
                ulong mysteryOutboundBytes0 = 0x7004002C7;

                ThreadAffinity.Set(1UL << monitoringThread);
                Ring0.WriteMsr(MSR_DF_PERF_CTL_0, mysteryOutboundBytes0);
                Ring0.WriteMsr(MSR_DF_PERF_CTL_1, mysteryOutboundBytes1);
                Ring0.WriteMsr(MSR_DF_PERF_CTL_2, mysteryOutboundBytes2);
                Ring0.WriteMsr(MSR_DF_PERF_CTL_3, mysteryOutboundBytes3);

                lastUpdateTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            }

            public MonitoringUpdateResults Update()
            {
                float normalizationFactor = dataFabric.GetNormalizationFactor(ref lastUpdateTime);
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[4][];
                ThreadAffinity.Set(1UL << monitoringThread);
                ulong mysteryOutboundBytes0 = ReadAndClearMsr(MSR_DF_PERF_CTR_0) * 32;
                ulong mysteryOutboundBytes1 = ReadAndClearMsr(MSR_DF_PERF_CTR_1) * 32;
                ulong mysteryOutboundBytes2 = ReadAndClearMsr(MSR_DF_PERF_CTR_2) * 32;
                ulong mysteryOutboundBytes3 = ReadAndClearMsr(MSR_DF_PERF_CTR_3) * 32;

                results.unitMetrics[0] = new string[] { "DF Evt 0x7C7 Umask 0x2", FormatLargeNumber(mysteryOutboundBytes0 * normalizationFactor) + "B/s" };
                results.unitMetrics[1] = new string[] { "DF Evt 0x807 Umask 0x2", FormatLargeNumber(mysteryOutboundBytes1 * normalizationFactor) + "B/s" };
                results.unitMetrics[2] = new string[] { "DF Evt 0x847 Umask 0x2", FormatLargeNumber(mysteryOutboundBytes2 * normalizationFactor) + "B/s" };
                results.unitMetrics[3] = new string[] { "DF Evt 0x847 Umask 0x2", FormatLargeNumber(mysteryOutboundBytes3 * normalizationFactor) + "B/s" };

                results.overallMetrics = new string[] { "Overall",
                    FormatLargeNumber((mysteryOutboundBytes0 + mysteryOutboundBytes1 + mysteryOutboundBytes2 + mysteryOutboundBytes3) * normalizationFactor) + "B/s" };
                return results;
            }
        }
    }
}
