using CapFrameX.Contracts.OcatInterface;
using CapFrameX.OcatInterface;
using Prism.Mvvm;
using System;
using System.IO;
using System.Collections.ObjectModel;
using System.Threading;
using System.Reactive.Linq;
using System.Linq;
using LiveCharts;
using System.Windows.Input;
using Prism.Commands;
using System.Windows;
using System.Windows.Media;
using System.Collections.Generic;
using LiveCharts.Geared;

namespace CapFrameX.ViewModel
{
	public class MainViewModel : BindableBase
	{
		private readonly IRecordDirectoryObserver _recordObserver;
		private OcatRecordInfo _selectedRecordInfo;
		private ZoomingOptions _zoomingMode;
		private SeriesCollection _seriesCollection;
		private Func<double, string> _xFormatter;
		private Func<double, string> _yFormatter;

		public SeriesCollection SeriesCollection
		{
			get { return _seriesCollection; }
			set
			{
				_seriesCollection = value;
				RaisePropertyChanged();
			}
		}

		public Func<double, string> XFormatter
		{
			get { return _xFormatter; }
			set
			{
				_xFormatter = value;
				RaisePropertyChanged();
			}
		}
		public Func<double, string> YFormatter
		{
			get { return _yFormatter; }
			set
			{
				_yFormatter = value;
				RaisePropertyChanged();
			}
		}

		public ObservableCollection<OcatRecordInfo> RecordInfoList { get; }
			= new ObservableCollection<OcatRecordInfo>();

		public OcatRecordInfo SelectedRecordInfo
		{
			get { return _selectedRecordInfo; }
			set
			{
				_selectedRecordInfo = value;
				RaisePropertyChanged();
				OnSelectedRecordInfoChanged();
			}
		}

		public ZoomingOptions ZoomingMode
		{
			get { return _zoomingMode; }
			set
			{
				_zoomingMode = value;
				RaisePropertyChanged();
			}
		}

		public ICommand ToogleZoomingModeCommand { get; }

		public MainViewModel(IRecordDirectoryObserver recordObserver)
		{
			_recordObserver = recordObserver;

			// ToDo: check wether to do this async
			var initialRecordList = _recordObserver.GetAllRecordFileInfo();

			foreach (var fileInfo in initialRecordList)
			{
				AddToRecordInfoList(fileInfo);
			}

			var context = SynchronizationContext.Current;
			_recordObserver.RecordCreatedStream.ObserveOn(context).SubscribeOn(context)
							.Subscribe(OnRecordCreated);
			_recordObserver.RecordDeletedStream.ObserveOn(context).SubscribeOn(context)
							.Subscribe(OnRecordDeleted);

			// Turn streams now on
			_recordObserver.IsActive = true;

			ToogleZoomingModeCommand = new DelegateCommand(OnToogleZoomingMode);

			ZoomingMode = ZoomingOptions.Y;
		}

		private void OnToogleZoomingMode()
		{
			switch (ZoomingMode)
			{
				case ZoomingOptions.None:
					ZoomingMode = ZoomingOptions.X;
					break;
				case ZoomingOptions.X:
					ZoomingMode = ZoomingOptions.Y;
					break;
				case ZoomingOptions.Y:
					ZoomingMode = ZoomingOptions.Xy;
					break;
				case ZoomingOptions.Xy:
					ZoomingMode = ZoomingOptions.None;
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		private void AddToRecordInfoList(FileInfo fileInfo)
		{
			var recordInfo = OcatRecordInfo.Create(fileInfo);
			if (recordInfo != null)
			{
				RecordInfoList.Add(recordInfo);
			}
		}

		private void OnRecordCreated(FileInfo fileInfo) => AddToRecordInfoList(fileInfo);

		private void OnRecordDeleted(FileInfo fileInfo)
		{
			var recordInfo = OcatRecordInfo.Create(fileInfo);
			if (recordInfo != null)
			{
				var match = RecordInfoList.FirstOrDefault(info => info.FullPath == fileInfo.FullName);

				if (match != null)
				{
					RecordInfoList.Remove(match);
				}
			}
		}

		private void OnSelectedRecordInfoChanged()
		{
			var session = RecordManager.LoadData(SelectedRecordInfo.FullPath);
			SetChart(session.FrameTimes);
		}

		private void SetChart(IList<double> frametimes)
		{
			var gradientBrush = new LinearGradientBrush
			{
				StartPoint = new Point(0, 0),
				EndPoint = new Point(0, 1)
			};
			gradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(33, 148, 241), 0));
			gradientBrush.GradientStops.Add(new GradientStop(Colors.Transparent, 1));

			var values = new GearedValues<double>();
			values.AddRange(frametimes);
			values.WithQuality(Quality.High);

			SeriesCollection = new SeriesCollection
			{
				new GLineSeries
				{
					Values = values,
					Fill = gradientBrush,
					StrokeThickness = 1,
					LineSmoothness= 0,
					PointGeometrySize = 0
				}
			};

			//XFormatter = val => new DateTime((long)val).ToString("dd MMM");
			//YFormatter = val => val.ToString("C");
		}
	}
}
