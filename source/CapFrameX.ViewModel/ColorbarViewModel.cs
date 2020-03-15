﻿using CapFrameX.Contracts.Configuration;
using CapFrameX.Data;
using CapFrameX.Extensions;
using CapFrameX.PresentMonInterface;
using Microsoft.WindowsAPICodePack.Dialogs;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Input;
using CapFrameX.Contracts.MVVM;
using Microsoft.Extensions.Logging;
using CapFrameX.Contracts.Data;
using CapFrameX.EventAggregation.Messages;

namespace CapFrameX.ViewModel
{
	public class ColorbarViewModel : BindableBase
	{
		private readonly IRegionManager _regionManager;
		private readonly IEventAggregator _eventAggregator;
		private readonly IAppConfiguration _appConfiguration;
		private readonly ILogger<ColorbarViewModel> _logger;
		private readonly IShell _shell;
		private readonly LoginManager _loginManager;
		private PubSubEvent<AppMessages.UpdateObservedDirectory> _updateObservedFolder;
		private PubSubEvent<AppMessages.OpenLoginWindow> _openLoginWindow;
		private PubSubEvent<AppMessages.LoginState> _logout;

		private bool _captureIsChecked = true;
		private bool _overlayIsChecked;
		private bool _singleRecordIsChecked;
		private bool _recordComparisonIsChecked;
		private bool _reportIsChecked;
		private int _selectWindowSize;
		private double _stutteringFactor;
		private bool _synchronizationIsChecked;
		private bool _cloudIsChecked;
		private int _fpsValuesRoundingDigits;
		private bool _aggregatioIsChecked;
		private string _screenshotDirectory;
		private bool _hasCustomInfo;
		private string _selectedView = "Options";
		private bool _optionsViewSelected = true;
		private bool _helpViewSelected;

		public bool CaptureIsChecked
		{
			get { return _captureIsChecked; }
			set
			{
				_captureIsChecked = value;
				RaisePropertyChanged();

				if (value)
					OnCaptureIsCheckedChanged();
			}
		}

		public bool OverlayIsChecked
		{
			get { return _overlayIsChecked; }
			set
			{
				_overlayIsChecked = value;
				RaisePropertyChanged();

				if (value)
					OnOverlayIsCheckedChanged();
			}
		}

		public bool SingleRecordIsChecked
		{
			get { return _singleRecordIsChecked; }
			set
			{
				_singleRecordIsChecked = value;
				RaisePropertyChanged();

				if (value)
					OnSingleRecordIsCheckedChanged();
			}
		}
		public bool AggregationIsChecked
		{
			get { return _aggregatioIsChecked; }
			set
			{
				_aggregatioIsChecked = value;
				RaisePropertyChanged();

				if (value)
					OnAggregationIsCheckedChanged();
			}
		}

		public bool RecordComparisonIsChecked
		{
			get { return _recordComparisonIsChecked; }
			set
			{
				_recordComparisonIsChecked = value;
				RaisePropertyChanged();

				if (value)
					OnRecordComparisonIsCheckedChanged();
			}
		}

		public bool ReportIsChecked
		{
			get { return _reportIsChecked; }
			set
			{
				_reportIsChecked = value;
				RaisePropertyChanged();

				if (value)
					OnReportIsCheckedChanged();
			}
		}

		public bool SynchronizationIsChecked
		{
			get { return _synchronizationIsChecked; }
			set
			{
				_synchronizationIsChecked = value;
				RaisePropertyChanged();

				if (value)
					OnSynchronizationIsCheckedChanged();
			}
		}
		public bool CloudIsChecked
		{
			get { return _cloudIsChecked; }
			set
			{
				_cloudIsChecked = value;
				RaisePropertyChanged();

				if (value)
					OnCloudIsCheckedChanged();
			}
		}


		public int SelectWindowSize
		{
			get { return _selectWindowSize; }
			set
			{
				_selectWindowSize = value;
				_appConfiguration.MovingAverageWindowSize = value;
				RaisePropertyChanged();
			}
		}

