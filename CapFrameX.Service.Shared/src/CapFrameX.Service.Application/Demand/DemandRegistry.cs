namespace CapFrameX.Service.Application.Demand;

/// <summary>
/// Tracks who currently needs what. Consumers - a visible overlay, an armed capture, a subscribed
/// view - take a lease on a demand key, and everything expensive is driven by whether a key has
/// leases: no lease means no timer, no child process and no loaded module.
/// </summary>
/// <remarks>
/// Releasing is delayed by a grace period so that switching views does not restart PresentMon or
/// re-open a driver. Thread-safe; <see cref="Changed"/> is raised outside the internal lock.
/// </remarks>
public sealed class DemandRegistry : IDisposable
{
    private static readonly TimeSpan DefaultReleaseGracePeriod = TimeSpan.FromSeconds(5);

    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _releaseGracePeriod;
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();
    private bool _disposed;

    /// <summary>Creates a registry.</summary>
    /// <param name="timeProvider">Clock, so the grace period is testable.</param>
    /// <param name="releaseGracePeriod">How long a key stays active after its last lease is released.</param>
    public DemandRegistry(TimeProvider? timeProvider = null, TimeSpan? releaseGracePeriod = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _releaseGracePeriod = releaseGracePeriod ?? DefaultReleaseGracePeriod;
    }

    /// <summary>Raised when a key activates, deactivates or its accepted interval changes.</summary>
    public event Action<DemandChange>? Changed;

    /// <summary>Keys that currently have leases or are inside their grace period.</summary>
    public IReadOnlyCollection<string> ActiveKeys
    {
        get
        {
            lock (_gate)
            {
                return _entries.Keys.ToArray();
            }
        }
    }

    /// <summary>
    /// Registers demand for a key. Dispose the result to release it; disposing twice is harmless.
    /// </summary>
    /// <param name="key">Demand key, see <see cref="DemandKeys"/>.</param>
    /// <param name="maxInterval">
    /// Longest update interval this holder accepts. The effective interval is the shortest of all
    /// holders' values, so the most demanding consumer wins.
    /// </param>
    public IDisposable Acquire(string key, TimeSpan? maxInterval = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        var lease = new Lease(this, key, maxInterval);
        DemandChange? change;

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!_entries.TryGetValue(key, out var entry))
            {
                entry = new Entry();
                _entries.Add(key, entry);
            }

            // A key inside its grace period is still active: re-acquiring must not restart it.
            var wasActive = entry.Leases.Count > 0 || entry.GraceTimer is not null;
            entry.Leases.Add(lease);
            StopGraceTimer(entry);

            change = Reconcile(key, entry, wasActive ? DemandChangeKind.IntervalChanged : DemandChangeKind.Activated);
        }

        RaiseChanged(change);
        return lease;
    }

    /// <summary>Whether anything currently needs this key.</summary>
    /// <param name="key">Demand key to test.</param>
    public bool IsActive(string key)
    {
        lock (_gate)
        {
            return _entries.ContainsKey(key);
        }
    }

    /// <summary>
    /// Shortest interval the current holders of a key insist on, or <c>null</c> when the key is
    /// inactive or no holder stated one.
    /// </summary>
    /// <param name="key">Demand key to query.</param>
    public TimeSpan? Interval(string key)
    {
        lock (_gate)
        {
            return _entries.TryGetValue(key, out var entry) ? ShortestInterval(entry) : null;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        List<Entry> orphaned;

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            orphaned = [.. _entries.Values];
            _entries.Clear();
        }

        foreach (var entry in orphaned)
        {
            entry.GraceTimer?.Dispose();
        }
    }

    private void Release(Lease lease)
    {
        DemandChange? change = null;

        lock (_gate)
        {
            if (_disposed || !_entries.TryGetValue(lease.Key, out var entry) || !entry.Leases.Remove(lease))
            {
                return;
            }

            if (entry.Leases.Count == 0)
            {
                // Do not report an interval change on the way out; the key is heading for
                // deactivation and a consumer that re-acquires within the grace period should see
                // no event at all.
                StartGraceTimer(lease.Key, entry);
            }
            else
            {
                change = Reconcile(lease.Key, entry, DemandChangeKind.IntervalChanged);
            }
        }

        RaiseChanged(change);
    }

    private void OnGracePeriodElapsed(string key)
    {
        DemandChange change;

        lock (_gate)
        {
            if (_disposed || !_entries.TryGetValue(key, out var entry) || entry.Leases.Count > 0)
            {
                return;
            }

            StopGraceTimer(entry);
            _entries.Remove(key);
            change = new DemandChange(key, DemandChangeKind.Deactivated, null);
        }

        RaiseChanged(change);
    }

    /// <summary>
    /// Updates the interval the entry last reported and returns the change to raise, or
    /// <c>null</c> when nothing observable changed.
    /// </summary>
    private static DemandChange? Reconcile(string key, Entry entry, DemandChangeKind kind)
    {
        var interval = ShortestInterval(entry);

        if (kind != DemandChangeKind.Activated && interval == entry.ReportedInterval)
        {
            return null;
        }

        entry.ReportedInterval = interval;
        return new DemandChange(key, kind, interval);
    }

    private static TimeSpan? ShortestInterval(Entry entry)
    {
        TimeSpan? shortest = null;

        foreach (var lease in entry.Leases)
        {
            if (lease.MaxInterval is { } candidate && (shortest is null || candidate < shortest))
            {
                shortest = candidate;
            }
        }

        return shortest;
    }

    private void StartGraceTimer(string key, Entry entry)
    {
        StopGraceTimer(entry);
        entry.GraceTimer = _timeProvider.CreateTimer(
            static state => ((Action)state!)(),
            (Action)(() => OnGracePeriodElapsed(key)),
            _releaseGracePeriod,
            Timeout.InfiniteTimeSpan);
    }

    private static void StopGraceTimer(Entry entry)
    {
        entry.GraceTimer?.Dispose();
        entry.GraceTimer = null;
    }

    private void RaiseChanged(DemandChange? change)
    {
        if (change is not null)
        {
            Changed?.Invoke(change);
        }
    }

    private sealed class Entry
    {
        public List<Lease> Leases { get; } = [];

        public ITimer? GraceTimer { get; set; }

        public TimeSpan? ReportedInterval { get; set; }
    }

    private sealed class Lease(DemandRegistry registry, string key, TimeSpan? maxInterval) : IDisposable
    {
        private DemandRegistry? _registry = registry;

        public string Key { get; } = key;

        public TimeSpan? MaxInterval { get; } = maxInterval;

        public void Dispose() => Interlocked.Exchange(ref _registry, null)?.Release(this);
    }
}
