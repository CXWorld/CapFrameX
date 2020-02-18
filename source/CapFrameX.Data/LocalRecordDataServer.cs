using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Windows;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Statistics;
using CapFrameX.Statistics;

namespace CapFrameX.Data
{
	public class LocalRecordDataServer : IRecordDataServer
	{
		private double _windowLength;
		private double _currentTime;
		private ERemoveOutlierMethod _removeOutlierMethod;
		private ISubject<IList<double>> _frametimeDataSubject;
		private ISubject<IList<Point>> _frametimePointDataSubject;
		private ISubject<IList<double>> _fpsDataSubject;
		private ISubject<IList<Point>> _fpsPointDataSubject;

		public bool IsActive { get; set; }

		public ISession CurrentSession { get; set; }

		public double WindowLength
		{
			get => _windowLength;
			set
			{
				_windowLength = value;
				DoUpdateWindowTrigger();
			}
		}

		public double CurrentTime
		{
			get => _currentTime;
			set
			{
				_currentTime = value;
				DoUpdateWindowTrigger();
			}
		}

		public ERemoveOutlierMethod RemoveOutlierMethod
		{
			get => _removeOutlierMethod;
			set
			{
				_removeOutlierMethod = value;
			}
		}

		public IObservable<IList<double>> FrametimeDataStream => _frametimeDataSubject.AsObservable();

		public IObservable<IList<Point>> FrametimePointDataStream => _frametimePointDataSubject.AsObservable();

		public IObservable<IList<double>> FpsDataStream => _fpsDataSubject.AsObservable();

		public IObservable<IList<Point>> FpsPointDataStream => _fpsPointDataSubject.AsObservable();

		public LocalRecordDataServer()
		{
			_frametimeDataSubject = new Subject<IList<double>>();
			_frametimePointDataSubject = new Subject<IList<Point>>();
			_fpsDataSubject = new Subject<IList<double>>();
			_fpsPointDataSubject = new Subject<IList<Point>>();

			IsActive = true;
		}

		public IList<double> GetFrametimeTimeWindow()
		{
			if (CurrentSession == null)
				return null;

			double startTime = CurrentTime;
			double endTime = startTime + WindowLength;
			return CurrentSession.GetFrametimeTimeWindow(startTime, endTime, RemoveOutlierMethod);
		}

		public IList<Point> GetFrametimePointTimeWindow()
		{
			if (CurrentSession == null)
				return null;

			double startTime = CurrentTime;
			double endTime = startTime + WindowLength;
			return CurrentSession.GetFrametimePointsTimeWindow(startTime, endTime, RemoveOutlierMethod);
		}

		public IList<double> GetFpsTimeWindow()
		{
			return GetFrametimeTimeWindow()?.Select(ft => 1000 / ft).ToList();
		}

		public IList<Point> GetFpsPointTimeWindow()
		{
			return GetFrametimePointTimeWindow()?.Select(pnt => new Point(pnt.X, 1000 / pnt.Y)).ToList();
		}

		private void DoUpdateWindowTrigger()
		{
			if (!IsActive)
				return;

			if (CurrentSession == null)
				return;

			_frametimeDataSubject.OnNext(GetFrametimeTimeWindow());
			_fpsDataSubject.OnNext(GetFpsTimeWindow());
		}

		public void SetTimeWindow(double currentTime, double windowLength)
		{
			CurrentTime = currentTime;
			WindowLength = windowLength;
		}
	}
}
