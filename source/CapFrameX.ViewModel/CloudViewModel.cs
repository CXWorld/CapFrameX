using CapFrameX.Contracts.Cloud;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Data;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Extensions;
using CapFrameX.Statistics;
using CapFrameX.Webservice.Data.DTO;
using GongSolutions.Wpf.DragDrop;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive.Subjects;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace CapFrameX.ViewModel
{
	public class CloudViewModel : BindableBase, INavigationAware, IDropTarget
	{
		private readonly IStatisticProvider _statisticProvider;
		private readonly IRecordDataProvider _recordDataProvider;
		private readonly IEventAggregator _eventAggregator;
		private readonly IAppConfiguration _appConfiguration;
		private readonly ILogger<CloudViewModel> _logger;
		private readonly IAppVersionProvider _appVersionProvider;

		private bool _useUpdateSession;
		private int _selectedCloudEntryIndex = -1;
		private bool _showHelpText = true;
		private bool _enableClearAndUploadButton;
		private List<IFileRecordInfo> _fileRecordInfoList = new List<IFileRecordInfo>();
		private string _downloadIdString;

		public int SelectedCloudEntryIndex
		{
			get
			{ return _selectedCloudEntryIndex; }
			set
			{
				_selectedCloudEntryIndex = value;
				RaisePropertyChanged();
			}
		}

		public bool ShowHelpText
		{
			get
			{ return _showHelpText; }
			set
			{
				_showHelpText = value;
				RaisePropertyChanged();
			}
		}

		public bool EnableClearAndUploadButton
		{
			get
			{ return _enableClearAndUploadButton; }
			set
			{
				_enableClearAndUploadButton = value;
				RaisePropertyChanged();
			}
		}
		public bool EnableDownloadButton { get; set; }
		
		public string CloudDownloadDirectory
		{
			get { return _appConfiguration.CloudDownloadDirectory; }
			set
			{
				_appConfiguration.CloudDownloadDirectory = value;
				RaisePropertyChanged();
			}
		}

		public string DownloadIDString
		{
			get
			{
				return _downloadIdString;
			}
			set
			{
				var regex = new Regex(@"(\{){0,1}[0-9a-fA-F]{8}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{12}(\}){0,1}");
				var match = regex.Match(value);
				if (match.Success)
				{
					_downloadIdString = match.Value;
				} else
				{
					_downloadIdString = value;
				}
				EnableDownloadButton = match.Success;
				RaisePropertyChanged(nameof(EnableDownloadButton));
			}
		}


		public ICommand ClearTableCommand { get; }

		public ICommand SelectDownloadFolderCommand { get; }

		public ICommand UploadRecordsCommand { get; }
		public ICommand DownloadRecordsCommand { get; }

		public ObservableCollection<ICloudEntry> CloudEntries { get; private set; }
			= new ObservableCollection<ICloudEntry>();

		public CloudViewModel(IStatisticProvider statisticProvider, IRecordDataProvider recordDataProvider,
			IEventAggregator eventAggregator, IAppConfiguration appConfiguration, ILogger<CloudViewModel> logger, IAppVersionProvider appVersionProvider)
		{
			_statisticProvider = statisticProvider;
			_recordDataProvider = recordDataProvider;
			_eventAggregator = eventAggregator;
			_appConfiguration = appConfiguration;
			_logger = logger;
			_appVersionProvider = appVersionProvider;
			ClearTableCommand = new DelegateCommand(OnClearTable);
			SelectDownloadFolderCommand = new DelegateCommand(OnSelectDownloadFolder);
			UploadRecordsCommand = new DelegateCommand(async () =>
			{
				await UploadRecords();
				OnClearTable();
			});

			DownloadRecordsCommand = new DelegateCommand(async () =>
			{
				await DownloadCaptureCollection(DownloadIDString);
			});


			SubscribeToUpdateSession();

			CloudEntries.CollectionChanged += new NotifyCollectionChangedEventHandler
				((sender, eventArg) => OnCloudEntriesChanged());
		}

		public void RemoveCloudEntry(ICloudEntry entry)
		{
			_fileRecordInfoList.Remove(entry.FileRecordInfo);
			CloudEntries.Remove(entry);
		}

		private void OnCloudEntriesChanged()
		{
			ShowHelpText = !CloudEntries.Any();
			EnableClearAndUploadButton = CloudEntries.Any();
		}

		private void SubscribeToUpdateSession()
		{
			_eventAggregator.GetEvent<PubSubEvent<ViewMessages.SelectSession>>()
							.Subscribe(msg =>
							{
								if (_useUpdateSession)
								{
									AddCloudEntry(msg.RecordInfo, msg.CurrentSession);
								}
							});
		}

		private void AddCloudEntry(IFileRecordInfo recordInfo, Session session)
		{
			if (recordInfo != null)
			{
				_fileRecordInfoList.Add(recordInfo);
			}
			else
				return;


			List<double> frametimes = session?.FrameTimes;

			if (session == null)
			{
				var localSession = RecordManager.LoadData(recordInfo.FullPath);
				frametimes = localSession?.FrameTimes;
			}

			CloudEntries.Add(new CloudEntry()
			{
				GameName = recordInfo.GameName,
				CreationDate = recordInfo.CreationDate,
				CreationTime = recordInfo.CreationTime,
				Comment = recordInfo.Comment,
				FileRecordInfo = recordInfo
			});
		}

		private void OnClearTable()
		{
			CloudEntries.Clear();
			_fileRecordInfoList.Clear();
		}

		private void OnSelectDownloadFolder()
		{
			var dialog = new CommonOpenFileDialog
			{
				IsFolderPicker = true
			};

			CommonFileDialogResult result = dialog.ShowDialog();

			if (result == CommonFileDialogResult.Ok)
			{
				_appConfiguration.CloudDownloadDirectory = dialog.FileName;
				CloudDownloadDirectory = dialog.FileName;
			}
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
					if (frameworkElement.Name == "CloudItemDataGrid"
						|| frameworkElement.Name == "DragAndDropInfoTextTextBlock")
					{
						if (dropInfo.Data is IFileRecordInfo recordInfo)
						{
							AddCloudEntry(recordInfo, null);
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

		private async Task UploadRecords()
		{

			var requestContent = new MultipartFormDataContent();

			requestContent.Add(new StringContent(_appVersionProvider.GetAppVersion().ToString()), "appVersion");
			foreach (var file in CloudEntries)
			{
				var fileInfo = file.FileRecordInfo.FileInfo;
				var imageContent = new ByteArrayContent(File.ReadAllBytes(fileInfo.FullName));
				requestContent.Add(imageContent, "capture", fileInfo.Name);
			}
			using (var client = new HttpClient())
			{
				var response = await client.PostAsync(@"https://capframex.com/api/capturecollections", requestContent);

				if (response.IsSuccessStatusCode)
				{
					_logger.LogInformation("Successfully uploaded Captures. ShareUrl is {shareUrl}", response.Headers.Location);
				}
				else
				{
					var content = await response.Content.ReadAsStringAsync();
					_logger.LogError("Upload of Captures failed. {error}", content);
				}
			}
		}

		private async Task DownloadCaptureCollection(string id)
		{
			var url = $@"https://capframex.com/api/capturecollections/{id}";
			using (var client = new HttpClient())
			{
				var response = await client.GetAsync(url);

				if (response.IsSuccessStatusCode)
				{
					var content = JsonConvert.DeserializeObject<Webservice.Data.DTO.CaptureCollection>(await response.Content.ReadAsStringAsync());

					var downloadDirectory = _appConfiguration.CloudDownloadDirectory;

					if (downloadDirectory.Contains(@"MyDocuments\CapFrameX\Captures\Cloud"))
					{
						downloadDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"CapFrameX\Captures\Cloud");
					}
					if (!Directory.Exists(downloadDirectory))
					{
						Directory.CreateDirectory(downloadDirectory);
					}
					foreach (var capture in content.Captures)
					{
						var fileInfo = new FileInfo(Path.Combine(downloadDirectory, capture.Name));
						File.WriteAllBytes(fileInfo.FullName, capture.BlobBytes);
					}
				}
				else
				{
					var content = await response.Content.ReadAsStringAsync();
					_logger.LogError("Download of CaptureCollection failed. {error}", content);
				}
			}
		}
	}
}