		public double StutteringFactor
		{
			get { return _stutteringFactor; }
			set
			{
				_stutteringFactor = value;
				_appConfiguration.StutteringFactor = value;
				RaisePropertyChanged();
			}
		}

		public int FpsValuesRoundingDigits
		{
			get { return _fpsValuesRoundingDigits; }
			set
			{
				_fpsValuesRoundingDigits = value;
				_appConfiguration.FpsValuesRoundingDigits = value;
				RaisePropertyChanged();
			}
		}

		public string ScreenshotDirectory
		{
			get { return _screenshotDirectory; }
			set
			{
				_screenshotDirectory = value;
				RaisePropertyChanged();
			}
		}

		public EHardwareInfoSource SelectedHardwareInfoSource
		{
			get { return _appConfiguration.HardwareInfoSource.ConvertToEnum<EHardwareInfoSource>(); }
			set
			{
				_appConfiguration.HardwareInfoSource = value.ConvertToString();
				OnHardwareInfoSourceChanged();
				RaisePropertyChanged();
			}
		}

		public bool HasCustomInfo
		{
			get { return _hasCustomInfo; }
			set
			{
				_hasCustomInfo = value;
				RaisePropertyChanged();
			}
		}

		public string CustomCpuDescription
		{
			get { return _appConfiguration.CustomCpuDescription; }
			set
			{
				_appConfiguration.CustomCpuDescription = value;
				RaisePropertyChanged();
			}
		}

		public string CustomGpuDescription
		{
			get { return _appConfiguration.CustomGpuDescription; }
			set
			{
				_appConfiguration.CustomGpuDescription = value;
				RaisePropertyChanged();
			}
		}

		public string CustomRamDescription
		{
			get { return _appConfiguration.CustomRamDescription; }
			set
			{
				_appConfiguration.CustomRamDescription = value;
				RaisePropertyChanged();
			}
		}

		public string SelectedView
		{
			get { return _selectedView; }
			set
			{
				_selectedView = value;
				RaisePropertyChanged();
			}
		}

		public bool OptionsViewSelected
		{
			get { return _optionsViewSelected; }
			set
			{
				_optionsViewSelected = value;
				OnViewSelectionChanged();
				RaisePropertyChanged();
			}
		}

		public bool HelpViewSelected
		{
			get { return _helpViewSelected; }
			set
			{
				_helpViewSelected = value;
				OnViewSelectionChanged();
				RaisePropertyChanged();
			}
		}

		public string HelpText => File.ReadAllText(@"HelpTexts\ChartControls.rtf");

		public bool IsCompatibleWithRunningOS => CaptureServiceInfo.IsCompatibleWithRunningOS;

		public Array HardwareInfoSourceItems => Enum.GetValues(typeof(EHardwareInfoSource))
										   .Cast<EHardwareInfoSource>()
										   .ToArray();

		public	IAppConfiguration AppConfiguration => _appConfiguration;

		public ILogger<ColorbarViewModel> Logger => _logger;

		public IShell Shell => _shell;

		public ICommand SelectScreenshotFolderCommand { get; }

		public ICommand OpenScreenshotFolderCommand { get; }

		public IList<int> WindowSizes { get; }

		public IList<int> RoundingDigits { get; }

		public bool IsLoggedIn { get; private set; }

		public ColorbarViewModel(IRegionManager regionManager,
								 IEventAggregator eventAggregator,
								 IAppConfiguration appConfiguration,
								 ILogger<ColorbarViewModel> logger,
								 IShell shell,
								 LoginManager loginManager)
		{
			_regionManager = regionManager;
			_eventAggregator = eventAggregator;
			_appConfiguration = appConfiguration;
			_logger = logger;
			_shell = shell;
			_loginManager = loginManager;
			StutteringFactor = _appConfiguration.StutteringFactor;
			SelectWindowSize = _appConfiguration.MovingAverageWindowSize;
			FpsValuesRoundingDigits = _appConfiguration.FpsValuesRoundingDigits;
			ScreenshotDirectory = _appConfiguration.ScreenshotDirectory;
			WindowSizes = new List<int>(Enumerable.Range(4, 100 - 4));
			RoundingDigits = new List<int>(Enumerable.Range(0, 8));
			SelectScreenshotFolderCommand = new DelegateCommand(OnSelectScreenshotFolder);
			OpenScreenshotFolderCommand = new DelegateCommand(OnOpenScreenshotFolder);

			HasCustomInfo = SelectedHardwareInfoSource == EHardwareInfoSource.Custom;
			SetAggregatorEvents();
			SetHardwareInfoDefaultsFromDatabase();
		}

