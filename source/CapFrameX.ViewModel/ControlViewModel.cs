using CapFrameX.EventAggregation.Messages;
using CapFrameX.Data;
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
using CapFrameX.PresentMonInterface;
using CapFrameX.Contracts.Data;
using System.Collections.Generic;
using System.Collections;
using System.Reactive.Subjects;
using CapFrameX.Contracts.PresentMonInterface;

namespace CapFrameX.ViewModel
{
	public class ControlViewModel : BindableBase
	{
		private readonly IRecordDirectoryObserver _recordObserver;
		private readonly IEventAggregator _eventAggregator;
		private readonly IAppConfiguration _appConfiguration;
		private readonly IRecordDataProvider _recordDataProvider;
		private readonly ISubject<FileInfo> _recordDeleteSubStream;

		private PubSubEvent<ViewMessages.UpdateSession> _updateSessionEvent;
		private PubSubEvent<ViewMessages.SelectSession> _selectSessionEvent;
		private PubSubEvent<ViewMessages.UpdateProcessIgnoreList> _updateProcessIgnoreListEvent;
		private PubSubEvent<ViewMessages.UpdateRecordInfos> _updateRecordInfosEvent;

		private IFileRecordInfo _selectedRecordInfo;
		private bool _hasValidSource;
		private string _customCpuDescription;
		private string _customGpuDescription;
		private string _customRamDescription;
		private string _customGameName;
		private string _customComment;
		private int _recordDataGridSelectedIndex;
		private List<IFileRecordInfo> _selectedRecordings;
		private bool _recordDeleteStreamActive = true;

		public IFileRecordInfo SelectedRecordInfo
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

		public string CustomRamDescription
		{
			get { return _customRamDescription; }
			set
			{
				_customRamDescription = value;
				RaisePropertyChanged();
			}
		}

