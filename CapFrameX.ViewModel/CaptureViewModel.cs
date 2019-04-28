using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.PresentMonInterface;
using CapFrameX.OcatInterface;
using CapFrameX.PresentMonInterface;
using Gma.System.MouseKeyHook;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Windows.Input;

namespace CapFrameX.ViewModel
{
	public class CaptureViewModel : BindableBase, INavigationAware
	{
		private readonly IAppConfiguration _appConfiguration;
		private readonly ICaptureService _captureService;

		private IDisposable _disposableSequence;
		private string _selectedProcessToCapture;
		private string _selectedProcessToIgnore;
		private bool _isAddToIgnoreListButtonActive = true;
		private bool _isCapturing;
		private bool _isCaptureModeActive = true;
		private string _captureStateInfo = string.Empty;

		public string SelectedProcessToCapture
		{
			get { return _selectedProcessToCapture; }
			set
			{
				_selectedProcessToCapture = value;
				RaisePropertyChanged();
				OnSelectedProcessToCaptureChanged();
			}
		}

		public string SelectedProcessToIgnore
		{
			get { return _selectedProcessToIgnore; }
			set
			{
				_selectedProcessToIgnore = value;
				RaisePropertyChanged();
				OnSelectedProcessToIgnoreChanged();
			}
		}

		public bool IsAddToIgnoreListButtonActive
		{
			get { return _isAddToIgnoreListButtonActive; }
			set
			{
				_isAddToIgnoreListButtonActive = value;
				RaisePropertyChanged();
			}
		}

		public string CaptureStateInfo
		{
			get { return _captureStateInfo; }
			set
			{
				_captureStateInfo = value;
				RaisePropertyChanged();
			}
		}

		public ObservableCollection<string> ProcessesToCapture { get; }
			= new ObservableCollection<string>();

		public ObservableCollection<string> ProcessesToIgnore { get; }
			= new ObservableCollection<string>();

		public ICommand AddToIgonreListCommand { get; }

		public ICommand RemoveFromIgnoreListCommand { get; }

		public ICommand ResetCaptureProcessCommand { get; }

		public CaptureViewModel(IAppConfiguration appConfiguration, ICaptureService captureService)
		{
			_appConfiguration = appConfiguration;
			_captureService = captureService;

			AddToIgonreListCommand = new DelegateCommand(OnAddToIgonreList);
			RemoveFromIgnoreListCommand = new DelegateCommand(OnRemoveFromIgnoreList);
			ResetCaptureProcessCommand = new DelegateCommand(OnResetCaptureProcess);

			CaptureStateInfo = "Capturing inactive... select process and press F12 to start.";

			ProcessesToIgnore.AddRange(CaptureServiceConfiguration.GetProcessIgnoreList());
			_disposableSequence = GetUpHeartBeat();
			SubscribeToGlobalHook();
		}

		public bool IsNavigationTarget(NavigationContext navigationContext)
		{
			return true;
		}

		public void OnNavigatedFrom(NavigationContext navigationContext)
		{
			_disposableSequence?.Dispose();
			_isCaptureModeActive = false;
		}

		public void OnNavigatedTo(NavigationContext navigationContext)
		{
			_disposableSequence?.Dispose();
			_disposableSequence = GetUpHeartBeat();
			_isCaptureModeActive = true;
			SubscribeToGlobalHook();
		}

		private void SubscribeToGlobalHook()
		{
			Hook.GlobalEvents().OnCombination(new Dictionary<Combination, Action>
			{
				{Combination.FromString("F12"), () =>
				{
					if (_isCaptureModeActive)
						SetCaptureMode();
				}}
			});
		}

		private void SetCaptureMode()
		{
			if (!_isCapturing)
			{
				_isCapturing = !_isCapturing;
				IsAddToIgnoreListButtonActive = false;

				if (string.IsNullOrWhiteSpace(SelectedProcessToCapture))
				{
					_isCapturing = !_isCapturing;
					IsAddToIgnoreListButtonActive = true;
					return;
				}

				_disposableSequence?.Dispose();

				CaptureStateInfo = "Capturing startet... press F12 to stop.";
				var filename = CaptureServiceConfiguration.GetCaptureFilename(SelectedProcessToCapture);

				string observedDirectory = RecordDirectoryObserver.GetInitialObservedDirectory(_appConfiguration.ObservedDirectory);
				ICaptureServiceConfiguration serviceConfig = new PresentMonServiceConfiguration
				{
					OutputFilename = Path.Combine(observedDirectory, filename),
					ProcessName = SelectedProcessToCapture + ".exe"
				};

				System.Media.SystemSounds.Beep.Play();
				_captureService.StartCaptureService(
					CaptureServiceConfiguration.GetServiceStartInfo
					(serviceConfig.ConfigParameterToArguments()));
			}
			else
			{
				_isCapturing = !_isCapturing;
				System.Media.SystemSounds.Beep.Play();
				_captureService.StopCaptureService();
				IsAddToIgnoreListButtonActive = true;
				_disposableSequence = GetUpHeartBeat();
			}
		}

		private void OnAddToIgonreList()
		{
			if (SelectedProcessToCapture == null)
				return;

			CaptureServiceConfiguration.AddProcessToIgnoreList(SelectedProcessToCapture);
			ProcessesToIgnore.Clear();
			ProcessesToIgnore.AddRange(CaptureServiceConfiguration.GetProcessIgnoreList());

			SelectedProcessToCapture = null;
		}

		private void OnRemoveFromIgnoreList()
		{
			if (SelectedProcessToIgnore == null)
				return;

			CaptureServiceConfiguration.RemoveProcessFromIgnoreList(SelectedProcessToIgnore);
			ProcessesToIgnore.Clear();
			ProcessesToIgnore.AddRange(CaptureServiceConfiguration.GetProcessIgnoreList());
		}

		private void OnResetCaptureProcess()
		{
			SelectedProcessToCapture = null;
		}

		private IDisposable GetUpHeartBeat()
		{
			var context = SynchronizationContext.Current;
			return Observable.Generate(0, // dummy initialState
										x => true, // dummy condition
										x => x, // dummy iterate
										x => x, // dummy resultSelector
										x => TimeSpan.FromSeconds(2))
										.ObserveOn(context)
										.SubscribeOn(context)
										.Subscribe(x => UpdateProcessToCaptureList());
		}

		private void UpdateProcessToCaptureList()
		{
			var selectedProcessToCapture = SelectedProcessToCapture;
			ProcessesToCapture.Clear();
			var filter = CaptureServiceConfiguration.GetProcessIgnoreList();
			var processList = _captureService.GetAllFilteredProcesses(filter).Distinct();
			ProcessesToCapture.AddRange(processList);

			if (!processList.Contains(selectedProcessToCapture))
				SelectedProcessToCapture = null;
			else
				SelectedProcessToCapture = selectedProcessToCapture;
		}

		private void OnSelectedProcessToCaptureChanged()
		{
			if (string.IsNullOrWhiteSpace(SelectedProcessToCapture))
			{
				CaptureStateInfo = "Capturing inactive... select process and press F12 to start.";
				return;
			}

			CaptureStateInfo = $"{SelectedProcessToCapture} selected, press F12 to start capture.";
		}

		private void OnSelectedProcessToIgnoreChanged()
		{
			// throw new NotImplementedException();
		}
	}
}
