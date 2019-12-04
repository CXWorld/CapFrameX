using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.OcatInterface;
using CapFrameX.Data;
using CapFrameX.Extensions;
using CapFrameX.EventAggregation.Messages;
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

namespace CapFrameX.ViewModel
{
	public class ColorbarViewModel : BindableBase
	{
		private readonly IRegionManager _regionManager;
		private readonly IRecordDirectoryObserver _recordDirectoryObserver;
		private readonly IEventAggregator _eventAggregator;
		private readonly IAppConfiguration _appConfiguration;
		private readonly IShell _shell;

		private PubSubEvent<AppMessages.UpdateObservedDirectory> _updateObservedFolder;
		private bool _captureIsChecked = true;
		private bool _singleRecordIsChecked;
		private bool _recordComparisonIsChecked;
		private bool _reportIsChecked;
		private int _selectWindowSize;
		private double _stutteringFactor;
		private string _observedDirectory;
		private bool _synchronizationIsChecked;
		private int _fpsValuesRoundingDigits;
		private bool _aggregatioIsChecked;
		private string _screenshotDirectory;

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

		public string ObservedDirectory
		{
			get { return _observedDirectory; }
			set
			{
				_observedDirectory = value;
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
			get { return _appConfiguration.HardwareInfoSource.ConverToEnum<EHardwareInfoSource>(); }
			set
			{
				_appConfiguration.HardwareInfoSource = value.ConvertToString();
				OnHardwareInfoSourceChanged();
				RaisePropertyChanged();
			}
		}

		public bool IsCompatibleWithRunningOS => CaptureServiceInfo.IsCompatibleWithRunningOS;

		public Array HardwareInfoSourceItems => Enum.GetValues(typeof(EHardwareInfoSource))
										   .Cast<EHardwareInfoSource>()
										   .ToArray();

		public IShell Shell => _shell;

		public ICommand SelectObservedFolderCommand { get; }

		public ICommand SelectScreenshotFolderCommand { get; }

		public ICommand OpenObservedFolderCommand { get; }

		public ICommand OpenScreenshotFolderCommand { get; }

		public IList<int> WindowSizes { get; }

		public IList<int> RoundingDigits { get; }

		public ColorbarViewModel(IRegionManager regionManager,
								 IRecordDirectoryObserver recordDirectoryObserver,
								 IEventAggregator eventAggregator,
								 IAppConfiguration appConfiguration,
								 IShell shell)
		{
			_regionManager = regionManager;
			_recordDirectoryObserver = recordDirectoryObserver;
			_eventAggregator = eventAggregator;
			_appConfiguration = appConfiguration;
			_shell = shell;

			StutteringFactor = _appConfiguration.StutteringFactor;
			SelectWindowSize = _appConfiguration.MovingAverageWindowSize;
			FpsValuesRoundingDigits = _appConfiguration.FpsValuesRoundingDigits;
			ObservedDirectory = _appConfiguration.ObservedDirectory;
			ScreenshotDirectory = _appConfiguration.ScreenshotDirectory;
			WindowSizes = new List<int>(Enumerable.Range(4, 100 - 4));
			RoundingDigits = new List<int>(Enumerable.Range(0, 8));
			SelectObservedFolderCommand = new DelegateCommand(OnSelectObeservedFolder);
			SelectScreenshotFolderCommand = new DelegateCommand(OnSelectScreenshotFolder);
			OpenObservedFolderCommand = new DelegateCommand(OnOpenObservedFolder);
			OpenScreenshotFolderCommand = new DelegateCommand(OnOpenScreenshotFolder);

			SetAggregatorEvents();

			SubscribeToOverlayActivate();
			SubscribeToOverlayDeactivate();
		}

		private void OnSelectObeservedFolder()
		{
			var dialog = new CommonOpenFileDialog
			{
				IsFolderPicker = true
			};

			CommonFileDialogResult result = dialog.ShowDialog();

			if (result == CommonFileDialogResult.Ok)
			{
				_appConfiguration.ObservedDirectory = dialog.FileName;
				_recordDirectoryObserver.UpdateObservedDirectory(dialog.FileName);
				ObservedDirectory = dialog.FileName;
				_updateObservedFolder.Publish(new AppMessages.UpdateObservedDirectory(dialog.FileName));
			}
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

		private void OnCaptureIsCheckedChanged()
		{
			
				_regionManager.RequestNavigate("DataRegion", "CaptureView");
		}

		private void OnSingleRecordIsCheckedChanged()
		{
			_regionManager.RequestNavigate("DataRegion", "DataView");
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

		private void OnAggregationIsCheckedChanged()
		{
			_regionManager.RequestNavigate("DataRegion", "AggregationView");
		}

		private void OnHardwareInfoSourceChanged()
		{
			throw new NotImplementedException();
		}

		private void SetAggregatorEvents()
		{
			_updateObservedFolder = _eventAggregator.GetEvent<PubSubEvent<AppMessages.UpdateObservedDirectory>>();
		}

		private void SubscribeToOverlayActivate()
		{
			_eventAggregator.GetEvent<PubSubEvent<ViewMessages.ShowOverlay>>()
							.Subscribe(msg =>
							{
								// This is crap, should be refactored
								var controlView = _regionManager.Regions["ControlRegion"].Views.FirstOrDefault();
								_regionManager.Regions["ControlRegion"].Deactivate(controlView);
								var colorbarView = _regionManager.Regions["ColorbarRegion"].Views.FirstOrDefault();
								_regionManager.Regions["ColorbarRegion"].Deactivate(colorbarView);

								var dataRegionViews = _regionManager.Regions["DataRegion"].ActiveViews;

								foreach (var view in dataRegionViews)
								{
									_regionManager.Regions["DataRegion"].Deactivate(view);
								}

								_regionManager.RequestNavigate("OverlayRegion", "OverlayView");
							});
		}

		private void SubscribeToOverlayDeactivate()
		{
			_eventAggregator.GetEvent<PubSubEvent<ViewMessages.HideOverlay>>()
							.Subscribe(msg =>
							{
								var overlayView = _regionManager.Regions["OverlayRegion"].Views.FirstOrDefault();
								_regionManager.Regions["OverlayRegion"].Deactivate(overlayView);

								_regionManager.RequestNavigate("ControlRegion", "ControlView");
								_regionManager.RequestNavigate("ColorbarRegion", "ColorbarView");

								if (CaptureIsChecked)
								{
									_regionManager.RequestNavigate("DataRegion", "CaptureView");
								}

								if (SingleRecordIsChecked)
								{
									_regionManager.RequestNavigate("DataRegion", "DataView");
								}

								if (RecordComparisonIsChecked)
								{
									_regionManager.RequestNavigate("DataRegion", "ComparisonView");
								}

								if (ReportIsChecked)
								{
									_regionManager.RequestNavigate("DataRegion", "ReportView");
								}

								if (SynchronizationIsChecked)
								{
									_regionManager.RequestNavigate("DataRegion", "SynchronizationView");
								}

								if (AggregationIsChecked)
								{
									_regionManager.RequestNavigate("DataRegion", "AggregationView");
								}
							});
		}
	}
}
