using System;
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
        // Every time window copies the whole session and, with outlier removal, sorts it. One
        // refresh of the analysis page asks for the same window several times, from the UI thread
        // and from chart tasks, so the windows are kept per session, time window and outlier
        // method. A few are enough to cover a refresh; each caller gets its own copy.
        internal const int MaxCachedWindows = 16;

        private double _windowLength;
        private double _currentTime;
        private ISession _currentSession;
        private readonly IAppConfiguration _appConfiguration;
        private readonly object _windowCacheLock = new object();
        private readonly Dictionary<WindowKey, object> _windowCache = new Dictionary<WindowKey, object>();
        private readonly Queue<WindowKey> _windowCacheOrder = new Queue<WindowKey>();

        public bool IsActive { get; set; }

        public ISession CurrentSession
        {
            get => _currentSession;
            set
            {
                if (ReferenceEquals(_currentSession, value))
                    return;
                _currentSession = value;
                // The windows of the previous session are never asked for again.
                lock (_windowCacheLock)
                {
                    _windowCache.Clear();
                    _windowCacheOrder.Clear();
                }
            }
        }

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
            => GetFrametimeTimeWindow(RemoveOutlierMethod);

        public IList<double> GetDisplayChangeTimeWindow()
            => GetValues(nameof(GetDisplayChangeTimeWindow), RemoveOutlierMethod, (session, startTime, endTime, method)
                => session.GetDisplayChangeTimeWindow(startTime, endTime, _appConfiguration, method));

        public IList<double> GetGpuActiveTimeTimeWindow()
            => GetGpuActiveTimeTimeWindow(RemoveOutlierMethod);

        public IList<double> GetAnimationErrorTimeWindow()
            => GetValues(nameof(GetAnimationErrorTimeWindow), ERemoveOutlierMethod.None, (session, startTime, endTime, _)
                => session.GetAnimationErrorTimeWindow(startTime, endTime));

        public IList<Point> GetFrametimePointTimeWindow()
            => GetPoints(nameof(GetFrametimePointTimeWindow), (session, startTime, endTime, method)
                => session.GetFrametimePointsTimeWindow(startTime, endTime, _appConfiguration, method));

        public IList<Point> GetGpuActiveTimePointTimeWindow()
            => GetPoints(nameof(GetGpuActiveTimePointTimeWindow), (session, startTime, endTime, method)
                => session.GetGpuActiveTimePointsTimeWindow(startTime, endTime, _appConfiguration, method));

        public IList<Point> GetCpuActiveTimePointTimeWindow()
            => GetPoints(nameof(GetCpuActiveTimePointTimeWindow), (session, startTime, endTime, method)
                => session.GetCpuActiveTimePointsTimeWindow(startTime, endTime, _appConfiguration, method));

        public IList<Point> GetFrametimeDistributionPointTimeWindow()
            => GetPoints(nameof(GetFrametimeDistributionPointTimeWindow), (session, startTime, endTime, method)
                => session.GetFrametimeDistributionPoints(startTime, endTime, _appConfiguration, method));

        public IList<double> GetFpsTimeWindow()
        {
            if (_appConfiguration.UseDisplayChangeMetrics)
            {
                return GetDisplayChangeTimeWindow()?.Select(dt => 1000 / dt).ToList();
            }
            else
            {
                return GetFrametimeTimeWindow()?.Select(ft => 1000 / ft).ToList();
            }
        }

        public IList<Point> GetFpsPointTimeWindow()
        {
            return GetFrametimePointTimeWindow()?.Select(pnt => new Point(pnt.X, 1000 / pnt.Y)).ToList();
        }

        public IList<Point> GetGpuActiveFpsPointTimeWindow()
        {
            return GetGpuActiveTimePointTimeWindow()?.Select(pnt => new Point(pnt.X, 1000 / pnt.Y)).ToList();
        }
        public IList<Point> GetDistributionPointTimeWindow()
        {
            return GetFrametimeDistributionPointTimeWindow()?.Select(pnt => new Point(pnt.X, pnt.Y)).ToList();
        }

        public double GetGpuActiveDeviationPercentage()
        {
            if (CurrentSession == null)
                return 0.0;

            // Always without outlier removal, as the session extension computes it.
            return SessionExtensions.GetGpuActiveDeviationPercentage(
                GetFrametimeTimeWindow(ERemoveOutlierMethod.None),
                GetGpuActiveTimeTimeWindow(ERemoveOutlierMethod.None));
        }

        public void SetTimeWindow(double currentTime, double windowLength)
        {
            CurrentTime = currentTime;
            WindowLength = windowLength;
        }

        private IList<double> GetFrametimeTimeWindow(ERemoveOutlierMethod method)
            => GetValues(nameof(GetFrametimeTimeWindow), method, (session, startTime, endTime, outlierMethod)
                => session.GetFrametimeTimeWindow(startTime, endTime, _appConfiguration, outlierMethod));

        private IList<double> GetGpuActiveTimeTimeWindow(ERemoveOutlierMethod method)
            => GetValues(nameof(GetGpuActiveTimeTimeWindow), method, (session, startTime, endTime, outlierMethod)
                => session.GetGpuActiveTimeTimeWindow(startTime, endTime, _appConfiguration, outlierMethod));

        private IList<double> GetValues(string kind, ERemoveOutlierMethod method,
            Func<ISession, double, double, ERemoveOutlierMethod, IList<double>> compute)
            => GetWindow(kind, method, compute, values => new List<double>(values));

        private IList<Point> GetPoints(string kind,
            Func<ISession, double, double, ERemoveOutlierMethod, IList<Point>> compute)
            => GetWindow(kind, RemoveOutlierMethod, compute,
                points => points.Select(point => new Point(point.X, point.Y)).ToList());

        private IList<T> GetWindow<T>(string kind, ERemoveOutlierMethod method,
            Func<ISession, double, double, ERemoveOutlierMethod, IList<T>> compute,
            Func<IList<T>, List<T>> copy)
        {
            var session = CurrentSession;
            if (session == null)
                return null;

            double startTime = CurrentTime;
            double endTime = startTime + WindowLength;
            var key = new WindowKey(kind, session, startTime, endTime, method);

            lock (_windowCacheLock)
            {
                if (_windowCache.TryGetValue(key, out var cached))
                    return copy((IList<T>)cached);
            }

            // Computed outside the lock: chart tasks ask for different windows at the same time.
            var window = copy(compute(session, startTime, endTime, method));

            lock (_windowCacheLock)
            {
                // A session switched in the meantime has cleared the cache; its windows stay out.
                if (ReferenceEquals(session, _currentSession) && !_windowCache.ContainsKey(key))
                {
                    if (_windowCache.Count >= MaxCachedWindows)
                        _windowCache.Remove(_windowCacheOrder.Dequeue());
                    _windowCache.Add(key, window);
                    _windowCacheOrder.Enqueue(key);
                }
            }

            return copy(window);
        }

        private readonly struct WindowKey : IEquatable<WindowKey>
        {
            private readonly string _kind;
            private readonly ISession _session;
            private readonly double _startTime;
            private readonly double _endTime;
            private readonly ERemoveOutlierMethod _method;

            public WindowKey(string kind, ISession session, double startTime, double endTime, ERemoveOutlierMethod method)
            {
                _kind = kind;
                _session = session;
                _startTime = startTime;
                _endTime = endTime;
                _method = method;
            }

            public bool Equals(WindowKey other)
                => _kind == other._kind && ReferenceEquals(_session, other._session)
                    && _startTime.Equals(other._startTime) && _endTime.Equals(other._endTime)
                    && _method == other._method;

            public override bool Equals(object obj) => obj is WindowKey other && Equals(other);

            public override int GetHashCode()
                => HashCode.Combine(_kind, System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(_session),
                    _startTime, _endTime, _method);
        }
    }
}
