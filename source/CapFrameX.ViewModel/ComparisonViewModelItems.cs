using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Localization;
using CapFrameX.Data;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Sensor.Reporting;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;
using DynamicData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CapFrameX.ViewModel
{
    public partial class ComparisonViewModel
    {
        private void AddComparisonItem(IFileRecordInfo recordInfo)
        {
            if (CheckListContains(recordInfo))
            {
                ShowRecordAlreadyInList();
                return;
            }

            var comparisonRecordInfo = GetComparisonRecordInfoFromFileRecordInfo(recordInfo);
            if (comparisonRecordInfo != null)
                AddComparisonRecords(new[] { comparisonRecordInfo });
        }

        /// <summary>
        /// Adds dropped records: they are loaded off the UI thread, then all of them are inserted
        /// and the charts are drawn once. Adding them one by one parsed each record on the UI
        /// thread and redrew every chart for every record, N(N+1)/2 series for N records.
        /// </summary>
        private async Task AddComparisonItemsAsync(IReadOnlyList<IFileRecordInfo> recordInfos)
        {
            var ids = new HashSet<string>();
            var newRecords = recordInfos
                .Where(recordInfo => !CheckListContains(recordInfo) && ids.Add(recordInfo.Id))
                .ToList();
            if (newRecords.Count < recordInfos.Count)
                ShowRecordAlreadyInList();

            var comparisonRecordInfos = await RecordBatchLoader.LoadAsync(newRecords,
                GetComparisonRecordInfoFromFileRecordInfo, _logger);
            AddComparisonRecords(comparisonRecordInfos);
        }

        private void AddComparisonRecords(IReadOnlyList<ComparisonRecordInfo> comparisonRecordInfos)
        {
            if (comparisonRecordInfos.Count == 0)
                return;

            foreach (var comparisonRecordInfo in comparisonRecordInfos)
            {
                var wrappedComparisonRecordInfo = GetWrappedRecordInfo(comparisonRecordInfo);

                // Insert into list (sorted)
                bool sourceChanged = UpdateComparisonMetricSource(wrappedComparisonRecordInfo);
                if (sourceChanged)
                {
                    foreach (ComparisonRecordInfoWrapper existingRecord in ComparisonRecords)
                        SetMetrics(existingRecord);
                }
                // Adding records resets the range to the whole longest record below, so a new
                // record's metrics cover all of it rather than the range from before.
                SetMetrics(wrappedComparisonRecordInfo, 0, 0);
                InsertComparisonRecordsSorted(wrappedComparisonRecordInfo);
            }

            HasComparisonItems = ComparisonRecords.Any();

            // Manage game name header
            HasUniqueGameNames = GetHasUniqueGameNames();
            CurrentGameName = comparisonRecordInfos[comparisonRecordInfos.Count - 1].Game;

            // Update height of bar chart control here
            UpdateBarChartHeight();
            UpdateVarianceChartHeight();
            UpdateRangeSliderParameter();

            //Draw charts and performance parameter
            UpdateCharts();
        }

        private void ShowRecordAlreadyInList()
        {
            MessageText = CxLang.T("ComparisonViewModel_RecordAlreadyInList");
            MessageDialogContentIsOpen = true;
        }

        private bool CheckListContains(IFileRecordInfo recordInfo)
        {
            var recordInfoWrapper = ComparisonRecords
                .FirstOrDefault(info => info.WrappedRecordInfo.FileRecordInfo.Id == recordInfo.Id);

            return recordInfoWrapper != null && ComparisonRecords.Any();
        }

        private void SetMetrics(ComparisonRecordInfoWrapper wrappedComparisonRecordInfo)
            => SetMetrics(wrappedComparisonRecordInfo, FirstSeconds, LastSeconds);

        // A lastSeconds of 0 stands for the record's last frame.
        private void SetMetrics(ComparisonRecordInfoWrapper wrappedComparisonRecordInfo,
            double startTime, double lastSeconds)
        {
            double lastFrameStart;
            if (!TryGetLastFrameStart(wrappedComparisonRecordInfo.WrappedRecordInfo.Session, out lastFrameStart))
            {
                wrappedComparisonRecordInfo.WrappedRecordInfo.FirstMetric = double.NaN;
                wrappedComparisonRecordInfo.WrappedRecordInfo.SecondMetric = double.NaN;
                wrappedComparisonRecordInfo.WrappedRecordInfo.ThirdMetric = double.NaN;
                wrappedComparisonRecordInfo.WrappedRecordInfo.SortingVariances = 0;
                return;
            }

            double endTime = lastSeconds <= 0 || lastSeconds > lastFrameStart
                ? lastFrameStart : lastSeconds;

            var frametimeTimeWindow = wrappedComparisonRecordInfo.WrappedRecordInfo.Session.GetFrametimeTimeWindow(startTime, endTime, _appConfiguration, ERemoveOutlierMethod.None);
            var displayChangeTimeWindow = _useDisplayChangeSamplesForComparison
                ? wrappedComparisonRecordInfo.WrappedRecordInfo.Session.GetDisplayChangeTimeWindow(
                    startTime, endTime, _appConfiguration, ERemoveOutlierMethod.None)
                : null;
            var gpuActiveTimeWindow = wrappedComparisonRecordInfo.WrappedRecordInfo.Session.GetGpuActiveTimeTimeWindow(startTime, endTime, _appConfiguration, ERemoveOutlierMethod.None);

            double GeMetricValue(IList<double> sequence, EMetric metric) =>
                    _frametimeStatisticProvider.GetFpsMetricValue(sequence, metric);          

            if (SelectedFirstMetric == EMetric.CpuFpsPerWatt)
            {
                wrappedComparisonRecordInfo.WrappedRecordInfo.FirstMetric =
                _frametimeStatisticProvider.GetPhysicalMetricValue(frametimeTimeWindow, EMetric.CpuFpsPerWatt,
                     SensorReport.GetAverageSensorValues(wrappedComparisonRecordInfo.WrappedRecordInfo.Session.Runs.Select(run => run.SensorData2), EReportSensorName.CpuPower,
                     startTime, endTime));
            }
            else if (SelectedFirstMetric == EMetric.GpuFpsPerWatt)
            {
                wrappedComparisonRecordInfo.WrappedRecordInfo.FirstMetric =
                    _frametimeStatisticProvider.GetPhysicalMetricValue(frametimeTimeWindow, EMetric.GpuFpsPerWatt,
                         SensorReport.GetAverageSensorValues(wrappedComparisonRecordInfo.WrappedRecordInfo.Session.Runs.Select(run => run.SensorData2), EReportSensorName.GpuPower,
                         startTime, endTime, _appConfiguration.UseTBPSim));
            }
            else if (SelectedFirstMetric == EMetric.GpuActiveAverage || SelectedFirstMetric == EMetric.GpuActiveP1 || SelectedFirstMetric == EMetric.GpuActiveOnePercentLowAverage)
            {
                wrappedComparisonRecordInfo.WrappedRecordInfo.FirstMetric =
                    GeMetricValue(gpuActiveTimeWindow, SelectedFirstMetric);
            }
            else
            {
                // Always use frame times when comes average fps
                if (SelectedFirstMetric == EMetric.Average)
                {
                    wrappedComparisonRecordInfo.WrappedRecordInfo.FirstMetric =
                        GeMetricValue(frametimeTimeWindow, SelectedFirstMetric);
                }
                else
                {
                    var samples = _useDisplayChangeSamplesForComparison
                        ? displayChangeTimeWindow : frametimeTimeWindow;

                    wrappedComparisonRecordInfo.WrappedRecordInfo.FirstMetric =
                        GeMetricValue(samples, SelectedFirstMetric);
                }
            }

            if (SelectedSecondMetric == EMetric.CpuFpsPerWatt)
            {
                wrappedComparisonRecordInfo.WrappedRecordInfo.SecondMetric =
                _frametimeStatisticProvider.GetPhysicalMetricValue(frametimeTimeWindow, EMetric.CpuFpsPerWatt,
                     SensorReport.GetAverageSensorValues(wrappedComparisonRecordInfo.WrappedRecordInfo.Session.Runs.Select(run => run.SensorData2), EReportSensorName.CpuPower,
                     startTime, endTime));
            }
            else if (SelectedSecondMetric == EMetric.GpuFpsPerWatt)
            {
                wrappedComparisonRecordInfo.WrappedRecordInfo.SecondMetric =
                    _frametimeStatisticProvider.GetPhysicalMetricValue(frametimeTimeWindow, EMetric.GpuFpsPerWatt,
                         SensorReport.GetAverageSensorValues(wrappedComparisonRecordInfo.WrappedRecordInfo.Session.Runs.Select(run => run.SensorData2), EReportSensorName.GpuPower,
                         startTime, endTime, _appConfiguration.UseTBPSim));
            }
            else if (SelectedSecondMetric == EMetric.GpuActiveAverage || SelectedSecondMetric == EMetric.GpuActiveP1 || SelectedSecondMetric == EMetric.GpuActiveOnePercentLowAverage)
            {
                wrappedComparisonRecordInfo.WrappedRecordInfo.SecondMetric
                = GeMetricValue(gpuActiveTimeWindow, SelectedSecondMetric);
            }
            else
            {
                // Always use frame times when comes average fps
                if (SelectedSecondMetric == EMetric.Average)
                {
                    wrappedComparisonRecordInfo.WrappedRecordInfo.SecondMetric =
                        GeMetricValue(frametimeTimeWindow, SelectedSecondMetric);
                }
                else
                {
                    var samples = _useDisplayChangeSamplesForComparison
                        ? displayChangeTimeWindow : frametimeTimeWindow;

                    wrappedComparisonRecordInfo.WrappedRecordInfo.SecondMetric =
                        GeMetricValue(samples, SelectedSecondMetric);
                }
            }

            if (SelectedThirdMetric == EMetric.CpuFpsPerWatt)
            {
                wrappedComparisonRecordInfo.WrappedRecordInfo.ThirdMetric =
               _frametimeStatisticProvider.GetPhysicalMetricValue(frametimeTimeWindow, EMetric.CpuFpsPerWatt,
                    SensorReport.GetAverageSensorValues(wrappedComparisonRecordInfo.WrappedRecordInfo.Session.Runs.Select(run => run.SensorData2), EReportSensorName.CpuPower,
                    startTime, endTime));
            }
            else if (SelectedThirdMetric == EMetric.GpuFpsPerWatt)
            {
                wrappedComparisonRecordInfo.WrappedRecordInfo.ThirdMetric =
                  _frametimeStatisticProvider.GetPhysicalMetricValue(frametimeTimeWindow, EMetric.GpuFpsPerWatt,
                       SensorReport.GetAverageSensorValues(wrappedComparisonRecordInfo.WrappedRecordInfo.Session.Runs.Select(run => run.SensorData2), EReportSensorName.GpuPower,
                       startTime, endTime, _appConfiguration.UseTBPSim));
            }
            else if (SelectedThirdMetric == EMetric.GpuActiveAverage || SelectedThirdMetric == EMetric.GpuActiveP1 || SelectedThirdMetric == EMetric.GpuActiveOnePercentLowAverage)
            {
                wrappedComparisonRecordInfo.WrappedRecordInfo.ThirdMetric =
                    GeMetricValue(gpuActiveTimeWindow, SelectedThirdMetric);
            }
            else
            {
                // Always use frame times when comes average fps
                if (SelectedThirdMetric == EMetric.Average)
                {
                    wrappedComparisonRecordInfo.WrappedRecordInfo.ThirdMetric =
                        GeMetricValue(frametimeTimeWindow, SelectedThirdMetric);
                }
                else
                {
                    var samples = _useDisplayChangeSamplesForComparison
                        ? displayChangeTimeWindow : frametimeTimeWindow;

                    wrappedComparisonRecordInfo.WrappedRecordInfo.ThirdMetric =
                        GeMetricValue(samples, SelectedThirdMetric);
                }
            }

            var varianceSequences = GetVarianceSequences(
                wrappedComparisonRecordInfo.WrappedRecordInfo.Session,
                _useDisplayChangeSamplesForComparison, startTime, endTime);
            var variances = _frametimeStatisticProvider.GetVariancePercentages(varianceSequences);

            wrappedComparisonRecordInfo.WrappedRecordInfo.SortingVariances
                = variances[0] + variances[1];
        }

        private static IEnumerable<IList<double>> GetVarianceSequences(ISession session,
            bool useDisplayChangeSamples, double startTime, double endTime)
        {
            foreach (ISessionRun run in session.Runs)
            {
                ISessionCaptureData captureData = run?.CaptureData;
                if (captureData == null)
                {
                    yield return new double[0];
                    continue;
                }

                IList<double> times = captureData.TimeInSeconds ?? new double[0];
                IList<double> values = useDisplayChangeSamples
                    ? captureData.MsBetweenDisplayChange : captureData.MsBetweenPresents;
                if (values == null)
                {
                    yield return new double[0];
                    continue;
                }

                int count = Math.Min(times.Count, values.Count);
                var sequence = new List<double>(count);
                for (int i = 0; i < count; i++)
                {
                    double time = times[i];
                    double value = values[i];
                    if (time >= startTime && time <= endTime && value > 0
                        && !double.IsNaN(value) && !double.IsInfinity(value))
                    {
                        sequence.Add(value);
                    }
                }

                yield return sequence;
            }
        }

        private void InsertComparisonRecordsSorted(ComparisonRecordInfoWrapper wrappedComparisonRecordInfo)
        {
            if (!ComparisonRecords.Any())
            {
                ComparisonRecords.Add(wrappedComparisonRecordInfo);
                return;
            }

            var list = new List<ComparisonRecordInfoWrapper>(ComparisonRecords)
            {
                wrappedComparisonRecordInfo
            };

            IEnumerable<ComparisonRecordInfoWrapper> orderedList = null;

            if (UseComparisonGrouping)
            {
                if (IsVarianceChartTabActive)
                {
                    orderedList = IsSortModeAscending ? list
                        .Select(info => info.Clone()).OrderBy(x => x.WrappedRecordInfo.Game)
                        .ThenBy(x => x.WrappedRecordInfo.SortingVariances)
                        : list.Select(info => info.Clone()).OrderBy(x => x.WrappedRecordInfo.Game)
                        .ThenByDescending(x => x.WrappedRecordInfo.SortingVariances);
                }
                else
                {
                    if (IsSortModeAscending)
                    {
                        if (SelectedSortMetric.Contains("Metric"))
                        {
                            // Case for numeric metrics
                            orderedList = list
                                .Select(info => info.Clone())
                                .OrderBy(x => x.WrappedRecordInfo.Game)
                                .ThenBy(x =>
                                    SelectedSortMetric == "First Metric" ? x.WrappedRecordInfo.FirstMetric :
                                    SelectedSortMetric == "Second Metric" ? x.WrappedRecordInfo.SecondMetric :
                                    x.WrappedRecordInfo.ThirdMetric); // Default for metric case
                        }
                        else if (SelectedSortMetric.Contains("Label"))
                        {
                            // Case for string labels with alphanumeric sorting
                            orderedList = list
                                .Select(info => info.Clone())
                                .OrderBy(x => x.WrappedRecordInfo.Game)
                                .ThenBy(x =>
                                    SelectedSortMetric == "Comment Label" ? x.WrappedRecordInfo.FileRecordInfo.Comment :
                                    SelectedSortMetric == "CPU Label" ? x.WrappedRecordInfo.FileRecordInfo.ProcessorName :
                                    x.WrappedRecordInfo.FileRecordInfo.GraphicCardName, AlphanumericComparer.Instance); // Use custom comparer
                        }
                    }
                    else
                    {
                        if (SelectedSortMetric.Contains("Metric"))
                        {
                            // Case for numeric metrics
                            orderedList = list
                                .Select(info => info.Clone())
                                .OrderBy(x => x.WrappedRecordInfo.Game)
                                .ThenByDescending(x =>
                                    SelectedSortMetric == "First Metric" ? x.WrappedRecordInfo.FirstMetric :
                                    SelectedSortMetric == "Second Metric" ? x.WrappedRecordInfo.SecondMetric :
                                    x.WrappedRecordInfo.ThirdMetric); // Default for metric case
                        }
                        else if (SelectedSortMetric.Contains("Label"))
                        {
                            // Case for string labels with alphanumeric sorting
                            orderedList = list
                                .Select(info => info.Clone())
                                .OrderBy(x => x.WrappedRecordInfo.Game)
                                .ThenByDescending(x =>
                                    SelectedSortMetric == "Comment Label" ? x.WrappedRecordInfo.FileRecordInfo.Comment :
                                    SelectedSortMetric == "CPU Label" ? x.WrappedRecordInfo.FileRecordInfo.ProcessorName :
                                    x.WrappedRecordInfo.FileRecordInfo.GraphicCardName, AlphanumericComparer.Instance); // Use custom comparer
                        }
                    }

                }
            }
            else
            {
                if (IsSortModeAscending)
                {
                    if (SelectedSortMetric.Contains("Metric"))
                    {
                        // Case for numeric metrics
                        orderedList = list
                            .Select(info => info.Clone())
                            .OrderBy(x =>
                                SelectedSortMetric == "First Metric" ? x.WrappedRecordInfo.FirstMetric :
                                SelectedSortMetric == "Second Metric" ? x.WrappedRecordInfo.SecondMetric :
                                x.WrappedRecordInfo.ThirdMetric); // Default for metric case
                    }
                    else if (SelectedSortMetric.Contains("Label"))
                    {
                        // Case for string labels with alphanumeric sorting
                        orderedList = list
                            .Select(info => info.Clone())
                            .OrderBy(x =>
                                SelectedSortMetric == "Comment Label" ? x.WrappedRecordInfo.FileRecordInfo.Comment :
                                SelectedSortMetric == "CPU Label" ? x.WrappedRecordInfo.FileRecordInfo.ProcessorName :
                                x.WrappedRecordInfo.FileRecordInfo.GraphicCardName, AlphanumericComparer.Instance); // Use custom comparer
                    }
                }
                else
                {
                    if (SelectedSortMetric.Contains("Metric"))
                    {
                        // Case for numeric metrics
                        orderedList = list
                            .Select(info => info.Clone())
                            .OrderByDescending(x =>
                                SelectedSortMetric == "First Metric" ? x.WrappedRecordInfo.FirstMetric :
                                SelectedSortMetric == "Second Metric" ? x.WrappedRecordInfo.SecondMetric :
                                x.WrappedRecordInfo.ThirdMetric); // Default for metric case
                    }
                    else if (SelectedSortMetric.Contains("Label"))
                    {
                        // Case for string labels with alphanumeric sorting
                        orderedList = list
                            .Select(info => info.Clone())
                            .OrderByDescending(x =>
                                SelectedSortMetric == "Comment Label" ? x.WrappedRecordInfo.FileRecordInfo.Comment :
                                SelectedSortMetric == "CPU Label" ? x.WrappedRecordInfo.FileRecordInfo.ProcessorName :
                                x.WrappedRecordInfo.FileRecordInfo.GraphicCardName, AlphanumericComparer.Instance); // Use custom comparer
                    }
                }
            }

            if (orderedList != null)
            {
                // get index of wrappedComparisonRecordInfo by comparing wrappedComparisonRecordInfo.WrappedRecordInfo
                var index = orderedList
                    .Select((item, idx) => new { item, idx })
                    .FirstOrDefault(x => x.item.WrappedRecordInfo.FileRecordInfo.Id == wrappedComparisonRecordInfo.WrappedRecordInfo.FileRecordInfo.Id)?.idx ?? -1;

                if (index >= 0)
                {
                    ComparisonRecords.Insert(index, wrappedComparisonRecordInfo);
                }
            }
        }

        public void RemoveComparisonItem(ComparisonRecordInfoWrapper wrappedComparisonRecordInfo)
        {
            _comparisonColorManager.FreeColor(wrappedComparisonRecordInfo.Color);
            ComparisonRecords.Remove(wrappedComparisonRecordInfo);

            HasComparisonItems = ComparisonRecords.Any();
            UpdateRangeSliderParameter();
            UpdateCharts();
            UpdateBarChartHeight();
            UpdateVarianceChartHeight();

            // Manage game name header		
            HasUniqueGameNames = GetHasUniqueGameNames();
            if (HasUniqueGameNames)
                CurrentGameName = ComparisonRecords.First().WrappedRecordInfo.Game;

            ComparisonFrametimesModel.InvalidatePlot(true);
        }

        private bool GetHasUniqueGameNames()
        {
            if (!ComparisonRecords.Any())
                return false;

            var firstName = ComparisonRecords.First().WrappedRecordInfo.Game;

            return !ComparisonRecords.Any(record => record.WrappedRecordInfo.Game != firstName);
        }

        public void RemoveAllComparisonItems(bool manageVisibility = true, bool resetSortMode = false)
        {
            if (resetSortMode)
            {
                _comparisonColorManager.FreeAllColors();
            }

            ComparisonRecords.Clear();
            UpdateCharts();

            if (resetSortMode)
                IsSortModeAscending = false;

            if (manageVisibility)
            {
                HasComparisonItems = false;
            }

            // Manage game name header
            HasUniqueGameNames = false;
            CurrentGameName = string.Empty;

            RemainingRecordingTime = "0.0 s";
            UpdateRangeSliderParameter();
            ComparisonFrametimesModel.InvalidatePlot(true);
        }

        public void SortComparisonItems()
        {
            if (!ComparisonRecords.Any())
                return;

            IEnumerable<ComparisonRecordInfoWrapper> comparisonRecordList = null;

            if (UseComparisonGrouping)
            {
                if (IsVarianceChartTabActive)
                {
                    comparisonRecordList = IsSortModeAscending ? ComparisonRecords.ToList()
                        .Select(info => info.Clone()).OrderBy(x => x.WrappedRecordInfo.Game)
                        .ThenBy(x => x.WrappedRecordInfo.SortingVariances)
                        :
                        ComparisonRecords.ToList()
                        .Select(info => info.Clone()).OrderBy(x => x.WrappedRecordInfo.Game)
                        .ThenByDescending(x => x.WrappedRecordInfo.SortingVariances);
                }
                else
                {
                    if (IsSortModeAscending)
                    {
                        if (SelectedSortMetric.Contains("Metric"))
                        {
                            // Case for numeric metrics
                            comparisonRecordList = ComparisonRecords.ToList()
                                .Select(info => info.Clone())
                                .OrderBy(x => x.WrappedRecordInfo.Game)
                                .ThenBy(x =>
                                    SelectedSortMetric == "First Metric" ? x.WrappedRecordInfo.FirstMetric :
                                    SelectedSortMetric == "Second Metric" ? x.WrappedRecordInfo.SecondMetric :
                                    x.WrappedRecordInfo.ThirdMetric); // Default for metric case
                        }
                        else if (SelectedSortMetric.Contains("Label"))
                        {
                            // Case for string labels with alphanumeric sorting
                            comparisonRecordList = ComparisonRecords.ToList()
                                .Select(info => info.Clone())
                                .OrderBy(x => x.WrappedRecordInfo.Game)
                                .ThenBy(x =>
                                    SelectedSortMetric == "Comment Label" ? x.WrappedRecordInfo.FileRecordInfo.Comment :
                                    SelectedSortMetric == "CPU Label" ? x.WrappedRecordInfo.FileRecordInfo.ProcessorName :
                                    x.WrappedRecordInfo.FileRecordInfo.GraphicCardName, AlphanumericComparer.Instance); // Use custom comparer
                        }
                    }
                    else
                    {
                        if (SelectedSortMetric.Contains("Metric"))
                        {
                            // Case for numeric metrics
                            comparisonRecordList = ComparisonRecords.ToList()
                                .Select(info => info.Clone())
                                .OrderBy(x => x.WrappedRecordInfo.Game)
                                .ThenByDescending(x =>
                                    SelectedSortMetric == "First Metric" ? x.WrappedRecordInfo.FirstMetric :
                                    SelectedSortMetric == "Second Metric" ? x.WrappedRecordInfo.SecondMetric :
                                    x.WrappedRecordInfo.ThirdMetric); // Default for metric case
                        }
                        else if (SelectedSortMetric.Contains("Label"))
                        {
                            // Case for string labels with alphanumeric sorting
                            comparisonRecordList = ComparisonRecords.ToList()
                                .Select(info => info.Clone())
                                .OrderBy(x => x.WrappedRecordInfo.Game)
                                .ThenByDescending(x =>
                                    SelectedSortMetric == "Comment Label" ? x.WrappedRecordInfo.FileRecordInfo.Comment :
                                    SelectedSortMetric == "CPU Label" ? x.WrappedRecordInfo.FileRecordInfo.ProcessorName :
                                    x.WrappedRecordInfo.FileRecordInfo.GraphicCardName, AlphanumericComparer.Instance); // Use custom comparer
                        }
                    }

                }
            }
            else
            {
                if (IsSortModeAscending)
                {
                    if (SelectedSortMetric.Contains("Metric"))
                    {
                        // Case for numeric metrics
                        comparisonRecordList = ComparisonRecords.ToList()
                            .Select(info => info.Clone())
                            .OrderBy(x =>
                                SelectedSortMetric == "First Metric" ? x.WrappedRecordInfo.FirstMetric :
                                SelectedSortMetric == "Second Metric" ? x.WrappedRecordInfo.SecondMetric :
                                x.WrappedRecordInfo.ThirdMetric); // Default for metric case
                    }
                    else if (SelectedSortMetric.Contains("Label"))
                    {
                        // Case for string labels with alphanumeric sorting
                        comparisonRecordList = ComparisonRecords.ToList()
                            .Select(info => info.Clone())
                            .OrderBy(x =>
                                SelectedSortMetric == "Comment Label" ? x.WrappedRecordInfo.FileRecordInfo.Comment :
                                SelectedSortMetric == "CPU Label" ? x.WrappedRecordInfo.FileRecordInfo.ProcessorName :
                                x.WrappedRecordInfo.FileRecordInfo.GraphicCardName, AlphanumericComparer.Instance); // Use custom comparer
                    }
                }
                else
                {
                    if (SelectedSortMetric.Contains("Metric"))
                    {
                        // Case for numeric metrics
                        comparisonRecordList = ComparisonRecords.ToList()
                            .Select(info => info.Clone())
                            .OrderByDescending(x =>
                                SelectedSortMetric == "First Metric" ? x.WrappedRecordInfo.FirstMetric :
                                SelectedSortMetric == "Second Metric" ? x.WrappedRecordInfo.SecondMetric :
                                x.WrappedRecordInfo.ThirdMetric); // Default for metric case
                    }
                    else if (SelectedSortMetric.Contains("Label"))
                    {
                        // Case for string labels with alphanumeric sorting
                        comparisonRecordList = ComparisonRecords.ToList()
                            .Select(info => info.Clone())
                            .OrderByDescending(x =>
                                SelectedSortMetric == "Comment Label" ? x.WrappedRecordInfo.FileRecordInfo.Comment :
                                SelectedSortMetric == "CPU Label" ? x.WrappedRecordInfo.FileRecordInfo.ProcessorName :
                                x.WrappedRecordInfo.FileRecordInfo.GraphicCardName, AlphanumericComparer.Instance); // Use custom comparer
                    }
                }
            }

            if (comparisonRecordList != null)
            {
                ComparisonRecords.Clear();

                foreach (var item in comparisonRecordList)
                {
                    ComparisonRecords.Add(item);
                }

                // The records are replaced by clones, so every series still holds a wrapper that
                // is no longer in the list. Without this the line charts keep the series built for
                // the previous order until some unrelated change happens to set the flags.
                SetChartUpdateFlags();

                //Draw charts and performance parameter
                UpdateCharts();
            }
        }
    }
}
