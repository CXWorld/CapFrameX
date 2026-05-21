using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using PmcReader.Interop;

namespace PmcReader
{
    public class GenericMonitoringArea : MonitoringArea
    {
        private delegate void SafeSetMonitoringListViewItems(MonitoringUpdateResults results, ListView monitoringListView);
        private delegate void SafeSetMonitoringListViewColumns(string[] columns, ListView monitoringListView);

        public MonitoringConfig[] monitoringConfigs;
        protected int threadCount = 0, coreCount = 0, targetLogCoreIndex = -1;
        protected string architectureName = "Generic";
        private Dictionary<int, Stopwatch> lastUpdateTimers;
        private string logFilePath = null;
        private Object logFileLock = new object();
        private bool logFileHeadersWritten = false;

        public GenericMonitoringArea()
        {
            threadCount = Environment.ProcessorCount;
            foreach (var item in new System.Management.ManagementObjectSearcher("Select * from Win32_Processor").Get())
            {
                coreCount += int.Parse(item["NumberOfCores"].ToString());
            }
        }

        public int GetThreadCount()
        {
            return threadCount;
        }

        public MonitoringConfig[] GetMonitoringConfigs()
        {
            return monitoringConfigs;
        }

        public string GetArchitectureName()
        {
            return architectureName;
        }

        /// <summary>
        /// Start logging to file
        /// </summary>
        /// <param name="filePath">File to log to</param>
        /// <returns>null if successful, error string if something went wrong</returns>
        public string StartLogToFile(string filePath, int targetCoreIndex)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return "No file path to log to";
            }

            if (!File.Exists(filePath))
            {
                try
                {
                    File.WriteAllText(filePath, "");
                }
                catch (Exception e)
                {
                    return e.Message;
                }
            }
            else
            {
                try
                {
                    // just do this to check permissions
                    File.WriteAllText(filePath, "");
                }
                catch (Exception e)
                {
                    return e.Message;
                }
            }

            if (targetCoreIndex >= coreCount)
            {
                // Ignore parameter if a nonexistent core index is specified
                targetCoreIndex = -1;
            }
            else
            {
                this.targetLogCoreIndex = targetCoreIndex;
            }

            logFileHeadersWritten = false;
            lock (logFileLock)
            {
                logFilePath = filePath;
            }

