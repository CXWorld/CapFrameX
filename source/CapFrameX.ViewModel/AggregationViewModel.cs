using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.OcatInterface;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Data;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Windows;

namespace CapFrameX.ViewModel
{
	public class AggregationViewModel : BindableBase, INavigationAware
	{
		// make UI parameter
		const double SELECTABLETIME = 10d;

		private readonly IRecordDirectoryObserver _recordObserver;
		private readonly IEventAggregator _eventAggregator;
		private readonly IAppConfiguration _appConfiguration;

		private PlotModel _frametimeModel;
		private bool _useUpdateSession = false;
		private double _timeOffset;
		private double _bufferStartTime;
		private double _selectableTime;
		private double _scrollEndTime;
		private Session _session;
		private IEnumerable<Point> _points;

		public PlotModel FrametimeModel
		{
			get { return _frametimeModel; }
			set
			{
				if (_frametimeModel != value)
				{
					_frametimeModel = value;
					RaisePropertyChanged();
				}
			}
		}

		public double TimeOffset
		{
			get { return _timeOffset; }
			set
			{
				_timeOffset = value;
				RaisePropertyChanged();
				OnTimeOffsetChanged();
			}
		}

		public double BufferStartTime
		{
			get { return _bufferStartTime; }
			set
			{
				_bufferStartTime = value;
				RaisePropertyChanged();
			}
		}

		public double ScrollEndTime
		{
			get { return _scrollEndTime; }
			set
			{
				_scrollEndTime = value;
				RaisePropertyChanged();
			}
		}

		public double SelectableTime
		{
			get { return _selectableTime; }
			set
			{
				_selectableTime = value;
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
									_session = msg.CurrentSession;
									_points = _session?.FrameTimes.Select((ft, i) => new Point(_session.FrameStart[i], ft));

									if (_points == null || !_points.Any())
										return;

									_timeOffset = _points.First().X;
									BufferStartTime = _points.First().X;
									ScrollEndTime = _points.Last().X - SELECTABLETIME;
									SelectableTime = SELECTABLETIME;

									var tmp = new PlotModel
									{
										PlotMargins = new OxyThickness(40, 10, 10, 40),
										PlotAreaBorderColor = OxyColor.FromArgb(64, 204, 204, 204)									
									};

									// Frametime graph
									var ls = new LineSeries { Title = "Test samples", StrokeThickness = 1, Color = OxyColor.FromRgb(139, 35, 35) };

									var slidingWindow = _points.Where(p => p.X >= TimeOffset && p.X <= TimeOffset + SelectableTime);
									var yMin = slidingWindow.Min(pnt => pnt.Y);
									var yMax = slidingWindow.Max(pnt => pnt.Y);
									double minXWindow = slidingWindow.First().X;
									foreach (var point in slidingWindow)
									{
										ls.Points.Add(new DataPoint(point.X - minXWindow, point.Y));
									}

									tmp.Series.Add(ls);

									//Axes
									//X
									tmp.Axes.Add(new LinearAxis()
									{
										Key = "xAxis",
										Position = AxisPosition.Bottom,
										Title = "Time [s]",
										Minimum = _points.First().X,
										Maximum = SELECTABLETIME,
										MajorGridlineStyle = LineStyle.Solid,
										MajorGridlineThickness = 1,
										MajorGridlineColor = OxyColor.FromArgb(64, 204, 204, 204),
										MinorTickSize = 0,
										MajorTickSize = 0
									});								

									//Y
									tmp.Axes.Add(new LinearAxis()
									{
										Key = "yAxis",
										Position = AxisPosition.Left,
										Title = "Frametimes [ms]",
										Minimum = yMin - (yMax - yMin)/ 6,
										Maximum = yMax + (yMax - yMin) / 6,
										MajorGridlineStyle = LineStyle.Solid,
										MajorGridlineThickness = 1,
										MajorGridlineColor = OxyColor.FromArgb(64, 204, 204, 204),
										MinorTickSize = 0,
										MajorTickSize = 0
									});

									FrametimeModel = tmp;
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

		private void OnTimeOffsetChanged()
		{
			if (_points == null || !_points.Any())
				return;			

			// Update frametime graph sliding window
			var ls = new LineSeries { Title = "Test samples", StrokeThickness = 1, Color = OxyColor.FromRgb(139, 35, 35) };

			var slidingWindow = _points.Where(p => p.X >= TimeOffset && p.X <= TimeOffset + SelectableTime);
			double minXWindow = slidingWindow.First().X;
			foreach (var point in slidingWindow)
			{
				ls.Points.Add(new DataPoint(point.X - minXWindow, point.Y));
			}

			FrametimeModel.Series.Clear();
			FrametimeModel.Series.Add(ls);
			FrametimeModel.InvalidatePlot(true);
		}
	}
}
