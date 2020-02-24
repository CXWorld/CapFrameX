using CapFrameX.Contracts.Aggregation;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Data;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Extensions;
using CapFrameX.Statistics;
using GongSolutions.Wpf.DragDrop;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
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
		private bool _useUpdateSession;
		private int _selectedAggregationEntryIndex = -1;
		private bool _showHelpText = true;
		private bool _enableClearButton = false;
		private bool _enableIncludeButton = false;
		private bool _enableExcludeButton = false;
		private List<IFileRecordInfo> _fileRecordInfoList = new List<IFileRecordInfo>();
		private bool _showResultString;
		private string _aggregationResultString = string.Empty;
		private bool _supressCollectionChanged;

		public int SelectedAggregationEntryIndex
		{
			get
			{return _selectedAggregationEntryIndex;}
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
			{return _appConfiguration.RelatedMetricAggregation;}
			set
			{
				_appConfiguration.RelatedMetricAggregation = value;
				UpdateAggregationEntries();
				RaisePropertyChanged();
			}
		}

		public int SelectedOutlierPercentage
		{
			get
			{return _appConfiguration.OutlierPercentageAggregation;}
			set
			{
				_appConfiguration.OutlierPercentageAggregation = value;
				UpdateAggregationEntries();
				RaisePropertyChanged();
			}
		}

		public bool ShowHelpText
		{
			get
			{return _showHelpText;}
			set
			{
				_showHelpText = value;
				RaisePropertyChanged();
			}
		}

		public string AggregationResultString
		{
			get
			{return _aggregationResultString;}
			set
			{
				_aggregationResultString = value;
				RaisePropertyChanged();
			}
		}

		public bool ShowResultString
		{
			get
			{return _showResultString;}
			set
			{
				_showResultString = value;
				RaisePropertyChanged();
			}
		}

		public bool EnableClearButton
		{
			get
			{return _enableClearButton;}
			set
			{
				_enableClearButton = value;
				RaisePropertyChanged();
			}
		}
		public bool EnableIncludeButton
		{
			get
			{return _enableIncludeButton;}
			set
			{
				_enableIncludeButton = value;
				RaisePropertyChanged();
			}
		}
		public bool EnableExcludeButton
		{
			get
			{return _enableExcludeButton;}
			set
			{
				_enableExcludeButton = value;
				RaisePropertyChanged();
			}
		}

		public Array RelatedMetricItemsSource => new[] { "Average", "Second", "Third" };

		public Array OutlierPercentageItemsSource => Enumerable.Range(2, 9).ToArray();

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

		public AggregationViewModel(IStatisticProvider statisticProvider,
			IEventAggregator eventAggregator, IAppConfiguration appConfiguration, IRecordManager recordManager)
		{
			_statisticProvider = statisticProvider;
			_eventAggregator = eventAggregator;
			_appConfiguration = appConfiguration;
			_recordManager = recordManager;
			ClearTableCommand = new DelegateCommand(OnClearTable);
			AggregateIncludeCommand = new DelegateCommand(OnAggregateInclude);
			AggregateExcludeCommand = new DelegateCommand(OnAggregateExclude);

			SubscribeToUpdateSession();

			AggregationEntries.CollectionChanged += new NotifyCollectionChangedEventHandler
				((sender, eventArg) => OnAggregationEntriesChanged());
		}

		public void RemoveAggregationEntry(IAggregationEntry entry)
		{
			_fileRecordInfoList.Remove(entry.FileRecordInfo);
			AggregationEntries.Remove(entry);
		}

		private void OnAggregationEntriesChanged()
		{
			ShowHelpText = !AggregationEntries.Any();
			EnableClearButton = AggregationEntries.Any();
			EnableIncludeButton = AggregationEntries.Count >= 2;
			EnableExcludeButton = AggregationEntries.Count >= 2;

			// Outlier analysis
			if (AggregationEntries.Count >= 2 && !_supressCollectionChanged)
			{
				var outlierFlags = _statisticProvider
					.GetOutlierAnalysis(AggregationEntries.Select(analysis => analysis.MetricAnalysis).ToList(),
					_appConfiguration.RelatedMetricAggregation, _appConfiguration.OutlierPercentageAggregation);

				OutlierFlagStream.OnNext(outlierFlags);

				EnableExcludeButton = outlierFlags.Any(x => x == false);
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
			if (recordInfo != null)
			{
				if(_fileRecordInfoList.Any())
				{
					if (!_fileRecordInfoList.All(info => info.ProcessName == recordInfo.ProcessName))
						return;
				}

				_fileRecordInfoList.Add(recordInfo);
			}
			else
				return;


			var frametimes = session.Runs.SelectMany(r => r.CaptureData.MsBetweenPresents).ToList();

			if (session == null)
			{
				var localSession = _recordManager.LoadData(recordInfo.FullPath);
				frametimes = localSession.Runs.SelectMany(r => r.CaptureData.MsBetweenPresents).ToList();
			}

			var metricAnalysis = _statisticProvider
				.GetMetricAnalysis(frametimes, SelectedSecondMetric.ConvertToString(),
					SelectedThirdMetric.ConvertToString());

			AggregationEntries.Add(new AggregationEntry()
			{
				GameName = recordInfo.GameName,
				CreationDate = recordInfo.CreationDate,
				CreationTime = recordInfo.CreationTime,
				AverageValue = metricAnalysis.Average,
				SecondMetricValue = metricAnalysis.Second,
				ThirdMetricValue = metricAnalysis.Third,
				MetricAnalysis = metricAnalysis,
				FileRecordInfo = recordInfo
			});
		}

		private void UpdateAggregationEntries()
		{
			if (!_fileRecordInfoList.Any())
				return;

			AggregationEntries.Clear();

			_supressCollectionChanged = true;
			foreach (var recordInfo in _fileRecordInfoList)
			{
				var localSession = _recordManager.LoadData(recordInfo.FullPath);
				var frametimes = localSession.Runs.SelectMany(r => r.CaptureData.MsBetweenPresents).ToList();

				var metricAnalysis = _statisticProvider
					.GetMetricAnalysis(frametimes, SelectedSecondMetric.ConvertToString(),
						SelectedThirdMetric.ConvertToString());

				AggregationEntries.Add(new AggregationEntry()
				{
					GameName = recordInfo.GameName,
					CreationDate = recordInfo.CreationDate,
					CreationTime = recordInfo.CreationTime,
					AverageValue = metricAnalysis.Average,
					SecondMetricValue = metricAnalysis.Second,
					ThirdMetricValue = metricAnalysis.Third,
					MetricAnalysis = metricAnalysis
				});
			}
			_supressCollectionChanged = false;
			OnAggregationEntriesChanged();
		}

		private void OnClearTable()
		{
			AggregationEntries.Clear();
			_fileRecordInfoList.Clear();
			AggregationResultString = string.Empty;
			ShowResultString = false;
		}

		private void OnAggregateInclude()
		{
			var concatedFrametimesInclude = new List<double>();

			foreach (var recordInfo in _fileRecordInfoList)
			{
				var localSession = _recordManager.LoadData(recordInfo.FullPath);
				var frametimes = localSession.Runs.SelectMany(r => r.CaptureData.MsBetweenPresents).ToList();
				concatedFrametimesInclude.AddRange(frametimes);
			}

			var resultString = _statisticProvider
				.GetMetricAnalysis(concatedFrametimesInclude,
				_appConfiguration.SecondMetricAggregation,
				_appConfiguration.ThirdMetricAggregation).ResultString;

			AggregationResultString = $"Result: {resultString}";
			ShowResultString = true;

			WriteAggregatedFileAsync(Enumerable.Repeat(false, _fileRecordInfoList.Count).ToArray());
		}

		private void OnAggregateExclude()
		{
			var outlierFlags = _statisticProvider
					.GetOutlierAnalysis(AggregationEntries.Select(analysis => analysis.MetricAnalysis).ToList(),
					_appConfiguration.RelatedMetricAggregation, _appConfiguration.OutlierPercentageAggregation);

			var concatedFrametimesExclude = new List<double>();

			foreach (var recordInfo in _fileRecordInfoList.Where((x, i) => !outlierFlags[i]))
			{
				var localSession = _recordManager.LoadData(recordInfo.FullPath);
				var frametimes = localSession.Runs.SelectMany(r => r.CaptureData.MsBetweenPresents).ToList();
				concatedFrametimesExclude.AddRange(frametimes);
			}

			var resultString = _statisticProvider
				.GetMetricAnalysis(concatedFrametimesExclude,
				_appConfiguration.SecondMetricAggregation,
				_appConfiguration.ThirdMetricAggregation).ResultString;

			AggregationResultString = $"Result: {resultString}";
			ShowResultString = true;

			WriteAggregatedFileAsync(outlierFlags);
		}

		private void WriteAggregatedFileAsync(bool[] outlierFlags)
		{
			// write aggregated file
			Task.Run(() =>
			{
				string process = string.Empty;
				var filteredFileRecordInfoList = _fileRecordInfoList.Where((x, i) => !outlierFlags[i]);

				var runs = new List<ISessionRun>();

				foreach (var recordInfo in filteredFileRecordInfoList)
				{
					var otherSession = _recordManager.LoadData(recordInfo.FullPath);
					process = otherSession.Info.ProcessName;
					runs.AddRange(otherSession.Runs);
				}

				_recordManager.SaveSessionRunsToFile(runs, process);
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

		void IDropTarget.Drop(IDropInfo dropInfo)
		{
			if (dropInfo != null)
			{
				if (dropInfo.VisualTarget is FrameworkElement frameworkElement)
				{
					if (frameworkElement.Name == "AggregationItemDataGrid"
						|| frameworkElement.Name == "DragAndDropInfoTextTextBlock")
					{
						if (dropInfo.Data is IFileRecordInfo recordInfo)
						{
							AddAggregationEntry(recordInfo, null);
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
	}
}