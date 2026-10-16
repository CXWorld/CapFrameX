using CapFrameX.Contracts.Aggregation;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Localization;
using CapFrameX.Data;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Extensions;
using CapFrameX.Extensions.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;
using GongSolutions.Wpf.DragDrop;
using Microsoft.Extensions.Logging;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace CapFrameX.ViewModel
{
    public class AggregationViewModel : BindableBase, INavigationAware, IDropTarget
    {
        private readonly IStatisticProvider _statisticProvider;
        private readonly IEventAggregator _eventAggregator;
        private readonly IAppConfiguration _appConfiguration;
        private readonly IRecordManager _recordManager;
        private readonly ILogger<AggregationViewModel> _logger;
        private bool _useUpdateSession;
        private int _selectedAggregationEntryIndex = -1;
        private bool _showHelpText = true;
        private bool _enableClearButton = false;
        private bool _showIncludeExcludeButtons = false;
        private bool _enableAggregationButton = false;
        // The records in table order, with the samples their metrics are computed from, so a
        // metric change or the aggregate does not parse every file again.
        private readonly List<AggregatedRecord> _records = new List<AggregatedRecord>();
        private bool _showResultString;
        private string _aggregationResultString = string.Empty;
        private bool _supressCollectionChanged;

        public int SelectedAggregationEntryIndex
        {
            get
            { return _selectedAggregationEntryIndex; }
            set
            {
                _selectedAggregationEntryIndex = value;
                RaisePropertyChanged();
            }
        }

        public EMetric SelectedSecondMetric
        {
            get
            {
                return _appConfiguration
                  .SecondMetricAggregation
                  .ConvertToEnum<EMetric>();
            }
            set
            {
                _appConfiguration.SecondMetricAggregation =
                    value.ConvertToString();
                UpdateAggregationEntries();
                RaisePropertyChanged();
            }
        }

        public EMetric SelectedThirdMetric
        {
            get
            {
                return _appConfiguration
                  .ThirdMetricAggregation
                  .ConvertToEnum<EMetric>();
            }
            set
            {
                _appConfiguration.ThirdMetricAggregation =
                    value.ConvertToString();
                UpdateAggregationEntries();
                RaisePropertyChanged();
            }
        }

        public string SelectedRelatedMetric
        {
            get
            { return _appConfiguration.RelatedMetricAggregation; }
            set
            {
                _appConfiguration.RelatedMetricAggregation = value;
                // The entries keep their metrics; only the outlier analysis depends on it.
                OnAggregationEntriesChanged();
                RaisePropertyChanged();
            }
        }

        public int SelectedOutlierPercentage
        {
            get
            { return _appConfiguration.OutlierPercentageAggregation; }
            set
            {
                _appConfiguration.OutlierPercentageAggregation = value;
                OnAggregationEntriesChanged();
                RaisePropertyChanged();
            }
        }

        public bool ShowHelpText
        {
            get
            { return _showHelpText; }
            set
            {
                _showHelpText = value;
                RaisePropertyChanged();
            }
        }

        public string AggregationResultString
        {
            get
            { return _aggregationResultString; }
            set
            {
                _aggregationResultString = value;
                RaisePropertyChanged();
            }
        }

        public bool ShowResultString
        {
            get
            { return _showResultString; }
            set
            {
                _showResultString = value;
                RaisePropertyChanged();
            }
        }

        public bool EnableClearButton
        {
            get
            { return _enableClearButton; }
            set
            {
                _enableClearButton = value;
                RaisePropertyChanged();
            }
        }
        public bool ShowIncludeExcludeButtons
        {
            get
            { return _showIncludeExcludeButtons; }
            set
            {
                _showIncludeExcludeButtons = value;
                RaisePropertyChanged();
            }
        }
        public bool EnableAggregationButton
        {
            get
            { return _enableAggregationButton; }
            set
            {
                _enableAggregationButton = value;
                RaisePropertyChanged();
            }
        }

        public Array RelatedMetricItemsSource => new[] { "Average", "Second", "Third" };

        public Array OutlierPercentageItemsSource => Enumerable.Range(1, 9).ToArray();

        public Array SecondMetricItems => Enum.GetValues(typeof(EMetric))
            .Cast<EMetric>()
            .Where(metric => metric != EMetric.Average)
            .ToArray();

        public Array ThirdMetricItems => Enum.GetValues(typeof(EMetric))
            .Cast<EMetric>()
            .Where(metric => metric != EMetric.Average)
            .ToArray();

        public ISubject<bool[]> OutlierFlagStream = new Subject<bool[]>();

        public ICommand ClearTableCommand { get; }
        public ICommand AggregateIncludeCommand { get; }
        public ICommand AggregateExcludeCommand { get; }

        public ObservableCollection<IAggregationEntry> AggregationEntries { get; private set; }
            = new ObservableCollection<IAggregationEntry>();

        // The last drop; tests wait for it.
        internal Task PendingDrop { get; private set; } = Task.CompletedTask;

        public AggregationViewModel(IStatisticProvider statisticProvider,
            IEventAggregator eventAggregator, IAppConfiguration appConfiguration, IRecordManager recordManager,
            ILogger<AggregationViewModel> logger)
        {
            _statisticProvider = statisticProvider;
            _eventAggregator = eventAggregator;
            _appConfiguration = appConfiguration;
            _recordManager = recordManager;
            _logger = logger;
            ClearTableCommand = new DelegateCommand(OnClearTable);
            AggregateIncludeCommand = new DelegateCommand(OnAggregateInclude);
            AggregateExcludeCommand = new DelegateCommand(OnAggregateExclude);

            SubscribeToUpdateSession();

            AggregationEntries.CollectionChanged += new NotifyCollectionChangedEventHandler
                ((sender, eventArg) => OnAggregationEntriesChanged());
        }

        public void RemoveAggregationEntry(IAggregationEntry entry)
        {
            int index = _records.FindIndex(record => record.RecordInfo == entry.FileRecordInfo);
            if (index >= 0)
                _records.RemoveAt(index);
            AggregationEntries.Remove(entry);
        }

        private void OnAggregationEntriesChanged()
        {
            ShowHelpText = !AggregationEntries.Any();
            EnableClearButton = AggregationEntries.Any();


            // Outlier analysis
            if (AggregationEntries.Count >= 2 && !_supressCollectionChanged)
            {
                var outlierFlags = _statisticProvider
                    .GetOutlierAnalysis(AggregationEntries.Select(analysis => analysis.MetricAnalysis).ToList(),
                    _appConfiguration.RelatedMetricAggregation, _appConfiguration.OutlierPercentageAggregation);

                OutlierFlagStream.OnNext(outlierFlags);

                bool includesOutliers = outlierFlags.Any(x => x);
                bool includesOnlyOutliers = outlierFlags.All(x => x);

                ShowIncludeExcludeButtons = includesOutliers && !includesOnlyOutliers;

                EnableAggregationButton = !ShowIncludeExcludeButtons;

            }
            else
            {
                EnableAggregationButton = false;
                ShowIncludeExcludeButtons = false;
            }


        }

        private void SubscribeToUpdateSession()
        {
            _eventAggregator.GetEvent<PubSubEvent<ViewMessages.SelectSession>>()
                .Subscribe(msg =>
                {
                    if (_useUpdateSession)
                    {
                        AddAggregationEntry(msg.RecordInfo, msg.CurrentSession);
                    }
                });
        }

        private void AddAggregationEntry(IFileRecordInfo recordInfo, ISession session)
        {
            if (recordInfo == null) return;

            session = session ?? _recordManager.LoadData(recordInfo.FullPath);
            AddAggregationRecord(AggregatedRecord.From(recordInfo, session));
        }

        /// <summary>
        /// Adds dropped records: they are parsed off the UI thread, then added in drop order with
        /// one outlier analysis.
        /// </summary>
        private async Task AddAggregationEntriesAsync(IReadOnlyList<IFileRecordInfo> recordInfos)
        {
            // Records of another process would be rejected after parsing them.
            string processName = _records.FirstOrDefault()?.RecordInfo.ProcessName;
            if (processName != null)
                recordInfos = recordInfos.Where(recordInfo => recordInfo.ProcessName == processName).ToList();

            var records = await RecordBatchLoader.LoadAsync(recordInfos,
                recordInfo => AggregatedRecord.From(recordInfo, _recordManager.LoadData(recordInfo.FullPath)), _logger);

            _supressCollectionChanged = true;
            try
            {
                foreach (var record in records)
                    AddAggregationRecord(record);
            }
            finally
            {
                _supressCollectionChanged = false;
            }
            OnAggregationEntriesChanged();
        }

        // A record without frames is left out: every record in the list needs its entry, since
        // outlier flags and the written aggregate map entries to records by position.
        private void AddAggregationRecord(AggregatedRecord record)
        {
            if (record == null)
                return;

            if (_records.Any(existing => existing.RecordInfo.ProcessName != record.RecordInfo.ProcessName))
                return;

            _records.Add(record);
            AggregationEntries.Add(CreateEntry(record));
        }

        private void UpdateAggregationEntries()
        {
            if (!_records.Any())
                return;

            AggregationEntries.Clear();

            _supressCollectionChanged = true;
            foreach (var record in _records)
                AggregationEntries.Add(CreateEntry(record));
            _supressCollectionChanged = false;
            OnAggregationEntriesChanged();
        }

        private AggregationEntry CreateEntry(AggregatedRecord record)
        {
            var metricAnalysis = _statisticProvider
                .GetMetricAnalysis(record.Frametimes, record.Displaytimes, _appConfiguration.UseDisplayChangeMetrics,
                    SelectedSecondMetric.ConvertToString(), SelectedThirdMetric.ConvertToString());

            return new AggregationEntry()
            {
                GameName = record.RecordInfo.GameName,
                CreationDate = record.RecordInfo.CreationDate,
                CreationTime = record.RecordInfo.CreationTime,
                AverageValue = metricAnalysis.Average,
                SecondMetricValue = metricAnalysis.Second,
                ThirdMetricValue = metricAnalysis.Third,
                MetricAnalysis = metricAnalysis,
                FileRecordInfo = record.RecordInfo
            };
        }

        private void OnClearTable()
        {
            AggregationEntries.Clear();
            _records.Clear();
            AggregationResultString = string.Empty;
            ShowResultString = false;
        }

        private void OnAggregateInclude()
        {
            try
            {
                var concatedFrametimesInclude = new List<double>();
                var concatedDisplaytimesInclude = new List<double>();

                foreach (var record in _records)
                {
                    concatedFrametimesInclude.AddRange(record.Frametimes);
                    concatedDisplaytimesInclude.AddRange(record.Displaytimes);
                }

                var resultString = _statisticProvider
                    .GetMetricAnalysis(concatedFrametimesInclude, concatedDisplaytimesInclude,
                        _appConfiguration.UseDisplayChangeMetrics,
                        _appConfiguration.SecondMetricAggregation,
                        _appConfiguration.ThirdMetricAggregation).ResultString;

                AggregationResultString = $"Result: {resultString}";
                ShowResultString = true;

                WriteAggregatedFileAsync(Enumerable.Repeat(false, _records.Count).ToArray());
            }
            catch
            {
                AggregationEntries.Clear();
                AggregationResultString = CxLang.T("AggregationViewModel_ErrorWhileCalculatingResult");
                ShowResultString = true;
            }
        }

        private void OnAggregateExclude()
        {
            try
            {
                var outlierFlags = _statisticProvider
                        .GetOutlierAnalysis(AggregationEntries.Select(analysis => analysis.MetricAnalysis).ToList(),
                        _appConfiguration.RelatedMetricAggregation, _appConfiguration.OutlierPercentageAggregation);

                var concatedFrametimesExclude = new List<double>();
                var concatedDisplaytimesExclude = new List<double>();

                foreach (var record in _records.Where((x, i) => !outlierFlags[i]))
                {
                    concatedFrametimesExclude.AddRange(record.Frametimes);
                    concatedDisplaytimesExclude.AddRange(record.Displaytimes);
                }

                var resultString = _statisticProvider
                    .GetMetricAnalysis(concatedFrametimesExclude, concatedDisplaytimesExclude,
                        _appConfiguration.UseDisplayChangeMetrics,
                        _appConfiguration.SecondMetricAggregation,
                        _appConfiguration.ThirdMetricAggregation).ResultString;

                AggregationResultString = $"Result: {resultString}";
                ShowResultString = true;

                WriteAggregatedFileAsync(outlierFlags);
            }
            catch
            {
                AggregationEntries.Clear();
                AggregationResultString = CxLang.T("AggregationViewModel_ErrorWhileCalculatingResult");
                ShowResultString = true;
            }
        }

        private void WriteAggregatedFileAsync(bool[] outlierFlags)
        {
            // write aggregated file
            var filteredFileRecordInfoList = _records.Where((x, i) => !outlierFlags[i])
                .Select(record => record.RecordInfo).ToList();
            Task.Run(() =>
            {
                string process = string.Empty;

                var hwInfo = new List<ISessionInfo>();

                var runs = new List<ISessionRun>();

                foreach (var recordInfo in filteredFileRecordInfoList)
                {
                    var otherSession = _recordManager.LoadData(recordInfo.FullPath);
                    process = otherSession.Info.ProcessName;
                    hwInfo.Add(otherSession.Info);
                    runs.AddRange(otherSession.Runs);
                }

                _recordManager.SaveSessionRunsToFile(runs, process, string.Empty, null, hwInfo);
            });
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _useUpdateSession = true;
        }

        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            return true;
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            _useUpdateSession = false;
        }

        async void IDropTarget.Drop(IDropInfo dropInfo)
        {
            if (dropInfo != null)
            {
                if (dropInfo.VisualTarget is FrameworkElement frameworkElement)
                {
                    if (frameworkElement.Name == "AggregationItemDataGrid"
                        || frameworkElement.Name == "DragAndDropInfoTextTextBlock")
                    {
                        List<IFileRecordInfo> recordInfos = null;
                        if (dropInfo.Data is IFileRecordInfo recordInfo)
                        {
                            recordInfos = new List<IFileRecordInfo> { recordInfo };
                        }
                        else if (dropInfo.Data is System.Collections.IEnumerable droppedItems)
                        {
                            recordInfos = droppedItems.OfType<IFileRecordInfo>().ToList();
                        }

                        if (recordInfos != null)
                        {
                            PendingDrop = AddAggregationEntriesAsync(recordInfos);
                            await PendingDrop;
                        }
                    }
                }
            }
        }

        void IDropTarget.DragOver(IDropInfo dropInfo)
        {
            if (dropInfo != null)
            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
                dropInfo.Effects = DragDropEffects.Move;
            }
        }

        private sealed class AggregatedRecord
        {
            private AggregatedRecord(IFileRecordInfo recordInfo, double[] frametimes, double[] displaytimes)
            {
                RecordInfo = recordInfo;
                Frametimes = frametimes;
                Displaytimes = displaytimes;
            }

            public IFileRecordInfo RecordInfo { get; }
            public double[] Frametimes { get; }
            public double[] Displaytimes { get; }

            // Null for a record without frames.
            public static AggregatedRecord From(IFileRecordInfo recordInfo, ISession session)
            {
                if (recordInfo == null || !ComparisonViewModel.HasFrames(session))
                    return null;

                return new AggregatedRecord(recordInfo,
                    session.Runs.SelectMany(r => r.CaptureData.MsBetweenPresents).ToArray(),
                    session.Runs.SelectMany(r => r.CaptureData.MsBetweenDisplayChange).ToArray());
            }
        }
    }
}
