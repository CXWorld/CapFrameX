using CapFrameX.Data.Session.Contracts;

namespace CapFrameX.Service.Analysis.Tests;

/// <summary>
/// What the cache is allowed to hand back, and what it has to forget.
/// </summary>
public sealed class SessionCacheTests
{
    private static readonly DateTime Monday = new(2026, 9, 14, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void A_capture_that_was_never_put_in_is_not_there()
    {
        var cache = new SessionCache();

        Assert.False(cache.TryGet(Key(), out var session));
        Assert.Null(session);
    }

    [Fact]
    public void A_capture_comes_back_as_it_went_in()
    {
        var cache = new SessionCache();
        var key = Key();
        var session = Capture(10);

        cache.Set(key, session);

        Assert.True(cache.TryGet(key, out var cached));
        Assert.Same(session, cached);
    }

    [Fact]
    public void A_rewritten_file_is_a_different_capture()
    {
        // Size and time are part of the key, so the old parse cannot outlive the file it came from.
        var cache = new SessionCache();
        var id = Guid.NewGuid();
        cache.Set(new SessionCacheKey(id, 1000, Monday), Capture(10));

        Assert.False(cache.TryGet(new SessionCacheKey(id, 2000, Monday), out _));
        Assert.False(cache.TryGet(new SessionCacheKey(id, 1000, Monday.AddMinutes(1)), out _));
    }

    [Fact]
    public void Putting_the_same_capture_in_twice_counts_it_once()
    {
        var cache = new SessionCache();
        var key = Key();

        cache.Set(key, Capture(10));
        cache.Set(key, Capture(10));

        Assert.Equal(1, cache.Count);
        Assert.Equal(10, cache.Frames);
    }

    [Fact]
    public void The_least_recently_used_capture_goes_first()
    {
        var cache = new SessionCache(capacityFrames: 30);
        var first = Key();
        var second = Key();
        var third = Key();

        cache.Set(first, Capture(10));
        cache.Set(second, Capture(10));
        cache.Set(third, Capture(10));

        // Touching the first one makes the second the oldest.
        Assert.True(cache.TryGet(first, out _));
        cache.Set(Key(), Capture(10));

        Assert.True(cache.TryGet(first, out _));
        Assert.False(cache.TryGet(second, out _));
        Assert.True(cache.TryGet(third, out _));
    }

    [Fact]
    public void A_long_capture_pushes_out_more_than_a_short_one()
    {
        // The bound is frames, not captures: two orders of magnitude separate a ten-second capture
        // from a benchmark run, and counting entries would be wrong for both.
        var cache = new SessionCache(capacityFrames: 100);
        var small = Key();
        cache.Set(small, Capture(10));

        cache.Set(Key(), Capture(95));

        Assert.False(cache.TryGet(small, out _));
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void A_capture_larger_than_the_cache_is_not_kept_at_all()
    {
        // Keeping it would evict everything else and still not survive the next lookup.
        var cache = new SessionCache(capacityFrames: 100);
        var kept = Key();
        cache.Set(kept, Capture(50));

        var huge = Key();
        cache.Set(huge, Capture(1000));

        Assert.False(cache.TryGet(huge, out _));
        Assert.True(cache.TryGet(kept, out _));
    }

    [Fact]
    public void An_empty_capture_still_takes_up_a_place()
    {
        // A weight of zero would let an unbounded number of them in.
        var cache = new SessionCache(capacityFrames: 2);

        cache.Set(Key(), Capture(0));
        cache.Set(Key(), Capture(0));
        cache.Set(Key(), Capture(0));

        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void Clearing_drops_everything()
    {
        var cache = new SessionCache();
        var key = Key();
        cache.Set(key, Capture(10));

        cache.Clear();

        Assert.Equal(0, cache.Count);
        Assert.Equal(0, cache.Frames);
        Assert.False(cache.TryGet(key, out _));
    }

    [Fact]
    public void Concurrent_use_neither_loses_nor_invents_entries()
    {
        // Every request thread reaches the same cache.
        var cache = new SessionCache(capacityFrames: 1000);
        var keys = Enumerable.Range(0, 200).Select(_ => Key()).ToArray();

        Parallel.ForEach(keys, key =>
        {
            cache.Set(key, Capture(10));
            cache.TryGet(key, out _);
        });

        Assert.Equal(100, cache.Count);
        Assert.Equal(1000, cache.Frames);
    }

    private static SessionCacheKey Key() => new(Guid.NewGuid(), 1000, Monday);

    private static ISession Capture(int frames) =>
        Captures.Of(Captures.Run(Captures.Steady(frames)));
}
