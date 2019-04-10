using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.OcatInterface;
using CapFrameX.EventAggregation.Messages;
using OxyPlot;
using OxyPlot.Series;
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

		private LineSeries _frametimeSeries;
		private bool _useUpdateSession = false;
		private IBufferObservable _scrollObservable;
		private double _currentOffset;

		public LineSeries FrametimeSeries
		{
			get { return _frametimeSeries; }
			set
			{
				if (_frametimeSeries != value)
				{
					_frametimeSeries = value;
					RaisePropertyChanged();
				}
			}
		}

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

									var tmp = new PlotModel
									{
										PlotMargins = new OxyThickness(50, 0, 0, 40)
									};

									var ls = new LineSeries { Title = "Test" };
									foreach (var point in points)
									{
										ls.Points.Add(new DataPoint(point.X, point.Y));
									}

									FrametimeSeries = ls;
									//CurrentOffset = 0;
									//ScrollObservable = Observable.Return<IEnumerable<Point>>(points);
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
