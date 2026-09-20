using CapFrameX.Service.Data.Models;
using CapFrameX.Service.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CapFrameX.Service.Data.Tests;

/// <summary>
/// Tests for SessionRepository
/// </summary>
public class SessionRepositoryTests : IDisposable
{
    private readonly CapFrameXDbContext _context;
    private readonly ISessionRepository _repository;
    private readonly Guid _suiteId;

    public SessionRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<CapFrameXDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new CapFrameXDbContext(options);
        _repository = new SessionRepository(_context);

        // Create a test suite
        _suiteId = Guid.NewGuid();
        _context.Suites.Add(new Suite
        {
            Id = _suiteId,
            Name = "Test Suite",
            Type = SuiteType.GameReview,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        _context.SaveChanges();
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateSession()
    {
        // Arrange
        var session = new Session
        {
            Id = Guid.NewGuid(),
            SuiteId = _suiteId,
            GameName = "Cyberpunk 2077",
            ProcessName = "Cyberpunk2077.exe",
            Processor = "AMD Ryzen 9 7950X",
            Gpu = "NVIDIA RTX 4090",
            Os = "Windows 11"
        };

        // Act
        var result = await _repository.CreateAsync(session);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(DateTime.MinValue, result.CreatedAt);

        var saved = await _context.Sessions.FindAsync(session.Id);
        Assert.NotNull(saved);
        Assert.Equal("Cyberpunk 2077", saved.GameName);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnSession()
    {
        // Arrange
        var session = new Session
        {
            Id = Guid.NewGuid(),
            SuiteId = _suiteId,
            GameName = "Test Game",
            ProcessName = "game.exe",
            Processor = "Test CPU",
            Gpu = "Test GPU",
            Os = "Windows 11",
            CreatedAt = DateTime.UtcNow
        };
        _context.Sessions.Add(session);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(session.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(session.Id, result.Id);
        Assert.Equal("Test Game", result.GameName);
    }

    [Fact]
    public async Task GetAllAsync_ShouldFilterByGameName()
    {
        // Arrange
        var sessions = new[]
        {
            new Session { Id = Guid.NewGuid(), SuiteId = _suiteId, GameName = "Cyberpunk 2077", ProcessName = "game1.exe", Processor = "CPU1", Gpu = "GPU1", Os = "Windows 11", CreatedAt = DateTime.UtcNow },
            new Session { Id = Guid.NewGuid(), SuiteId = _suiteId, GameName = "The Witcher 3", ProcessName = "game2.exe", Processor = "CPU1", Gpu = "GPU1", Os = "Windows 11", CreatedAt = DateTime.UtcNow },
            new Session { Id = Guid.NewGuid(), SuiteId = _suiteId, GameName = "Cyberpunk 2077", ProcessName = "game3.exe", Processor = "CPU1", Gpu = "GPU1", Os = "Windows 11", CreatedAt = DateTime.UtcNow }
        };
        _context.Sessions.AddRange(sessions);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync(gameName: "Cyberpunk");

        // Assert
        Assert.Equal(2, result.Count());
        Assert.All(result, s => Assert.Contains("Cyberpunk", s.GameName));
    }

    [Fact]
    public async Task GetAllAsync_ShouldFilterByHardware()
    {
        // Arrange
        var sessions = new[]
        {
            new Session { Id = Guid.NewGuid(), SuiteId = _suiteId, GameName = "Game1", ProcessName = "game1.exe", Processor = "AMD Ryzen 9 7950X", Gpu = "NVIDIA RTX 4090", Os = "Windows 11", CreatedAt = DateTime.UtcNow },
            new Session { Id = Guid.NewGuid(), SuiteId = _suiteId, GameName = "Game2", ProcessName = "game2.exe", Processor = "Intel i9-13900K", Gpu = "NVIDIA RTX 4090", Os = "Windows 11", CreatedAt = DateTime.UtcNow },
            new Session { Id = Guid.NewGuid(), SuiteId = _suiteId, GameName = "Game3", ProcessName = "game3.exe", Processor = "AMD Ryzen 9 7950X", Gpu = "AMD RX 7900 XTX", Os = "Windows 11", CreatedAt = DateTime.UtcNow }
        };
        _context.Sessions.AddRange(sessions);
        await _context.SaveChangesAsync();

        // Act
        var amdCpuSessions = await _repository.GetAllAsync(processor: "AMD Ryzen");
        var nvidiaGpuSessions = await _repository.GetAllAsync(gpu: "NVIDIA");

        // Assert
        Assert.Equal(2, amdCpuSessions.Count());
        Assert.Equal(2, nvidiaGpuSessions.Count());
    }

    [Fact]
    public async Task GetBySuiteIdAsync_ShouldReturnSessionsForSuite()
    {
        // Arrange
        var sessions = new[]
        {
            new Session { Id = Guid.NewGuid(), SuiteId = _suiteId, GameName = "Game1", ProcessName = "game1.exe", Processor = "CPU1", Gpu = "GPU1", Os = "Windows 11", CreatedAt = DateTime.UtcNow },
            new Session { Id = Guid.NewGuid(), SuiteId = _suiteId, GameName = "Game2", ProcessName = "game2.exe", Processor = "CPU1", Gpu = "GPU1", Os = "Windows 11", CreatedAt = DateTime.UtcNow }
        };
        _context.Sessions.AddRange(sessions);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetBySuiteIdAsync(_suiteId);

        // Assert
        Assert.Equal(2, result.Count());
    }

    [Fact]
    public async Task SearchByGameAsync_ShouldFindGames()
    {
        // Arrange
        var sessions = new[]
        {
            new Session { Id = Guid.NewGuid(), SuiteId = _suiteId, GameName = "Cyberpunk 2077", ProcessName = "Cyberpunk2077.exe", Processor = "CPU1", Gpu = "GPU1", Os = "Windows 11", CreatedAt = DateTime.UtcNow },
            new Session { Id = Guid.NewGuid(), SuiteId = _suiteId, GameName = "The Witcher 3", ProcessName = "witcher3.exe", Processor = "CPU1", Gpu = "GPU1", Os = "Windows 11", CreatedAt = DateTime.UtcNow },
            new Session { Id = Guid.NewGuid(), SuiteId = _suiteId, GameName = "Starfield", ProcessName = "Starfield.exe", Processor = "CPU1", Gpu = "GPU1", Os = "Windows 11", CreatedAt = DateTime.UtcNow }
        };
        _context.Sessions.AddRange(sessions);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.SearchByGameAsync("Cyber");

        // Assert
        Assert.Single(result);
        Assert.Contains("Cyberpunk", result.First().GameName);
    }

    [Fact]
    public async Task DeleteAsync_ShouldDeleteSession()
    {
        // Arrange
        var session = new Session
        {
            Id = Guid.NewGuid(),
            SuiteId = _suiteId,
            GameName = "To Delete",
            ProcessName = "delete.exe",
            Processor = "CPU1",
            Gpu = "GPU1",
            Os = "Windows 11",
            CreatedAt = DateTime.UtcNow
        };
        _context.Sessions.Add(session);
        await _context.SaveChangesAsync();

        // Act
        await _repository.DeleteAsync(session.Id);

        // Assert
        var deleted = await _context.Sessions.FindAsync(session.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task GetByIdAsync_WithRuns_ShouldIncludeRuns()
    {
        // Arrange
        var session = new Session
        {
            Id = Guid.NewGuid(),
            SuiteId = _suiteId,
            GameName = "Test Game",
            ProcessName = "game.exe",
            Processor = "CPU1",
            Gpu = "GPU1",
            Os = "Windows 11",
            CreatedAt = DateTime.UtcNow
        };
        _context.Sessions.Add(session);

        var run = new SessionRun
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            SampleTime = 60.0,
            CreatedAt = DateTime.UtcNow
        };
        _context.SessionRuns.Add(run);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(session.Id, includeRuns: true);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Runs);
        Assert.Single(result.Runs);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