            return null;
        }

        public void StopLoggingToFile()
        {
            lock (logFileLock)
            {
                logFilePath = null;
            }
        }

        /// <summary>
        /// Starts background monitoring thread that periodically updates monitoring list view 
        /// with new results
        /// </summary>
        /// <param name="configId">Monitoring config to use</param>
        /// <param name="listView">List view to update</param>
        /// <param name="cancelToken">Cancellation token - since perf counters are limited,
        /// this thread has to be cancelled before one for a new config is started</param>
        public void MonitoringThread(int configId, ListView listView, CancellationToken cancelToken)
        {
            CultureInfo ci = new CultureInfo("en-US");
            MonitoringConfig selectedConfig = monitoringConfigs[configId];
            lastUpdateTimers = null;

            if (cancelToken.IsCancellationRequested) return;

            selectedConfig.Initialize();
            SafeSetMonitoringListViewColumns cd = new SafeSetMonitoringListViewColumns(SetMonitoringListViewColumns);
            listView.Invoke(cd, selectedConfig.GetColumns(), listView);
            while (!cancelToken.IsCancellationRequested)
            {
                MonitoringUpdateResults updateResults = selectedConfig.Update();
                // update list box with results (and we're always on a different thread)
                SafeSetMonitoringListViewItems d = new SafeSetMonitoringListViewItems(SetMonitoringListView);
                listView.Invoke(d, updateResults, listView);

                // log to file, if we're doing that
                lock (logFileLock)
                {
                    if (!string.IsNullOrEmpty(logFilePath))
                    {
                        // log raw counter values if available. otherwise log metrics
                        // eventually I want to move everything over to log raw counter values
                        bool first;
                        if (updateResults.overallCounterValues != null)
                        {
                            if (!logFileHeadersWritten)
                            {
                                string csvHeader = "";
                                first = true;
                                foreach(Tuple<string, float> counterValue in updateResults.overallCounterValues)
                                {
                                    if (first) csvHeader += counterValue.Item1;
                                    else csvHeader += "," + counterValue.Item1;
                                    first = false;
                                }

                                csvHeader += "\n";
                                File.AppendAllText(logFilePath, csvHeader);
                                logFileHeadersWritten = true;
                            }

                            string csvLine = "";
                            first = true;
                            foreach(Tuple<string, float> counterValue in updateResults.overallCounterValues)
                            {
                                if (first) csvLine += counterValue.Item2.ToString("G", ci);
                                else csvLine += "," + counterValue.Item2.ToString("G", ci);
                                first = false;
                            }

                            csvLine += "\n";
                            File.AppendAllText(logFilePath, csvLine);
                        }
                        else if (updateResults.overallMetrics != null)
                        {
                            if (!logFileHeadersWritten)
                            {
                                string csvHeader = "";
                                first = true;
                                foreach(string columnHeader in selectedConfig.GetColumns())
                                {
                                    if (first) csvHeader += columnHeader;
                                    else csvHeader += "," + columnHeader;
                                    first = false;
                                }

                                csvHeader += "\n";
                                File.AppendAllText(logFilePath, csvHeader);
                                logFileHeadersWritten = true;
                            }

                            string csvLine = "";
                            first = true;
                            foreach(string value in updateResults.overallMetrics)
                            {
                                if (first) csvLine += value;
                                else csvLine += "," + value;
                                first = false;
                            }

                            csvLine += "\n";
                            File.AppendAllText(logFilePath, csvLine);
                        }
                    }
                }

                Thread.Sleep(1000);
            }
        }

        /// <summary>
        /// Init monitoring list view with new columns
        /// </summary>
        /// <param name="columns">New cols</param>
        /// <param name="monitoringListView">List view to update</param>
        public void SetMonitoringListViewColumns(string[] columns, ListView monitoringListView)
        {
            monitoringListView.Columns.Clear();
            monitoringListView.Items.Clear();
            foreach (string column in columns)
            {
                monitoringListView.Columns.Add(column);
            }

            foreach (ColumnHeader column in monitoringListView.Columns)
            {
                // nasty heuristic
                if (column.Text.Length < 10) column.Width = 65;
                else column.Width = -2;
            }
        }

        /// <summary>
        /// Apply updated results to monitoring list view
        /// </summary>
        /// <param name="updateResults">New perf counter metrics</param>
        /// <param name="monitoringListView">List view to update</param>
        public void SetMonitoringListView(MonitoringUpdateResults updateResults, ListView monitoringListView)
        {
            if (updateResults.unitMetrics != null && monitoringListView.Items.Count == updateResults.unitMetrics.Length + 1)
            {
                UpdateListViewItem(updateResults.overallMetrics, monitoringListView.Items[0]);
                if (updateResults.unitMetrics != null)
                {
                    for (int unitIdx = 0; unitIdx < updateResults.unitMetrics.Length; unitIdx++)
                    {
                        UpdateListViewItem(updateResults.unitMetrics[unitIdx], monitoringListView.Items[unitIdx + 1]);
                    }
                }
            }
            else
            {
                monitoringListView.Items.Clear();
                monitoringListView.Items.Add(new ListViewItem(updateResults.overallMetrics));
                if (updateResults.unitMetrics != null)
                {
                    for (int unitIdx = 0; unitIdx < updateResults.unitMetrics.Length; unitIdx++)
                    {
                        monitoringListView.Items.Add(new ListViewItem(updateResults.unitMetrics[unitIdx]));
                    }
                }
            }
        }

        /// <summary>
        /// Update text in existing ListViewItem
        /// darn it, it still flashes
        /// </summary>
        /// <param name="newFields">updated values</param>
        /// <param name="listViewItem">list view item to update</param>
        public static void UpdateListViewItem(string[] newFields, ListViewItem listViewItem)
        {
            for (int subItemIdx = 0; subItemIdx < listViewItem.SubItems.Count && subItemIdx < newFields.Length; subItemIdx++)
            {
                listViewItem.SubItems[subItemIdx].Text = newFields[subItemIdx];
            }
        }

        /// <summary>
        /// Make big number readable
        /// </summary>
        /// <param name="n">stupidly big number</param>
        /// <returns>Formatted string, with G/M/K suffix if big</returns>
        public static string FormatLargeNumber(ulong n)
        {
            if (n > 2000000000000UL)
            {
                return string.Format("{0:F2} T", (float)n / 1000000000000);
            }
            else if (n > 1000000000)
            {
                return string.Format("{0:F2} G", (float)n / 1000000000);
            }
            else if (n > 1000000)
            {
                return string.Format("{0:F2} M", (float)n / 1000000);
            }
            else if (n > 500)
            {
                return string.Format("{0:F2} K", (float)n / 1000);
            }

            return string.Format("{0} ", n);
        }

        public static string FormatLargeNumber(float n)
        {
            if (n > 2000000000000)
            {
                return string.Format("{0:F2} T", (float)n / 1000000000000);
            }
            else if (n > 1000000000)
            {
                return string.Format("{0:F2} G", n / 1000000000);
            }
            else if (n > 1000000)
            {
                return string.Format("{0:F2} M", n / 1000000);
            }
            else if (n > 500)
            {
                return string.Format("{0:F2} K", n / 1000);
            }

            return string.Format("{0:F2} ", n);
        }

        public static string FormatPercentage(float n, float total)
        {
            return string.Format("{0:F2}%", 100 * n / total);
        }

        /// <summary>
        /// Read and zero a MSR
        /// Useful for reading PMCs over a set interval
        /// Terrifyingly dangerous everywhere else
        /// </summary>
        /// <param name="msrIndex">MSR index</param>
        /// <returns>value read from MSR</returns>
        public static ulong ReadAndClearMsr(uint msrIndex)
        {
            ulong retval;
            Ring0.ReadMsr(msrIndex, out retval);
            Ring0.WriteMsr(msrIndex, 0);
            return retval;
        }

        /// <summary>
        /// Get normalization factor assuming 1000 ms interval
        /// </summary>
        /// <param name="lastUpdateTime">last updated time in unix ms, will be updated</param>
        /// <returns>normalization factor</returns>
        public float GetNormalizationFactor(ref long lastUpdateTime)
        {
            long currentTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            float timeNormalization = (float)1000 / (currentTime - lastUpdateTime);
            lastUpdateTime = currentTime;
            return timeNormalization;
        }

        /// <summary>
        /// Get normalization factor (for 1000 ms interval) given stopwatch index
        /// also resets stopwatch
        /// </summary>
        /// <param name="index">Item index</param>
        /// <returns>normalization factor</returns>
        public float GetNormalizationFactor(int index)
        {
            if (lastUpdateTimers == null) lastUpdateTimers = new Dictionary<int, Stopwatch>();

            Stopwatch sw;
            if (! lastUpdateTimers.TryGetValue(index, out sw))
            {
                sw = new Stopwatch();
                sw.Start();
                lastUpdateTimers.Add(index, sw);
                return 1;
            }

            sw.Stop();
            float retval = 1000 / (float)sw.ElapsedMilliseconds;
            sw.Restart();
            return retval;
        }

        public virtual void InitializeCrazyControls(FlowLayoutPanel flowLayoutPanel, Label errorLabel) {}

        protected Button CreateButton(string buttonText, EventHandler handler)
        {
            Button button = new Button();
            button.Text = buttonText;
            button.AutoSize = true;
            button.Click += handler;
            return button;
        }
    }
}
