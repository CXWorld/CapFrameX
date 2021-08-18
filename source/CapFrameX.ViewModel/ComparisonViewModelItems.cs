using CapFrameX.Contracts.Data;
using CapFrameX.Data;
using CapFrameX.Sensor.Reporting;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.ViewModel
{
    public partial class ComparisonViewModel
    {
        private void AddComparisonItem(IFileRecordInfo recordInfo)
        {
            if (CheckListContains(recordInfo))
            {
                MessageText = $"The list already contains this record and therefore cannot be inserted. " +
                    $"Select a different record for the comparison.";
                MessageDialogContentIsOpen = true;
                return;
            }

            var comparisonRecordInfo = GetComparisonRecordInfoFromFileRecordInfo(recordInfo);
            var wrappedComparisonRecordInfo = GetWrappedRecordInfo(comparisonRecordInfo);

            // Insert into list (sorted)
            SetMetrics(wrappedComparisonRecordInfo);
            InsertComparisonRecordsSorted(wrappedComparisonRecordInfo);

            HasComparisonItems = ComparisonRecords.Any();

            // Manage game name header
            HasUniqueGameNames = GetHasUniqueGameNames();
            CurrentGameName = comparisonRecordInfo.Game;

            // Update height of bar chart control here
            UpdateBarChartHeight();
            UpdateVarianceChartHeight();
            UpdateRangeSliderParameter();

            //Draw charts and performance parameter
            UpdateCharts();
        }

        private bool CheckListContains(IFileRecordInfo recordInfo)
        {
            var recordInfoWrapper = ComparisonRecords
                .FirstOrDefault(info => info.WrappedRecordInfo.FileRecordInfo.Id == recordInfo.Id);

            return recordInfoWrapper != null && ComparisonRecords.Any();
        }

        private void SetMetrics(ComparisonRecordInfoWrapper wrappedComparisonRecordInfo)
        {
            double startTime = FirstSeconds;
            double lastFrameStart = wrappedComparisonRecordInfo.WrappedRecordInfo.Session.Runs.SelectMany(r => r.CaptureData.TimeInSeconds).Last();
            double endTime = LastSeconds > lastFrameStart ? lastFrameStart : lastFrameStart + LastSeconds;
            var frametimeTimeWindow = wrappedComparisonRecordInfo.WrappedRecordInfo.Session.GetFrametimeTimeWindow(startTime, endTime, _appConfiguration, ERemoveOutlierMethod.None);
            double GeMetricValue(IList<double> sequence, EMetric metric) =>
                    _frametimeStatisticProvider.GetFpsMetricValue(sequence, metric);
            var variances = _frametimeStatisticProvider.GetFrametimeVariancePercentages(frametimeTimeWindow);

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
                         startTime, endTime));
            }
            else
            {
                wrappedComparisonRecordInfo.WrappedRecordInfo.FirstMetric
                = GeMetricValue(frametimeTimeWindow, SelectedFirstMetric);
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
                         startTime, endTime));
            }
            else
            {
                wrappedComparisonRecordInfo.WrappedRecordInfo.SecondMetric
                    = GeMetricValue(frametimeTimeWindow, SelectedSecondMetric);
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
                       startTime, endTime));
            }
            else
            {
                wrappedComparisonRecordInfo.WrappedRecordInfo.ThirdMetric
                    = GeMetricValue(frametimeTimeWindow, SelectedThirdMetric);
            }

            wrappedComparisonRecordInfo.WrappedRecordInfo.SortingVariances
                = variances[0] + variances[1];
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

            List<ComparisonRecordInfoWrapper> orderedList = null;

            if (UseComparisonGrouping)
            {
                if (IsVarianceChartTabActive)
                {
                    orderedList = IsSortModeAscending ? list.OrderBy(x =>
                         x.WrappedRecordInfo.Game).ThenBy(x => x.WrappedRecordInfo.SortingVariances).ToList()
                         :
                         list.OrderBy(x => x.WrappedRecordInfo.Game)
                         .ThenByDescending(x => x.WrappedRecordInfo.SortingVariances).ToList();
                }
                else
                {
                    orderedList = IsSortModeAscending ? list.OrderBy(x => x.WrappedRecordInfo.Game).ThenBy(x =>
                            SelectedSortMetric == "First" ? x.WrappedRecordInfo.FirstMetric :
                            SelectedSortMetric == "Second" ? x.WrappedRecordInfo.SecondMetric :
                            x.WrappedRecordInfo.ThirdMetric).ToList()
                            :
                            list.OrderBy(x => x.WrappedRecordInfo.Game).ThenByDescending(x =>
                            SelectedSortMetric == "First" ? x.WrappedRecordInfo.FirstMetric :
                            SelectedSortMetric == "Second" ? x.WrappedRecordInfo.SecondMetric :
                            x.WrappedRecordInfo.ThirdMetric).ToList();
                }
            }
            else
            {
                if (IsVarianceChartTabActive)
                {
                    orderedList = IsSortModeAscending ? list.OrderBy(x =>
                        x.WrappedRecordInfo.SortingVariances).ToList()
                        :
                        list.OrderByDescending(x => x.WrappedRecordInfo.SortingVariances).ToList();
                }
                else
                {
                    orderedList = IsSortModeAscending ? list.OrderBy(x =>
                        SelectedSortMetric == "First" ? x.WrappedRecordInfo.FirstMetric :
                        SelectedSortMetric == "Second" ? x.WrappedRecordInfo.SecondMetric :
                        x.WrappedRecordInfo.ThirdMetric).ToList()
                        :
                        list.OrderByDescending(x =>
                        SelectedSortMetric == "First" ? x.WrappedRecordInfo.FirstMetric :
                        SelectedSortMetric == "Second" ? x.WrappedRecordInfo.SecondMetric :
                        x.WrappedRecordInfo.ThirdMetric).ToList();
                }
            }


            if (orderedList != null)
            {
                var index = orderedList.IndexOf(wrappedComparisonRecordInfo);
                ComparisonRecords.Insert(index, wrappedComparisonRecordInfo);
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
                    comparisonRecordList = IsSortModeAscending ? ComparisonRecords.ToList()
                        .Select(info => info.Clone()).OrderBy(x => x.WrappedRecordInfo.Game).ThenBy(x =>
                        SelectedSortMetric == "First" ? x.WrappedRecordInfo.FirstMetric :
                        SelectedSortMetric == "Second" ? x.WrappedRecordInfo.SecondMetric :
                        x.WrappedRecordInfo.ThirdMetric)
                        :
                        ComparisonRecords.ToList().Select(info => info.Clone()).OrderBy(x => x.WrappedRecordInfo.Game).ThenByDescending(x =>
                        SelectedSortMetric == "First" ? x.WrappedRecordInfo.FirstMetric :
                        SelectedSortMetric == "Second" ? x.WrappedRecordInfo.SecondMetric :
                        x.WrappedRecordInfo.ThirdMetric);
                }
            }
            else
            {
                if (IsVarianceChartTabActive)
                {
                    comparisonRecordList = IsSortModeAscending ? ComparisonRecords.ToList()
                            .Select(info => info.Clone()).OrderBy(x => x.WrappedRecordInfo.SortingVariances)
                            :
                            ComparisonRecords.ToList()
                            .Select(info => info.Clone()).OrderByDescending(x => x.WrappedRecordInfo.SortingVariances);
                }
                else
                {
                    comparisonRecordList = IsSortModeAscending ? ComparisonRecords.ToList()
                        .Select(info => info.Clone()).OrderBy(x =>
                        SelectedSortMetric == "First" ? x.WrappedRecordInfo.FirstMetric :
                        SelectedSortMetric == "Second" ? x.WrappedRecordInfo.SecondMetric :
                        x.WrappedRecordInfo.ThirdMetric)
                        :
                        ComparisonRecords.ToList().Select(info => info.Clone()).OrderByDescending(x =>
                        SelectedSortMetric == "First" ? x.WrappedRecordInfo.FirstMetric :
                        SelectedSortMetric == "Second" ? x.WrappedRecordInfo.SecondMetric :
                        x.WrappedRecordInfo.ThirdMetric);
                }
            }

            if (comparisonRecordList != null)
            {
                ComparisonRecords.Clear();

                foreach (var item in comparisonRecordList)
                {
                    ComparisonRecords.Add(item);
                }

                //Draw charts and performance parameter
                UpdateCharts();
            }
        }
    }
}
