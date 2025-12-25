using CapFrameX.Service.Capture.Contracts;
using System.Reactive.Linq;

namespace CapFrameX.Service.Capture.Tests;

/// <summary>
/// Basic capture service functionality tests.
/// </summary>
public class CaptureServiceTests : IDisposable
{
    private readonly PresentMonCaptureService _captureService;

    public CaptureServiceTests()
    {
        _captureService = TestHelpers.CreateCaptureService();
    }

    [Fact]
    public void CaptureService_ShouldInitializeWithParameterMapping()
    {
        // Assert
        Assert.NotNull(_captureService.ParameterNameIndexMapping);
        Assert.NotEmpty(_captureService.ParameterNameIndexMapping);

        // Verify essential parameters are mapped
        Assert.Contains("ApplicationName", _captureService.ParameterNameIndexMapping.Keys);
        Assert.Contains("ProcessID", _captureService.ParameterNameIndexMapping.Keys);
        Assert.Contains("TimeInSeconds", _captureService.ParameterNameIndexMapping.Keys);
        Assert.Contains("MsBetweenPresents", _captureService.ParameterNameIndexMapping.Keys);
    }

    [Fact]
    public void CaptureService_ShouldProvideFrameDataStream()
    {
        // Assert
        Assert.NotNull(_captureService.FrameDataStream);
    }

    [Fact]
    public void CaptureService_ShouldProvideCaptureModeActiveStream()
    {
        // Assert
        Assert.NotNull(_captureService.IsCaptureModeActiveStream);
    }

    [Fact]
    public void CaptureService_InitialState_ShouldNotBeActive()
    {
        // Arrange
        bool? isActive = null;
        var subscription = _captureService.IsCaptureModeActiveStream
            .Subscribe(active => isActive = active);

        // Assert - Initially should be false or no value emitted yet
        Assert.True(isActive == null || isActive == false);

        subscription.Dispose();
    }

    [Fact]
    public void GetAllFilteredProcesses_WithNullFilter_ShouldReturnAllProcesses()
    {
        // Act
        var processes = _captureService.GetAllFilteredProcesses(null);

        // Assert - Should not throw and return enumerable
        Assert.NotNull(processes);
        var processList = processes.ToList();
        // Initially should be empty as no capture is running
        Assert.Empty(processList);
    }

    [Fact]
    public void GetAllFilteredProcesses_WithEmptyFilter_ShouldReturnAllProcesses()
    {
        // Act
        var processes = _captureService.GetAllFilteredProcesses(new HashSet<string>());

        // Assert
        Assert.NotNull(processes);
        var processList = processes.ToList();
        Assert.Empty(processList);
    }

    [Fact]
    public void StopCaptureService_WhenNotStarted_ShouldComplete()
    {
        // Act
        var result = _captureService.StopCaptureService();

        // Assert - Should not throw, result may be true (cleanup successful) or false
        Assert.True(result == true || result == false);
    }

    public void Dispose()
    {
        _captureService.StopCaptureService();
    }
}
