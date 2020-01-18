using CapFrameX.Contracts.Aggregation;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.EventAggregation.Messages;
using GongSolutions.Wpf.DragDrop;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.ObjectModel;
using System.Windows;

namespace CapFrameX.ViewModel
{
	public class AggregationViewModel : BindableBase, INavigationAware, IDropTarget
	{
		private readonly IRecordDataProvider _recordDataProvider;
		private readonly IEventAggregator _eventAggregator;
		private readonly IAppConfiguration _appConfiguration;

		private bool _useUpdateSession;
		private int _selectedAggregationEntryIndex = -1;
		private string _selectedSecondMetric;
		private string _selectedThirdMetric;

		public int SelectedAggregationEntryIndex
		{
			get { return _selectedAggregationEntryIndex; }
			set
			{
				_selectedAggregationEntryIndex = value;
				RaisePropertyChanged();
			}
		}

		public string SelectedSecondMetric
		{
			get { return _appConfiguration.SecondMetricAggregation; }
			set
			{
				_appConfiguration.SecondMetricAggregation = value;
				RaisePropertyChanged();
			}
		}

		public string SelectedThirdMetric
		{
			get { return _appConfiguration.ThirdMetricAggregation; }
			set
			{
				_appConfiguration.ThirdMetricAggregation = value;
				RaisePropertyChanged();
			}
		}

		public ObservableCollection<IAggregationEntry> AggregationEntries { get; private set; }
			= new ObservableCollection<IAggregationEntry>();

		public AggregationViewModel(IRecordDataProvider recordDataProvider,
			IEventAggregator eventAggregator, IAppConfiguration appConfiguration)
		{
			_recordDataProvider = recordDataProvider;
			_eventAggregator = eventAggregator;
			_appConfiguration = appConfiguration;

			SubscribeToUpdateSession();
		}

		private void SubscribeToUpdateSession()
		{
			_eventAggregator.GetEvent<PubSubEvent<ViewMessages.UpdateSession>>()
							.Subscribe(msg =>
							{
								if (_useUpdateSession)
								{
									AddAggregationEntry(msg.RecordInfo);
								}
							});
		}

		private void AddAggregationEntry(IFileRecordInfo recordInfo)
		{ 
			throw new NotImplementedException();
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
							AddAggregationEntry(recordInfo);
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