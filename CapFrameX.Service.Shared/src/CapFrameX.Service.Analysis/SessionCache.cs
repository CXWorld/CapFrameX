using CapFrameX.Data.Session.Contracts;

namespace CapFrameX.Service.Analysis;

/// <summary>Which capture, and which version of it.</summary>
/// <param name="Id">Identity of the indexed record.</param>
/// <param name="Size">Size of its file when it was read.</param>
/// <param name="ModifiedUtc">Last write time of its file when it was read.</param>
public readonly record struct SessionCacheKey(Guid Id, long Size, DateTime ModifiedUtc);

/// <summary>
/// Keeps recently analysed captures in memory.
/// </summary>
/// <remarks>
/// Every tab switch, range drag and metric change asks the same question of the same capture, and
/// parsing a hundred megabytes of JSON for each of them is the difference between a chart that
/// follows the mouse and one that does not. The bound is a frame count rather than a number of
/// captures: captures differ in size by two orders of magnitude, so counting them would either
/// waste memory on short ones or run out on long ones. The file's size and time are part of the
/// key, so a capture rewritten on disk is a different entry rather than a stale one.
/// </remarks>
public sealed class SessionCache
{
    /// <summary>Frames held at most, across all cached captures.</summary>
    /// <remarks>
    /// Roughly a dozen long captures. Each frame is a handful of doubles across the parsed arrays,
    /// so this is on the order of a few hundred megabytes in the worst case and far less in
    /// practice.
    /// </remarks>
    public const int DefaultCapacityFrames = 4_000_000;

    private readonly LinkedList<Entry> _entries = new();
    private readonly Dictionary<SessionCacheKey, LinkedListNode<Entry>> _byKey = [];
    private readonly Lock _gate = new();
    private readonly int _capacity;

    private long _frames;

    /// <summary>Creates the cache.</summary>
    /// <param name="capacityFrames">Frames to hold at most.</param>
    public SessionCache(int capacityFrames = DefaultCapacityFrames)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacityFrames);

        _capacity = capacityFrames;
    }

    /// <summary>How many captures are held.</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _byKey.Count;
            }
        }
    }

    /// <summary>How many frames they add up to.</summary>
    public long Frames
    {
        get
        {
            lock (_gate)
            {
                return _frames;
            }
        }
    }

    /// <summary>Looks a capture up, and marks it as just used.</summary>
    /// <param name="key">Which capture, and which version of it.</param>
    /// <param name="session">The parsed capture, when it is held.</param>
    public bool TryGet(SessionCacheKey key, out ISession? session)
    {
        lock (_gate)
        {
            if (!_byKey.TryGetValue(key, out var node))
            {
                session = null;

                return false;
            }

            _entries.Remove(node);
            _entries.AddFirst(node);
            session = node.Value.Session;

            return true;
        }
    }

    /// <summary>Puts a capture in, evicting the least recently used ones to make room.</summary>
    /// <param name="key">Which capture, and which version of it.</param>
    /// <param name="session">The parsed capture.</param>
    public void Set(SessionCacheKey key, ISession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var frames = FrameCount(session);

        lock (_gate)
        {
            if (_byKey.TryGetValue(key, out var existing))
            {
                _frames -= existing.Value.Frames;
                _entries.Remove(existing);
                _byKey.Remove(key);
            }

            // A capture larger than the whole cache is served but not kept: holding it would evict
            // everything else and still not survive the next lookup.
            if (frames > _capacity)
            {
                return;
            }

            var node = _entries.AddFirst(new Entry(key, session, frames));
            _byKey[key] = node;
            _frames += frames;

            while (_frames > _capacity && _entries.Last is { } last)
            {
                _entries.RemoveLast();
                _byKey.Remove(last.Value.Key);
                _frames -= last.Value.Frames;
            }
        }
    }

    /// <summary>Drops everything, for instance when the user changed where captures live.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
            _byKey.Clear();
            _frames = 0;
        }
    }

    private static long FrameCount(ISession session)
    {
        var frames = 0L;

        foreach (var run in session.Runs ?? [])
        {
            frames += run?.CaptureData?.MsBetweenPresents?.Length ?? 0;
        }

        // A capture with no frames still occupies an entry, and a weight of zero would let an
        // unbounded number of them in.
        return Math.Max(frames, 1);
    }

    private sealed record Entry(SessionCacheKey Key, ISession Session, long Frames);
}
