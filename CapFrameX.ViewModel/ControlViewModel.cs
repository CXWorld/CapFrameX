using CapFrameX.Contracts.OcatInterface;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.OcatInterface;
using Prism.Events;
using Prism.Mvvm;
using System;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Prism.Commands;
using CapFrameX.Contracts.Configuration;

namespace CapFrameX.ViewModel
{
	public class ControlViewModel : BindableBase
	{
		private readonly IRecordDirectoryObserver _recordObserver;
		private readonly IEventAggregator _eventAggregator;
		private readonly IAppConfiguration _appConfiguration;

		private PubSubEvent<ViewMessages.UpdateSession> _updateSessionEvent;
		private PubSubEvent<ViewMessages.SelectSession> _selectSessionEvent;
		private PubSubEvent<ViewMessages.ShowOverlay> _showOverlayEvent;
		private OcatRecordInfo _selectedRecordInfo;
		private bool _hasValidSource;

		public OcatRecordInfo SelectedRecordInfo
		{
			get { return _selectedRecordInfo; }
			set
			{
				_selectedRecordInfo = value;
				RaisePropertyChanged();
				OnSelectedRecordInfoChanged();
			}
		}

		public bool HasValidSource
		{
			get { return _hasValidSource; }
			set { _hasValidSource = value; RaisePropertyChanged(); }
		}

		public ObservableCollection<OcatRecordInfo> RecordInfoList { get; }
			= new ObservableCollection<OcatRecordInfo>();

		public ICommand OpenEditingDialogCommand { get; }

		public ICommand AddToIgnoreListCommand { get; }

		public ControlViewModel(IRecordDirectoryObserver recordObserver, 
								IEventAggregator eventAggregator, 
								IAppConfiguration appConfiguration)
		{
			_recordObserver = recordObserver;
			_eventAggregator = eventAggregator;
			_appConfiguration = appConfiguration;

			//Commands
			OpenEditingDialogCommand = new DelegateCommand(OnOpenEditingDialog);
			AddToIgnoreListCommand = new DelegateCommand(OnAddToIgnoreList);

			HasValidSource = recordObserver.HasValidSource;

			if (recordObserver.HasValidSource)
			{
				var initialRecordList = _recordObserver.GetAllRecordFileInfo();

				foreach (var fileInfo in initialRecordList)
				{
					AddToRecordInfoList(fileInfo);
				}
			}

			var context = SynchronizationContext.Current;
			_recordObserver.RecordCreatedStream.ObserveOn(context).SubscribeOn(context)
							.Subscribe(OnRecordCreated);
			_recordObserver.RecordDeletedStream.ObserveOn(context).SubscribeOn(context)
							.Subscribe(OnRecordDeleted);

			// Turn streams now on
			if (_recordObserver.HasValidSource)
				_recordObserver.IsActive = true;

			SetAggregatorEvents();
			SubscribeToResetRecord();
			SubscribeToObservedDiretoryUpdated();
		}


		private void OnOpenEditingDialog()
		{
			_showOverlayEvent.Publish(new ViewMessages.ShowOverlay());
		}

		private void OnAddToIgnoreList()
		{
			_appConfiguration.AddAppNameToIgnoreList(SelectedRecordInfo.GameName);

			SelectedRecordInfo = null;
			RecordInfoList.Clear();
			LoadRecordList();
		}

		public void OnRecordSelectByDoubleClick()
		{
			if (SelectedRecordInfo != null && _selectSessionEvent != null)
			{
				var session = RecordManager.LoadData(SelectedRecordInfo.FullPath);
				_selectSessionEvent.Publish(new ViewMessages.SelectSession(session, SelectedRecordInfo));
			}
		}

		private void OnSelectedRecordInfoChanged()
		{
			if (SelectedRecordInfo != null && _updateSessionEvent != null)
			{
				var session = RecordManager.LoadData(SelectedRecordInfo.FullPath);
				_updateSessionEvent.Publish(new ViewMessages.UpdateSession(session, SelectedRecordInfo));
			}
		}

		private void AddToRecordInfoList(FileInfo fileInfo)
		{
			var recordInfo = OcatRecordInfo.Create(fileInfo);
			if (recordInfo != null)
			{
				RecordInfoList.Add(recordInfo);
			}
		}

		private void OnRecordCreated(FileInfo fileInfo) => AddToRecordInfoList(fileInfo);

		private void OnRecordDeleted(FileInfo fileInfo)
		{
			var recordInfo = OcatRecordInfo.Create(fileInfo);
			if (recordInfo != null)
			{
				var match = RecordInfoList.FirstOrDefault(info => info.FullPath == fileInfo.FullName);

				if (match != null)
				{
					RecordInfoList.Remove(match);
				}
			}
		}

		private void LoadRecordList()
		{
			foreach (var fileInfo in _recordObserver.GetAllRecordFileInfo())
			{
				AddToRecordInfoList(fileInfo);
			}
		}

		private void SetAggregatorEvents()
		{
			_updateSessionEvent = _eventAggregator.GetEvent<PubSubEvent<ViewMessages.UpdateSession>>();
			_selectSessionEvent = _eventAggregator.GetEvent<PubSubEvent<ViewMessages.SelectSession>>();
			_showOverlayEvent = _eventAggregator.GetEvent<PubSubEvent<ViewMessages.ShowOverlay>>();
		}

		private void SubscribeToResetRecord()
		{
			_eventAggregator.GetEvent<PubSubEvent<ViewMessages.ResetRecord>>()
							.Subscribe(msg =>
							{
								SelectedRecordInfo = null;
							});
		}

		private void SubscribeToObservedDiretoryUpdated()
		{
			_eventAggregator.GetEvent<PubSubEvent<AppMessages.UpdateObservedDirectory>>()
							.Subscribe(msg =>
							{
								SelectedRecordInfo = null;
								RecordInfoList.Clear();

								HasValidSource = _recordObserver.HasValidSource;

								if (_recordObserver.HasValidSource)
								{
									LoadRecordList();
								}
							});
		}
	}
}
