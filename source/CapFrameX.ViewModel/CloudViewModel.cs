using CapFrameX.Contracts.Cloud;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Data;
using CapFrameX.Data.Session.Classes;
using CapFrameX.Data.Session.Contracts;
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
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
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
		private readonly IRecordManager _recordManager;
		private readonly IEventAggregator _eventAggregator;
		private readonly IAppConfiguration _appConfiguration;
		private readonly ILogger<CloudViewModel> _logger;
		private readonly IAppVersionProvider _appVersionProvider;
		private readonly LoginManager _loginManager;
		private bool _useUpdateSession;
		private int _selectedCloudEntryIndex = -1;
		private bool _showHelpText = true;
		private bool _enableClearAndUploadButton;
		private List<IFileRecordInfo> _fileRecordInfoList = new List<IFileRecordInfo>();
		private string _downloadIdString;
		private string _shareUrl;

		public string ShareUrl
		{
			get
			{
				return _shareUrl;
			}
			set
			{
				_shareUrl = value;
				RaisePropertyChanged();
				RaisePropertyChanged(nameof(ShareUrlVisible));
			}
		}

		public bool ShareUrlVisible
		{
			get
			{
				return !string.IsNullOrWhiteSpace(ShareUrl);
			}
		}

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
				}
				else
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

		public CloudViewModel(IStatisticProvider statisticProvider, IRecordManager recordManager,
			IEventAggregator eventAggregator, IAppConfiguration appConfiguration, ILogger<CloudViewModel> logger, IAppVersionProvider appVersionProvider, LoginManager loginManager)
		{
			_statisticProvider = statisticProvider;
			_recordManager = recordManager;
			_eventAggregator = eventAggregator;
			_appConfiguration = appConfiguration;
			_logger = logger;
			_appVersionProvider = appVersionProvider;
			_loginManager = loginManager;
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

		private void AddCloudEntry(IFileRecordInfo recordInfo, ISession session)
		{
			if (recordInfo != null)
			{
				_fileRecordInfoList.Add(recordInfo);
			}
			else
				return;

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

			var sessions = new List<ISession>();
			foreach (var entry in CloudEntries)
			{
				sessions.Add(_recordManager.LoadData(entry.FileRecordInfo.FullPath));
			}

			var contentAsJson = JsonConvert.SerializeObject(sessions, new JsonSerializerSettings()
			{
				TypeNameHandling = TypeNameHandling.Auto
			});
			using (var client = new HttpClient() {
				BaseAddress = new Uri(ConfigurationManager.AppSettings["WebserviceUri"])
			})
			{
				if (_loginManager.State?.Token != null)
				{
					try
					{
						await _loginManager.RefreshTokenIfNeeded();
						if (_loginManager.State.IsSigned)
						{
							client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _loginManager.State.Token.AccessToken);
						}
					} catch(Exception e)
					{
						_logger.LogWarning(e, "Something went wrong while Refreshing the Accesstoken. Using Guest Mode");
					}
				}
				var content = new StringContent(contentAsJson);
				content.Headers.ContentType.MediaType = "application/json";
				var response = await client.PostAsync(@"SessionCollections", content);

				if (response.IsSuccessStatusCode)
				{
					ShareUrl = response.Headers.Location.ToString();
					_logger.LogInformation("Successfully uploaded Captures. ShareUrl is {shareUrl}", response.Headers.Location);
				}
				else
				{
					var responseBody = await response.Content.ReadAsStringAsync();
					_logger.LogError("Upload of Captures failed. {error}", responseBody);
				}
			}
		}

		private async Task DownloadCaptureCollection(string id)
		{
			var url = $@"SessionCollections/{id}";
			using (var client = new HttpClient() { 
				BaseAddress = new Uri(ConfigurationManager.AppSettings["WebserviceUri"])
			})
			{
				var response = await client.GetAsync(url);

				if (response.IsSuccessStatusCode)
				{
					var content = JsonConvert.DeserializeObject<SessionCollectionDTO>(await response.Content.ReadAsStringAsync(), new JsonSerializerSettings()
					{
						TypeNameHandling = TypeNameHandling.Auto
					});

					var downloadDirectory = _appConfiguration.CloudDownloadDirectory;

					if (downloadDirectory.Contains(@"MyDocuments\CapFrameX\Captures\Cloud"))
					{
						downloadDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"CapFrameX\Captures\Cloud");
					}
					if (!Directory.Exists(downloadDirectory))
					{
						Directory.CreateDirectory(downloadDirectory);
					}
					foreach (var session in content.Sessions)
					{
						var fileInfo = new FileInfo(Path.Combine(downloadDirectory, $"{session.Hash}.json"));
						File.WriteAllText(fileInfo.FullName, JsonConvert.SerializeObject(session, new JsonSerializerSettings()
						{
							TypeNameHandling = TypeNameHandling.Auto
						}));
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