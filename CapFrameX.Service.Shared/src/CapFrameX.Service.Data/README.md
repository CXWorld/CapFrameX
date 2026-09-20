# CapFrameX.Service.Data

Data access layer for CapFrameX using Entity Framework Core and SQLite.

## Features

### Database Technology
- **SQLite** - Lightweight embedded database (~1MB)
- **Entity Framework Core 9.0** - Modern ORM with code-first migrations
- **No Installation Required** - Database runs locally in app folder
- **Cross-Platform** - Same code works on Windows, Linux, macOS

### Domain Model

#### Suite
- Groups related benchmark sessions
- Types: `HardwareReview`, `GameBenchmark`, `ComparisonSet`
- One-to-many relationship with Sessions

#### Session
- Contains hardware and game information
- Based on legacy `ISessionInfo` structure
- Properties:
  - Game: GameName, ProcessName, Comment
  - Hardware: Processor, Motherboard, SystemRam, GPU, GpuCount, Clocks
  - Drivers: BaseDriverVersion, DriverPackage, GPUDriverVersion
  - System: OS, ApiInfo, ResizableBar, WinGameMode, HAGS, PresentationMode, Resolution
- One-to-many relationship with SessionRuns

#### SessionRun
- Individual benchmark run with frame timing and sensor data
- Large arrays stored as JSON for efficiency
- Pre-computed metrics for fast queries:
  - FPS metrics: Max, P99, P95, Average, Median, P5, P1, P0.1, P0.01
  - Hardware metrics: AvgCpuTemp, AvgGpuTemp, AvgCpuPower, AvgGpuPower, AvgCpuUsage, AvgGpuUsage
- Based on legacy `ISessionRun`, `ISessionCaptureData`, `ISessionSensorData`

### Repository Pattern

RESTful repository interfaces and implementations:

- **ISuiteRepository** - CRUD operations for suites with filtering by type
- **ISessionRepository** - Session management with hardware/game filtering
- **ISessionRunRepository** - Run queries with metric-based filtering and sorting

All repositories support:
- Pagination (skip/take)
- Async operations
- Filtering and searching
- Aggregation (count)

## Database Location

**Default**: `%LocalAppData%\CapFrameX\capframex.db`

Custom paths supported via `CapFrameXDbContextFactory.Create(customPath)`.

## Usage

### Creating the Database

```csharp
using CapFrameX.Service.Data;

// Use default location
var context = CapFrameXDbContextFactory.Create();

// Or specify custom path
var context = CapFrameXDbContextFactory.Create("C:\\MyData\\capframex.db");

// Apply migrations
await context.Database.MigrateAsync();
```

### Using Repositories

```csharp
using CapFrameX.Service.Data.Repositories;

// Create context and repositories
var context = CapFrameXDbContextFactory.Create();
var suiteRepo = new SuiteRepository(context);
var sessionRepo = new SessionRepository(context);
var runRepo = new SessionRunRepository(context);

// Create a suite
var suite = await suiteRepo.CreateAsync(new Suite
{
    Id = Guid.NewGuid(),
    Name = "RTX 4090 Gaming Benchmark",
    Type = SuiteType.GameBenchmark,
    Description = "Performance testing with RTX 4090"
});

// Create a session
var session = await sessionRepo.CreateAsync(new Session
{
    Id = Guid.NewGuid(),
    SuiteId = suite.Id,
    GameName = "Cyberpunk 2077",
    ProcessName = "Cyberpunk2077.exe",
    Processor = "AMD Ryzen 9 7950X",
    Gpu = "NVIDIA RTX 4090",
    Os = "Windows 11 Pro",
    ApiInfo = "DirectX 12",
    ResolutionInfo = "3840x2160",
    GpuDriverVersion = "537.42"
});

// Create a run with metrics
var run = await runRepo.CreateAsync(new SessionRun
{
    Id = Guid.NewGuid(),
    SessionId = session.Id,
    SampleTime = 120.0,
    CaptureDataJson = "{\"frames\": [...]}",
    SensorDataJson = "{\"sensors\": [...]}",
    AverageFps = 144.5,
    P1Fps = 120.0,
    P99Fps = 160.0,
    AvgGpuTemp = 72.5,
    AvgCpuTemp = 68.3
});
```