		public string CustomGameName
		{
			get { return _customGameName; }
			set
			{
				_customGameName = value;
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

		public int RecordDataGridSelectedIndex
		{
			get { return _recordDataGridSelectedIndex; }
			set
			{
				_recordDataGridSelectedIndex = value;
				RaisePropertyChanged();
			}
		}

		public ObservableCollection<IFileRecordInfo> RecordInfoList { get; }
			= new ObservableCollection<IFileRecordInfo>();

		public IAppConfiguration AppConfiguration => _appConfiguration;

		public ICommand OpenEditingDialogCommand { get; }

		public ICommand AddToIgnoreListCommand { get; }

		public ICommand DeleteRecordFileCommand { get; }

		public ICommand AcceptEditingDialogCommand { get; }

		public ICommand CancelEditingDialogCommand { get; }

		public ICommand AddCpuInfoCommand { get; }

		public ICommand AddGpuInfoCommand { get; }

		public ICommand AddRamInfoCommand { get; }

		public ICommand DeleteRecordCommand { get; }

		public ICommand SelectedRecordingsCommand { get; }

		public ControlViewModel(IRecordDirectoryObserver recordObserver,
								IEventAggregator eventAggregator,
								IAppConfiguration appConfiguration,
								IRecordDataProvider recordDataProvider)
		{
			_recordObserver = recordObserver;
			_eventAggregator = eventAggregator;
			_appConfiguration = appConfiguration;
			_recordDataProvider = recordDataProvider;

			//Commands
			AddToIgnoreListCommand = new DelegateCommand(OnAddToIgnoreList);
			DeleteRecordFileCommand = new DelegateCommand(OnDeleteRecordFile);
			AcceptEditingDialogCommand = new DelegateCommand(OnAcceptEditingDialog);
			CancelEditingDialogCommand = new DelegateCommand(OnCancelEditingDialog);
			AddCpuInfoCommand = new DelegateCommand(OnAddCpuInfo);
			AddGpuInfoCommand = new DelegateCommand(OnAddGpuInfo);
			AddRamInfoCommand = new DelegateCommand(OnAddRamInfo);
			DeleteRecordCommand = new DelegateCommand(OnPressDeleteKey);
			SelectedRecordingsCommand = new DelegateCommand<object>(OnSelectedRecordings);

			HasValidSource = recordObserver.HasValidSource;

			Task.Factory.StartNew(() =>
			{
				if (recordObserver.HasValidSource)
				{
					var initialRecordFileInfoList = _recordDataProvider?.GetFileRecordInfoList();

					foreach (var recordFileInfo in initialRecordFileInfoList)
					{
						AddToRecordInfoList(recordFileInfo);
					}
				}
			});

			RecordDataGridSelectedIndex = -1;

			_recordDeleteSubStream = new Subject<FileInfo>();

			var context = SynchronizationContext.Current;
			_recordObserver.RecordCreatedStream
						   .ObserveOn(context)
						   .SubscribeOn(context)
						   .Subscribe(OnRecordCreated);
			_recordObserver.RecordDeletedStream
						   .Merge(_recordDeleteSubStream)
						   .Where(x => _recordDeleteStreamActive)
						   .ObserveOn(context)
						   .SubscribeOn(context)
						   .Subscribe(x => OnRecordDeleted());

			// Turn streams now on
			if (_recordObserver.HasValidSource)
				_recordObserver.IsActive = true;

			SetAggregatorEvents();
			SubscribeToResetRecord();
			SubscribeToObservedDiretoryUpdated();
			SubscribeToSetFileRecordInfoExternal();
		}

		private void OnDeleteRecordFile()
		{
			if (!RecordInfoList.Any())
				return;

			try
			{
				if (_selectedRecordings?.Count > 1)
				{
					_recordDeleteStreamActive = false;

					foreach (var item in _selectedRecordings)
					{
						File.Delete(item.FullPath);
					}

					_ = _recordObserver.RecordingFileWatcher
						.WaitForChanged(WatcherChangeTypes.Deleted, 1000);

					_recordDeleteStreamActive = true;
					_recordDeleteSubStream.OnNext(null);
				}
				else
				{
					File.Delete(SelectedRecordInfo.FullPath);
				}

				SelectedRecordInfo = null;
				_selectedRecordings = null;
				ResetInfoEditBoxes();

				_updateSessionEvent.Publish(new ViewMessages.UpdateSession(null, null));
			}
			catch { }
		}

		private void OnAddToIgnoreList()
		{
			if (!RecordInfoList.Any())
				return;

			CaptureServiceConfiguration.AddProcessToIgnoreList(SelectedRecordInfo.GameName);
			_updateProcessIgnoreListEvent.Publish(new ViewMessages.UpdateProcessIgnoreList());

			SelectedRecordInfo = null;
			RecordInfoList.Clear();
			LoadRecordList();
		}

		private void OnCancelEditingDialog()
		{
			if (SelectedRecordInfo != null)
			{
				CustomCpuDescription = string.Copy(SelectedRecordInfo.ProcessorName ?? string.Empty);
				CustomGpuDescription = string.Copy(SelectedRecordInfo.GraphicCardName ?? string.Empty);
				CustomRamDescription = string.Copy(SelectedRecordInfo.SystemRamInfo ?? string.Empty);
				CustomGameName = string.Copy(SelectedRecordInfo.GameName ?? string.Empty);
				CustomComment = string.Copy(SelectedRecordInfo.Comment ?? string.Empty);
			}
			else
			{
				ResetInfoEditBoxes();
			}
		}

		private void ResetInfoEditBoxes()
		{
			CustomCpuDescription = string.Empty;
			CustomGpuDescription = string.Empty;
			CustomRamDescription = string.Empty;
			CustomGameName = string.Empty;
			CustomComment = string.Empty;
		}

		private void OnAcceptEditingDialog()
		{
			if (CustomCpuDescription == null || CustomGpuDescription == null || CustomRamDescription == null
				|| CustomGameName == null || CustomComment == null || _selectedRecordInfo == null)
				return;

			// hint: _selectedRecordInfo must not be uptated, because after reload
			// it will be set to null
			RecordManager.UpdateCustomData(_selectedRecordInfo,
				CustomCpuDescription, CustomGpuDescription, CustomRamDescription, CustomGameName, CustomComment);

			_recordDataProvider.AddGameNameToMatchingList(_selectedRecordInfo.ProcessName, CustomGameName);

			var id = SelectedRecordInfo.Id;
			ReloadRecordList();

			// Get recordInfo after update via id
			var updatedRecordInfo = RecordInfoList.FirstOrDefault(info => info.Id == id);

			if (updatedRecordInfo != null)
			{
				SelectedRecordInfo = updatedRecordInfo;
				_updateRecordInfosEvent.Publish(new ViewMessages.UpdateRecordInfos(updatedRecordInfo));
			}
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
					CustomCpuDescription = string.Copy(SelectedRecordInfo.ProcessorName ?? string.Empty);
					CustomGpuDescription = string.Copy(SelectedRecordInfo.GraphicCardName ?? string.Empty);
					CustomRamDescription = string.Copy(SelectedRecordInfo.SystemRamInfo ?? string.Empty);
					CustomGameName = string.Copy(SelectedRecordInfo.GameName ?? string.Empty);
					CustomComment = string.Copy(SelectedRecordInfo.Comment ?? string.Empty);
				}
				else
				{
					ResetInfoEditBoxes();
				}

				_updateSessionEvent.Publish(new ViewMessages.UpdateSession(session, SelectedRecordInfo));
			}
		}

