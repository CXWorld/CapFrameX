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
        private ISession _currentSession;
        private ERemoveOutlierMethod _removeOutlierMethod;
        private EFilterMode _filterMode;
        private readonly IAppConfiguration _appConfiguration;
        private readonly object _windowCacheSync = new object();
        private IList<double> _frametimeWindow;
        private IList<double> _displayChangeWindow;
        private IList<double> _gpuActiveWindow;
        private IList<double> _animationErrorWindow;
        private IList<Point> _frametimePointWindow;
        private IList<Point> _gpuActivePointWindow;
        private IList<Point> _cpuActivePointWindow;
        private IList<Point> _displayChangePointWindow;
        private IList<Point> _distributionPointWindow;
        private IList<double> _fpsWindow;
        private IList<double> _gpuActiveFpsWindow;
        private IList<Point> _fpsPointWindow;
        private IList<Point> _gpuActiveFpsPointWindow;
        private double? _gpuActiveDeviationPercentage;
        private bool? _cachedUseDisplayChangeMetrics;

        public bool IsActive { get; set; }

        public ISession CurrentSession
        {
            get => _currentSession;
            set
            {
                if (!ReferenceEquals(_currentSession, value))
                {
                    _currentSession = value;
                    InvalidateWindowCache();
                }
            }
        }

        public double WindowLength
        {
            get => _windowLength;
            set
            {
                if (_windowLength != value)
                {
                    _windowLength = value;
                    InvalidateWindowCache();
                }
            }
        }

        public double CurrentTime
        {
            get => _currentTime;
            set
            {
                if (_currentTime != value)
                {
                    _currentTime = value;
                    InvalidateWindowCache();
                }
            }
        }

        public ERemoveOutlierMethod RemoveOutlierMethod
        {
            get => _removeOutlierMethod;
            set
            {
                if (_removeOutlierMethod != value)
                {
                    _removeOutlierMethod = value;
                    InvalidateWindowCache();
                }
            }
        }

        public EFilterMode FilterMode
        {
            get => _filterMode;
            set
            {
                if (_filterMode != value)
                {
                    _filterMode = value;
                    InvalidateWindowCache();
                }
            }
        }

        public LocalRecordDataServer(IAppConfiguration appConfiguration)
        {
            IsActive = true;
            _appConfiguration = appConfiguration;
        }

        public IList<double> GetFrametimeTimeWindow()
        {
            if (CurrentSession == null)
                return null;

            lock (_windowCacheSync)
            {
                if (_frametimeWindow == null)
                {
                    double startTime = CurrentTime;
                    _frametimeWindow = CurrentSession.GetFrametimeTimeWindow(
                        startTime, startTime + WindowLength, _appConfiguration, RemoveOutlierMethod);
                }
                return _frametimeWindow;
            }
        }

        public IList<double> GetDisplayChangeTimeWindow()
        {
            if (CurrentSession == null)
                return null;

            lock (_windowCacheSync)
            {
                if (_displayChangeWindow == null)
                {
                    double startTime = CurrentTime;
                    _displayChangeWindow = CurrentSession.GetDisplayChangeTimeWindow(
                        startTime, startTime + WindowLength, _appConfiguration, RemoveOutlierMethod);
                }
                return _displayChangeWindow;
            }
        }


        public IList<double> GetGpuActiveTimeTimeWindow()
        {
            if (CurrentSession == null)
                return null;

            lock (_windowCacheSync)
            {
                if (_gpuActiveWindow == null)
                {
                    double startTime = CurrentTime;
                    _gpuActiveWindow = CurrentSession.GetGpuActiveTimeTimeWindow(
                        startTime, startTime + WindowLength, _appConfiguration, RemoveOutlierMethod);
                }
                return _gpuActiveWindow;
            }
        }

        public IList<double> GetAnimationErrorTimeWindow()
        {
            if (CurrentSession == null)
                return null;

            lock (_windowCacheSync)
            {
                if (_animationErrorWindow == null)
                {
                    double startTime = CurrentTime;
                    _animationErrorWindow = CurrentSession.GetAnimationErrorTimeWindow(
                        startTime, startTime + WindowLength);
                }
                return _animationErrorWindow;
            }
        }

        public IList<Point> GetFrametimePointTimeWindow()
        {
            if (CurrentSession == null)
                return null;

            lock (_windowCacheSync)
            {
                if (_frametimePointWindow == null)
                {
                    double startTime = CurrentTime;
                    _frametimePointWindow = CurrentSession.GetFrametimePointsTimeWindow(
                        startTime, startTime + WindowLength, _appConfiguration, RemoveOutlierMethod);
                }
                return _frametimePointWindow;
            }
        }

        public IList<Point> GetGpuActiveTimePointTimeWindow()
        {
            if (CurrentSession == null)
                return null;

            lock (_windowCacheSync)
            {
                if (_gpuActivePointWindow == null)
                {
                    double startTime = CurrentTime;
                    _gpuActivePointWindow = CurrentSession.GetGpuActiveTimePointsTimeWindow(
                        startTime, startTime + WindowLength, _appConfiguration, RemoveOutlierMethod);
                }
                return _gpuActivePointWindow;
            }
        }

        public IList<Point> GetCpuActiveTimePointTimeWindow()
        {
            if (CurrentSession == null)
                return null;

            lock (_windowCacheSync)
            {
                if (_cpuActivePointWindow == null)
                {
                    double startTime = CurrentTime;
                    _cpuActivePointWindow = CurrentSession.GetCpuActiveTimePointsTimeWindow(
                        startTime, startTime + WindowLength, _appConfiguration, RemoveOutlierMethod);
                }
                return _cpuActivePointWindow;
            }
        }

        public IList<Point> GetFrametimeDistributionPointTimeWindow()
        {
            if (CurrentSession == null)
                return null;

            EnsureDisplayMetricCacheIsCurrent();
            lock (_windowCacheSync)
            {
                if (_distributionPointWindow == null)
                {
                    double startTime = CurrentTime;
                    double endTime = startTime + WindowLength;
                    if (_appConfiguration.UseDisplayChangeMetrics)
                    {
                        var displayDistribution = CurrentSession.GetDisplayTimeDistributionPoints(
                            startTime, endTime, _appConfiguration, RemoveOutlierMethod);
                        if (displayDistribution.Any())
                            _distributionPointWindow = displayDistribution;
                    }

                    if (_distributionPointWindow == null)
                    {
                        _distributionPointWindow = CurrentSession.GetFrametimeDistributionPoints(
                            startTime, endTime, _appConfiguration, RemoveOutlierMethod);
                    }
                }

                return _distributionPointWindow;
            }
        }

        public IList<double> GetFpsTimeWindow()
        {
            EnsureDisplayMetricCacheIsCurrent();
            lock (_windowCacheSync)
            {
                if (_fpsWindow == null)
                {
                    if (_appConfiguration.UseDisplayChangeMetrics)
                    {
                        var displayTimes = GetDisplayChangeTimeWindow();
                        if (displayTimes != null && displayTimes.Any())
                            _fpsWindow = displayTimes.Select(time => 1000 / time).ToList();
                    }

                    if (_fpsWindow == null)
                        _fpsWindow = GetFrametimeTimeWindow()?.Select(time => 1000 / time).ToList();
                }

                return _fpsWindow;
            }
        }

        public IList<double> GetGpuActiveFpsTimeWindow()
        {
            lock (_windowCacheSync)
            {
                if (_gpuActiveFpsWindow == null)
                    _gpuActiveFpsWindow = GetGpuActiveTimeTimeWindow()?.Select(ft => 1000 / ft).ToList();
                return _gpuActiveFpsWindow;
            }
        }

        public IList<Point> GetFpsPointTimeWindow()
        {
            if (CurrentSession == null)
                return null;

            EnsureDisplayMetricCacheIsCurrent();
            lock (_windowCacheSync)
            {
                if (_fpsPointWindow == null)
                {
                    IList<Point> timingPoints = null;
                    if (_appConfiguration.UseDisplayChangeMetrics)
                    {
                        if (_displayChangePointWindow == null)
                        {
                            _displayChangePointWindow = CurrentSession.GetDisplayChangeTimePointsTimeWindow(
                                CurrentTime, CurrentTime + WindowLength, _appConfiguration, RemoveOutlierMethod);
                        }
                        timingPoints = _displayChangePointWindow;
                    }

                    if (timingPoints == null || !timingPoints.Any())
                        timingPoints = GetFrametimePointTimeWindow();

                    _fpsPointWindow = timingPoints?
                        .Select(point => new Point(point.X, 1000 / point.Y)).ToList();
                }

                return _fpsPointWindow;
            }
        }

        public IList<Point> GetGpuActiveFpsPointTimeWindow()
        {
            lock (_windowCacheSync)
            {
                if (_gpuActiveFpsPointWindow == null)
                {
                    _gpuActiveFpsPointWindow = GetGpuActiveTimePointTimeWindow()?
                        .Select(pnt => new Point(pnt.X, 1000 / pnt.Y)).ToList();
                }
                return _gpuActiveFpsPointWindow;
            }
        }
        public IList<Point> GetDistributionPointTimeWindow()
        {
            return GetFrametimeDistributionPointTimeWindow()?.Select(pnt => new Point(pnt.X, pnt.Y)).ToList();
        }

        public double GetGpuActiveDeviationPercentage()
        {
            if (CurrentSession == null)
                return 0.0;

            lock (_windowCacheSync)
            {
                if (!_gpuActiveDeviationPercentage.HasValue)
                {
                    double startTime = CurrentTime;
                    double endTime = startTime + WindowLength;
                    _gpuActiveDeviationPercentage = CurrentSession.GetGpuActiveDeviationPercentage(
                        startTime, endTime, _appConfiguration, RemoveOutlierMethod);
                }
                return _gpuActiveDeviationPercentage.Value;
            }
        }

        public void SetTimeWindow(double currentTime, double windowLength)
        {
            if (_currentTime == currentTime && _windowLength == windowLength)
                return;

            _currentTime = currentTime;
            _windowLength = windowLength;
            InvalidateWindowCache();
        }

        private void InvalidateWindowCache()
        {
            lock (_windowCacheSync)
            {
                _frametimeWindow = null;
                _displayChangeWindow = null;
                _gpuActiveWindow = null;
                _animationErrorWindow = null;
                _frametimePointWindow = null;
                _gpuActivePointWindow = null;
                _cpuActivePointWindow = null;
                _displayChangePointWindow = null;
                _distributionPointWindow = null;
                _fpsWindow = null;
                _gpuActiveFpsWindow = null;
                _fpsPointWindow = null;
                _gpuActiveFpsPointWindow = null;
                _gpuActiveDeviationPercentage = null;
            }
        }

        private void EnsureDisplayMetricCacheIsCurrent()
        {
            bool useDisplayChangeMetrics = _appConfiguration.UseDisplayChangeMetrics;
            lock (_windowCacheSync)
            {
                if (_cachedUseDisplayChangeMetrics.HasValue
                    && _cachedUseDisplayChangeMetrics.Value != useDisplayChangeMetrics)
                {
                    InvalidateWindowCache();
                }
                _cachedUseDisplayChangeMetrics = useDisplayChangeMetrics;
            }
        }
    }
}
