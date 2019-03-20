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
using System.Threading.Tasks;
using System.Windows;

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
		private string _customCpuDescription;
		private string _customGpuDescription;
		private string _customComment;

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

		public string CustomCpuDescription
		{
			get { return _customCpuDescription; }
			set
			{
				_customCpuDescription = value;
				RaisePropertyChanged();
			}
		}

		public string CustomGpuDescription
		{
			get { return _customGpuDescription; }
			set
			{
				_customGpuDescription = value;
				RaisePropertyChanged();
			}
		}

		public string CustomComment
		{
			get { return _customComment; }
			set
			{
				_customComment = value;
				RaisePropertyChanged();
			}
		}


		public ObservableCollection<OcatRecordInfo> RecordInfoList { get; }
			= new ObservableCollection<OcatRecordInfo>();

		public ICommand OpenEditingDialogCommand { get; }

		public ICommand AddToIgnoreListCommand { get; }

		public ICommand DeleteRecordFileCommand { get; }

		public ICommand AcceptEditingDialogCommand { get; }

		public ICommand CancelEditingDialogCommand { get; }

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
			DeleteRecordFileCommand = new DelegateCommand(OnDeleteRecordFile);
			AcceptEditingDialogCommand = new DelegateCommand(OnAcceptEditingDialog);
			CancelEditingDialogCommand = new DelegateCommand(OnCancelEditingDialog);

			HasValidSource = recordObserver.HasValidSource;

			Task.Factory.StartNew(() =>
			{
				if (recordObserver.HasValidSource)
				{
					var initialRecordList = _recordObserver.GetAllRecordFileInfo();

					foreach (var fileInfo in initialRecordList)
					{
						AddToRecordInfoList(fileInfo);
					}
				}
			});

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

		private void OnDeleteRecordFile()
		{
			if (!RecordInfoList.Any())
				return;

			File.Delete(SelectedRecordInfo.FullPath);
			SelectedRecordInfo = null;
			_updateSessionEvent.Publish(new ViewMessages.UpdateSession(null, null));
		}

		private void OnOpenEditingDialog()
		{
			if (!RecordInfoList.Any())
				return;

			_showOverlayEvent.Publish(new ViewMessages.ShowOverlay());
		}

		private void OnAddToIgnoreList()
		{
			if (!RecordInfoList.Any())
				return;

			_appConfiguration.AddAppNameToIgnoreList(SelectedRecordInfo.GameName);

			SelectedRecordInfo = null;
			RecordInfoList.Clear();
			LoadRecordList();
		}

		private void OnCancelEditingDialog()
		{
			// Undo
			var session = RecordManager.LoadData(SelectedRecordInfo.FullPath);

			if (session != null)
			{
				CustomCpuDescription = string.Copy(session.ProcessorName ?? "-");
				CustomGpuDescription = string.Copy(session.GraphicCardName ?? "-");
				CustomComment = string.Copy(session.Comment ?? "-");
			}
			else
			{
				CustomCpuDescription = "-";
				CustomGpuDescription = "-";
				CustomComment = "-";
			}
		}

		private void OnAcceptEditingDialog()
		{
			if (CustomCpuDescription == null || CustomGpuDescription == null || CustomComment == null)
				return;

			var adjustedCustomCpuDescription = CustomCpuDescription.Replace(",", "").Replace(";", "");
			var adjustedCustomGpuDescription = CustomGpuDescription.Replace(",", "").Replace(";", "");
			var adjustedCustomComment = CustomComment.Replace(",", "").Replace(";", "");
			RecordManager.UpdateCustomData(_selectedRecordInfo,
				adjustedCustomCpuDescription, adjustedCustomGpuDescription, adjustedCustomComment);
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

				if (session != null)
				{
					CustomCpuDescription = string.Copy(session.ProcessorName ?? "-");
					CustomGpuDescription = string.Copy(session.GraphicCardName ?? "-");
					CustomComment = string.Copy(session.Comment ?? "-");
				}
				else
				{
					CustomCpuDescription = "-";
					CustomGpuDescription = "-";
					CustomComment = "-";
				}

				_updateSessionEvent.Publish(new ViewMessages.UpdateSession(session, SelectedRecordInfo));
			}
		}

		private void AddToRecordInfoList(FileInfo fileInfo)
		{
			var recordInfo = OcatRecordInfo.Create(fileInfo);
			if (recordInfo != null)
			{
				Application.Current.Dispatcher.BeginInvoke(new Action(() =>
				{
					RecordInfoList.Add(recordInfo);
				}));
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
