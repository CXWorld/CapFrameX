using CapFrameX.Service.Data.Models;
using CapFrameX.Service.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CapFrameX.Service.Data.Tests;

/// <summary>
/// Tests for SessionRunRepository
/// </summary>
public class SessionRunRepositoryTests : IDisposable
{
    private readonly CapFrameXDbContext _context;
    private readonly ISessionRunRepository _repository;
    private readonly Guid _sessionId;

    public SessionRunRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<CapFrameXDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new CapFrameXDbContext(options);
        _repository = new SessionRunRepository(_context);

        // Create test suite and session
        var suiteId = Guid.NewGuid();
        _context.Suites.Add(new Suite
        {
            Id = suiteId,
            Name = "Test Suite",
            Type = SuiteType.GameReview,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        _sessionId = Guid.NewGuid();
        _context.Sessions.Add(new Session
        {
            Id = _sessionId,
            SuiteId = suiteId,
            GameName = "Test Game",
            ProcessName = "game.exe",
            Processor = "Test CPU",
            Gpu = "Test GPU",
            Os = "Windows 11",
            CreatedAt = DateTime.UtcNow
        });
        _context.SaveChanges();
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateRun()
    {
        // Arrange
        var run = new SessionRun
        {
            Id = Guid.NewGuid(),
            SessionId = _sessionId,
            SampleTime = 60.0,
            CaptureDataJson = "{\"frames\": []}",
            SensorDataJson = "{\"sensors\": []}",
            AverageFps = 144.5,
            P1Fps = 120.0,
            P99Fps = 160.0
        };

        // Act
        var result = await _repository.CreateAsync(run);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(DateTime.MinValue, result.CreatedAt);

        var saved = await _context.SessionRuns.FindAsync(run.Id);
        Assert.NotNull(saved);
        Assert.Equal(144.5, saved.AverageFps);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnRun()
    {
        // Arrange
        var run = new SessionRun
        {
            Id = Guid.NewGuid(),
            SessionId = _sessionId,
            SampleTime = 60.0,
            CreatedAt = DateTime.UtcNow
        };
        _context.SessionRuns.Add(run);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(run.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(run.Id, result.Id);
    }

    [Fact]
    public async Task GetAllAsync_ShouldFilterByFps()
    {
        // Arrange
        var runs = new[]
        {
            new SessionRun { Id = Guid.NewGuid(), SessionId = _sessionId, SampleTime = 60.0, AverageFps = 60.0, CreatedAt = DateTime.UtcNow },
            new SessionRun { Id = Guid.NewGuid(), SessionId = _sessionId, SampleTime = 60.0, AverageFps = 120.0, CreatedAt = DateTime.UtcNow },
            new SessionRun { Id = Guid.NewGuid(), SessionId = _sessionId, SampleTime = 60.0, AverageFps = 144.0, CreatedAt = DateTime.UtcNow },
            new SessionRun { Id = Guid.NewGuid(), SessionId = _sessionId, SampleTime = 60.0, AverageFps = 165.0, CreatedAt = DateTime.UtcNow }
        };
        _context.SessionRuns.AddRange(runs);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync(minAverageFps: 100.0, maxAverageFps: 150.0);

        // Assert
        Assert.Equal(2, result.Count());
        Assert.All(result, r => Assert.InRange(r.AverageFps!.Value, 100.0, 150.0));
    }

    [Fact]
    public async Task GetAllAsync_ShouldSortByMetric()
    {
        // Arrange
        var runs = new[]
        {
            new SessionRun { Id = Guid.NewGuid(), SessionId = _sessionId, SampleTime = 60.0, AverageFps = 120.0, P1Fps = 100.0, CreatedAt = DateTime.UtcNow.AddSeconds(-3) },
            new SessionRun { Id = Guid.NewGuid(), SessionId = _sessionId, SampleTime = 60.0, AverageFps = 144.0, P1Fps = 130.0, CreatedAt = DateTime.UtcNow.AddSeconds(-2) },
            new SessionRun { Id = Guid.NewGuid(), SessionId = _sessionId, SampleTime = 60.0, AverageFps = 100.0, P1Fps = 85.0, CreatedAt = DateTime.UtcNow.AddSeconds(-1) }
        };
        _context.SessionRuns.AddRange(runs);
        await _context.SaveChangesAsync();

        // Act - Sort by AverageFps descending
        var avgResult = await _repository.GetAllAsync(sortBy: "averagefps", descending: true);

        // Assert
        var avgList = avgResult.ToList();
        Assert.Equal(144.0, avgList[0].AverageFps);
        Assert.Equal(120.0, avgList[1].AverageFps);
        Assert.Equal(100.0, avgList[2].AverageFps);
    }

    [Fact]
    public async Task GetBySessionIdAsync_ShouldReturnRunsForSession()
    {
        // Arrange
        var runs = new[]
        {
            new SessionRun { Id = Guid.NewGuid(), SessionId = _sessionId, SampleTime = 60.0, CreatedAt = DateTime.UtcNow },
            new SessionRun { Id = Guid.NewGuid(), SessionId = _sessionId, SampleTime = 60.0, CreatedAt = DateTime.UtcNow },
            new SessionRun { Id = Guid.NewGuid(), SessionId = _sessionId, SampleTime = 60.0, CreatedAt = DateTime.UtcNow }
        };
        _context.SessionRuns.AddRange(runs);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetBySessionIdAsync(_sessionId);

        // Assert
        Assert.Equal(3, result.Count());
    }

    [Fact]
    public async Task GetTopRunsAsync_ShouldReturnTopPerformers()
    {
        // Arrange
        var runs = new[]
        {
            new SessionRun { Id = Guid.NewGuid(), SessionId = _sessionId, SampleTime = 60.0, AverageFps = 100.0, P1Fps = 80.0, CreatedAt = DateTime.UtcNow },
            new SessionRun { Id = Guid.NewGuid(), SessionId = _sessionId, SampleTime = 60.0, AverageFps = 120.0, P1Fps = 95.0, CreatedAt = DateTime.UtcNow },
            new SessionRun { Id = Guid.NewGuid(), SessionId = _sessionId, SampleTime = 60.0, AverageFps = 144.0, P1Fps = 110.0, CreatedAt = DateTime.UtcNow },
            new SessionRun { Id = Guid.NewGuid(), SessionId = _sessionId, SampleTime = 60.0, AverageFps = 165.0, P1Fps = 125.0, CreatedAt = DateTime.UtcNow },
            new SessionRun { Id = Guid.NewGuid(), SessionId = _sessionId, SampleTime = 60.0, AverageFps = 90.0, P1Fps = 70.0, CreatedAt = DateTime.UtcNow }
        };
        _context.SessionRuns.AddRange(runs);
        await _context.SaveChangesAsync();

        // Act
        var topAvg = await _repository.GetTopRunsAsync(metric: "AverageFps", count: 3);
        var topP1 = await _repository.GetTopRunsAsync(metric: "P1Fps", count: 2);

        // Assert
        Assert.Equal(3, topAvg.Count());
        Assert.Equal(165.0, topAvg.First().AverageFps);

        Assert.Equal(2, topP1.Count());
        Assert.Equal(125.0, topP1.First().P1Fps);
    }

    [Fact]
    public async Task DeleteAsync_ShouldDeleteRun()
    {
        // Arrange
        var run = new SessionRun
        {
            Id = Guid.NewGuid(),
            SessionId = _sessionId,
            SampleTime = 60.0,
            CreatedAt = DateTime.UtcNow
        };
        _context.SessionRuns.Add(run);
        await _context.SaveChangesAsync();

        // Act
        await _repository.DeleteAsync(run.Id);

        // Assert
        var deleted = await _context.SessionRuns.FindAsync(run.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateMetrics()
    {
        // Arrange
        var run = new SessionRun
        {
            Id = Guid.NewGuid(),
            SessionId = _sessionId,
            SampleTime = 60.0,
            AverageFps = 100.0,
            CreatedAt = DateTime.UtcNow
        };
        _context.SessionRuns.Add(run);
        await _context.SaveChangesAsync();

        // Act
        run.AverageFps = 120.0;
        run.P1Fps = 95.0;
        await _repository.UpdateAsync(run);

        // Assert
        var updated = await _context.SessionRuns.FindAsync(run.Id);
        Assert.NotNull(updated);
        Assert.Equal(120.0, updated.AverageFps);
        Assert.Equal(95.0, updated.P1Fps);
    }

    [Fact]
    public async Task CountAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        var runs = new[]
        {
            new SessionRun { Id = Guid.NewGuid(), SessionId = _sessionId, SampleTime = 60.0, CreatedAt = DateTime.UtcNow },
            new SessionRun { Id = Guid.NewGuid(), SessionId = _sessionId, SampleTime = 60.0, CreatedAt = DateTime.UtcNow },
            new SessionRun { Id = Guid.NewGuid(), SessionId = _sessionId, SampleTime = 60.0, CreatedAt = DateTime.UtcNow }
        };
        _context.SessionRuns.AddRange(runs);
        await _context.SaveChangesAsync();

        // Act
        var count = await _repository.CountAsync(sessionId: _sessionId);

        // Assert
        Assert.Equal(3, count);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
