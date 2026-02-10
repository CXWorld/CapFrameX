using System;
using System.Diagnostics.Eventing.Reader;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PmcReader
{
    public partial class HaswellForm : Form
    {
        private string cpuManufacturer;
        private byte cpuFamily, cpuModel, cpuStepping;
        MonitoringSetup coreMonitoring, l3Monitoring, dfMonitoring;
        GenericMonitoringArea crazyThings;

        /// <summary>
        /// Yeah it's called haswell because I started there first
        /// and auto-renaming creates some ridiculous issues
        /// </summary>
        public HaswellForm()
        {
            // Use opcode to pick CPU based on cpuid
            cpuManufacturer = Interop.OpCode.GetManufacturerId();
            Interop.OpCode.GetProcessorVersion(out cpuFamily, out cpuModel, out cpuStepping);

            coreMonitoring = new MonitoringSetup();
            l3Monitoring = new MonitoringSetup();
            dfMonitoring = new MonitoringSetup();

            // Override the "data fabric" label since I want to monitor different
            // things on different CPUs, and 'uncore' architectures vary a lot
            string dfLabelOverride = null;
            string l3LabelOverride = null; // thanks piledriver

            if (cpuManufacturer.Equals("GenuineIntel"))
            {
                if (cpuFamily == 0x6)
                {
                    // 0x3D is broadwell but eh, close enough. Most of the events are the same
                    if (cpuModel == 0x46 || cpuModel == 0x45 || cpuModel == 0x3C || cpuModel == 0x3F || cpuModel == 0x3D)
                    {
                        coreMonitoring.monitoringArea = new Intel.Haswell();
                        if (cpuModel == 0x46 || cpuModel == 0x45 || cpuModel == 0x3C || cpuModel == 0x3D)
                        {
                            l3Monitoring.monitoringArea = new Intel.HaswellClientL3();
                            dfMonitoring.monitoringArea = new Intel.HaswellClientArb();
                            dfLabelOverride = "System Agent Monitoring Configs (pick one):";
                        }
                        else if (cpuModel == 0x3F)
                        {
                            l3Monitoring.monitoringArea = new Intel.HaswellEL3();
                        }
                    }
                    else if (cpuModel == 0x2A || cpuModel == 0x2D)
                    {
                        coreMonitoring.monitoringArea = new Intel.SandyBridge();
                        if (cpuModel == 0x2D)
                        {
                            l3Monitoring.monitoringArea = new Intel.SandyBridgeEL3();
                            dfMonitoring.monitoringArea = new Intel.SandyBridgeUncore();
                            dfLabelOverride = "Power Control Unit Monitoring Configs (pick one):";
                        }
                    }
                    // low 4 bits of SKL model = 0xE, except for comet lake h/s, which has model = 0xa5
                    else if ((cpuModel & 0xF) == 0xE || cpuModel == 0xA5)
                    {
                        coreMonitoring.monitoringArea = new Intel.Skylake();
                        l3Monitoring.monitoringArea = new Intel.SkylakeClientL3();
                        dfMonitoring.monitoringArea = new Intel.SkylakeClientArb();
                        dfLabelOverride = "System Agent Monitoring Configs (pick one):";
;                   }
                    else if (cpuModel == 0x7A)
                    {
                        coreMonitoring.monitoringArea = new Intel.GoldmontPlus();
                        dfLabelOverride = "Unused";
                        l3LabelOverride = "Unused";
                    }
                    // ADL-S (0x97), ADL-P (0x9A), RPL (0xB7), RPL-H (0xBA), RPL-HX (0xBF), RPL-U (0xBE)
                    else if (cpuModel == 0x97 || cpuModel == 0x9A
                        || cpuModel == 0xB7 || cpuModel == 0xBA
                        || cpuModel == 0xBF || cpuModel == 0xBE)
                    {
                        coreMonitoring.monitoringArea = new Intel.AlderLake();
                        l3Monitoring.monitoringArea = new Intel.AlderLakeL3();
                        dfLabelOverride = "Unused";
                    }
                    // Meteor Lake
                    else if (cpuModel == 0xAA)
                    {
                        coreMonitoring.monitoringArea = new Intel.MeteorLake();
                        l3Monitoring.monitoringArea = new Intel.MeteorLakeL3();
                        dfMonitoring.monitoringArea = new Intel.MeteorLakeArb();
                    }
                    else if (cpuModel == 0xC6)
                    {
                        coreMonitoring.monitoringArea = new Intel.ArrowLake();
                        l3Monitoring.monitoringArea = new Intel.ArrowLakeL3();
                        dfMonitoring.monitoringArea = new Intel.MeteorLakeArb();
                    }
                    else
                    {
                        coreMonitoring.monitoringArea = new Intel.ModernIntelCpu();
                        dfLabelOverride = "Unused";
                        l3LabelOverride = "Unused";
                    }

                    crazyThings = new Intel.ModernIntelCpu();
                }
            }
            else if (cpuManufacturer.Equals("AuthenticAMD"))
            {
                if (cpuFamily == 0x17)
                {
                    // Matisse (desktop), Epyc/Threadripper, Van Gogh (tiny APU), Renoir (APU)
                    if (cpuModel == 0x71 || cpuModel == 0x31 || cpuModel == 0x90 || cpuModel == 0x60)
                    {
                        coreMonitoring.monitoringArea = new AMD.Zen2();
                        l3Monitoring.monitoringArea = new AMD.Zen2L3Cache();

                        // At least the counters that work on Matisse also seem to work in the same way on Renoir
                        if (cpuModel == 0x71 || cpuModel == 0x90 || cpuModel == 0x60) dfMonitoring.monitoringArea = new AMD.Zen2DataFabric(AMD.Zen2DataFabric.DfType.Client);

                        // Epyc and TR have the same CPU model so we'll drop Epyc on the floor.
                        // We only have enough DF counters to track four channels at a time anyway
                        else if (cpuModel == 0x31) dfMonitoring.monitoringArea = new AMD.Zen2DataFabric(AMD.Zen2DataFabric.DfType.DestkopThreadripper);
                    }
                    else if (cpuModel == 0x1 || cpuModel == 0x18 || cpuModel == 0x8)
                    {
                        coreMonitoring.monitoringArea = new AMD.Zen1();
                        l3Monitoring.monitoringArea = new AMD.ZenL3Cache();
                    }

                    crazyThings = new AMD.Amd17hCpu();
                }
                else if (cpuFamily == 0x19)
                {
                    if (cpuModel == 0x61)
                    {
                        coreMonitoring.monitoringArea = new AMD.Zen4();
                        l3Monitoring.monitoringArea = new AMD.Zen4L3Cache();
                        dfMonitoring.monitoringArea = new AMD.Zen4DataFabric(AMD.Zen4DataFabric.DfType.Client);
                        crazyThings = new AMD.Amd19hCpu();
                    }
                    else
                    {
                        coreMonitoring.monitoringArea = new AMD.Zen3();
                        l3Monitoring.monitoringArea = new AMD.Zen3L3Cache();
                        dfMonitoring.monitoringArea = new AMD.Zen2DataFabric(AMD.Zen2DataFabric.DfType.Client);
                        crazyThings = new AMD.Amd17hCpu();
                    }
                }
                else if (cpuFamily == 0x1A)
                {
                    if (cpuModel == 0x44 || cpuModel == 0x60)
                    {
                        coreMonitoring.monitoringArea = new AMD.Zen5();
                        l3Monitoring.monitoringArea = new AMD.Zen5L3Cache();
                        dfMonitoring.monitoringArea = new AMD.Zen5DataFabric(AMD.Zen5DataFabric.DfType.Client);
                        crazyThings = new AMD.Amd19hCpu();
                    }
                }
                else if (cpuFamily == 0x16)
                {
                    // Jaguar/Beema/Mullins/Puma
                    coreMonitoring.monitoringArea = new AMD.Jaguar();
                    l3Monitoring.monitoringArea = new AMD.JaguarL2();
                    dfMonitoring.monitoringArea = new AMD.JaguarNorthbridge();
                    l3LabelOverride = "L2 Interface PMC Configurations";
                    dfLabelOverride = "Northbridge PMC Configurations";
                }
                else if (cpuFamily == 0x15)
                {
                    if (cpuModel == 0x2 || cpuModel == 0x10)
                    {
                        coreMonitoring.monitoringArea = new AMD.Piledriver();
                        l3Monitoring.monitoringArea = new AMD.PiledriverNorthbridge();
                        dfLabelOverride = "Unused";
                        l3LabelOverride = "Northbridge PMC Configurations (pick one):";
                    }
                    else if (cpuModel == 0x1)
                    {
                        coreMonitoring.monitoringArea = new AMD.Bulldozer();
                        l3Monitoring.monitoringArea = new AMD.PiledriverNorthbridge();
                        dfLabelOverride = "Unused";
                        l3LabelOverride = "Northbridge PMC Configurations (pick one):";
                    }

                    crazyThings = new AMD.Amd15hCpu();
                }
                else if (cpuFamily == 0x10)
                {
                    coreMonitoring.monitoringArea = new AMD.K10();
                    dfLabelOverride = "Unused";
                    l3LabelOverride = "Unused";
                }
                else if (cpuFamily == 0x12)
                {
                    coreMonitoring.monitoringArea = new AMD.K10();
                }
            }

            InitializeComponent();
            coreMonitoring.targetListView = monitoringListView;
            l3Monitoring.targetListView = L3MonitoringListView;
            dfMonitoring.targetListView = dfMonitoringListView;
            monitoringListView.FullRowSelect = true;
            L3MonitoringListView.FullRowSelect = true;
            dfMonitoringListView.FullRowSelect = true;
            if (dfLabelOverride != null) DataFabricConfigLabel.Text = dfLabelOverride;
            if (l3LabelOverride != null) L3CacheConfigLabel.Text = l3LabelOverride;

            if (crazyThings != null)
            {
                crazyThingsLabel.Text = "Do not push these buttons:";
                crazyThings.InitializeCrazyControls(crazyThingsPanel, errorLabel);
            }

            cpuidLabel.Text = string.Format("CPU: {0} Family 0x{1:X}, Model 0x{2:X}, Stepping 0x{3:x} - {4}", 
                cpuManufacturer, 
                cpuFamily, 
                cpuModel, 
                cpuStepping, 
                coreMonitoring.monitoringArea == null ? "Not Supported" : coreMonitoring.monitoringArea.GetArchitectureName());

            if (coreMonitoring.monitoringArea != null)
            {
                fillConfigListView(coreMonitoring.monitoringArea.GetMonitoringConfigs(), configSelect);
            }

            if (l3Monitoring.monitoringArea != null)
            {
                fillConfigListView(l3Monitoring.monitoringArea.GetMonitoringConfigs(), L3ConfigSelect);
            }

            if (dfMonitoring.monitoringArea != null)
            {
                fillConfigListView(dfMonitoring.monitoringArea.GetMonitoringConfigs(), dfConfigSelect);
            }

            this.FormClosed += HaswellForm_FormClosed;
        }

        private void HaswellForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (coreMonitoring != null && coreMonitoring.monitoringThreadCancellation != null)
            {
                coreMonitoring.monitoringThreadCancellation.Cancel();
            }

            if (l3Monitoring != null && l3Monitoring.monitoringThreadCancellation != null)
            {
                l3Monitoring.monitoringThreadCancellation.Cancel();
            }

            if (dfMonitoring != null && dfMonitoring.monitoringThreadCancellation != null)
            {
                dfMonitoring.monitoringThreadCancellation.Cancel();
            }
        }

        private void applyDfConfigButton_Click(object sender, EventArgs e)
        {
            applyMonitoringConfig(dfMonitoring, dfConfigSelect);
        }

        private void applyL3ConfigButton_Click(object sender, EventArgs e)
        {
            applyMonitoringConfig(l3Monitoring, L3ConfigSelect);
        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void HaswellForm_Load(object sender, EventArgs e)
        {
            errorLabel.Text = "";
        }

        private void logButton_Click(object sender, EventArgs e)
        {
            int targetCoreIndex;
            string targetCore = RestrictCoreLoggingTextBox.Text;
            if (!int.TryParse(targetCore, out targetCoreIndex))
            {
                targetCoreIndex = -1;
            }

            // Only log core events for now
            if (coreMonitoring.monitoringArea != null)
            {
                coreMonitoring.monitoringArea.StopLoggingToFile();
                string error = coreMonitoring.monitoringArea.StartLogToFile(logFilePathTextBox.Text, targetCoreIndex);
                if (error != null) errorLabel.Text = error;
                else errorLabel.Text = "Logging started" + (targetCoreIndex >= 0 ? " for core " + targetCoreIndex : string.Empty);
            }
            else errorLabel.Text = "No core mon area selected";
        }

        private void stopLoggingButton_Click(object sender, EventArgs e)
        {
            coreMonitoring.monitoringArea.StopLoggingToFile();
            errorLabel.Text = "Logging stopped";
        }

        private void L3LogToFileButton_Click(object sender, EventArgs e)
        {
            if (l3Monitoring.monitoringArea != null)
            {
                l3Monitoring.monitoringArea.StopLoggingToFile();
                string error = l3Monitoring.monitoringArea.StartLogToFile(L3LogToFileTextBox.Text, -1);
                if (error != null) errorLabel.Text = error;
                else errorLabel.Text = "L3 Logging Started";
            }
            else errorLabel.Text = "No L3 mon area selected";
        }

        private void L3StopLoggingButton_Click(object sender, EventArgs e)
        {
            if (l3Monitoring.monitoringArea != null) l3Monitoring.monitoringArea.StopLoggingToFile();
            errorLabel.Text = "L3 Logging stopped";
        }

        private void DfLogToFileButton_Click(object sender, EventArgs e)
        {
            if (dfMonitoring.monitoringArea != null)
            {
                string error = dfMonitoring.monitoringArea.StartLogToFile(DfLogToFileTextBox.Text, -1);
                if (error != null) errorLabel.Text = error;
                else errorLabel.Text = "DF Logging Started";
            }
            else errorLabel.Text = "No DF mon area selected";
        }

        private void DfStopLoggingButton_Click(object sender, EventArgs e)
        {
            if (dfMonitoring.monitoringArea != null) dfMonitoring.monitoringArea.StopLoggingToFile();
            errorLabel.Text = "DF Logging stopped";
        }

        private void applyConfigButton_Click(object sender, EventArgs e)
        {
            applyMonitoringConfig(coreMonitoring, configSelect);
        }

        /// <summary>
        /// Populate list view with monitoring configurations
        /// </summary>
        /// <param name="configs">Array of monitoring configurations</param>
        /// <param name="configListView">Target list view</param>
        private void fillConfigListView(MonitoringConfig[] configs, ListView configListView)
        {
            configListView.Items.Clear();
            if (configs == null)
            {
                return;
            }

            for (int cfgIdx = 0; cfgIdx < configs.Length; cfgIdx++)
            {
                ListViewItem cfgItem = new ListViewItem(configs[cfgIdx].GetConfigName());
                cfgItem.Tag = cfgIdx;
                configListView.Items.Add(cfgItem);
            }
        }


        /// <summary>
        /// (re)starts the background monitoring thread for a monitoring area
        /// </summary>
        /// <param name="setup">Monitoring setup</param>
        /// <param name="configSelectListView">Target list view for monitoring thread to send output to</param>
        /// <param name="helpLabel">Label to put help text in</param>
        private void applyMonitoringConfig(MonitoringSetup setup, ListView configSelectListView)
        {
            lock (cpuManufacturer)
            {
                if (setup.monitoringThreadCancellation != null && setup.monitoringThreadCancellation.IsCancellationRequested) 
                    return;

                int cfgIdx;
                if (configSelectListView.SelectedItems.Count > 0)
                    cfgIdx = (int)configSelectListView.SelectedItems[0].Tag;
                else
                {
                    errorLabel.Text = "No config selected";
                    return;
                }

                Task.Run(() =>
                {
                    if (setup.monitoringThread != null && setup.monitoringThreadCancellation != null)
                    {
                        coreMonitoring.monitoringArea.StopLoggingToFile();
                        setup.monitoringThreadCancellation.Cancel();
                        setup.monitoringThread.Wait();
                    }

                    setup.monitoringThreadCancellation = new CancellationTokenSource();
                    setup.monitoringThread = Task.Run(() => setup.monitoringArea.MonitoringThread(cfgIdx, setup.targetListView, setup.monitoringThreadCancellation.Token));
                });

                errorLabel.Text = "";
            }
        }

        private class MonitoringSetup
        {
            public Task monitoringThread;
            public MonitoringArea monitoringArea;
            public CancellationTokenSource monitoringThreadCancellation;
            public ListView targetListView;
        }
    }
}