		public void OpenLoginWindow()
		{
			_openLoginWindow.Publish(new AppMessages.OpenLoginWindow());
		}

		public async void Logout()
		{
			await _loginManager.Logout();
		}

		private void SetHardwareInfoDefaultsFromDatabase()
		{
			if (CustomCpuDescription == "CPU")
				CustomCpuDescription = SystemInfo.GetProcessorName();

			if (CustomGpuDescription == "GPU")
				CustomGpuDescription = SystemInfo.GetGraphicCardName();

			if (CustomRamDescription == "RAM")
				CustomRamDescription = SystemInfo.GetSystemRAMInfoName();
		}

		private void OnSelectScreenshotFolder()
		{
			var dialog = new CommonOpenFileDialog
			{
				IsFolderPicker = true
			};

			CommonFileDialogResult result = dialog.ShowDialog();

			if (result == CommonFileDialogResult.Ok)
			{
				_appConfiguration.ScreenshotDirectory = dialog.FileName;
				ScreenshotDirectory = dialog.FileName;
			}
		}

		private void OnOpenScreenshotFolder()
		{
			try
			{
				var path = _appConfiguration.ScreenshotDirectory;
				if (path.Contains(@"MyDocuments\CapFrameX\Screenshots"))
				{
					var documentFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
					path = Path.Combine(documentFolder, @"CapFrameX\Screenshots");
				}
				Process.Start(path);
			}
			catch { }
		}

		private void OnCaptureIsCheckedChanged()
		{
			_regionManager.RequestNavigate("DataRegion", "CaptureView");
		}

		private void OnOverlayIsCheckedChanged()
		{
			_regionManager.RequestNavigate("DataRegion", "OverlayView");
		}

		private void OnSingleRecordIsCheckedChanged()
		{
			_regionManager.RequestNavigate("DataRegion", "DataView");
		}

		private void OnAggregationIsCheckedChanged()
		{
			_regionManager.RequestNavigate("DataRegion", "AggregationView");
		}

		private void OnRecordComparisonIsCheckedChanged()
		{
			_regionManager.RequestNavigate("DataRegion", "ComparisonView");
		}

		private void OnReportIsCheckedChanged()
		{
			_regionManager.RequestNavigate("DataRegion", "ReportView");
		}

		private void OnSynchronizationIsCheckedChanged()
		{
			_regionManager.RequestNavigate("DataRegion", "SynchronizationView");
		}

		private void OnCloudIsCheckedChanged()
		{
			_regionManager.RequestNavigate("DataRegion", "CloudView");
		}

		private void OnHardwareInfoSourceChanged()
		{
			// mange visibility
			HasCustomInfo = SelectedHardwareInfoSource == EHardwareInfoSource.Custom;

			// manage defaults
			SetHardwareInfoDefaultsFromDatabase();
		}

		private void OnViewSelectionChanged()
		{
			if (OptionsViewSelected)
				SelectedView = "Options";

			if (HelpViewSelected)
				SelectedView = "Help";
		}

		private void SetAggregatorEvents()
		{
			_updateObservedFolder = _eventAggregator.GetEvent<PubSubEvent<AppMessages.UpdateObservedDirectory>>();
			_openLoginWindow = _eventAggregator.GetEvent<PubSubEvent<AppMessages.OpenLoginWindow>>();
			_logout = _eventAggregator.GetEvent<PubSubEvent<AppMessages.LoginState>>();
			_eventAggregator.GetEvent<PubSubEvent<AppMessages.LoginState>>().Subscribe(state => {
				IsLoggedIn = state.IsLoggedIn;
				RaisePropertyChanged(nameof(IsLoggedIn));
			});
		}
	}
}
