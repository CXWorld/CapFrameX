using CapFrameX.Contracts.OcatInterface;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.OcatInterface;
using CapFrameX.MVVM.Dialogs;
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

namespace CapFrameX.ViewModel
{
	public class ControlViewModel : BindableBase
	{
		private readonly IRecordDirectoryObserver _recordObserver;
		private readonly IEventAggregator _eventAggregator;

		private PubSubEvent<ViewMessages.UpdateSession> _updateSessionEvent;
		private PubSubEvent<ViewMessages.SelectSession> _selectSessionEvent;
		private PubSubEvent<ViewMessages.ShowOverlay> _showOverlayEvent;
		private OcatRecordInfo _selectedRecordInfo;

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

		public ObservableCollection<OcatRecordInfo> RecordInfoList { get; }
			= new ObservableCollection<OcatRecordInfo>();

		public ICommand OpenEditingDialogCommand { get; }

		public ControlViewModel(IRecordDirectoryObserver recordObserver, IEventAggregator eventAggregator)
		{
			_recordObserver = recordObserver;
			_eventAggregator = eventAggregator;

			//Commands
			OpenEditingDialogCommand = new DelegateCommand(OnOpenEditingDialog);

			// ToDo: check wether to do this async
			var initialRecordList = _recordObserver.GetAllRecordFileInfo();

			foreach (var fileInfo in initialRecordList)
			{
				AddToRecordInfoList(fileInfo);
			}

			var context = SynchronizationContext.Current;
			_recordObserver.RecordCreatedStream.ObserveOn(context).SubscribeOn(context)
							.Subscribe(OnRecordCreated);
			_recordObserver.RecordDeletedStream.ObserveOn(context).SubscribeOn(context)
							.Subscribe(OnRecordDeleted);

			// Turn streams now on
			_recordObserver.IsActive = true;

			SetAggregatorEvents();
			SubscribeToResetRecord();
		}

		private void OnOpenEditingDialog()
		{
			_showOverlayEvent.Publish(new ViewMessages.ShowOverlay());			
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
	}
}
