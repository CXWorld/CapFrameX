using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using CapFrameX.Contracts.Data;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Statistics.NetStandard.Contracts;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Configuration;
using CapFrameX.Contracts.Configuration;

namespace CapFrameX.Data
{
	public class LocalRecordDataServer : IRecordDataServer
	{
		private double _windowLength;
		private double _currentTime;
		private ISubject<IList<double>> _frametimeDataSubject;
		private ISubject<IList<Point>> _frametimePointDataSubject;
		private ISubject<IList<double>> _fpsDataSubject;
		private ISubject<IList<Point>> _fpsPointDataSubject;
		private ISubject<IList<Point>> _loadsPointDataSubject;
        private readonly IAppConfiguration _appConfiguration;

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

		public ERemoveOutlierMethod RemoveOutlierMethod { get; set; }

		public EFilterMode FilterMode { get; set; }

		public IObservable<IList<double>> FrametimeDataStream => _frametimeDataSubject.AsObservable();

		public IObservable<IList<Point>> FrametimePointDataStream => _frametimePointDataSubject.AsObservable();

		public IObservable<IList<double>> FpsDataStream => _fpsDataSubject.AsObservable();

		public IObservable<IList<Point>> FpsPointDataStream => _fpsPointDataSubject.AsObservable();

		public LocalRecordDataServer(IAppConfiguration appConfiguration)
		{
			_frametimeDataSubject = new Subject<IList<double>>();
			_frametimePointDataSubject = new Subject<IList<Point>>();
			_fpsDataSubject = new Subject<IList<double>>();
			_fpsPointDataSubject = new Subject<IList<Point>>();
			_loadsPointDataSubject = new Subject<IList<Point>>();

			IsActive = true;
            _appConfiguration = appConfiguration;
        }

		public IList<double> GetFrametimeTimeWindow()
		{
			if (CurrentSession == null)
				return null;

			double startTime = CurrentTime;
			double endTime = startTime + WindowLength;
			return CurrentSession.GetFrametimeTimeWindow(startTime, endTime, _appConfiguration ,RemoveOutlierMethod);
		}

		public IList<double> GetGpuActiveTimeTimeWindow()
		{
			if (CurrentSession == null)
				return null;

			double startTime = CurrentTime;
			double endTime = startTime + WindowLength;
			return CurrentSession.GetGpuActiveTimeTimeWindow(startTime, endTime, _appConfiguration, RemoveOutlierMethod);
		}

		public IList<Point> GetFrametimePointTimeWindow()
		{
			if (CurrentSession == null)
				return null;

			double startTime = CurrentTime;
			double endTime = startTime + WindowLength;
			return CurrentSession.GetFrametimePointsTimeWindow(startTime, endTime, _appConfiguration, RemoveOutlierMethod);
		}

        public IList<Point> GetGpuActiveTimePointTimeWindow()
        {
            if (CurrentSession == null)
                return null;

            double startTime = CurrentTime;
            double endTime = startTime + WindowLength;
            return CurrentSession.GetGpuActiveTimePointsTimeWindow(startTime, endTime, _appConfiguration, RemoveOutlierMethod);
        }

        public IList<double> GetFpsTimeWindow()
		{
			return GetFrametimeTimeWindow()?.Select(ft => 1000 / ft).ToList();
		}

        public IList<double> GetGpuActiveFpsTimeWindow()
        {
            return GetGpuActiveTimeTimeWindow()?.Select(ft => 1000 / ft).ToList();
        }

        public IList<Point> GetFpsPointTimeWindow()
		{
			return GetFrametimePointTimeWindow()?.Select(pnt => new Point(pnt.X, 1000 / pnt.Y)).ToList();
		}

        public IList<Point> GetGpuActiveFpsPointTimeWindow()
        {
            return GetGpuActiveTimePointTimeWindow()?.Select(pnt => new Point(pnt.X, 1000 / pnt.Y)).ToList();
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
