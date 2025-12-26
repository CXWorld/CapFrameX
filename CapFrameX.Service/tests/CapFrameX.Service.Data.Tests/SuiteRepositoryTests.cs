using CapFrameX.Service.Data.Models;
using CapFrameX.Service.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CapFrameX.Service.Data.Tests;

/// <summary>
/// Tests for SuiteRepository
/// </summary>
public class SuiteRepositoryTests : IDisposable
{
    private readonly CapFrameXDbContext _context;
    private readonly ISuiteRepository _repository;

    public SuiteRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<CapFrameXDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new CapFrameXDbContext(options);
        _repository = new SuiteRepository(_context);
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateSuite()
    {
        // Arrange
        var suite = new Suite
        {
            Id = Guid.NewGuid(),
            Name = "Test Suite",
            Description = "Test Description",
            Type = SuiteType.GameBenchmark
        };

        // Act
        var result = await _repository.CreateAsync(suite);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(DateTime.MinValue, result.CreatedAt);
        Assert.NotEqual(DateTime.MinValue, result.UpdatedAt);

        var saved = await _context.Suites.FindAsync(suite.Id);
        Assert.NotNull(saved);
        Assert.Equal("Test Suite", saved.Name);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnSuite()
    {
        // Arrange
        var suite = new Suite
        {
            Id = Guid.NewGuid(),
            Name = "Test Suite",
            Type = SuiteType.HardwareReview,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Suites.Add(suite);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(suite.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(suite.Id, result.Id);
        Assert.Equal("Test Suite", result.Name);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnFilteredSuites()
    {
        // Arrange
        var suites = new[]
        {
            new Suite { Id = Guid.NewGuid(), Name = "Suite 1", Type = SuiteType.GameBenchmark, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Suite { Id = Guid.NewGuid(), Name = "Suite 2", Type = SuiteType.HardwareReview, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Suite { Id = Guid.NewGuid(), Name = "Suite 3", Type = SuiteType.GameBenchmark, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        };
        _context.Suites.AddRange(suites);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync(type: SuiteType.GameBenchmark);

        // Assert
        Assert.Equal(2, result.Count());
        Assert.All(result, s => Assert.Equal(SuiteType.GameBenchmark, s.Type));
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateSuite()
    {
        // Arrange
        var suite = new Suite
        {
            Id = Guid.NewGuid(),
            Name = "Original Name",
            Type = SuiteType.ComparisonSet,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Suites.Add(suite);
        await _context.SaveChangesAsync();

        // Act
        suite.Name = "Updated Name";
        var originalUpdatedAt = suite.UpdatedAt;
        await Task.Delay(10); // Small delay to ensure UpdatedAt changes
        await _repository.UpdateAsync(suite);

        // Assert
        var updated = await _context.Suites.FindAsync(suite.Id);
        Assert.NotNull(updated);
        Assert.Equal("Updated Name", updated.Name);
        Assert.True(updated.UpdatedAt > originalUpdatedAt);
    }

    [Fact]
    public async Task DeleteAsync_ShouldDeleteSuite()
    {
        // Arrange
        var suite = new Suite
        {
            Id = Guid.NewGuid(),
            Name = "To Delete",
            Type = SuiteType.GameBenchmark,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Suites.Add(suite);
        await _context.SaveChangesAsync();

        // Act
        await _repository.DeleteAsync(suite.Id);

        // Assert
        var deleted = await _context.Suites.FindAsync(suite.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task CountAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        var suites = new[]
        {
            new Suite { Id = Guid.NewGuid(), Name = "Suite 1", Type = SuiteType.GameBenchmark, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Suite { Id = Guid.NewGuid(), Name = "Suite 2", Type = SuiteType.GameBenchmark, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Suite { Id = Guid.NewGuid(), Name = "Suite 3", Type = SuiteType.HardwareReview, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        };
        _context.Suites.AddRange(suites);
        await _context.SaveChangesAsync();

        // Act
        var totalCount = await _repository.CountAsync();
        var gameCount = await _repository.CountAsync(SuiteType.GameBenchmark);

        // Assert
        Assert.Equal(3, totalCount);
        Assert.Equal(2, gameCount);
    }

    [Fact]
    public async Task GetByIdAsync_WithSessions_ShouldIncludeSessions()
    {
        // Arrange
        var suite = new Suite
        {
            Id = Guid.NewGuid(),
            Name = "Suite with Sessions",
            Type = SuiteType.GameBenchmark,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Suites.Add(suite);

        var session = new Session
        {
            Id = Guid.NewGuid(),
            SuiteId = suite.Id,
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
        var result = await _repository.GetByIdAsync(suite.Id, includeSessions: true);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Sessions);
        Assert.Single(result.Sessions);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
