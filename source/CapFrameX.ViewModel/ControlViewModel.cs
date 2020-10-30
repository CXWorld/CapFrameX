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
using CapFrameX.Contracts.Data;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.Reactive.Subjects;
using CapFrameX.Contracts.PresentMonInterface;
using Microsoft.VisualBasic.FileIO;
using Microsoft.WindowsAPICodePack.Dialogs;
using CapFrameX.Extensions;
using CapFrameX.Data.Session.Contracts;
using System.Reactive;
using CapFrameX.MVVM.Dialogs;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace CapFrameX.ViewModel
{
    public class ControlViewModel : BindableBase
    {
        private readonly IRecordDirectoryObserver _recordObserver;
        private readonly IEventAggregator _eventAggregator;
        private readonly IAppConfiguration _appConfiguration;
        private readonly IRecordManager _recordManager;
        private readonly ISystemInfo _systemInfo;
        private readonly ProcessList _processList;
        private readonly ILogger<ControlViewModel> _logger;
        private PubSubEvent<ViewMessages.UpdateSession> _updateSessionEvent;
        private PubSubEvent<ViewMessages.SelectSession> _selectSessionEvent;
        private PubSubEvent<ViewMessages.UpdateRecordInfos> _updateRecordInfosEvent;
        private IFileRecordInfo _selectedRecordInfo;
        private IList _selectedRecordInfos;
        private string _customCpuDescription;
        private string _customGpuDescription;
        private string _customRamDescription;
        private string _customGameName;
        private string _customComment;
        private int _recordDataGridSelectedIndex;
        private List<IFileRecordInfo> _selectedRecordings;
        public bool _directoryLoading;
        private CreateFolderDialog _createFolderDialogContent;
        private bool _createFolderDialogIsOpen;
        private string _treeViewSubFolderName = string.Empty;
        private bool _customCpuDescriptionChanged = false;
        private bool _customGpuDescriptionChanged = false;
        private bool _customRamDescriptionChanged = false;
        private bool _customGameNameChanged = false;
        private bool _customCommentChanged = false;

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

        public IList SelectedRecordInfos
        {
            get { return _selectedRecordInfos; }
            set
            {
                _selectedRecordInfos = value;
                RaisePropertyChanged();
            }
        }

        public bool HasValidSource { set; get; }

        public string CustomCpuDescription
        {
            get { return _customCpuDescription; }
            set
            {
                _customCpuDescription = value;
                _customCpuDescriptionChanged = true;
                RaisePropertyChanged();
            }
        }

        public string CustomGpuDescription
        {
            get { return _customGpuDescription; }
            set
            {
                _customGpuDescription = value;
                _customGpuDescriptionChanged = true;
                RaisePropertyChanged();
            }
        }

        public string CustomRamDescription
        {
            get { return _customRamDescription; }
            set
            {
                _customRamDescription = value;
                _customRamDescriptionChanged = true;
                RaisePropertyChanged();
            }
        }

        public string CustomGameName
        {
            get { return _customGameName; }
            set
            {
                _customGameName = value;
                _customGameNameChanged = true;
                RaisePropertyChanged();
            }
        }

        public string CustomComment
        {
            get { return _customComment; }
            set
            {
                _customComment = value;
                _customCommentChanged = true;
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

        public bool DirectoryLoading
        {
            get { return _directoryLoading; }
            set
            {
                _directoryLoading = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(DirectoryIsEmpty));
            }
        }

        public string RootDirectory
        {
            get { return _appConfiguration.CaptureRootDirectory; }
            set
            {
                _appConfiguration.CaptureRootDirectory = value;
                RaisePropertyChanged();
            }
        }

        public CreateFolderDialog CreateFolderDialogContent
        {
            get { return _createFolderDialogContent; }
            set
            {
                _createFolderDialogContent = value;
                RaisePropertyChanged();
            }
        }

        public bool CreateFolderDialogIsOpen
        {
            get { return _createFolderDialogIsOpen; }
            set
            {
                _createFolderDialogIsOpen = value;
                RaisePropertyChanged();
            }
        }

        public string TreeViewSubFolderName
        {
            get { return _treeViewSubFolderName; }
            set
            {
                _treeViewSubFolderName = value;
                RaisePropertyChanged();
            }
        }

        public string ObservedDirectory { get; private set; }

        public bool DirectoryIsEmpty => !DirectoryLoading && !RecordInfoList.Any();

        public ObservableCollection<IFileRecordInfo> RecordInfoList { get; }
            = new ObservableCollection<IFileRecordInfo>();

        public IAppConfiguration AppConfiguration => _appConfiguration;

        public IRecordDirectoryObserver RecordObserver => _recordObserver;

        public ICommand DeleteRecordFileCommand { get; }

        public ICommand MoveRecordFileCommand { get; }

        public ICommand DuplicateRecordFileCommand { get; }

        public ICommand AcceptEditingDialogCommand { get; }

        public ICommand CancelEditingDialogCommand { get; }

        public ICommand AddCpuInfoCommand { get; }

        public ICommand AddGpuInfoCommand { get; }

        public ICommand AddRamInfoCommand { get; }

        public ICommand DeleteRecordCommand { get; }

        public ICommand OpenObservedFolderCommand { get; }

        public ICommand DeleteFolderCommand { get; }

        public ICommand OpenCreateSubFolderDialogCommand { get; }

        public ICommand SelectedRecordingsCommand { get; }

        public ICommand CreateFolderCommand { get; }

        public ICommand CloseCreateFolderDialogCommand { get; }

        public ICommand ReloadRootFolderCommand { get; }

        public ISubject<string> TreeViewItemCreatedStream = new Subject<string>();

        public ISubject<string> TreeViewItemDeletedStream = new Subject<string>();

        public ISubject<Unit> TreeViewUpdateStream = new Subject<Unit>();

        public ISubject<bool> CreateFolderdialogIsOpenStream = new BehaviorSubject<bool>(false);

        public ControlViewModel(IRecordDirectoryObserver recordObserver,
                                IEventAggregator eventAggregator,
                                IAppConfiguration appConfiguration, RecordManager recordManager,
                                ISystemInfo systemInfo,
                                ProcessList processList,
                                ILogger<ControlViewModel> logger)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            _recordObserver = recordObserver;
            _eventAggregator = eventAggregator;
            _appConfiguration = appConfiguration;
            _recordManager = recordManager;
            _systemInfo = systemInfo;
            _processList = processList;
            _logger = logger;

            //Commands
            DeleteRecordFileCommand = new DelegateCommand(OnDeleteRecordFile);
            MoveRecordFileCommand = new DelegateCommand(OnMoveRecordFile);
            DuplicateRecordFileCommand = new DelegateCommand(OnDuplicateRecordFile);
            AcceptEditingDialogCommand = new DelegateCommand(OnAcceptEditingDialog);
            CancelEditingDialogCommand = new DelegateCommand(OnCancelEditingDialog);
            AddCpuInfoCommand = new DelegateCommand(OnAddCpuInfo);
            AddGpuInfoCommand = new DelegateCommand(OnAddGpuInfo);
            AddRamInfoCommand = new DelegateCommand(OnAddRamInfo);
            DeleteRecordCommand = new DelegateCommand(OnPressDeleteKey);
            OpenObservedFolderCommand = new DelegateCommand(OnOpenObservedFolder);
            DeleteFolderCommand = new DelegateCommand(OnDeleteFolder);
            OpenCreateSubFolderDialogCommand = new DelegateCommand(() =>
            {
                CreateFolderDialogIsOpen = true;
                TreeViewSubFolderName = string.Empty;
                CreateFolderdialogIsOpenStream.OnNext(true);
            });
            SelectedRecordingsCommand = new DelegateCommand<object>(OnSelectedRecordings);
            CreateFolderCommand = new DelegateCommand(OnCreateSubFolder);
            CloseCreateFolderDialogCommand = new DelegateCommand(() =>
            {
                CreateFolderDialogIsOpen = false;
                CreateFolderdialogIsOpenStream.OnNext(false);
            }
            );
            ReloadRootFolderCommand = new DelegateCommand(() => TreeViewUpdateStream.OnNext(default));

            RecordDataGridSelectedIndex = -1;

            CreateFolderDialogContent = new CreateFolderDialog();

            SetAggregatorEvents();
            SubscribeToCloudFolderChanged();
            SubscribeToCloudFolderSelected();
            SubscribeToResetRecord();
            SubscribeToSetFileRecordInfoExternal();

            RecordInfoList.CollectionChanged += (sender, args) =>
            {
                RaisePropertyChanged(nameof(DirectoryIsEmpty));
            };
            SetupObservers(SynchronizationContext.Current);

            stopwatch.Stop();
            _logger.LogInformation(GetType().Name + " {initializationTime}s initialization time",
                Math.Round(stopwatch.ElapsedMilliseconds * 1E-03, 1));
        }

        private void OnCreateSubFolder()
        {
            if (!_appConfiguration.ObservedDirectory.Any())
                return;

            try
            {
                var path = Path.Combine(_appConfiguration.ObservedDirectory, TreeViewSubFolderName);
                FileSystem.CreateDirectory(path);
                _appConfiguration.ObservedDirectory = path;
                TreeViewUpdateStream.OnNext(default);
                CreateFolderdialogIsOpenStream.OnNext(false);
                CreateFolderDialogIsOpen = false;
            }
            catch { }
        }

        private void OnDeleteFolder()
        {
            if (!_appConfiguration.ObservedDirectory.Any())
                return;

            try
            {
                var parentFolder = Directory.GetParent(_appConfiguration.ObservedDirectory);
                FileSystem.DeleteDirectory(_appConfiguration.ObservedDirectory, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);

                _updateSessionEvent.Publish(new ViewMessages.UpdateSession(null, null));
                _appConfiguration.ObservedDirectory = parentFolder.FullName;
                TreeViewUpdateStream.OnNext(default);
            }
            catch { }
        }

        public bool OnSelectRootFolder()
        {
            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true
            };

            CommonFileDialogResult result = dialog.ShowDialog();

            if (result == CommonFileDialogResult.Ok)
            {
                RootDirectory = dialog.FileName;
                _appConfiguration.ObservedDirectory = RootDirectory;
                return true;
            }
            return false;
        }
        private void OnOpenObservedFolder()
        {
            try
            {
                var path = _appConfiguration.ObservedDirectory;
                if (path.Contains(@"MyDocuments\CapFrameX\Captures"))
                {
                    var documentFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    path = Path.Combine(documentFolder, @"CapFrameX\Captures");
                }
                Process.Start(path);
            }
            catch { }
        }


        private void SetupObservers(SynchronizationContext context)
        {
            IObservable<IFileRecordInfo> GetFileRecordInfo(FileInfo fi) =>
                Observable.FromAsync(() => _recordManager.GetFileRecordInfo(fi))
                    .Catch<IFileRecordInfo, Exception>(e => Observable.Return<IFileRecordInfo>(null));

            _recordObserver.ObservingDirectoryStream
                .ObserveOn(context)
                .Subscribe(directory =>
                {
                    HasValidSource = directory?.Exists ?? false;
                    RaisePropertyChanged(nameof(HasValidSource));
                });


            _recordObserver.DirectoryFilesStream
                .DistinctUntilChanged()
                .Do(_ =>
                {
                    RecordInfoList.Clear();
                    DirectoryLoading = true;
                    RaisePropertyChanged(nameof(DirectoryLoading));
                })
                .Select(fileInfos =>
                {
                    return Observable.Merge(fileInfos.Select(GetFileRecordInfo), 30)
                        .Where(recordFileInfo => recordFileInfo is IFileRecordInfo)
                        .Distinct(recordFileInfo => recordFileInfo.Hash)
                        .ToArray();
                }
                ).Switch()
                .ObserveOn(context)
                .Subscribe(recordFileInfos =>
                {
                    RecordInfoList.Clear();
                    DirectoryLoading = false;
                    RaisePropertyChanged(nameof(DirectoryLoading));
                    RecordInfoList.AddRange(recordFileInfos);
                });

            _recordObserver.FileCreatedStream
                .SelectMany(GetFileRecordInfo)
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
                .SelectMany(GetFileRecordInfo)
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

        private void OnMoveRecordFile()
        {
            if (!RecordInfoList.Any())
                return;

            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true
            };

            CommonFileDialogResult result = dialog.ShowDialog();

            if (result == CommonFileDialogResult.Ok)
            {
                string destinationfolder = dialog.FileName;
                try
                {

                    if (_selectedRecordings?.Count > 1)
                    {
                        foreach (var item in _selectedRecordings)
                        {
                            string destinationFullPath = Path.Combine(destinationfolder, item.FileInfo.Name);
                            FileSystem.MoveFile(item.FullPath, destinationFullPath);
                        }
                    }
                    else
                    {
                        string destinationFullPath = Path.Combine(destinationfolder, SelectedRecordInfo.FileInfo.Name);
                        FileSystem.MoveFile(SelectedRecordInfo.FullPath, destinationFullPath);
                    }

                    SelectedRecordInfo = null;
                    _selectedRecordings = null;

                    _updateSessionEvent.Publish(new ViewMessages.UpdateSession(null, null));

                }
                catch { }
            }
            TreeViewUpdateStream.OnNext(default);
        }

        private void OnDuplicateRecordFile()
        {
            if (!RecordInfoList.Any())
                return;

            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true
            };

            CommonFileDialogResult result = dialog.ShowDialog();

            if (result == CommonFileDialogResult.Ok)
            {
                string destinationfolder = dialog.FileName;
                try
                {

                    if (_selectedRecordings?.Count > 1)
                    {
                        foreach (var item in _selectedRecordings)
                        {
                            string destinationFullPath = Path.Combine(destinationfolder, item.FileInfo.Name);
                            FileSystem.CopyFile(item.FullPath, destinationFullPath);
                        }
                    }
                    else
                    {
                        string destinationFullPath = Path.Combine(destinationfolder, SelectedRecordInfo.FileInfo.Name);
                        FileSystem.CopyFile(SelectedRecordInfo.FullPath, destinationFullPath);
                    }

                    SelectedRecordInfo = null;
                    _selectedRecordings = null;

                    _updateSessionEvent.Publish(new ViewMessages.UpdateSession(null, null));

                }
                catch { }
            }
            TreeViewUpdateStream.OnNext(default);
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

        private void ResetDescriptionChangedFlags()
        {
            _customCpuDescriptionChanged = false;
            _customGpuDescriptionChanged = false;
            _customRamDescriptionChanged = false;
            _customGameNameChanged = false;
            _customCommentChanged = false;
        }

        private void OnAcceptEditingDialog() => SaveDescriptions();

        public void SaveDescriptions()
        {
            if (!ObjectExtensions.IsAllNotNull(CustomCpuDescription,
                CustomGpuDescription, CustomRamDescription, CustomGameName,
                CustomComment, _selectedRecordInfo, _selectedRecordInfos))
                return;

            if (_selectedRecordInfos.Count == 1)
            {
                _recordManager.UpdateCustomData(_selectedRecordInfo, CustomCpuDescription,
                    CustomGpuDescription, CustomRamDescription, CustomGameName, CustomComment);
            }
            else if (_selectedRecordInfos.Count > 1)
            {
                foreach (var recordInfoObject in _selectedRecordInfos)
                {
                    var recordInfo = recordInfoObject as IFileRecordInfo;
                    var session = _recordManager.LoadData(recordInfo.FullPath);

                    _recordManager.UpdateCustomData(recordInfo,
                            _customCpuDescriptionChanged ? CustomCpuDescription : session.Info.Processor,
                            _customGpuDescriptionChanged ? CustomGpuDescription : session.Info.GPU,
                            _customRamDescriptionChanged ? CustomRamDescription : session.Info.SystemRam,
                            _customGameNameChanged ? CustomGameName : session.Info.GameName,
                            _customCommentChanged ? CustomComment : session.Info.Comment);
                }
            }

            AddOrUpdateProcess(_selectedRecordInfo.ProcessName, CustomGameName);
            ResetDescriptionChangedFlags();
        }

        private void AddOrUpdateProcess(string processName, string gameName)
        {
            if (string.IsNullOrWhiteSpace(processName) || string.IsNullOrWhiteSpace(gameName)
                || (processName.Replace(".exe", string.Empty) == gameName))
            {
                return;
            }

            try
            {
                var process = _processList.FindProcessByName(processName);
                if (process is null)
                {
                    _processList.AddEntry(processName, gameName);
                    process = _processList.FindProcessByName(processName);
                }
                else
                {
                    process.UpdateDisplayName(gameName);
                }
                _processList.Save();
                RecordInfoList.Where(record => record.ProcessName == processName).ForEach(record =>
                {
                    record.GameName = process.DisplayName;
                    ((FileRecordInfo)record).NotifyPropertyChanged(nameof(record.GameName));
                });

                RaisePropertyChanged(nameof(RecordInfoList));
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error updating ProcessList");
            }
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
            if (_selectedRecordInfo is null)
            {
                ResetInfoEditBoxes();
            }
            else
            {
                var session = _recordManager.LoadData(_selectedRecordInfo.FullPath);
                if (session is ISession)
                {
                    if (_updateSessionEvent != null)
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

            ResetDescriptionChangedFlags();
        }

        private void OnAddCpuInfo()
        {
            CustomCpuDescription = _systemInfo.GetProcessorName();
        }

        private void OnAddGpuInfo()
        {
            CustomGpuDescription = _systemInfo.GetGraphicCardName();
        }

        private void OnAddRamInfo()
        {
            CustomRamDescription = _systemInfo.GetSystemRAMInfoName();
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

        private void SubscribeToCloudFolderChanged()
        {
            _eventAggregator.GetEvent<PubSubEvent<AppMessages.CloudFolderChanged>>()
                .Subscribe(msg =>
                {
                    TreeViewUpdateStream.OnNext(default);
                });
        }

        private void SubscribeToCloudFolderSelected()
        {
            _eventAggregator.GetEvent<PubSubEvent<AppMessages.SelectCloudFolder>>()
                .Subscribe(msg =>
                {
                    TreeViewUpdateStream.OnNext(default);
                });
        }
    }
}
