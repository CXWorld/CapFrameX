using CapFrameX.Contracts.PresentMonInterface;
using CapFrameX.PresentMonInterface;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.ObjectModel;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace CapFrameX.ViewModel
{
	public class CaptureViewModel : BindableBase, INavigationAware
	{
		private readonly ICaptureService _captureService;

		private bool _isCaptureModeActive;
		private IDisposable _disposableSequence;
		private string _selectedProcessToCapture;
		private string _selectedProcessToIgnore;

		public bool IsCaptureModeActive
		{
			get { return _isCaptureModeActive; }
			set
			{
				_isCaptureModeActive = value;
				RaisePropertyChanged();
			}
		}

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

		public ObservableCollection<string> ProcessesToCapture { get; }
			= new ObservableCollection<string>();

		public ObservableCollection<string> ProcessesToIgnore { get; }
			= new ObservableCollection<string>();

		public CaptureViewModel(ICaptureService captureService)
		{
			_captureService = captureService;
		}

		public void OnCaptureModeChanged(bool state)
		{
			string arguments = string.Empty;

			if (state)
				_captureService.StartCaptureService(
					CaptureServiceConfiguration.GetServiceStartInfo(arguments));
			else
				_captureService.StopCaptureService();
		}

		public bool IsNavigationTarget(NavigationContext navigationContext)
		{
			return true;
		}

		public void OnNavigatedFrom(NavigationContext navigationContext)
		{
			_disposableSequence?.Dispose();
		}

		public void OnNavigatedTo(NavigationContext navigationContext)
		{
			_disposableSequence?.Dispose();
			_disposableSequence = Observable.Generate(0, // dummy initialState
										x => true, // dummy condition
										x => x, // dummy iterate
										x => x, // dummy resultSelector
										x => TimeSpan.FromSeconds(1)).ObserveOn(new EventLoopScheduler())
										.Subscribe(x => UpdateProcessToCaptureList());
		}

		private void UpdateProcessToCaptureList()
		{

		}

		private void OnSelectedProcessToCaptureChanged()
		{
			throw new NotImplementedException();
		}

		private void OnSelectedProcessToIgnoreChanged()
		{
			throw new NotImplementedException();
		}
	}
}
