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
using CapFrameX.PresentMonInterface;
using CapFrameX.Contracts.Data;

namespace CapFrameX.ViewModel
{
    public class ControlViewModel : BindableBase
    {
        private readonly IRecordDirectoryObserver _recordObserver;
        private readonly IEventAggregator _eventAggregator;
        private readonly IAppConfiguration _appConfiguration;
        private readonly IRecordDataProvider _recordDataProvider;

        private PubSubEvent<ViewMessages.UpdateSession> _updateSessionEvent;
        private PubSubEvent<ViewMessages.SelectSession> _selectSessionEvent;
        private PubSubEvent<ViewMessages.ShowOverlay> _showOverlayEvent;
        private PubSubEvent<ViewMessages.UpdateProcessIgnoreList> _updateProcessIgnoreListEvent;

        private IFileRecordInfo _selectedRecordInfo;
        private bool _hasValidSource;
        private string _customCpuDescription;
        private string _customGpuDescription;
        private string _customGameName;
        private string _customComment;
        private int _recordDataGridSelectedIndex;

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

        public ICommand OpenEditingDialogCommand { get; }

        public ICommand AddToIgnoreListCommand { get; }

        public ICommand DeleteRecordFileCommand { get; }

        public ICommand AcceptEditingDialogCommand { get; }

        public ICommand CancelEditingDialogCommand { get; }

        public ICommand AddCpuInfoCommand { get; }

        public ICommand AddGpuInfoCommand { get; }

		public ICommand DeleteRecordCommand { get; }

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
            OpenEditingDialogCommand = new DelegateCommand(OnOpenEditingDialog);
            AddToIgnoreListCommand = new DelegateCommand(OnAddToIgnoreList);
            DeleteRecordFileCommand = new DelegateCommand(OnDeleteRecordFile);
            AcceptEditingDialogCommand = new DelegateCommand(OnAcceptEditingDialog);
            CancelEditingDialogCommand = new DelegateCommand(OnCancelEditingDialog);
            AddCpuInfoCommand = new DelegateCommand(OnAddCpuInfo);
            AddGpuInfoCommand = new DelegateCommand(OnAddGpuInfo);
			DeleteRecordCommand = new DelegateCommand(OnPressDeleteKey);

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
			ResetInfoEditBoxes();

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
                CustomCpuDescription = string.Copy(SelectedRecordInfo.ProcessorName ?? "");
                CustomGpuDescription = string.Copy(SelectedRecordInfo.GraphicCardName ?? "");
                CustomGameName = string.Copy(SelectedRecordInfo.GameName ?? "");
                CustomComment = string.Copy(SelectedRecordInfo.Comment ?? "");
            }
            else
            {
				ResetInfoEditBoxes();
			}
        }

		private void ResetInfoEditBoxes()
		{
			CustomCpuDescription = "";
			CustomGpuDescription = "";
			CustomGameName = "";
			CustomComment = "";
		}

        private void OnAcceptEditingDialog()
        {
            if (CustomCpuDescription == null || CustomGpuDescription == null
                || CustomGameName == null || CustomComment == null || _selectedRecordInfo == null)
                return;

            // hint: _selectedRecordInfo must not be uptated, because after reload
            // it will be set to null
            RecordManager.UpdateCustomData(_selectedRecordInfo,
                CustomCpuDescription, CustomGpuDescription, CustomGameName, CustomComment);

            _recordDataProvider.AddGameNameToMatchingList(_selectedRecordInfo.ProcessName, CustomGameName);

            int selectedIndex = RecordInfoList.IndexOf(SelectedRecordInfo);
            ReloadRecordList();
            RecordDataGridSelectedIndex = selectedIndex;
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
                    CustomCpuDescription = string.Copy(SelectedRecordInfo.ProcessorName ?? "");
                    CustomGpuDescription = string.Copy(SelectedRecordInfo.GraphicCardName ?? "");
                    CustomGameName = string.Copy(SelectedRecordInfo.GameName ?? "");
                    CustomComment = string.Copy(SelectedRecordInfo.Comment ?? "");
                }
                else
                {
                    CustomCpuDescription = "";
                    CustomGpuDescription = "";
                    CustomGameName = "";
                    CustomComment = "";
                }

                _updateSessionEvent.Publish(new ViewMessages.UpdateSession(session, SelectedRecordInfo));
            }
        }

        private void OnAddCpuInfo()
        {
            CustomCpuDescription = PresentMonInterface.SystemInfo.GetProcessorName();
        }

        private void OnAddGpuInfo()
        {
            CustomGpuDescription = PresentMonInterface.SystemInfo.GetGraphicCardName();
        }

		private void OnPressDeleteKey()
			=> OnDeleteRecordFile();

		private void AddToRecordInfoList(IFileRecordInfo recordFileInfo, bool insertAtFirst = false)
        {
            if (recordFileInfo != null)
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (insertAtFirst)
                    {
                        RecordInfoList.Insert(0, recordFileInfo);
                    }
                    else
                        RecordInfoList.Add(recordFileInfo);
                }));
            }
        }

        private void OnRecordCreated(FileInfo fileInfo)
            => AddToRecordInfoList(_recordDataProvider.GetIFileRecordInfo(fileInfo), true);

        private void OnRecordDeleted(FileInfo fileInfo)
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
            _showOverlayEvent = _eventAggregator.GetEvent<PubSubEvent<ViewMessages.ShowOverlay>>();
            _updateProcessIgnoreListEvent = _eventAggregator.GetEvent<PubSubEvent<ViewMessages.UpdateProcessIgnoreList>>();
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
