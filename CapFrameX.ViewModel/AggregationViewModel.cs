using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.OcatInterface;
using CapFrameX.EventAggregation.Messages;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Windows;
using IBufferObservable = System.IObservable<System.Collections.Generic.IEnumerable<System.Windows.Point>>;

namespace CapFrameX.ViewModel
{
	public class AggregationViewModel : BindableBase, INavigationAware
	{
		private readonly IRecordDirectoryObserver _recordObserver;
		private readonly IEventAggregator _eventAggregator;
		private readonly IAppConfiguration _appConfiguration;

		private bool _useUpdateSession = false;
		private IBufferObservable _scrollObservable;
		private double _currentOffset;

		public IBufferObservable ScrollObservable
		{
			get { return _scrollObservable; }
			set
			{
				_scrollObservable = value;
				RaisePropertyChanged();
			}
		}

		public double CurrentOffset
		{
			get { return _currentOffset; }
			set
			{
				_currentOffset = value;
				RaisePropertyChanged();
			}
		}

		public AggregationViewModel(IRecordDirectoryObserver recordObserver,
			IEventAggregator eventAggregator, IAppConfiguration appConfiguration)
		{
			_recordObserver = recordObserver;
			_eventAggregator = eventAggregator;
			_appConfiguration = appConfiguration;

			SubscribeToUpdateSession();
		}

		private void SubscribeToUpdateSession()
		{
			_eventAggregator.GetEvent<PubSubEvent<ViewMessages.UpdateSession>>()
							.Subscribe(msg =>
							{
								if (_useUpdateSession)
								{
									var session = msg.OcatSession;
									var points = session.FrameTimes.Select((ft, i) => new Point(session.FrameStart[i], ft));
									CurrentOffset = 0;
									ScrollObservable = Observable.Return<IEnumerable<Point>>(points);
								}
							});
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
	}
}
