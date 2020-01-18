using CapFrameX.Contracts.Aggregation;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Data;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Extensions;
using CapFrameX.Statistics;
using GongSolutions.Wpf.DragDrop;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;

namespace CapFrameX.ViewModel
{
	public class AggregationViewModel : BindableBase, INavigationAware, IDropTarget
	{
		private readonly IStatisticProvider _statisticProvider;
		private readonly IRecordDataProvider _recordDataProvider;
		private readonly IEventAggregator _eventAggregator;
		private readonly IAppConfiguration _appConfiguration;

		private bool _useUpdateSession;
		private int _selectedAggregationEntryIndex = -1;
		private bool _showHelpText = true;

		public int SelectedAggregationEntryIndex
		{
			get { return _selectedAggregationEntryIndex; }
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
				RaisePropertyChanged();
			}
		}

		public string SelectedRelatedMetric
		{
			get { return _appConfiguration.RelatedMetricAggregation; }
			set
			{
				_appConfiguration.RelatedMetricAggregation = value;
				RaisePropertyChanged();
			}
		}

		public int SelectedOutlierPercentage
		{
			get { return _appConfiguration.OutlierPercentageAggregation; }
			set
			{
				_appConfiguration.OutlierPercentageAggregation = value;
				RaisePropertyChanged();
			}
		}

		public bool ShowHelpText
		{
			get { return _showHelpText; }
			set
			{
				_showHelpText = value;
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



		public ObservableCollection<IAggregationEntry> AggregationEntries { get; private set; }
			= new ObservableCollection<IAggregationEntry>();

		public AggregationViewModel(IStatisticProvider statisticProvider, IRecordDataProvider recordDataProvider,
			IEventAggregator eventAggregator, IAppConfiguration appConfiguration)
		{
			_statisticProvider = statisticProvider;
			_recordDataProvider = recordDataProvider;
			_eventAggregator = eventAggregator;
			_appConfiguration = appConfiguration;

			SubscribeToUpdateSession();

			AggregationEntries.CollectionChanged += new NotifyCollectionChangedEventHandler
				((sender, eventArg) => ShowHelpText = !AggregationEntries.Any());
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

		private void AddAggregationEntry(IFileRecordInfo recordInfo, Session session)
		{
			List<double> frametimes = session?.FrameTimes;

			if (session == null)
			{
				var localSession = RecordManager.LoadData(recordInfo.FullPath);
				frametimes = localSession?.FrameTimes;
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
				ThirdMetricValue = metricAnalysis.Third
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
					if (frameworkElement.Name == "AggregationItemDataGrid")
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
				dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight;
				dropInfo.Effects = DragDropEffects.Move;
			}
		}
	}
}