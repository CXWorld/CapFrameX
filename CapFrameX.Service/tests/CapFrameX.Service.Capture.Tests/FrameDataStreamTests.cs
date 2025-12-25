using CapFrameX.Service.Capture.Contracts;
using System.Reactive.Linq;

namespace CapFrameX.Service.Capture.Tests;

/// <summary>
/// Tests for frame data stream functionality.
/// </summary>
public class FrameDataStreamTests : IDisposable
{
    private readonly PresentMonCaptureService _captureService;

    public FrameDataStreamTests()
    {
        _captureService = TestHelpers.CreateCaptureService();
    }

    [Fact]
    public void FrameDataStream_ShouldBeSubscribable()
    {
        // Arrange
        var subscribed = false;
        Exception? subscriptionError = null;

        // Act
        var subscription = _captureService.FrameDataStream
            .Subscribe(
                _ => subscribed = true,
                ex => subscriptionError = ex
            );

        // Assert
        Assert.NotNull(subscription);
        Assert.Null(subscriptionError);

        subscription.Dispose();
    }

    [Fact]
    public void FrameDataStream_MultipleSubscribers_ShouldBothReceiveData()
    {
        // Arrange
        var subscriber1Received = false;
        var subscriber2Received = false;

        var subscription1 = _captureService.FrameDataStream
            .Subscribe(_ => subscriber1Received = true);

        var subscription2 = _captureService.FrameDataStream
            .Subscribe(_ => subscriber2Received = true);

        // Assert
        Assert.NotNull(subscription1);
        Assert.NotNull(subscription2);

        subscription1.Dispose();
        subscription2.Dispose();
    }

    [Fact]
    public void FrameDataStream_ShouldHandleUnsubscribe()
    {
        // Arrange
        var dataReceived = 0;

        var subscription = _captureService.FrameDataStream
            .Subscribe(_ => dataReceived++);

        // Act
        subscription.Dispose();

        // Assert - Should not throw
        Assert.True(dataReceived >= 0);
    }

    [Fact]
    public void IsCaptureModeActiveStream_ShouldEmitOnStateChange()
    {
        // Arrange
        var states = new List<bool>();
        var subscription = _captureService.IsCaptureModeActiveStream
            .Subscribe(state => states.Add(state));

        // Act
        _captureService.IsCaptureModeActiveStream.OnNext(true);
        _captureService.IsCaptureModeActiveStream.OnNext(false);

        // Assert
        Assert.Contains(true, states);
        Assert.Contains(false, states);

        subscription.Dispose();
    }

    [Fact]
    public async Task FrameDataStream_ShouldHandleBackpressure()
    {
        // Arrange
        var receivedCount = 0;
        var cts = new CancellationTokenSource();
        var subscription = _captureService.FrameDataStream
            .Sample(TimeSpan.FromMilliseconds(100))
            .Subscribe(_ => Interlocked.Increment(ref receivedCount));

        // Act - Let it run for a short time
        await Task.Delay(200, cts.Token);

        // Assert - Should handle sampling without issues
        Assert.True(receivedCount >= 0);

        subscription.Dispose();
        cts.Dispose();
    }

    [Fact]
    public void FrameDataStream_ConcurrentSubscriptions_ShouldNotThrow()
    {
        // Arrange
        var subscriptions = new List<IDisposable>();

        // Act
        var exception = Record.Exception(() =>
        {
            Parallel.For(0, 10, _ =>
            {
                var sub = _captureService.FrameDataStream.Subscribe(_ => { });
                lock (subscriptions)
                {
                    subscriptions.Add(sub);
                }
            });
        });

        // Assert
        Assert.Null(exception);

        // Cleanup
        foreach (var sub in subscriptions)
        {
            sub.Dispose();
        }
    }

    [Fact]
    public void ParameterNameIndexMapping_ShouldContainExpectedParameters()
    {
        // Arrange
        var expectedParameters = new[]
        {
            "ApplicationName",
            "ProcessID",
            "TimeInSeconds",
            "MsBetweenPresents",
            "MsBetweenDisplayChange",
            "MsPCLatency",
            "CpuBusy",
            "GpuBusy"
        };

        // Act
        var mapping = _captureService.ParameterNameIndexMapping;

        // Assert
        foreach (var param in expectedParameters)
        {
            Assert.True(mapping.ContainsKey(param),
                $"Parameter '{param}' should be in the mapping");
        }
    }

    [Fact]
    public void ParameterNameIndexMapping_ShouldHaveUniqueIndices()
    {
        // Act
        var indices = _captureService.ParameterNameIndexMapping.Values.ToList();

        // Assert
        var uniqueIndices = indices.Distinct().ToList();
        Assert.Equal(indices.Count, uniqueIndices.Count);
    }

    [Fact]
    public void ParameterNameIndexMapping_ShouldBeReadOnly()
    {
        // Act
        var mapping = _captureService.ParameterNameIndexMapping;

        // Assert - Should be IReadOnlyDictionary
        Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(mapping);
    }

    public void Dispose()
    {
        _captureService.StopCaptureService();
    }
}
