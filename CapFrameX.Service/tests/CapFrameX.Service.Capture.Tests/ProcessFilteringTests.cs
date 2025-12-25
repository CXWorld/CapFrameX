using CapFrameX.Service.Capture.Contracts;

namespace CapFrameX.Service.Capture.Tests;

/// <summary>
/// Tests for process filtering functionality.
/// </summary>
public class ProcessFilteringTests : IDisposable
{
    private readonly PresentMonCaptureService _captureService;

    public ProcessFilteringTests()
    {
        _captureService = TestHelpers.CreateCaptureService();
    }

    [Fact]
    public void GetAllFilteredProcesses_WithFilter_ShouldExcludeFilteredProcesses()
    {
        // Arrange
        var filter = new HashSet<string> { "explorer", "svchost" };

        // Act
        var processes = _captureService.GetAllFilteredProcesses(filter);

        // Assert
        Assert.NotNull(processes);
        var processList = processes.ToList();

        // Verify filtered processes are not in the list
        foreach (var (processName, _) in processList)
        {
            Assert.DoesNotContain(processName, filter);
        }
    }

    [Fact]
    public void GetAllFilteredProcesses_WithFilter_ShouldBeCaseInsensitive()
    {
        // Arrange
        var filter = new HashSet<string> { "EXPLORER", "SvcHost" };

        // Act
        var processes = _captureService.GetAllFilteredProcesses(filter);

        // Assert
        Assert.NotNull(processes);
        var processList = processes.ToList();

        // Verify case-insensitive filtering
        foreach (var (processName, _) in processList)
        {
            Assert.DoesNotContain(processName.ToLower(),
                filter.Select(f => f.ToLower()));
        }
    }

    [Fact]
    public void GetAllFilteredProcesses_MultipleCalls_ShouldReturnConsistentResults()
    {
        // Arrange
        var filter = new HashSet<string> { "test" };

        // Act
        var processes1 = _captureService.GetAllFilteredProcesses(filter).ToList();
        var processes2 = _captureService.GetAllFilteredProcesses(filter).ToList();

        // Assert
        Assert.Equal(processes1.Count, processes2.Count);
    }

    [Fact]
    public void GetAllFilteredProcesses_WithNullAndEmptyFilter_ShouldReturnSameResults()
    {
        // Act
        var processesWithNull = _captureService.GetAllFilteredProcesses(null).ToList();
        var processesWithEmpty = _captureService.GetAllFilteredProcesses(new HashSet<string>()).ToList();

        // Assert
        Assert.Equal(processesWithNull.Count, processesWithEmpty.Count);
    }

    [Fact]
    public void GetAllFilteredProcesses_ThreadSafety_ShouldHandleConcurrentReads()
    {
        // Arrange
        var filters = new[]
        {
            new HashSet<string> { "test1" },
            new HashSet<string> { "test2" },
            new HashSet<string> { "test3" }
        };

        // Act - Concurrent reads should not throw
        var tasks = filters.Select(filter =>
            Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    var _ = _captureService.GetAllFilteredProcesses(filter).ToList();
                }
            })
        ).ToArray();

        // Assert - Should complete without exceptions
        var exception = Record.Exception(() => Task.WaitAll(tasks));
        Assert.Null(exception);
    }

    public void Dispose()
    {
        _captureService.StopCaptureService();
    }
}
