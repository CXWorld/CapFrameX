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
using Microsoft.VisualBasic.FileIO;

namespace CapFrameX.ViewModel
{
	public class ControlViewModel : BindableBase
	{
		private readonly IRecordDirectoryObserver _recordObserver;
		private readonly IEventAggregator _eventAggregator;
		private readonly IAppConfiguration _appConfiguration;
		private readonly IRecordManager _recordManager;

		private PubSubEvent<ViewMessages.UpdateSession> _updateSessionEvent;
		private PubSubEvent<ViewMessages.SelectSession> _selectSessionEvent;
		private PubSubEvent<ViewMessages.UpdateProcessIgnoreList> _updateProcessIgnoreListEvent;
		private PubSubEvent<ViewMessages.UpdateRecordInfos> _updateRecordInfosEvent;

		private IFileRecordInfo _selectedRecordInfo;
		private string _customCpuDescription;
		private string _customGpuDescription;
		private string _customRamDescription;
		private string _customGameName;
		private string _customComment;
		private int _recordDataGridSelectedIndex;
		private List<IFileRecordInfo> _selectedRecordings;

		public IFileRecordInfo SelectedRecordInfo
		{
			get { return _selectedRecordInfo; }
			set
			{
				_selectedRecordInfo = value;
				OnSelectedRecordInfoChanged();
				RaisePropertyChanged();
			}
		}

		public bool HasValidSource { set; private get; }

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
								IAppConfiguration appConfiguration, RecordManager recordManager)
		{
			_recordObserver = recordObserver;
			_eventAggregator = eventAggregator;
			_appConfiguration = appConfiguration;
			_recordManager = recordManager;

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

			RecordDataGridSelectedIndex = -1;

			SetAggregatorEvents();
			SubscribeToResetRecord();
			SubscribeToSetFileRecordInfoExternal();
			SetupObservers(SynchronizationContext.Current);
		}

		private void SetupObservers(SynchronizationContext context)
		{
			_recordObserver.ObservingDirectoryStream
				.ObserveOn(context)
				.Subscribe(directory =>
				{
					HasValidSource = directory?.Exists ?? false;
					RaisePropertyChanged(nameof(HasValidSource));
				});

			_recordObserver.DirectoryFilesStream
				.DistinctUntilChanged()
				.Do(_ => RecordInfoList.Clear())
				.SelectMany(fileInfos => 
					Observable.Merge(fileInfos.Select(fileInfo => Observable.FromAsync(() => _recordManager.GetFileRecordInfo(fileInfo))))
					.Where(recordFileInfo => recordFileInfo is IFileRecordInfo)
					.Distinct(recordFileInfo => recordFileInfo.Hash)
				)
				.ObserveOn(context)
				.Subscribe(recordFileInfos =>
				{
					RecordInfoList.Add(recordFileInfos);
				});

			_recordObserver.FileCreatedStream
				.SelectMany(fileInfo => _recordManager.GetFileRecordInfo(fileInfo))
				.Where(recordInfo => recordInfo is IFileRecordInfo)
				.ObserveOn(context)
				.Subscribe(recordInfo =>
				{
					RecordInfoList.Add(recordInfo);
				});

			_recordObserver.FileDeletedStream
				.ObserveOn(context)
				.Subscribe(fileInfo =>
				{
					var item = RecordInfoList.FirstOrDefault(ri => ri.FullPath.Equals(fileInfo.FullName));
					if (item is IFileRecordInfo)
					{
						RecordInfoList.Remove(item);
					}
				});

			_recordObserver.FileChangedStream
				.SelectMany(fileInfo => _recordManager.GetFileRecordInfo(fileInfo))
				.Where(recordInfo => recordInfo is IFileRecordInfo)
				.ObserveOn(context)
				.Subscribe(recordInfo =>
				{
				var itemToRemove = RecordInfoList.FirstOrDefault(ri => ri.FullPath.Equals(recordInfo.FullPath));
				if (itemToRemove is IFileRecordInfo)
				{
					var selectedRecordId = _selectedRecordInfo?.Id;
					var itemIndex = RecordInfoList.IndexOf(itemToRemove);
					RecordInfoList[itemIndex] = recordInfo;
					if (selectedRecordId?.Equals(itemToRemove.Id) ?? false)
					{
						SelectedRecordInfo = recordInfo;
						_updateRecordInfosEvent.Publish(new ViewMessages.UpdateRecordInfos(itemToRemove));
						}
					}
				});
		}

		private void OnDeleteRecordFile()
		{
			if (!RecordInfoList.Any())
				return;

			try
			{
				if (_selectedRecordings?.Count > 1)
				{
					foreach (var item in _selectedRecordings)
					{
						FileSystem.DeleteFile(item.FullPath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
					}
				}
				else
				{
					FileSystem.DeleteFile(SelectedRecordInfo.FullPath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
				}

				SelectedRecordInfo = null;
				_selectedRecordings = null;

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

			_recordManager.UpdateCustomData(_selectedRecordInfo, CustomCpuDescription, CustomGpuDescription, CustomRamDescription, CustomGameName, CustomComment);
			_recordManager.AddGameNameToMatchingList(_selectedRecordInfo.ProcessName, CustomGameName);
		}

		public void OnRecordSelectByDoubleClick()
		{
			if (SelectedRecordInfo != null && _selectSessionEvent != null)
			{
				var session = _recordManager.LoadData(SelectedRecordInfo.FullPath);
				_selectSessionEvent.Publish(new ViewMessages.SelectSession(session, SelectedRecordInfo));
			}
		}

		private void OnSelectedRecordInfoChanged()
		{
			if (_selectedRecordInfo is null) {
				ResetInfoEditBoxes();
			} else 
			{
				var session = _recordManager.LoadData(_selectedRecordInfo.FullPath);
				if (session is ISession)
				{
					if(_updateSessionEvent != null)
					{
						_updateSessionEvent.Publish(new ViewMessages.UpdateSession(session, SelectedRecordInfo));
					}
					CustomCpuDescription = string.Copy(SelectedRecordInfo.ProcessorName ?? string.Empty);
					CustomGpuDescription = string.Copy(SelectedRecordInfo.GraphicCardName ?? string.Empty);
					CustomRamDescription = string.Copy(SelectedRecordInfo.SystemRamInfo ?? string.Empty);
					CustomGameName = string.Copy(SelectedRecordInfo.GameName ?? string.Empty);
					CustomComment = string.Copy(SelectedRecordInfo.Comment ?? string.Empty);
				}
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
