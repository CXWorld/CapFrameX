using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Statistics.NetStandard.Contracts;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Contracts.Configuration;

namespace CapFrameX.Data
{
	public class LocalRecordDataServer : IRecordDataServer
	{
		private double _windowLength;
		private double _currentTime;
        private readonly IAppConfiguration _appConfiguration;

        public bool IsActive { get; set; }

		public ISession CurrentSession { get; set; }

		public double WindowLength
		{
			get => _windowLength;
			set
			{
				_windowLength = value;
			}
		}

		public double CurrentTime
		{
			get => _currentTime;
			set
			{
				_currentTime = value;
			}
		}

		public ERemoveOutlierMethod RemoveOutlierMethod { get; set; }

		public EFilterMode FilterMode { get; set; }

		public LocalRecordDataServer(IAppConfiguration appConfiguration)
		{
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

		public double GetGpuActiveDeviationPercentage()
		{
            if (CurrentSession == null)
                return 0.0;

            double startTime = CurrentTime;
            double endTime = startTime + WindowLength;
            return CurrentSession.GetGpuActiveDeviationPercentage(startTime, endTime, _appConfiguration, RemoveOutlierMethod);
		}

		public void SetTimeWindow(double currentTime, double windowLength)
		{
			CurrentTime = currentTime;
			WindowLength = windowLength;
		}
	}
}