### Querying Data

```csharp
// Get all game benchmark suites
var gameSuites = await suiteRepo.GetAllAsync(type: SuiteType.GameBenchmark);

// Search sessions by game name
var cyberpunkSessions = await sessionRepo.SearchByGameAsync("Cyberpunk");

// Get sessions with specific hardware
var rtx4090Sessions = await sessionRepo.GetAllAsync(gpu: "RTX 4090");

// Get top performing runs
var topRuns = await runRepo.GetTopRunsAsync(metric: "AverageFps", count: 10);

// Get runs sorted by P1 FPS
var runs = await runRepo.GetAllAsync(
    sortBy: "P1Fps",
    descending: true,
    skip: 0,
    take: 50
);

// Filter runs by FPS range
var smoothRuns = await runRepo.GetAllAsync(
    minAverageFps: 60.0,
    maxAverageFps: 144.0
);
```

### REST API Integration

Repositories are designed for REST API consumption with:
- Pagination support
- Flexible filtering
- Sorting by multiple metrics
- Efficient queries using indexes

Example ASP.NET Core controller:

```csharp
[ApiController]
[Route("api/[controller]")]
public class SessionsController : ControllerBase
{
    private readonly ISessionRepository _repository;

    public SessionsController(ISessionRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<IActionResult> GetSessions(
        [FromQuery] string? gameName = null,
        [FromQuery] string? gpu = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50)
    {
        var sessions = await _repository.GetAllAsync(
            gameName: gameName,
            gpu: gpu,
            skip: skip,
            take: take
        );

        var total = await _repository.CountAsync(gameName: gameName);

        return Ok(new { sessions, total, skip, take });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetSession(Guid id, [FromQuery] bool includeRuns = false)
    {
        var session = await _repository.GetByIdAsync(id, includeRuns);
        return session == null ? NotFound() : Ok(session);
    }
}
```

## Migrations

### Creating Migrations

```bash
cd CapFrameX.Service/src/CapFrameX.Service.Data
dotnet ef migrations add MigrationName
```

### Applying Migrations

```bash
# Update database to latest
dotnet ef database update

# Update to specific migration
dotnet ef database update MigrationName

# Rollback all migrations
dotnet ef database update 0
```

### Removing Last Migration

```bash
dotnet ef migrations remove
```

## Database Schema

### Tables

- **Suites** - Benchmark suite collections
- **Sessions** - Individual benchmark sessions
- **SessionRuns** - Individual runs within sessions

### Relationships

```
Suite (1) ──→ (N) Sessions
                   │
                   └──→ (N) SessionRuns
```

All relationships use cascade delete.

### Indexes

**Suites**:
- `Type`
- `CreatedAt`
- `(Type, CreatedAt)` - Composite

**Sessions**:
- `GameName`
- `ProcessName`
- `Processor`
- `Gpu`
- `CreatedAt`
- `(GameName, CreatedAt)` - Composite
- `(SuiteId, CreatedAt)` - Composite

**SessionRuns**:
- `SessionId`
- `CreatedAt`
- `AverageFps`
- `P1Fps`
- `P99Fps`
- `(SessionId, CreatedAt)` - Composite

Indexes optimize common REST API queries for filtering and sorting.

## JSON Storage Strategy

Large arrays (frame timings, sensor data) are stored as JSON to avoid:
- Creating thousands of rows per run
- Complex joins on large datasets
- Database bloat

### Stored as JSON

- `CaptureDataJson` - Frame timing arrays (TimeInSeconds, MsBetweenPresents, etc.)
- `SensorDataJson` - Hardware monitoring data (CPU/GPU temps, clocks, usage)
- `RtssFrameTimesJson` - RTSS frame times (optional)
- `PmdGpuPowerJson` - PMD GPU power data (optional)
- `PmdCpuPowerJson` - PMD CPU power data (optional)
- `PmdSystemPowerJson` - PMD system power data (optional)

### Pre-computed Metrics

Key metrics are computed and stored as columns for fast queries:
- FPS percentiles (P99, P95, P1, etc.)
- Average hardware metrics (temps, power, usage)

