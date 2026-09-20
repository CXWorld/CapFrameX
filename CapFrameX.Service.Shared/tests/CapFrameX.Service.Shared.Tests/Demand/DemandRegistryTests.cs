using CapFrameX.Service.Application.Demand;
using Microsoft.Extensions.Time.Testing;

namespace CapFrameX.Service.Shared.Tests.Demand;

/// <summary>
/// The demand registry is what turns "evaluate on demand" into something the rest of the service
/// can act on, so its release behaviour is pinned down here rather than discovered in production.
/// </summary>
public sealed class DemandRegistryTests
{
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(5);

    private static DemandRegistry Create(FakeTimeProvider time) => new(time, Grace);

    [Fact]
    public void Key_without_lease_is_inactive()
    {
        using var registry = Create(new FakeTimeProvider());

        Assert.False(registry.IsActive(DemandKeys.Overlay));
        Assert.Null(registry.Interval(DemandKeys.Overlay));
        Assert.Empty(registry.ActiveKeys);
    }

    [Fact]
    public void First_lease_activates_the_key_once()
    {
        var time = new FakeTimeProvider();
        using var registry = Create(time);
        var changes = new List<DemandChange>();
        registry.Changed += changes.Add;

        using var first = registry.Acquire(DemandKeys.Overlay);
        using var second = registry.Acquire(DemandKeys.Overlay);

        Assert.True(registry.IsActive(DemandKeys.Overlay));
        Assert.Equal(new[] { DemandKeys.Overlay }, registry.ActiveKeys);
        var change = Assert.Single(changes);
        Assert.Equal(DemandChangeKind.Activated, change.Kind);
        Assert.Equal(DemandKeys.Overlay, change.Key);
    }

    [Fact]
    public void Key_stays_active_while_another_lease_is_held()
    {
        var time = new FakeTimeProvider();
        using var registry = Create(time);
        var first = registry.Acquire(DemandKeys.Overlay);
        using var second = registry.Acquire(DemandKeys.Overlay);

        first.Dispose();
        time.Advance(Grace * 2);

        Assert.True(registry.IsActive(DemandKeys.Overlay));
    }

    [Fact]
    public void Last_release_deactivates_the_key_only_after_the_grace_period()
    {
        var time = new FakeTimeProvider();
        using var registry = Create(time);
        var changes = new List<DemandChange>();
        registry.Changed += changes.Add;

        registry.Acquire(DemandKeys.ProcessList).Dispose();

        Assert.True(registry.IsActive(DemandKeys.ProcessList));
        time.Advance(Grace - TimeSpan.FromMilliseconds(1));
        Assert.True(registry.IsActive(DemandKeys.ProcessList));

        time.Advance(TimeSpan.FromMilliseconds(1));

        Assert.False(registry.IsActive(DemandKeys.ProcessList));
        Assert.Equal(
            new[] { DemandChangeKind.Activated, DemandChangeKind.Deactivated },
            changes.Select(c => c.Kind));
    }

    [Fact]
    public void Re_acquiring_inside_the_grace_period_does_not_restart_the_key()
    {
        var time = new FakeTimeProvider();
        using var registry = Create(time);
        var changes = new List<DemandChange>();
        registry.Changed += changes.Add;

        registry.Acquire(DemandKeys.ProcessList).Dispose();
        time.Advance(Grace / 2);
        using var again = registry.Acquire(DemandKeys.ProcessList);
        time.Advance(Grace * 2);

        Assert.True(registry.IsActive(DemandKeys.ProcessList));
        Assert.Equal(DemandChangeKind.Activated, Assert.Single(changes).Kind);
    }

    [Fact]
    public void Effective_interval_is_the_shortest_any_holder_insists_on()
    {
        var time = new FakeTimeProvider();
        using var registry = Create(time);
        var key = DemandKeys.Sensor("gpu.load");

        using var relaxed = registry.Acquire(key, TimeSpan.FromSeconds(1));
        Assert.Equal(TimeSpan.FromSeconds(1), registry.Interval(key));

        var strict = registry.Acquire(key, TimeSpan.FromMilliseconds(100));
        Assert.Equal(TimeSpan.FromMilliseconds(100), registry.Interval(key));

        strict.Dispose();
        Assert.Equal(TimeSpan.FromSeconds(1), registry.Interval(key));
    }

    [Fact]
    public void Holder_without_an_interval_does_not_constrain_the_others()
    {
        var time = new FakeTimeProvider();
        using var registry = Create(time);
        var key = DemandKeys.Sensor("cpu.load");

        using var indifferent = registry.Acquire(key);
        using var strict = registry.Acquire(key, TimeSpan.FromMilliseconds(250));

        Assert.Equal(TimeSpan.FromMilliseconds(250), registry.Interval(key));
    }

    [Fact]
    public void Interval_change_is_reported_while_the_key_stays_active()
    {
        var time = new FakeTimeProvider();
        using var registry = Create(time);
        var key = DemandKeys.Sensor("gpu.power");
        var changes = new List<DemandChange>();
        registry.Changed += changes.Add;

        using var relaxed = registry.Acquire(key, TimeSpan.FromSeconds(1));
        using var strict = registry.Acquire(key, TimeSpan.FromMilliseconds(100));

        Assert.Equal(
            new[] { DemandChangeKind.Activated, DemandChangeKind.IntervalChanged },
            changes.Select(c => c.Kind));
        Assert.Equal(TimeSpan.FromMilliseconds(100), changes[^1].Interval);
    }

    [Fact]
    public void Releasing_a_lease_twice_is_harmless()
    {
        var time = new FakeTimeProvider();
        using var registry = Create(time);
        var lease = registry.Acquire(DemandKeys.Overlay);
        using var other = registry.Acquire(DemandKeys.Overlay);

        lease.Dispose();
        lease.Dispose();

        Assert.True(registry.IsActive(DemandKeys.Overlay));
    }

    [Fact]
    public void Keys_are_independent()
    {
        var time = new FakeTimeProvider();
        using var registry = Create(time);

        using var overlay = registry.Acquire(DemandKeys.Overlay);
        registry.Acquire(DemandKeys.Frames(1234)).Dispose();
        time.Advance(Grace * 2);

        Assert.True(registry.IsActive(DemandKeys.Overlay));
        Assert.False(registry.IsActive(DemandKeys.Frames(1234)));
    }

    [Fact]
    public void Concurrent_acquire_and_release_keeps_the_key_consistent()
    {
        var time = new FakeTimeProvider();
        using var registry = Create(time);
        using var anchor = registry.Acquire(DemandKeys.Overlay);

        Parallel.For(0, 200, _ =>
        {
            var lease = registry.Acquire(DemandKeys.Overlay, TimeSpan.FromMilliseconds(500));
            lease.Dispose();
        });

        Assert.True(registry.IsActive(DemandKeys.Overlay));
        Assert.Null(registry.Interval(DemandKeys.Overlay));
    }

    [Fact]
    public void Disposing_the_registry_releases_everything()
    {
        var time = new FakeTimeProvider();
        var registry = Create(time);
        using var lease = registry.Acquire(DemandKeys.Overlay);

        registry.Dispose();

        Assert.False(registry.IsActive(DemandKeys.Overlay));
        Assert.Empty(registry.ActiveKeys);
    }
}