		private void OnAddCpuInfo()
		{
			CustomCpuDescription = SystemInfo.GetProcessorName();
		}

		private void OnAddGpuInfo()
		{
			CustomGpuDescription = SystemInfo.GetGraphicCardName();
		}

		private void OnAddRamInfo()
		{
			CustomRamDescription = SystemInfo.GetSystemRAMInfoName();
		}

		private void OnPressDeleteKey()
			=> OnDeleteRecordFile();

		void OnSelectedRecordings(object selectedRecordings)
		{
			if (selectedRecordings == null)
				return;

			_selectedRecordings = new List<IFileRecordInfo>((selectedRecordings as IList).Cast<IFileRecordInfo>());
		}

		private void AddToRecordInfoList(IFileRecordInfo recordFileInfo)
		{
			if (recordFileInfo != null && !RecordInfoList.Any(info => info.Id == recordFileInfo.Id))
			{
				Application.Current.Dispatcher.Invoke(new Action(() =>
				{
					RecordInfoList.Add(recordFileInfo);
				}));
			}
		}

		private void OnRecordCreated(FileInfo fileInfo)
			=> AddToRecordInfoList(_recordDataProvider.GetFileRecordInfo(fileInfo));

		private void OnRecordDeleted()
		{
			ReloadRecordList();
		}

		private void ReloadRecordList()
		{
			RecordInfoList.Clear();
			LoadRecordList();
		}

		private void LoadRecordList()
		{
			foreach (var fileRecordInfo in _recordDataProvider?.GetFileRecordInfoList())
			{
				AddToRecordInfoList(fileRecordInfo);
			}
		}

		private void SetAggregatorEvents()
		{
			_updateSessionEvent = _eventAggregator.GetEvent<PubSubEvent<ViewMessages.UpdateSession>>();
			_selectSessionEvent = _eventAggregator.GetEvent<PubSubEvent<ViewMessages.SelectSession>>();
			_updateProcessIgnoreListEvent = _eventAggregator.GetEvent<PubSubEvent<ViewMessages.UpdateProcessIgnoreList>>();
			_updateRecordInfosEvent = _eventAggregator.GetEvent<PubSubEvent<ViewMessages.UpdateRecordInfos>>();
		}

		private void SubscribeToResetRecord()
		{
			_eventAggregator.GetEvent<PubSubEvent<ViewMessages.ResetRecord>>()
							.Subscribe(msg =>
							{
								SelectedRecordInfo = null;
								_selectedRecordings = null;
							});
		}

		private void SubscribeToObservedDiretoryUpdated()
		{
			_eventAggregator.GetEvent<PubSubEvent<AppMessages.UpdateObservedDirectory>>()
							.Subscribe(msg =>
							{
								SelectedRecordInfo = null;
								_selectedRecordings = null;
								RecordInfoList.Clear();

								HasValidSource = _recordObserver.HasValidSource;

								if (HasValidSource)
								{
									LoadRecordList();
								}
							});
		}

		private void SubscribeToSetFileRecordInfoExternal()
		{
			_eventAggregator.GetEvent<PubSubEvent<ViewMessages.SetFileRecordInfoExternal>>()
							.Subscribe(msg =>
							{
								SelectedRecordInfo = RecordInfoList
									.FirstOrDefault(info => info.Id == msg.RecordInfo.Id);
								_selectedRecordings = null;
							});
		}
	}
}