This hybrid approach provides:
- Fast queries on metrics (indexed columns)
- Complete raw data preservation (JSON)
- Compact storage

## Testing

### Unit Tests (24 tests)

**SuiteRepositoryTests** (8 tests)
- CRUD operations
- Filtering by type
- Counting
- Cascade relationships

**SessionRepositoryTests** (9 tests)
- CRUD operations
- Filtering by game/hardware
- Search functionality
- Suite relationships

**SessionRunRepositoryTests** (11 tests)
- CRUD operations
- FPS range filtering
- Metric-based sorting
- Top performers queries
- Counting

### Running Tests

```bash
dotnet test CapFrameX.Service/tests/CapFrameX.Service.Data.Tests
```

All tests use in-memory database for fast execution.

## Design Decisions

### Why SQLite over Embedded PostgreSQL?

| Feature | SQLite | Embedded PostgreSQL |
|---------|--------|---------------------|
| Size | ~1MB | ~100MB |
| Installation | None | Complex |
| Platform | Single file | Platform-specific binaries |
| Performance | Excellent for local | Overkill for local |
| Complexity | Minimal | High |

For a desktop application storing local benchmark data, SQLite is the clear choice.

### Why JSON for Arrays?

Frame capture can produce 10,000+ samples per run. Traditional relational approach:
- ❌ 10,000 rows per run (millions of rows)
- ❌ Complex joins
- ❌ Database bloat
- ❌ Slow queries

JSON approach:
- ✅ 1 row per run
- ✅ Complete data preservation
- ✅ Fast metrics queries (indexed columns)
- ✅ Compact storage

### Why Repository Pattern?

- ✅ Clean separation of concerns
- ✅ Testable (in-memory provider)
- ✅ REST API-ready interfaces
- ✅ Encapsulates query complexity
- ✅ Supports dependency injection

## Comparison with Legacy

### Legacy (JSON Files)
- ✅ Simple storage
- ✅ Human-readable
- ❌ No querying capabilities
- ❌ No relationships
- ❌ Manual file management
- ❌ No filtering/sorting
- ❌ No aggregation

### New (SQLite + EF Core)
- ✅ Powerful queries (LINQ)
- ✅ Relationships enforced
- ✅ Indexes for performance
- ✅ Migrations for schema changes
- ✅ REST API integration
- ✅ Concurrent access
- ✅ ACID transactions
- ❌ Requires database knowledge

## Dependencies

- `Microsoft.EntityFrameworkCore` (9.0.0) - Core ORM
- `Microsoft.EntityFrameworkCore.Sqlite` (9.0.0) - SQLite provider
- `Microsoft.EntityFrameworkCore.Design` (9.0.0) - Migration tooling
- `Microsoft.Extensions.Logging.Abstractions` - Logging interface

## Integration Notes

### Dependency Injection Setup

```csharp
// In Program.cs or Startup.cs
services.AddDbContext<CapFrameXDbContext>(options =>
    options.UseSqlite($"Data Source={CapFrameXDbContextFactory.GetDefaultDatabasePath()}"));

services.AddScoped<ISuiteRepository, SuiteRepository>();
services.AddScoped<ISessionRepository, SessionRepository>();
services.AddScoped<ISessionRunRepository, SessionRunRepository>();
```

### Migration on Startup

```csharp
// Apply migrations automatically
using var scope = app.Services.CreateScope();
var context = scope.ServiceProvider.GetRequiredService<CapFrameXDbContext>();
await context.Database.MigrateAsync();
```

## Future Considerations

### Potential Enhancements
- Read-only query optimization (AsNoTracking)
- Batch operations (EF Core bulk extensions)
- Full-text search on game names
- Database compression for archival
- Export to CSV/Excel via repositories

### Scalability Notes
SQLite is limited to ~1TB database size, which should handle:
- ~1,000,000 runs (at ~1MB JSON each)
- Years of benchmark data
- Multiple user profiles

For cloud deployment or multi-user scenarios, consider migrating to PostgreSQL using same EF Core code.

## License

Part of CapFrameX - Frame capture and analysis tool
